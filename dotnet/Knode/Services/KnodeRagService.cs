using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Knode.Services;

public sealed record BuildIndexStats(
    int TotalRows,
    int EmbeddedRows,
    int ReusedRows,
    int DeletedRowsFromBaseline,
    bool LoadedFromExactCache);

/// <summary>Loads corpus.jsonl, builds Gemini embedding index, answers via generateContent.</summary>
public sealed class KnodeRagService
{
    private readonly string _embeddingModel;
    private readonly string _chatModel;
    private readonly KnodeRagOptions _options;
    private readonly VectorIndex _index = new();
    private List<HighlightRecord> _records = new();
    private volatile bool _ready;

    public KnodeRagService(string embeddingModel, string chatModel, KnodeRagOptions? options = null)
    {
        _embeddingModel = embeddingModel;
        _chatModel = chatModel;
        _options = options ?? new KnodeRagOptions();
    }

    public bool IsReady => _ready;

    public int RecordCount => _records.Count;

    private static string BuildCacheSignature(string corpusSha, string? extraSignature) =>
        string.IsNullOrWhiteSpace(extraSignature) ? corpusSha : $"{corpusSha}|{extraSignature.Trim()}";

    private static string ComputeEmbedContentHash(HighlightRecord record)
    {
        var payload = record.EmbedText;
        var bytes = Encoding.UTF8.GetBytes(payload);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    /// <summary>Loads index from disk when corpus hash and embedding model match.</summary>
    public async Task<bool> TryLoadPersistedIndexAsync(
        string corpusPath,
        IProgress<string>? progress,
        string? extraCacheSignature = null,
        CancellationToken ct = default)
    {
        _ready = false;
        var path = Path.GetFullPath(corpusPath);
        if (!File.Exists(path))
            return false;

        progress?.Report("Checking saved index…");
        var shaHex = await PersistentIndexStore.ComputeCorpusSha256HexAsync(path, ct).ConfigureAwait(false);
        var cacheSignature = BuildCacheSignature(shaHex, extraCacheSignature);
        if (!PersistentIndexStore.TryLoad(_embeddingModel, cacheSignature, out var recs, out var vecs, out var loadMsg) ||
            recs is null || vecs is null)
        {
            if (!string.IsNullOrEmpty(loadMsg))
                progress?.Report(loadMsg);
            return false;
        }

        _records = recs;
        _index.Load(vecs, recs);
        _ready = true;
        progress?.Report($"Ready (loaded saved index, {_records.Count} highlights).");
        return true;
    }

    /// <param name="delayBetweenEmbeddingBatchesMs">
    /// Pause between each embedding batch. Free tier is ~100 embed requests/minute; with batch size 32, ~18–20s stays under the cap.
    /// Set 0 if your project has higher quota.
    /// </param>
    public async Task<BuildIndexStats> BuildIndexAsync(
        GeminiClient client,
        string corpusPath,
        IProgress<string>? progress,
        int delayBetweenEmbeddingBatchesMs = 18500,
        bool forceFullRebuild = false,
        IReadOnlyList<HighlightRecord>? additionalRecords = null,
        string? extraCacheSignature = null,
        CancellationToken ct = default)
    {
        _ready = false;
        var path = Path.GetFullPath(corpusPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Corpus file not found.", path);

        progress?.Report("Checking saved index…");
        var corpusSha = await PersistentIndexStore.ComputeCorpusSha256HexAsync(path, ct).ConfigureAwait(false);
        var cacheSignature = BuildCacheSignature(corpusSha, extraCacheSignature);
        if (!forceFullRebuild &&
            PersistentIndexStore.TryLoad(_embeddingModel, cacheSignature, out var cachedRecords, out var cachedVectors, out _) &&
            cachedRecords is not null && cachedVectors is not null)
        {
            _records = cachedRecords;
            _index.Load(cachedVectors, _records);
            _ready = true;
            progress?.Report(
                $"Ready — loaded saved index ({_records.Count} highlights). No Gemini calls. " +
                "Check “Force full re-embed” and click Build index again if you need a fresh embedding pass.");
            return new BuildIndexStats(
                TotalRows: _records.Count,
                EmbeddedRows: 0,
                ReusedRows: _records.Count,
                DeletedRowsFromBaseline: 0,
                LoadedFromExactCache: true);
        }

        if (forceFullRebuild)
            progress?.Report("Force full re-embed: skipping disk cache, reading corpus and calling Gemini…");

        var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
        _records = new List<HighlightRecord>(lines.Length);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var r = HighlightRecordJson.DeserializeCorpusLine(line);
            if (r is not null && !string.IsNullOrWhiteSpace(r.Text))
                _records.Add(r);
        }

        if (additionalRecords is not null && additionalRecords.Count > 0)
        {
            var nonEmpty = additionalRecords.Where(static r => !string.IsNullOrWhiteSpace(r.Text));
            _records.AddRange(nonEmpty);
        }

        if (_records.Count == 0)
            throw new InvalidOperationException("No highlights in corpus.");

        // Keep first occurrence for duplicate non-empty ids so unchanged rows can reuse prior vectors deterministically.
        var deduped = new List<HighlightRecord>(_records.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rec in _records)
        {
            var id = rec.Id.Trim();
            if (id.Length == 0)
            {
                deduped.Add(rec);
                continue;
            }

            if (seenIds.Add(id))
                deduped.Add(rec);
        }

        _records = deduped;
        var currentIds = _records
            .Select(static r => r.Id.Trim())
            .Where(static id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<string, (HighlightRecord Record, float[] Vector)> baselineById = new(StringComparer.Ordinal);
        string? baselineMessage = null;
        if (!forceFullRebuild &&
            PersistentIndexStore.TryLoadLatestForModel(_embeddingModel, out var baselineRecords, out var baselineVectors, out baselineMessage) &&
            baselineRecords is not null && baselineVectors is not null)
        {
            for (var i = 0; i < baselineRecords.Count; i++)
            {
                var id = baselineRecords[i].Id.Trim();
                if (id.Length == 0 || baselineById.ContainsKey(id))
                    continue;
                baselineById[id] = (baselineRecords[i], baselineVectors[i]);
            }
            progress?.Report($"Incremental baseline loaded ({baselineById.Count} prior row ids).");
        }
        else if (!string.IsNullOrWhiteSpace(baselineMessage))
        {
            progress?.Report($"No incremental baseline: {baselineMessage}");
        }
        var deletedRowsFromBaseline = baselineById.Keys.Count(k => !currentIds.Contains(k));

        const int batchSize = 32;
        var allVectors = new float[_records.Count][];
        var toEmbedIndices = new List<int>(_records.Count);
        var toEmbedTexts = new List<string>(_records.Count);
        var reusedCount = 0;

        for (var i = 0; i < _records.Count; i++)
        {
            var rec = _records[i];
            var newHash = ComputeEmbedContentHash(rec);
            rec.EmbedContentHash = newHash;

            var id = rec.Id.Trim();
            if (id.Length > 0 && baselineById.TryGetValue(id, out var baseline))
            {
                var baselineHash = string.IsNullOrWhiteSpace(baseline.Record.EmbedContentHash)
                    ? ComputeEmbedContentHash(baseline.Record)
                    : baseline.Record.EmbedContentHash!.Trim();

                if (string.Equals(baselineHash, newHash, StringComparison.OrdinalIgnoreCase))
                {
                    allVectors[i] = baseline.Vector;
                    reusedCount++;
                    continue;
                }
            }

            toEmbedIndices.Add(i);
            toEmbedTexts.Add(rec.EmbedText);
        }

        var embedCount = toEmbedIndices.Count;
        if (embedCount == 0)
        {
            progress?.Report($"No text changes detected. Reused vectors for all {_records.Count} rows.");
        }

        var batches = (embedCount + batchSize - 1) / batchSize;
        for (var b = 0; b < batches; b++)
        {
            ct.ThrowIfCancellationRequested();
            var start = b * batchSize;
            var take = Math.Min(batchSize, embedCount - start);
            var chunk = toEmbedTexts.Skip(start).Take(take).ToArray();
            progress?.Report($"Gemini embedding batch {b + 1}/{batches} ({take} changed/new items; reused {reusedCount})…");
            var emb = await client.BatchEmbedDocumentsAsync(_embeddingModel, chunk, ct).ConfigureAwait(false);
            for (var i = 0; i < emb.Length; i++)
                allVectors[toEmbedIndices[start + i]] = emb[i];

            if (b < batches - 1 && delayBetweenEmbeddingBatchesMs > 0)
            {
                var s = delayBetweenEmbeddingBatchesMs / 1000;
                progress?.Report($"Rate limit: waiting {s}s before next batch…");
                await Task.Delay(delayBetweenEmbeddingBatchesMs, ct).ConfigureAwait(false);
            }
        }

        if (allVectors.Any(static v => v is null))
            throw new InvalidOperationException("Incremental build produced missing vectors.");

        _index.Load(allVectors, _records);
        _ready = true;
        progress?.Report("Saving index to disk…");
        await PersistentIndexStore.SaveAsync(path, _embeddingModel, cacheSignature, _records, allVectors, ct).ConfigureAwait(false);
        progress?.Report($"Index ready ({_records.Count} highlights; embedded {embedCount}, reused {reusedCount}).");
        return new BuildIndexStats(
            TotalRows: _records.Count,
            EmbeddedRows: embedCount,
            ReusedRows: reusedCount,
            DeletedRowsFromBaseline: deletedRowsFromBaseline,
            LoadedFromExactCache: false);
    }

    public async Task<(string Answer, IReadOnlyList<(HighlightRecord Record, float Score)> Passages, string RetrievalBanner)> AskAsync(
        GeminiClient client,
        string question,
        int topK,
        CancellationToken ct = default)
    {
        if (!_ready || _records.Count == 0)
            throw new InvalidOperationException("Index not built.");

        ct.ThrowIfCancellationRequested();

        var scope = BookScopeResult.Resolve(
            question,
            _records,
            _options.BookScopeMinTitleChars,
            _options.BookScopeEnabled);

        var yearIndices = _options.YearScopeEnabled
            ? YearScopeResolver.TryGetScopedRecordIndices(question, _records)
            : null;
        var mentionedYears = YearScopeResolver.MentionedYears(question);

        if (yearIndices is not null && yearIndices.Count == 0)
        {
            var yList = string.Join(", ", mentionedYears);
            var msg =
                "No highlights match a notebook last-accessed year in " + yList + ". "
                + "Rebuild your corpus with the latest parse_dump (field last_accessed) and rebuild the index with Force full re-embed.";
            return (msg, Array.Empty<(HighlightRecord, float)>(),
                $"Retrieval: year filter ({yList}) — 0 rows with last_accessed in corpus.");
        }

        var combined = RetrievalScope.Combine(scope.Indices, yearIndices);
        if (combined is not null && combined.Count == 0)
        {
            var yList = string.Join(", ", mentionedYears);
            return (
                "No highlights match both the book filter and the year filter. Try widening your question (different year or book).",
                Array.Empty<(HighlightRecord, float)>(),
                $"Retrieval: book ∩ year empty (years {yList}).");
        }

        IReadOnlyList<string>? scopedTitlesLog = null;
        var candidateCount = _records.Count;
        if (combined is not null)
            candidateCount = combined.Count;
        else if (scope.Indices is not null)
            candidateCount = scope.Indices.Count;

        if (scope.Indices is not null)
        {
            scopedTitlesLog = scope.Indices
                .Select(i => _records[i].BookTitle.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(t => t.Length)
                .ToList();
        }

        var banner = scope.BannerText;
        if (yearIndices is not null)
        {
            var yList = string.Join(", ", mentionedYears);
            banner += $" Year filter: {yList} → {yearIndices.Count} highlight(s) with last_accessed in those years.";
        }

        var q = await client.EmbedQueryAsync(_embeddingModel, question, ct).ConfigureAwait(false);
        var hits = _index.Search(q, topK, combined);
        var passages = hits.ToList();

        RagQueryLogger.Log(
            _options.RagLoggingEnabled,
            _embeddingModel,
            _chatModel,
            question,
            topK,
            combined is not null,
            scopedTitlesLog,
            _records.Count,
            candidateCount,
            passages,
            banner,
            scope.LogDetail + (yearIndices is not null ? $" YearScope: years={string.Join(',', mentionedYears)} yearRows={yearIndices.Count}" : ""));

        var ctx = string.Join("\n\n", passages.Select((p, i) =>
            $"[{i + 1}] {p.Record.CitationLine}\n{p.Record.Text}"));

        ct.ThrowIfCancellationRequested();

        var answer = await client.GenerateContentAsync(
            _chatModel,
            "You answer using ONLY the provided reading highlights. Cite book titles when relevant. "
            + "Each passage may include [accessed YYYY-MM-DD] — that is when the highlight appeared in Kindle Notebook, not necessarily when the book was published. "
            + "If passages do not support an answer, say so briefly. "
            + "Do not include the current calendar date, time of day, or phrases like 'as of [date]' unless that exact wording appears in a passage.",
            $"Question:\n{question}\n\nPassages:\n{ctx}",
            0.3,
            ct).ConfigureAwait(false);

        return (answer, passages, banner);
    }
}

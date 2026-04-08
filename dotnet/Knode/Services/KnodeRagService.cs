using System.IO;
using System.Linq;
using System.Text.Json;

namespace Knode.Services;

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

    /// <summary>Loads index from disk when corpus hash and embedding model match.</summary>
    public async Task<bool> TryLoadPersistedIndexAsync(string corpusPath, IProgress<string>? progress, CancellationToken ct = default)
    {
        _ready = false;
        var path = Path.GetFullPath(corpusPath);
        if (!File.Exists(path))
            return false;

        progress?.Report("Checking saved index…");
        var shaHex = await PersistentIndexStore.ComputeCorpusSha256HexAsync(path, ct).ConfigureAwait(false);
        if (!PersistentIndexStore.TryLoad(_embeddingModel, shaHex, out var recs, out var vecs, out _) ||
            recs is null || vecs is null)
            return false;

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
    public async Task BuildIndexAsync(
        GeminiClient client,
        string corpusPath,
        IProgress<string>? progress,
        int delayBetweenEmbeddingBatchesMs = 18500,
        bool forceFullRebuild = false,
        CancellationToken ct = default)
    {
        _ready = false;
        var path = Path.GetFullPath(corpusPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Corpus file not found.", path);

        progress?.Report("Checking saved index…");
        var corpusSha = await PersistentIndexStore.ComputeCorpusSha256HexAsync(path, ct).ConfigureAwait(false);
        if (!forceFullRebuild &&
            PersistentIndexStore.TryLoad(_embeddingModel, corpusSha, out var cachedRecords, out var cachedVectors, out _) &&
            cachedRecords is not null && cachedVectors is not null)
        {
            _records = cachedRecords;
            _index.Load(cachedVectors, _records);
            _ready = true;
            progress?.Report(
                $"Ready — loaded saved index ({_records.Count} highlights). No Gemini calls. " +
                "Check “Force full re-embed” and click Build index again if you need a fresh embedding pass.");
            return;
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

        if (_records.Count == 0)
            throw new InvalidOperationException("No highlights in corpus.");

        const int batchSize = 32;
        var allVectors = new float[_records.Count][];
        var batches = (_records.Count + batchSize - 1) / batchSize;
        for (var b = 0; b < batches; b++)
        {
            ct.ThrowIfCancellationRequested();
            var start = b * batchSize;
            var take = Math.Min(batchSize, _records.Count - start);
            var chunk = _records.Skip(start).Take(take).Select(x => x.EmbedText).ToArray();
            progress?.Report($"Gemini embedding batch {b + 1}/{batches} ({take} items)…");
            var emb = await client.BatchEmbedDocumentsAsync(_embeddingModel, chunk, ct).ConfigureAwait(false);
            for (var i = 0; i < emb.Length; i++)
                allVectors[start + i] = emb[i];

            if (b < batches - 1 && delayBetweenEmbeddingBatchesMs > 0)
            {
                var s = delayBetweenEmbeddingBatchesMs / 1000;
                progress?.Report($"Rate limit: waiting {s}s before next batch…");
                await Task.Delay(delayBetweenEmbeddingBatchesMs, ct).ConfigureAwait(false);
            }
        }

        _index.Load(allVectors, _records);
        _ready = true;
        progress?.Report("Saving index to disk…");
        await PersistentIndexStore.SaveAsync(path, _embeddingModel, corpusSha, _records, allVectors, ct).ConfigureAwait(false);
        progress?.Report($"Index ready ({_records.Count} highlights).");
    }

    public async Task<(string Answer, IReadOnlyList<(HighlightRecord Record, float Score)> Passages, string RetrievalBanner)> AskAsync(
        GeminiClient client,
        string question,
        int topK,
        CancellationToken ct = default)
    {
        if (!_ready || _records.Count == 0)
            throw new InvalidOperationException("Index not built.");

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

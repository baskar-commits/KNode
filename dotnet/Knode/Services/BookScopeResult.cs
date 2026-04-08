namespace Knode.Services;

/// <summary>Outcome of stage-1 metadata filtering (for UI and logs).</summary>
public sealed class BookScopeResult
{
    public IReadOnlySet<int>? Indices { get; init; }

    /// <summary>One line for the UI (retrieval banner).</summary>
    public string BannerText { get; init; } = "";

    /// <summary>Deeper detail for logs.</summary>
    public string LogDetail { get; init; } = "";

    public static BookScopeResult Resolve(
        string question,
        IReadOnlyList<HighlightRecord> records,
        int minTitleChars,
        bool enabled)
    {
        if (!enabled)
            return new BookScopeResult
            {
                Indices = null,
                BannerText = "Retrieval: full library (book filter off in settings).",
                LogDetail = "BookScope:Enabled=false",
            };

        if (string.IsNullOrWhiteSpace(question) || records.Count == 0)
            return new BookScopeResult
            {
                Indices = null,
                BannerText = "Retrieval: full library.",
                LogDetail = "BookScope: empty question or no records",
            };

        var recordsWithScopeLabel = records.Count(r =>
            BookScopeResolver.EffectiveScopeLabelNorm(r, minTitleChars).Length > 0);
        if (recordsWithScopeLabel == 0)
            return new BookScopeResult
            {
                Indices = null,
                BannerText =
                    "Retrieval: full library — no usable book label in highlights (need non-empty book_title or author, each at least min length after normalize). Rebuild corpus or check JSON fields.",
                LogDetail = "BookScope: no EffectiveScopeLabel for any record",
            };

        var indices = BookScopeResolver.TryGetScopedRecordIndices(question, records, minTitleChars, enabled);
        if (indices is null)
        {
            var sampleRec = records.FirstOrDefault(r =>
                BookScopeResolver.EffectiveScopeDisplay(r, minTitleChars).Length > 0);
            var sample = sampleRec is null
                ? ""
                : BookScopeResolver.EffectiveScopeDisplay(sampleRec, minTitleChars);
            var sampleHint = sample.Length > 0 && sample.Length <= 80
                ? $" Example in corpus: «{sample}»"
                : "";
            return new BookScopeResult
            {
                Indices = null,
                BannerText =
                    $"Retrieval: full library — no corpus book label found inside your question (min length {minTitleChars} chars). Paste the title (or author field if your export uses it) as in the corpus.{sampleHint}",
                LogDetail = $"BookScope: no title substring match (minTitleChars={minTitleChars}, recordsWithScopeLabel={recordsWithScopeLabel})",
            };
        }

        var titles = indices
            .Select(i => BookScopeResolver.EffectiveScopeDisplay(records[i], minTitleChars))
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(t => t.Length)
            .ToList();
        var titleList = string.Join("; ", titles.Take(3));
        if (titles.Count > 3)
            titleList += $" (+{titles.Count - 3} more)";

        return new BookScopeResult
        {
            Indices = indices,
            BannerText = $"Retrieval: scoped to {indices.Count} highlight(s) from {titles.Count} book(s): {titleList}",
            LogDetail = $"BookScope: scopedRecords={indices.Count} books={titles.Count} titles={titleList}",
        };
    }
}

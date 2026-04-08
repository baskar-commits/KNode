using System.Text;

namespace Knode.Services;

/// <summary>
/// Stage-1 metadata filter: finds highlights whose book label appears in the user question.
/// Uses <see cref="HighlightRecord.BookTitle"/> when long enough; otherwise <see cref="HighlightRecord.Author"/>
/// (some exports leave <c>book_title</c> empty but put the work name in <c>author</c>).
/// Prunes shorter labels dominated by longer matches (e.g. a phrase inside a full title).
/// Returns record indices to search; null means search the full index (two-stage retrieval falls back to global).
/// </summary>
public static class BookScopeResolver
{
    /// <summary>Normalize quotes, NBSP, Unicode dashes, and compatibility forms so Kindle vs pasted text still matches.</summary>
    public static string NormalizeForMatch(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        var t = s.Trim()
            .Replace('\u00a0', ' ')
            .Replace('\u202f', ' ');
        var sb = new StringBuilder(t.Length);
        foreach (var c in t)
        {
            if (c is '\u2013' or '\u2014' or '\u2212')
                sb.Append('-');
            else
                sb.Append(c);
        }
        t = sb.ToString();
        t = t.Replace("\u201c", "", StringComparison.Ordinal)
            .Replace("\u201d", "", StringComparison.Ordinal)
            .Replace("\u2018", "", StringComparison.Ordinal)
            .Replace("\u2019", "", StringComparison.Ordinal);
        t = t.Trim();
        return t.Normalize(NormalizationForm.FormKC);
    }

    /// <summary>Normalized string used to match the question to a book (empty if neither field is long enough).</summary>
    public static string EffectiveScopeLabelNorm(HighlightRecord r, int minTitleChars)
    {
        var bt = NormalizeForMatch(r.BookTitle);
        if (bt.Length >= minTitleChars)
            return bt;
        var au = NormalizeForMatch(r.Author);
        return au.Length >= minTitleChars ? au : "";
    }

    /// <summary>Original text for UI (same preference rule as <see cref="EffectiveScopeLabelNorm"/>).</summary>
    public static string EffectiveScopeDisplay(HighlightRecord r, int minTitleChars)
    {
        if (NormalizeForMatch(r.BookTitle).Length >= minTitleChars)
            return (r.BookTitle ?? "").Trim();
        if (NormalizeForMatch(r.Author).Length >= minTitleChars)
            return (r.Author ?? "").Trim();
        return "";
    }

    /// <param name="minTitleChars">Titles shorter than this are ignored for matching (reduces noisy single-word matches).</param>
    public static HashSet<int>? TryGetScopedRecordIndices(
        string question,
        IReadOnlyList<HighlightRecord> records,
        int minTitleChars,
        bool enabled)
    {
        if (!enabled || string.IsNullOrWhiteSpace(question) || records.Count == 0)
            return null;

        var qNorm = NormalizeForMatch(question);

        var distinctTitles = records
            .Select(r => EffectiveScopeLabelNorm(r, minTitleChars))
            .Where(t => t.Length >= minTitleChars)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var containedInQuestion = distinctTitles
            .Where(t => qNorm.Contains(t, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.Length)
            .ToList();

        if (containedInQuestion.Count == 0)
            return null;

        var scopedTitles = new List<string>();
        foreach (var t in containedInQuestion)
        {
            var dominated = scopedTitles.Any(p =>
                p.Length > t.Length &&
                p.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (dominated)
                continue;
            scopedTitles.Add(t);
        }

        var titleSet = scopedTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var indices = new HashSet<int>();
        for (var i = 0; i < records.Count; i++)
        {
            var label = EffectiveScopeLabelNorm(records[i], minTitleChars);
            if (label.Length > 0 && titleSet.Contains(label))
                indices.Add(i);
        }

        return indices.Count == 0 ? null : indices;
    }
}

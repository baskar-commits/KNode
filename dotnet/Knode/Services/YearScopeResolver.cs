using System.Text.RegularExpressions;

namespace Knode.Services;

/// <summary>
/// When the user mentions a year (e.g. 2022), restrict retrieval to highlights whose
/// <see cref="HighlightRecord.LastAccessed"/> (Notebook “last accessed” date) falls in that year.
/// Uses <c>20xx</c> only in the question to avoid treating book titles like <i>1984</i> as years.
/// </summary>
public static class YearScopeResolver
{
    // Reading-era years; excludes 19xx (e.g. "1984" the book).
    private static readonly Regex YearRegex = new(@"\b(20\d{2})\b", RegexOptions.Compiled);

    public static IReadOnlyList<int> MentionedYears(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return Array.Empty<int>();
        return YearRegex.Matches(question)
            .Select(m => int.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(y => y)
            .ToList();
    }

    /// <summary>null = no year mentioned; empty = mentioned but no rows have that year in <see cref="HighlightRecord.LastAccessed"/>.</summary>
    public static HashSet<int>? TryGetScopedRecordIndices(string question, IReadOnlyList<HighlightRecord> records)
    {
        var years = MentionedYears(question);
        if (years.Count == 0)
            return null;

        var yearSet = years.ToHashSet();
        var indices = new HashSet<int>();
        for (var i = 0; i < records.Count; i++)
        {
            if (TryGetYear(records[i].LastAccessed, out var y) && yearSet.Contains(y))
                indices.Add(i);
        }

        return indices;
    }

    /// <summary>Parse leading year from <c>YYYY-MM-DD</c> (Kindle export).</summary>
    public static bool TryGetYear(string? isoDate, out int year)
    {
        year = 0;
        if (string.IsNullOrWhiteSpace(isoDate) || isoDate.Length < 4)
            return false;
        if (!int.TryParse(isoDate.AsSpan(0, 4), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out year))
            return false;
        return year is >= 2000 and <= 2099;
    }
}

namespace Knode.Services;

public static class CitationFormatter
{
    /// <summary>Readable citation block for the passages panel (no raw IDs, no timestamps).</summary>
    public static string FormatPassage(int rank, HighlightRecord r, float cosineScore, int maxQuoteChars = 900)
    {
        var title = string.IsNullOrWhiteSpace(r.BookTitle) ? r.Author.Trim() : r.BookTitle.Trim();
        var authorPart = string.IsNullOrWhiteSpace(r.Author) || string.Equals(r.Author.Trim(), title, StringComparison.Ordinal)
            ? ""
            : $" — {r.Author.Trim()}";
        var source = string.IsNullOrWhiteSpace(r.Source) ? "kindle" : r.Source.Trim().ToLowerInvariant();
        var locLabel = source == "onenote" ? "Page" : "Loc.";
        var loc = string.IsNullOrWhiteSpace(r.Location) ? "" : $" · {locLabel} {r.Location.Trim()}";
        var matchPct = Math.Clamp((int)Math.Round(Math.Max(0, Math.Min(1, cosineScore)) * 100), 0, 100);

        var body = r.Text.Trim().Replace("\r\n", "\n");
        if (body.Length > maxQuoteChars)
            body = body[..maxQuoteChars].TrimEnd() + "…";

        return $"◆ {rank}. {title}{authorPart}{loc}\n   Match ~{matchPct}%\n\n   {body}\n";
    }
}

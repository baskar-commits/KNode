using System.Text;

namespace Knode.Services;

/// <summary>Builds Markdown for the sources panel (rendered to HTML in WebView2).</summary>
public static class SourcesMarkdown
{
    public static string FromPassages(IReadOnlyList<(HighlightRecord Record, float Score)> passages, int maxQuoteChars = 900)
    {
        if (passages.Count == 0)
            return "_No passages retrieved._";

        var sb = new StringBuilder(passages.Count * 256);
        for (var i = 0; i < passages.Count; i++)
        {
            var p = passages[i];
            var r = p.Record;
            var title = string.IsNullOrWhiteSpace(r.BookTitle) ? r.Author.Trim() : r.BookTitle.Trim();
            var authorPart = string.IsNullOrWhiteSpace(r.Author) || string.Equals(r.Author.Trim(), title, StringComparison.Ordinal)
                ? ""
                : $" — {r.Author.Trim()}";
            var source = string.IsNullOrWhiteSpace(r.Source) ? "kindle" : r.Source.Trim().ToLowerInvariant();
            var locLabel = source == "onenote" ? "Page" : "Loc.";
            var loc = string.IsNullOrWhiteSpace(r.Location) ? "" : $" · {locLabel} {r.Location.Trim()}";
            var matchPct = Math.Clamp((int)Math.Round(Math.Max(0, Math.Min(1, p.Score)) * 100), 0, 100);

            var body = r.Text.Trim().Replace("\r\n", "\n");
            if (body.Length > maxQuoteChars)
                body = body[..maxQuoteChars].TrimEnd() + "…";

            sb.Append("### ◆ ").Append(i + 1).Append(". ").Append(title).Append(authorPart).Append(loc);
            sb.Append("\n\n**Match ~").Append(matchPct).Append("%**\n\n");
            foreach (var line in body.Split('\n'))
                sb.Append("> ").Append(line).Append('\n');
            sb.Append('\n');
        }

        return sb.ToString();
    }
}

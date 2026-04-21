using System.Text.Json.Serialization;

namespace Knode.Services;

/// <summary>One row from corpus.jsonl (kindle_agent.parse_dump output).</summary>
public sealed class HighlightRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("source_section_id")]
    public string? SourceSectionId { get; set; }

    /// <summary>Stable SHA-256 over <see cref="EmbedText"/> used for incremental re-embed decisions.</summary>
    [JsonPropertyName("embed_content_hash")]
    public string? EmbedContentHash { get; set; }

    [JsonPropertyName("book_title")]
    public string BookTitle { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>ISO date <c>YYYY-MM-DD</c> from Kindle Notebook “last accessed” (<c>parse_dump</c>).</summary>
    [JsonPropertyName("last_accessed")]
    public string LastAccessed { get; set; } = "";

    public string EmbedText
    {
        get
        {
            var src = string.IsNullOrWhiteSpace(Source) ? "kindle" : Source;
            var meta = $"{BookTitle}\n{Author}\nSource: {src}";
            if (!string.IsNullOrWhiteSpace(LastAccessed))
                meta += $"\nLast accessed: {LastAccessed}";
            return meta + $"\n{Text}" + (string.IsNullOrEmpty(Note) ? "" : $"\n[Your note: {Note}]");
        }
    }

    public string CitationLine
    {
        get
        {
            var source = string.IsNullOrWhiteSpace(Source) ? "kindle" : Source;
            var locationLabel = source.Equals("onenote", StringComparison.OrdinalIgnoreCase) ? "Page" : "Location";
            var cite = string.IsNullOrWhiteSpace(Location)
                ? $"{BookTitle} — {Author}"
                : $"{BookTitle} — {Author} ({locationLabel} {Location})";
            if (!string.IsNullOrWhiteSpace(LastAccessed))
                cite += $" [accessed {LastAccessed}]";
            return cite;
        }
    }
}

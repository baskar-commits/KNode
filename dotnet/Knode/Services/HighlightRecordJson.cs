using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Knode.Services;

/// <summary>Shared JSON parsing for corpus.jsonl and persisted records.json.</summary>
public static class HighlightRecordJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Strip BOM / trim so <see cref="JsonNode.Parse(string)"/> does not fail on saved files.</summary>
    public static string PrepareJsonText(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        return raw.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
    }

    /// <summary>One line of corpus.jsonl (parse_dump / Python shape).</summary>
    public static HighlightRecord? DeserializeCorpusLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;
        try
        {
            var node = JsonNode.Parse(line);
            if (node is not JsonObject obj)
                return null;
            var r = obj.Deserialize<HighlightRecord>(Options);
            if (r is null)
                return null;
            CoalesceFromObject(obj, r);
            InferBookTitleFromUnknownKeys(obj, r);
            Normalize(r);
            return r;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Persisted <c>records.json</c> array (same rows as corpus).</summary>
    public static List<HighlightRecord>? DeserializeRecordsArray(string json)
    {
        json = PrepareJsonText(json);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonArray arr)
                return null;
            var list = new List<HighlightRecord>(arr.Count);
            foreach (var item in arr)
            {
                if (item is not JsonObject obj)
                    continue;
                var r = obj.Deserialize<HighlightRecord>(Options);
                if (r is null)
                    continue;
                CoalesceFromObject(obj, r);
                InferBookTitleFromUnknownKeys(obj, r);
                Normalize(r);
                list.Add(r);
            }

            return list.Count == 0 ? null : list;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// When index rows have no <c>book_title</c>, reload corpus and copy metadata by stable <c>id</c> (same as parse_dump).
    /// </summary>
    public static void BackfillMetadataFromCorpusById(IList<HighlightRecord> records, string corpusPath)
    {
        if (records.Count == 0 || !File.Exists(corpusPath))
            return;

        var needsTitle = records.Any(r => string.IsNullOrWhiteSpace(r.BookTitle));
        if (!needsTitle)
            return;

        var byId = new Dictionary<string, HighlightRecord>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(corpusPath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var r = DeserializeCorpusLine(line);
            if (r != null && !string.IsNullOrEmpty(r.Id) && !byId.ContainsKey(r.Id))
                byId[r.Id] = r;
        }

        if (byId.Count == 0)
            return;

        foreach(var rec in records)
        {
            if (!byId.TryGetValue(rec.Id, out var src))
                continue;
            if (string.IsNullOrWhiteSpace(rec.BookTitle) && !string.IsNullOrWhiteSpace(src.BookTitle))
                rec.BookTitle = src.BookTitle;
            if (string.IsNullOrWhiteSpace(rec.Author) && !string.IsNullOrWhiteSpace(src.Author))
                rec.Author = src.Author;
            if (string.IsNullOrWhiteSpace(rec.LastAccessed) && !string.IsNullOrWhiteSpace(src.LastAccessed))
                rec.LastAccessed = src.LastAccessed;
        }
    }

    private static void CoalesceFromObject(JsonObject obj, HighlightRecord r)
    {
        if (string.IsNullOrWhiteSpace(r.Source))
            r.Source = FirstString(obj, "source", "Source") ?? "";

        if (string.IsNullOrWhiteSpace(r.SourceSectionId))
            r.SourceSectionId = FirstString(obj, "source_section_id", "sourceSectionId", "SourceSectionId");

        if (string.IsNullOrWhiteSpace(r.EmbedContentHash))
            r.EmbedContentHash = FirstString(obj, "embed_content_hash", "embedContentHash", "EmbedContentHash");

        if (string.IsNullOrWhiteSpace(r.BookTitle))
            r.BookTitle = FirstString(obj, "book_title", "BookTitle", "bookTitle", "title", "Title", "book_name", "BookName") ?? "";

        if (string.IsNullOrWhiteSpace(r.Author))
            r.Author = FirstString(obj, "author", "Author") ?? "";

        if (string.IsNullOrWhiteSpace(r.Id))
            r.Id = FirstString(obj, "id", "Id") ?? "";

        if (string.IsNullOrWhiteSpace(r.Location))
            r.Location = FirstString(obj, "location", "Location") ?? "";

        if (string.IsNullOrWhiteSpace(r.Text))
            r.Text = FirstString(obj, "text", "Text") ?? "";

        if (string.IsNullOrWhiteSpace(r.Note))
            r.Note = FirstString(obj, "note", "Note");

        if (string.IsNullOrWhiteSpace(r.LastAccessed))
            r.LastAccessed = FirstString(obj, "last_accessed", "LastAccessed", "lastAccessed") ?? "";
    }

    /// <summary>Last resort: any JSON string property whose name looks like a book/title field.</summary>
    private static void InferBookTitleFromUnknownKeys(JsonObject obj, HighlightRecord r)
    {
        if (!string.IsNullOrWhiteSpace(r.BookTitle))
            return;

        foreach (var kv in obj)
        {
            if (kv.Value is JsonObject or JsonArray)
                continue;
            if (!LooksLikeBookTitlePropertyName(kv.Key))
                continue;
            var s = JsonNodeToString(kv.Value);
            if (!string.IsNullOrWhiteSpace(s))
            {
                r.BookTitle = s;
                return;
            }
        }

        if (obj["meta"] is JsonObject meta)
            CoalesceFromObject(meta, r);
    }

    private static bool LooksLikeBookTitlePropertyName(string key)
    {
        var k = key.ToLowerInvariant().Replace("_", "").Replace("-", "");
        if (k is "booktitle" or "title" or "book" or "bookname" or "worktitle" or "name")
            return true;
        if (k.Contains("booktitle", StringComparison.Ordinal))
            return true;
        return k.Contains("book", StringComparison.Ordinal) && k.Contains("title", StringComparison.Ordinal);
    }

    private static string? JsonNodeToString(JsonNode? n)
    {
        if (n is null)
            return null;
        try
        {
            return n.GetValue<string>();
        }
        catch
        {
            var t = n.ToString();
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }
    }

    private static string? FirstString(JsonObject obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            var n = obj[k];
            var s = JsonNodeToString(n);
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }

        return null;
    }

    private static void Normalize(HighlightRecord r)
    {
        r.Id ??= "";
        r.Source ??= "";
        r.SourceSectionId ??= "";
        r.EmbedContentHash ??= "";
        r.BookTitle ??= "";
        r.Author ??= "";
        r.Location ??= "";
        r.Text ??= "";
        r.LastAccessed ??= "";
    }
}

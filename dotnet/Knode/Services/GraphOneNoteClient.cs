using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Knode.Services;

public sealed class OneNoteNotebook
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
}

public sealed class OneNoteSection
{
    public string Id { get; init; } = "";
    public string NotebookId { get; init; } = "";
    public string NotebookName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Label => $"{NotebookName} / {DisplayName}";
}

public sealed class OneNotePage
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string ContentUrl { get; init; } = "";
    public string WebUrl { get; init; } = "";
    public DateTimeOffset LastModified { get; init; }
}

public sealed class GraphOneNoteClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };
    private readonly string _token;

    public GraphOneNoteClient(string bearerToken)
    {
        _token = bearerToken;
    }

    public async Task<IReadOnlyList<OneNoteNotebook>> GetNotebooksAsync(CancellationToken ct = default)
    {
        var url = "https://graph.microsoft.com/v1.0/me/onenote/notebooks?$select=id,displayName";
        var result = new List<OneNoteNotebook>();
        while (!string.IsNullOrEmpty(url))
        {
            using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
            foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? "" : "";
                if (id.Length == 0)
                    continue;
                var name = item.TryGetProperty("displayName", out var d) ? d.GetString() ?? "(untitled notebook)" : "(untitled notebook)";
                result.Add(new OneNoteNotebook { Id = id, DisplayName = name });
            }

            url = TryGetNextLink(doc.RootElement);
        }

        return result;
    }

    public async Task<IReadOnlyList<OneNoteSection>> GetSectionsAsync(IReadOnlyList<OneNoteNotebook> notebooks, CancellationToken ct = default)
    {
        var byNotebook = notebooks.ToDictionary(n => n.Id, n => n.DisplayName, StringComparer.Ordinal);
        var url = "https://graph.microsoft.com/v1.0/me/onenote/sections?$select=id,displayName,parentNotebook";
        var result = new List<OneNoteSection>();
        while (!string.IsNullOrEmpty(url))
        {
            using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
            foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? "" : "";
                if (id.Length == 0)
                    continue;
                var sectionName = item.TryGetProperty("displayName", out var d) ? d.GetString() ?? "(untitled section)" : "(untitled section)";
                var notebookId = "";
                var notebookNameFromParent = "";
                if (item.TryGetProperty("parentNotebook", out var parent))
                {
                    notebookId = parent.TryGetProperty("id", out var pId) ? pId.GetString() ?? "" : "";
                    if (parent.TryGetProperty("displayName", out var pDisp))
                        notebookNameFromParent = pDisp.GetString()?.Trim() ?? "";
                }

                var notebookName = !string.IsNullOrEmpty(notebookNameFromParent)
                    ? notebookNameFromParent
                    : byNotebook.TryGetValue(notebookId, out var mapped)
                        ? mapped
                        : "(unknown notebook)";
                result.Add(new OneNoteSection
                {
                    Id = id,
                    DisplayName = sectionName,
                    NotebookId = notebookId,
                    NotebookName = notebookName,
                });
            }

            url = TryGetNextLink(doc.RootElement);
        }

        return result;
    }

    public async Task<IReadOnlyList<OneNotePage>> GetPagesForSectionAsync(string sectionId, DateTimeOffset? modifiedAfterUtc, CancellationToken ct = default)
    {
        var url =
            $"https://graph.microsoft.com/v1.0/me/onenote/sections/{Uri.EscapeDataString(sectionId)}/pages" +
            "?$select=id,title,lastModifiedDateTime,contentUrl,links&$top=100";
        var pages = new List<OneNotePage>();
        while (!string.IsNullOrEmpty(url))
        {
            using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
            foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? "" : "";
                if (id.Length == 0)
                    continue;
                var modified = DateTimeOffset.MinValue;
                if (item.TryGetProperty("lastModifiedDateTime", out var modNode)
                    && DateTimeOffset.TryParse(modNode.GetString(), out var parsed))
                {
                    modified = parsed.ToUniversalTime();
                }

                if (modifiedAfterUtc is not null && modified <= modifiedAfterUtc.Value)
                    continue;

                var webUrl = "";
                if (item.TryGetProperty("links", out var links)
                    && links.TryGetProperty("oneNoteWebUrl", out var web)
                    && web.TryGetProperty("href", out var href))
                {
                    webUrl = href.GetString() ?? "";
                }

                pages.Add(new OneNotePage
                {
                    Id = id,
                    Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? "(untitled page)" : "(untitled page)",
                    ContentUrl = item.TryGetProperty("contentUrl", out var content) ? content.GetString() ?? "" : "",
                    LastModified = modified,
                    WebUrl = webUrl,
                });
            }

            url = TryGetNextLink(doc.RootElement);
        }

        return pages;
    }

    public async Task<string> GetPagePlainTextAsync(string contentUrl, int maxChars = 12000, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contentUrl))
            return "";
        using var req = new HttpRequestMessage(HttpMethod.Get, contentUrl);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"OneNote page content fetch failed ({(int)resp.StatusCode}): {raw}");

        var noScript = Regex.Replace(raw, "<(script|style)[^>]*>.*?</\\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var noTags = Regex.Replace(noScript, "<[^>]+>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(noTags);
        var squashed = Regex.Replace(decoded, @"\s+", " ").Trim();
        if (squashed.Length > maxChars)
            return squashed[..maxChars].TrimEnd() + "…";
        return squashed;
    }

    public void Dispose() => _http.Dispose();

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Graph request failed ({(int)resp.StatusCode}): {body}");
        return JsonDocument.Parse(body);
    }

    private static string? TryGetNextLink(JsonElement root)
    {
        if (!root.TryGetProperty("@odata.nextLink", out var next))
            return null;
        return next.GetString();
    }
}

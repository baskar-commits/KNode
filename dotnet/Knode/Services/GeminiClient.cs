using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knode.Services;

/// <summary>Google Gemini API (embeddings + generateContent). Key from Google AI Studio.</summary>
public sealed class GeminiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public GeminiClient(string apiKey, string baseUrl = "https://generativelanguage.googleapis.com/v1beta/")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Gemini API key is required.", nameof(apiKey));
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/') + "/";
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task<float[][]> BatchEmbedDocumentsAsync(
        string model,
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        if (texts.Count == 0)
            return Array.Empty<float[]>();

        // Each batch item must include model per Gemini API (INVALID_ARGUMENT if omitted).
        var pathId = model.StartsWith("models/", StringComparison.Ordinal) ? model["models/".Length..] : model;
        var modelResource = $"models/{pathId}";

        var url = $"{_baseUrl}models/{Uri.EscapeDataString(pathId)}:batchEmbedContents?key={Uri.EscapeDataString(_apiKey)}";
        var requests = texts.Select(t => new BatchEmbedItem
        {
            Model = modelResource,
            Content = new ContentObj { Parts = new[] { new PartObj { Text = t } } },
            TaskType = "RETRIEVAL_DOCUMENT",
        }).ToArray();

        var body = new BatchEmbedBody { Requests = requests };
        var jsonBody = JsonSerializer.Serialize(body, JsonOpts);

        const int maxAttempts = 6;
        string json = "";
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxAttempts)
            {
                var wait = ParseRetryAfterDelay(resp, json, attempt);
                await Task.Delay(wait, ct).ConfigureAwait(false);
                continue;
            }

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Gemini batchEmbed HTTP {(int)resp.StatusCode}: {json}");
            break;
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("embeddings", out var arr))
            throw new InvalidOperationException("Gemini batchEmbed: missing 'embeddings'.");

        var result = new float[arr.GetArrayLength()][];
        var i = 0;
        foreach (var el in arr.EnumerateArray())
        {
            var vals = el.GetProperty("values");
            var emb = new float[vals.GetArrayLength()];
            var j = 0;
            foreach (var n in vals.EnumerateArray())
                emb[j++] = (float)n.GetDouble();
            result[i++] = emb;
        }
        if (i != texts.Count)
            throw new InvalidOperationException($"Gemini batchEmbed: expected {texts.Count} vectors, got {i}.");
        return result;
    }

    public async Task<float[]> EmbedQueryAsync(string model, string text, CancellationToken ct = default)
    {
        var pathId = model.StartsWith("models/", StringComparison.Ordinal) ? model["models/".Length..] : model;
        var url = $"{_baseUrl}models/{Uri.EscapeDataString(pathId)}:embedContent?key={Uri.EscapeDataString(_apiKey)}";
        var jsonBody = JsonSerializer.Serialize(new EmbedContentBody
        {
            Content = new ContentObj { Parts = new[] { new PartObj { Text = text } } },
            TaskType = "RETRIEVAL_QUERY",
        }, JsonOpts);

        const int maxAttempts = 6;
        string json = "";
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxAttempts)
            {
                await Task.Delay(ParseRetryAfterDelay(resp, json, attempt), ct).ConfigureAwait(false);
                continue;
            }

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Gemini embedContent HTTP {(int)resp.StatusCode}: {json}");
            break;
        }

        using var doc = JsonDocument.Parse(json);
        var vals = doc.RootElement.GetProperty("embedding").GetProperty("values");
        var emb = new float[vals.GetArrayLength()];
        var j = 0;
        foreach (var n in vals.EnumerateArray())
            emb[j++] = (float)n.GetDouble();
        return emb;
    }

    public async Task<string> GenerateContentAsync(
        string model,
        string systemInstruction,
        string userText,
        double temperature,
        CancellationToken ct = default)
    {
        var pathId = model.StartsWith("models/", StringComparison.Ordinal) ? model["models/".Length..] : model;
        var url = $"{_baseUrl}models/{Uri.EscapeDataString(pathId)}:generateContent?key={Uri.EscapeDataString(_apiKey)}";
        var jsonBody = JsonSerializer.Serialize(new GenerateBody
        {
            SystemInstruction = new ContentObj { Parts = new[] { new PartObj { Text = systemInstruction } } },
            Contents = new[]
            {
                new ContentTurn { Role = "user", Parts = new[] { new PartObj { Text = userText } } },
            },
            GenerationConfig = new GenConfig { Temperature = temperature },
        }, JsonOpts);

        const int maxAttempts = 6;
        string json = "";
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxAttempts)
            {
                await Task.Delay(ParseRetryAfterDelay(resp, json, attempt), ct).ConfigureAwait(false);
                continue;
            }

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Gemini generateContent HTTP {(int)resp.StatusCode}: {json}");
            break;
        }

        using var doc = JsonDocument.Parse(json);
        var candidates = doc.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() == 0)
            return "(No response from model — possibly blocked by safety filters.)";

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var t))
                sb.Append(t.GetString());
        }
        return sb.Length > 0 ? sb.ToString() : "(Empty model response.)";
    }

    public void Dispose() => _http.Dispose();

    /// <summary>HTTP 429 body often includes google.rpc.RetryInfo.retryDelay (e.g. "55s").</summary>
    private static TimeSpan ParseRetryAfterDelay(HttpResponseMessage resp, string jsonBody, int attempt)
    {
        if (resp.Headers.RetryAfter?.Delta is { TotalSeconds: > 0 } d)
            return d;

        try
        {
            using var doc = JsonDocument.Parse(jsonBody);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("details", out var details))
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (!detail.TryGetProperty("retryDelay", out var rd))
                        continue;
                    if (rd.ValueKind == JsonValueKind.String)
                    {
                        var s = rd.GetString();
                        if (!string.IsNullOrEmpty(s) && s.EndsWith("s", StringComparison.Ordinal) &&
                            int.TryParse(s.AsSpan(0, s.Length - 1), out var sec) && sec > 0)
                            return TimeSpan.FromSeconds(sec);
                    }
                }
            }
        }
        catch
        {
            // fall through
        }

        return TimeSpan.FromSeconds(Math.Min(90, 10 * attempt));
    }

    private sealed class BatchEmbedBody
    {
        public BatchEmbedItem[] Requests { get; set; } = Array.Empty<BatchEmbedItem>();
    }

    private sealed class BatchEmbedItem
    {
        public string Model { get; set; } = "";
        public ContentObj Content { get; set; } = null!;
        public string TaskType { get; set; } = "";
    }

    private sealed class EmbedContentBody
    {
        public ContentObj Content { get; set; } = null!;
        public string TaskType { get; set; } = "";
    }

    private sealed class ContentObj
    {
        public PartObj[] Parts { get; set; } = Array.Empty<PartObj>();
    }

    private sealed class PartObj
    {
        public string Text { get; set; } = "";
    }

    private sealed class GenerateBody
    {
        public ContentObj SystemInstruction { get; set; } = null!;
        public ContentTurn[] Contents { get; set; } = Array.Empty<ContentTurn>();
        public GenConfig? GenerationConfig { get; set; }
    }

    private sealed class ContentTurn
    {
        public string Role { get; set; } = "";
        public PartObj[] Parts { get; set; } = Array.Empty<PartObj>();
    }

    private sealed class GenConfig
    {
        public double Temperature { get; set; }
    }
}

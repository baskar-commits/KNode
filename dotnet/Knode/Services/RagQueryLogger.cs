using System.IO;
using System.Text;

namespace Knode.Services;

/// <summary>Append-only diagnostic log for RAG queries under %LocalAppData%\Knode\logs\.</summary>
public static class RagQueryLogger
{
    private static readonly object FileLock = new();

    public static void Log(
        bool enabled,
        string embeddingModel,
        string chatModel,
        string question,
        int topKRequested,
        bool scopeApplied,
        IReadOnlyCollection<string>? scopedBookTitles,
        int corpusSize,
        int scopedCandidateCount,
        IReadOnlyList<(HighlightRecord Record, float Score)> hits,
        string bannerText,
        string logDetail)
    {
        if (!enabled)
            return;

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Knode", "logs");
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"rag-{DateTime.UtcNow:yyyyMMdd}.log");
            var sb = new StringBuilder(512);
            sb.AppendLine($"---- {DateTime.UtcNow:O} ----");
            sb.AppendLine($"embeddingModel={embeddingModel} chatModel={chatModel} topK={topKRequested}");
            sb.AppendLine($"corpusRecords={corpusSize} scopeApplied={scopeApplied} scopedCandidates={scopedCandidateCount}");
            sb.AppendLine("detail=" + logDetail);
            if (!string.IsNullOrEmpty(bannerText))
                sb.AppendLine("banner=" + bannerText.Replace('\r', ' ').Replace('\n', ' '));
            if (scopedBookTitles is { Count: > 0 })
                sb.AppendLine("scopedTitles=" + string.Join(" | ", scopedBookTitles));
            var qPreview = question.Length > 480 ? question[..480] + "…" : question;
            sb.AppendLine("question=" + qPreview.Replace('\r', ' ').Replace('\n', ' '));
            sb.AppendLine("hits:");
            for (var i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                sb.AppendLine($"  {i + 1}. score={h.Score:F4} id={h.Record.Id} title={h.Record.BookTitle}");
            }

            sb.AppendLine();
            var line = sb.ToString();
            lock (FileLock)
                File.AppendAllText(path, line, Encoding.UTF8);
        }
        catch
        {
            // Logging must never break Ask.
        }
    }
}

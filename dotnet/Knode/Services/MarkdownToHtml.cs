using System.Text;
using Ganss.Xss;
using Markdig;
using Microsoft.Web.WebView2.Wpf;

namespace Knode.Services;

/// <summary>Renders Markdown to a safe HTML document for <see cref="WebView2"/>.</summary>
public static class MarkdownToHtml
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly HtmlSanitizer Sanitizer = new();

    public static string ToSafeHtmlDocument(string? markdown, string panelTitle)
    {
        var md = string.IsNullOrWhiteSpace(markdown) ? "" : markdown;
        var raw = Markdown.ToHtml(md, Pipeline);
        var safe = Sanitizer.Sanitize(raw);
        return WrapDocument(panelTitle, safe);
    }

    public static async Task NavigateMarkdownAsync(WebView2 webView, string? markdown, string panelTitle)
    {
        if (webView.CoreWebView2 is null)
            await webView.EnsureCoreWebView2Async().ConfigureAwait(true);
        webView.NavigateToString(ToSafeHtmlDocument(markdown, panelTitle));
    }

    public static async Task NavigatePlaceholderAsync(WebView2 webView, string message, string panelTitle)
    {
        var html = WrapDocument(panelTitle, $"""<p class="muted">{System.Net.WebUtility.HtmlEncode(message)}</p>""");
        if (webView.CoreWebView2 is null)
            await webView.EnsureCoreWebView2Async().ConfigureAwait(true);
        webView.NavigateToString(html);
    }

    private static string WrapDocument(string panelTitle, string bodyInnerHtml)
    {
        var title = System.Net.WebUtility.HtmlEncode(panelTitle);
        var sb = new StringBuilder(2048);
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>").Append(title).Append("</title><style>");
        sb.Append("""
            :root { color-scheme: light; }
            body { font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; font-size: 14px; line-height: 1.55; color: #1e293b; margin: 0; padding: 14px 18px; background: #fcfcfd; }
            article { max-width: 52rem; }
            h1 { font-size: 1.35rem; margin: 0 0 0.75rem; color: #0f172a; font-weight: 600; }
            h2 { font-size: 1.12rem; margin: 1.1rem 0 0.5rem; color: #0f172a; font-weight: 600; }
            h3 { font-size: 1.05rem; margin: 1rem 0 0.45rem; color: #334155; font-weight: 600; }
            p { margin: 0.55rem 0; }
            ul, ol { margin: 0.45rem 0 0.55rem; padding-left: 1.35rem; }
            li { margin: 0.25rem 0; }
            strong { font-weight: 600; color: #0f172a; }
            code { font-family: Consolas, 'Cascadia Code', monospace; font-size: 0.9em; background: #f1f5f9; padding: 2px 6px; border-radius: 4px; }
            pre { background: #f1f5f9; padding: 12px 14px; border-radius: 8px; overflow-x: auto; font-size: 13px; }
            pre code { background: none; padding: 0; }
            blockquote { border-left: 4px solid #38bdf8; margin: 0.6rem 0; padding: 0.35rem 0 0.35rem 14px; color: #475569; background: #f8fafc; }
            a { color: #0369a1; }
            .muted { color: #94a3b8; }
            hr { border: none; border-top: 1px solid #e2e8f0; margin: 1.2rem 0; }
            """);
        sb.Append("</style></head><body><article>");
        sb.Append(bodyInnerHtml);
        sb.Append("</article></body></html>");
        return sb.ToString();
    }
}

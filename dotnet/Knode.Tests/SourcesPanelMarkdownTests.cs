using Knode.Services;
using Xunit;

namespace Knode.Tests;

public sealed class SourcesPanelMarkdownTests
{
    [Fact]
    public void SourcesPanelMarkdown_empty_list_returns_empty_coaching_not_placeholder_italic()
    {
        var md = PromptCoach.SourcesPanelMarkdown(Array.Empty<(HighlightRecord, float)>());
        Assert.Contains("No passages were retrieved", md);
        Assert.Contains("What to try next", md);
        Assert.DoesNotContain("_No passages retrieved._", md);
    }

    [Fact]
    public void SourcesPanelMarkdown_single_passage_uses_sources_markdown_with_match_percent()
    {
        var r = new HighlightRecord
        {
            BookTitle = "Test Book",
            Author = "Test Author",
            Location = "123",
            Text = "Hello world highlight text.",
            LastAccessed = "2024-01-15",
        };
        var md = PromptCoach.SourcesPanelMarkdown(new List<(HighlightRecord, float)> { (r, 0.85f) });
        Assert.Contains("Test Book", md);
        Assert.Contains("Hello world", md);
        Assert.Contains("Match ~85%", md);
    }
}

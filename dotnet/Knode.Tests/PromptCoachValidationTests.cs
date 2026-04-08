using Knode.Services;
using Xunit;

namespace Knode.Tests;

/// <summary>Validation tests for P0/P1 prompt-coaching copy (tooltips, empty states, errors, sample chips).</summary>
public sealed class PromptCoachValidationTests
{
    [Fact]
    public void RetrievalScopeTooltip_lists_full_library_book_year_and_intersection()
    {
        var t = PromptCoach.RetrievalScopeTooltip;
        Assert.False(string.IsNullOrWhiteSpace(t));
        Assert.Contains("Full library", t);
        Assert.Contains("Book-scoped", t);
        Assert.Contains("Year filter", t);
        Assert.Contains("Empty intersection", t);
        Assert.Contains("last_accessed", t);
    }

    [Fact]
    public void AnswerSectionTooltip_mentions_synthesis_and_sources_only()
    {
        var t = PromptCoach.AnswerSectionTooltip;
        Assert.Contains("Sources", t);
        Assert.Contains("synthesis", t, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("open web", t);
    }

    [Fact]
    public void SourcesSectionTooltip_mentions_match_and_cosine()
    {
        var t = PromptCoach.SourcesSectionTooltip;
        Assert.Contains("match", t, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cosine", t, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NextStepsCoachingMd_mentions_key_quota_and_rebuild()
    {
        var md = PromptCoach.NextStepsCoachingMd();
        Assert.Contains("API key", md);
        Assert.Contains("quota", md, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Force full re-embed", md);
        Assert.Contains("corpus", md, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptySourcesCoachingMd_embeds_next_steps()
    {
        var md = PromptCoach.EmptySourcesCoachingMd();
        Assert.Contains("No passages were retrieved", md);
        Assert.Contains(PromptCoach.NextStepsCoachingMd(), md);
    }

    [Fact]
    public void BlankAnswerPlaceholderMd_embeds_next_steps()
    {
        var md = PromptCoach.BlankAnswerPlaceholderMd();
        Assert.Contains("no answer text", md, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(PromptCoach.NextStepsCoachingMd(), md);
    }

    [Fact]
    public void AskFailedUserMessage_includes_technical_line_and_persistence_hints()
    {
        var msg = PromptCoach.AskFailedUserMessage("Test error: 429");
        Assert.Contains("Test error: 429", msg);
        Assert.Contains("network", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Build index", msg);
    }

    [Fact]
    public void AskFailedSourcesMd_includes_heading_and_next_steps()
    {
        var md = PromptCoach.AskFailedSourcesMd("Connection reset");
        Assert.Contains("Ask failed", md);
        Assert.Contains("Connection reset", md);
        Assert.Contains(PromptCoach.NextStepsCoachingMd(), md);
    }

    [Fact]
    public void SampleQuestionChips_All_has_four_distinct_nonempty_questions()
    {
        var all = PromptCoach.SampleQuestionChips.All;
        Assert.Equal(4, all.Count);
        var questions = all.Select(x => x.Question).ToHashSet();
        Assert.Equal(4, questions.Count);
        foreach (var (label, question) in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.False(string.IsNullOrWhiteSpace(question));
        }
    }

    [Fact]
    public void SampleQuestionChips_Include_cross_book_year_book_scope_and_connect()
    {
        var qs = PromptCoach.SampleQuestionChips.All.Select(t => t.Question).ToList();
        Assert.Contains(qs, q => q.Contains("across my Kindle", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(qs, q => q.Contains("2024", StringComparison.Ordinal));
        Assert.Contains(qs, q => q.Contains("[accessed YYYY-MM-DD]", StringComparison.Ordinal));
        Assert.Contains(qs, q => q.Contains("PASTE_TITLE_FROM_SOURCES", StringComparison.Ordinal));
        Assert.Contains(qs, q => q.Contains("leadership", StringComparison.OrdinalIgnoreCase));
    }
}

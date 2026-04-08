namespace Knode.Services;

/// <summary>In-app copy for prompt coaching (empty states, errors, Markdown snippets).</summary>
public static class PromptCoach
{
    /// <summary>Markdown for the Sources web view: coaching when empty, otherwise passage list.</summary>
    public static string SourcesPanelMarkdown(IReadOnlyList<(HighlightRecord Record, float Score)> passages) =>
        passages.Count == 0 ? EmptySourcesCoachingMd() : SourcesMarkdown.FromPassages(passages);

    public const string RetrievalScopeTooltip =
        "This line summarizes how Knode filtered your corpus before similarity search.\n\n"
        + "• Full library — no book or year filter matched; every highlight is a search candidate.\n"
        + "• Book-scoped — part of your question matched a book title in the index; only those rows are searched. Copy an exact title from Sources into a new question to stay scoped.\n"
        + "• Year filter — mentioning a year (e.g. 2024) keeps highlights whose Kindle Notebook last_accessed falls in that calendar year. Citations may include [accessed YYYY-MM-DD] from the corpus.\n"
        + "• Empty intersection — book and year filters left no rows; try a wider year, a different title, or rebuild the index after updating last_accessed in corpus.jsonl.";

    public const string AnswerSectionTooltip =
        "The model writes this answer using only the passages listed under Sources—not the open web. "
        + "It is a synthesis grounded in those excerpts; if nothing relevant was retrieved, the answer should say so.";

    public const string SourcesSectionTooltip =
        "Each block is a retrieved highlight from your corpus, ranked by semantic match. "
        + "Match ~% is cosine similarity to your question (higher means closer, not “confidence”). "
        + "Use titles and quotes here to narrow your next question (book scope) or to sanity-check the answer.";

    public static string NextStepsCoachingMd() =>
        """
        **What to try next**
        - Confirm your **API key** and **Gemini quota** (Google AI Studio).
        - **Rebuild the index** with **Force full re-embed** after editing corpus.jsonl or updating Knode.
        - If you used a **year** or **book** hint, relax it: filters can reduce the corpus to zero rows.
        - Ask a broader question, then narrow using a title you see under Sources.
        """;

    public static string EmptySourcesCoachingMd() =>
        "**No passages were retrieved** — the answer (if any) was not grounded in excerpts.\n\n"
        + NextStepsCoachingMd();

    /// <summary>Answer panel Markdown when the model returns blank text after cleanup.</summary>
    public static string BlankAnswerPlaceholderMd() =>
        "_The model returned no answer text._ If Sources is also empty, widen your question or check filters; "
        + "otherwise try asking again.\n\n"
        + NextStepsCoachingMd();

    public static string AskFailedUserMessage(string technicalMessage) =>
        $"{technicalMessage}\n\n"
        + "**If this persists:** check your network, Gemini quota, and that **Build index** completed successfully. "
        + "Try **Force full re-embed** if the corpus or app changed since the last index build.";

    public static string AskFailedSourcesMd(string technicalMessage) =>
        $"### Ask failed\n\n{technicalMessage}\n\n" + NextStepsCoachingMd();

    /// <summary>Sample question chips — tags are assigned in MainWindow on startup. Validated in Knode.Tests.</summary>
    public static class SampleQuestionChips
    {
        public const string CrossBookLabel = "Cross-book themes";
        public const string CrossBookQuestion =
            "What ideas about learning, habits, or productivity show up across my Kindle highlights?";

        public const string BookScopeLabel = "Book scope (paste title)";
        public const string BookScopeQuestion =
            "Summarize the main ideas from my highlights in the book \"PASTE_TITLE_FROM_SOURCES\" — replace with an exact title from the Sources panel.";

        public const string YearFilterLabel = "Year filter (2024 demo)";
        public const string YearFilterQuestion =
            "What themes stand out in my 2024 highlights? Use notebook last_accessed year; expect [accessed YYYY-MM-DD] in citations when present.";

        public const string ConnectTopicsLabel = "Connect two topics";
        public const string ConnectTopicsQuestion =
            "How do my notes on leadership and teamwork connect?";

        public static IReadOnlyList<(string Label, string Question)> All { get; } =
        [
            (CrossBookLabel, CrossBookQuestion),
            (BookScopeLabel, BookScopeQuestion),
            (YearFilterLabel, YearFilterQuestion),
            (ConnectTopicsLabel, ConnectTopicsQuestion),
        ];
    }
}

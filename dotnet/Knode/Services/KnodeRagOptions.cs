namespace Knode.Services;

public sealed class KnodeRagOptions
{
    public bool BookScopeEnabled { get; init; } = true;

    /// <summary>Ignore book titles shorter than this when matching. Kindle often uses a one-line title like "Switch" (~6 chars); 10 was too high and skipped scope entirely.</summary>
    public int BookScopeMinTitleChars { get; init; } = 4;

    /// <summary>If the question mentions a 20xx year, only retrieve highlights whose <c>last_accessed</c> falls in those years.</summary>
    public bool YearScopeEnabled { get; init; } = true;

    public bool RagLoggingEnabled { get; init; } = true;
}

namespace Knode.Services;

/// <summary>Combine book-title scope with optional year scope (intersection).</summary>
public static class RetrievalScope
{
    /// <summary>null = search entire index.</summary>
    public static HashSet<int>? Combine(IReadOnlySet<int>? bookIndices, HashSet<int>? yearIndices)
    {
        if (bookIndices is null && yearIndices is null)
            return null;
        if (bookIndices is not null && yearIndices is null)
            return new HashSet<int>(bookIndices);
        if (bookIndices is null && yearIndices is not null)
            return yearIndices;
        var x = new HashSet<int>(bookIndices!);
        x.IntersectWith(yearIndices!);
        return x;
    }
}

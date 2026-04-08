namespace Knode.Services;

/// <summary>In-memory cosine search over embedding vectors.</summary>
public sealed class VectorIndex
{
    private float[][] _vectors = Array.Empty<float[]>();
    private HighlightRecord[] _records = Array.Empty<HighlightRecord>();

    public int Count => _records.Length;

    public void Load(IReadOnlyList<float[]> vectors, IReadOnlyList<HighlightRecord> records)
    {
        if (vectors.Count != records.Count)
            throw new ArgumentException("Vectors and records length mismatch.");
        _vectors = vectors.ToArray();
        _records = records.ToArray();
    }

    public IReadOnlyList<(HighlightRecord Record, float Score)> Search(
        float[] queryEmbedding,
        int topK,
        IReadOnlySet<int>? allowedRecordIndices = null)
    {
        if (_vectors.Length == 0 || queryEmbedding.Length == 0)
            return Array.Empty<(HighlightRecord, float)>();

        var dim = queryEmbedding.Length;
        var scores = new List<(int idx, float score)>();
        for (var i = 0; i < _vectors.Length; i++)
        {
            if (allowedRecordIndices is not null && !allowedRecordIndices.Contains(i))
                continue;
            var v = _vectors[i];
            if (v.Length != dim)
                continue;
            var s = CosineSimilarity(queryEmbedding, v);
            scores.Add((i, s));
        }
        scores.Sort((a, b) => b.score.CompareTo(a.score));
        return scores.Take(topK).Select(x => (_records[x.idx], x.score)).ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        var d = Math.Sqrt(na) * Math.Sqrt(nb);
        return d < 1e-10 ? 0 : (float)(dot / d);
    }
}

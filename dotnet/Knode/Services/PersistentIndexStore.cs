using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Knode.Services;

public sealed class IndexManifest
{
    public int FormatVersion { get; set; }
    public string EmbeddingModel { get; set; } = "";
    public string CorpusPath { get; set; } = "";
    public string CorpusSha256Hex { get; set; } = "";
    public int Dimensions { get; set; }
    public int RecordCount { get; set; }
}

/// <summary>Saves embeddings + records under %LocalAppData%\Knode\index for fast startup.</summary>
public static class PersistentIndexStore
{
    private static string IndexDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Knode", "index");

    private static string ManifestPath => Path.Combine(IndexDirectory, "manifest.json");
    private static string RecordsPath => Path.Combine(IndexDirectory, "records.json");
    private static string VectorsPath => Path.Combine(IndexDirectory, "vectors.bin");

    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions RecordsJson = HighlightRecordJson.Options;

    public static async Task<string> ComputeCorpusSha256HexAsync(string corpusPath, CancellationToken ct = default)
    {
        await using var fs = new FileStream(
            corpusPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1 << 20,
            FileOptions.Asynchronous);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    public static bool TryLoad(
        string embeddingModel,
        string corpusSha256Hex,
        [NotNullWhen(true)] out List<HighlightRecord>? records,
        out float[][]? vectors,
        out string? message)
    {
        records = null;
        vectors = null;
        message = null;

        if (!File.Exists(ManifestPath) || !File.Exists(RecordsPath) || !File.Exists(VectorsPath))
        {
            message = "No saved index in %LocalAppData%\\Knode\\index (first run on this PC, or folder was cleared).";
            return false;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<IndexManifest>(File.ReadAllText(ManifestPath), ManifestJson);
            if (manifest is null || manifest.FormatVersion != 1)
            {
                message = "Saved index format not recognized.";
                return false;
            }

            if (!ModelMatches(manifest.EmbeddingModel, embeddingModel))
            {
                message = "Saved index was built with a different embedding model.";
                return false;
            }

            if (!string.Equals(manifest.CorpusSha256Hex, corpusSha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                message =
                    "This corpus.jsonl is different from the one used for the saved index (file changed, replaced, or sync). " +
                    "Rebuild index, or pick the same file as before.";
                return false;
            }

            var rawRecords = File.ReadAllText(RecordsPath);
            var prepared = HighlightRecordJson.PrepareJsonText(rawRecords);
            records = HighlightRecordJson.DeserializeRecordsArray(prepared)
                ?? JsonSerializer.Deserialize<List<HighlightRecord>>(prepared, RecordsJson);
            if (records is null || records.Count == 0)
            {
                message = "Saved index has no records.";
                return false;
            }

            if (records.TrueForAll(static r => string.IsNullOrWhiteSpace(r.BookTitle)) &&
                !string.IsNullOrWhiteSpace(manifest.CorpusPath) &&
                File.Exists(manifest.CorpusPath))
                HighlightRecordJson.BackfillMetadataFromCorpusById(records, manifest.CorpusPath);

            if (records.Count != manifest.RecordCount)
            {
                message = "Saved index manifest does not match records.";
                return false;
            }

            vectors = ReadVectors(VectorsPath, manifest.RecordCount, manifest.Dimensions);
            if (vectors is null)
            {
                message = "Saved index vector file is invalid or incomplete.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            message = $"Could not load saved index: {ex.Message}";
            return false;
        }
    }

    public static async Task SaveAsync(
        string corpusFullPath,
        string embeddingModel,
        string corpusSha256Hex,
        IReadOnlyList<HighlightRecord> records,
        IReadOnlyList<float[]> vectors,
        CancellationToken ct = default)
    {
        if (records.Count != vectors.Count || records.Count == 0)
            throw new ArgumentException("Records and vectors must align and be non-empty.");

        var dim = vectors[0].Length;
        foreach (var v in vectors)
        {
            if (v.Length != dim)
                throw new InvalidOperationException("All vectors must use the same dimension.");
        }

        Directory.CreateDirectory(IndexDirectory);

        var manifest = new IndexManifest
        {
            FormatVersion = 1,
            EmbeddingModel = NormalizeModelId(embeddingModel),
            CorpusPath = corpusFullPath,
            CorpusSha256Hex = corpusSha256Hex,
            Dimensions = dim,
            RecordCount = records.Count,
        };

        var mTmp = ManifestPath + ".new";
        var rTmp = RecordsPath + ".new";
        var vTmp = VectorsPath + ".new";

        try
        {
            await File.WriteAllTextAsync(mTmp, JsonSerializer.Serialize(manifest, ManifestJson), ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(rTmp, JsonSerializer.Serialize(records, RecordsJson), ct).ConfigureAwait(false);
            await WriteVectorsAsync(vTmp, vectors, dim, ct).ConfigureAwait(false);

            ReplaceIfExists(ManifestPath, mTmp);
            ReplaceIfExists(RecordsPath, rTmp);
            ReplaceIfExists(VectorsPath, vTmp);
        }
        finally
        {
            TryDelete(mTmp);
            TryDelete(rTmp);
            TryDelete(vTmp);
        }
    }

    private static void ReplaceIfExists(string dest, string srcNew)
    {
        if (File.Exists(dest))
            File.Delete(dest);
        File.Move(srcNew, dest);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private static async Task WriteVectorsAsync(string path, IReadOnlyList<float[]> vectors, int dim, CancellationToken ct)
    {
        await using var fs = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            FileOptions.Asynchronous);
        await using var bw = new BinaryWriter(fs);
        bw.Write(vectors.Count);
        bw.Write(dim);
        foreach (var v in vectors)
        {
            ct.ThrowIfCancellationRequested();
            for (var i = 0; i < dim; i++)
                bw.Write(v[i]);
        }
    }

    private static float[][]? ReadVectors(string path, int expectedCount, int expectedDim)
    {
        using var fs = File.OpenRead(path);
        if (fs.Length < 8)
            return null;
        using var br = new BinaryReader(fs);
        var count = br.ReadInt32();
        var dim = br.ReadInt32();
        if (count != expectedCount || dim != expectedDim)
            return null;
        var expectedLen = 8L + (long)count * dim * 4;
        if (fs.Length != expectedLen)
            return null;

        var result = new float[count][];
        for (var r = 0; r < count; r++)
        {
            var v = new float[dim];
            for (var i = 0; i < dim; i++)
                v[i] = br.ReadSingle();
            result[r] = v;
        }

        return result;
    }

    private static bool ModelMatches(string saved, string current) =>
        string.Equals(NormalizeModelId(saved), NormalizeModelId(current), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeModelId(string model)
    {
        var m = model.Trim();
        return m.StartsWith("models/", StringComparison.Ordinal) ? m["models/".Length..] : m;
    }
}

using System.IO;
using System.Text.Json;

namespace Knode.Services;

public sealed class OneNoteSectionSelection
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed class OneNoteSectionSyncState
{
    public string SectionId { get; set; } = "";
    public DateTimeOffset? LastSyncUtc { get; set; }
}

public sealed class OneNoteSettingsData
{
    public bool Enabled { get; set; }
    public string? AccountUsername { get; set; }
    public string? LastIndexSignature { get; set; }
    public List<OneNoteSectionSelection> SelectedSections { get; set; } = new();
    public List<OneNoteSectionSyncState> SectionSyncState { get; set; } = new();
}

public static class OneNoteSettingsStore
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Knode", "onenote_settings.json");

    public static OneNoteSettingsData Load()
    {
        if (!File.Exists(FilePath))
            return new OneNoteSettingsData();
        try
        {
            return JsonSerializer.Deserialize<OneNoteSettingsData>(File.ReadAllText(FilePath), s_json) ?? new OneNoteSettingsData();
        }
        catch
        {
            return new OneNoteSettingsData();
        }
    }

    public static void Save(OneNoteSettingsData data)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (dir is not null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, s_json));
    }
}

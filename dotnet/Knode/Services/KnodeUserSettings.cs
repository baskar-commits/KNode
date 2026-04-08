using System.IO;
using System.Text.Json;

namespace Knode.Services;

public sealed class KnodeUserSettingsData
{
    public string? LastCorpusPath { get; set; }

    /// <summary>When true, the setup checklist expander starts collapsed.</summary>
    public bool GettingStartedGuideHidden { get; set; }
}

/// <summary>Plain JSON under %LocalAppData%\Knode (path only; not secret).</summary>
public static class KnodeUserSettings
{
    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Knode", "user_settings.json");

    private static readonly JsonSerializerOptions s_json = new() { WriteIndented = true };

    public static void SaveLastCorpusPath(string fullPath)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var data = TryLoadData() ?? new KnodeUserSettingsData();
        data.LastCorpusPath = Path.GetFullPath(fullPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, s_json));
    }

    private static KnodeUserSettingsData? TryLoadData()
    {
        if (!File.Exists(FilePath))
            return null;
        try
        {
            return JsonSerializer.Deserialize<KnodeUserSettingsData>(File.ReadAllText(FilePath));
        }
        catch
        {
            return null;
        }
    }

    public static string? TryGetLastCorpusPath()
    {
        var data = TryLoadData();
        return string.IsNullOrWhiteSpace(data?.LastCorpusPath) ? null : data.LastCorpusPath;
    }

    public static bool IsGettingStartedGuideHidden() =>
        TryLoadData()?.GettingStartedGuideHidden == true;

    public static void SaveGettingStartedGuideHidden(bool hidden)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var data = TryLoadData() ?? new KnodeUserSettingsData();
        data.GettingStartedGuideHidden = hidden;
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, s_json));
    }
}

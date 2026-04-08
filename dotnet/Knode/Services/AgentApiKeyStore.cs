using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Knode.Services;

/// <summary>
/// Stores the user’s AI agent API key with Windows DPAPI (CurrentUser) under %LocalAppData%\Knode.
/// Legacy file name <c>gemini_api_key.protected</c> is still read for one-session migration.
/// </summary>
public static class AgentApiKeyStore
{
    private static string PrimaryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Knode", "agent_api_key.protected");

    private static string LegacyGeminiPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Knode", "gemini_api_key.protected");

    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("Knode AI Agent API.key DPAPI v2");

    public static void Save(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is empty.", nameof(apiKey));

        var dir = Path.GetDirectoryName(PrimaryPath);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var plain = Encoding.UTF8.GetBytes(apiKey.Trim());
        var protectedBytes = ProtectedData.Protect(plain, s_entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(PrimaryPath, protectedBytes);

        try
        {
            if (File.Exists(LegacyGeminiPath))
                File.Delete(LegacyGeminiPath);
        }
        catch
        {
            // ignore — old key file may be missing or locked
        }
    }

    public static bool TryGet(out string apiKey)
    {
        if (TryDecryptFile(PrimaryPath, out apiKey))
            return true;

        // Legacy entropy for v1 Gemini-only store (migrate on next Save).
        if (!File.Exists(LegacyGeminiPath))
            return false;
        try
        {
            var legacyEntropy = Encoding.UTF8.GetBytes("Knode Gemini API key DPAPI v1");
            var enc = File.ReadAllBytes(LegacyGeminiPath);
            var plain = ProtectedData.Unprotect(enc, legacyEntropy, DataProtectionScope.CurrentUser);
            apiKey = Encoding.UTF8.GetString(plain).Trim();
            return !string.IsNullOrEmpty(apiKey);
        }
        catch
        {
            apiKey = "";
            return false;
        }
    }

    private static bool TryDecryptFile(string path, out string apiKey)
    {
        apiKey = "";
        if (!File.Exists(path))
            return false;
        try
        {
            var enc = File.ReadAllBytes(path);
            var plain = ProtectedData.Unprotect(enc, s_entropy, DataProtectionScope.CurrentUser);
            apiKey = Encoding.UTF8.GetString(plain).Trim();
            return !string.IsNullOrEmpty(apiKey);
        }
        catch
        {
            return false;
        }
    }

    public static void Clear()
    {
        foreach (var path in new[] { PrimaryPath, LegacyGeminiPath })
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
    }

    public static bool HasStoredKey =>
        File.Exists(PrimaryPath) || File.Exists(LegacyGeminiPath);
}

using System.IO;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Knode.Services;

/// <summary>
/// MSAL public client for OneNote (Graph). Persists token cache under %LocalAppData%\Knode and prefers silent refresh
/// so Connect / section picker / Build index do not each force a browser sign-in.
/// </summary>
public sealed class OneNoteAuthService
{
    public const string ScopeNotesRead = "Notes.Read";
    public const string ScopeOfflineAccess = "offline_access";
    private static readonly string[] Scopes = { ScopeNotesRead, ScopeOfflineAccess };

    private readonly IPublicClientApplication _app;
    private readonly SemaphoreSlim _cacheInitLock = new(1, 1);
    private MsalCacheHelper? _cacheHelper;
    private bool _cacheRegistered;

    public OneNoteAuthService(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException(
                "OneNote ClientId is not set. Add Knode:OneNote:ClientId in appsettings.Local.json.");

        _app = PublicClientApplicationBuilder
            .Create(clientId.Trim())
            .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
            .WithRedirectUri("http://localhost")
            .Build();
    }

    /// <summary>
    /// Returns a valid access token. Tries <see cref="IClientApplicationBase.AcquireTokenSilent"/> first when an account
    /// exists in the cache; opens the browser only when necessary (first sign-in, expired refresh token, or revoked consent).
    /// </summary>
    /// <param name="allowInteractiveWhenNeeded">If false, throws instead of opening a sign-in window when silent auth fails.</param>
    public async Task<(string AccessToken, string Username)> AcquireTokenAsync(
        bool allowInteractiveWhenNeeded = true,
        CancellationToken ct = default)
    {
        await EnsurePersistentUserCacheAsync(ct).ConfigureAwait(false);

        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();

        if (account is not null)
        {
            try
            {
                var silent = await _app.AcquireTokenSilent(Scopes, account).ExecuteAsync(ct).ConfigureAwait(false);
                return (silent.AccessToken, silent.Account?.Username ?? "");
            }
            catch (MsalUiRequiredException)
            {
                // Refresh token expired, password changed, or consent revoked — need interactive once.
            }
        }

        if (!allowInteractiveWhenNeeded)
        {
            throw new InvalidOperationException(
                "OneNote sign-in is required or your session expired. Open Setup and click Connect OneNote.");
        }

        var result = await _app
            .AcquireTokenInteractive(Scopes)
            .WithPrompt(Prompt.SelectAccount)
            .WithUseEmbeddedWebView(false)
            .ExecuteAsync(ct)
            .ConfigureAwait(false);

        return (result.AccessToken, result.Account?.Username ?? "");
    }

    private async Task EnsurePersistentUserCacheAsync(CancellationToken ct)
    {
        if (_cacheRegistered)
            return;

        await _cacheInitLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cacheRegistered)
                return;

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Knode");
            Directory.CreateDirectory(dir);
            var storage = new StorageCreationPropertiesBuilder("msal_onenote.bin", dir).Build();
            _cacheHelper = await MsalCacheHelper.CreateAsync(storage).ConfigureAwait(false);
            _cacheHelper.RegisterCache(_app.UserTokenCache);
            _cacheRegistered = true;
        }
        finally
        {
            _cacheInitLock.Release();
        }
    }

}

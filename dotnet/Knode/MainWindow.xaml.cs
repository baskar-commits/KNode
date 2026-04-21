using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using Knode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;

namespace Knode;

public partial class MainWindow : Window
{
    private readonly IConfiguration _config;
    private readonly KnodeRagService _rag;
    private readonly OneNoteSettingsData _oneNoteSettings;
    private readonly string _oneNoteClientId;
    private GeminiClient? _client;
    private OneNoteAuthService? _oneNoteAuth;
    private CancellationTokenSource? _askCts;
    private volatile bool _askInFlight;
    /// <summary>When true, a programmatic <see cref="Window.Close"/> after cancel is allowed (second pass of shutdown).</summary>
    private bool _deferShutdownForAsk;
    private bool _navExpanded = true;
    private GridLength? _sourcesColumnWidthRestore;

    public MainWindow()
    {
        InitializeComponent();
        _config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var emb = _config["Knode:EmbeddingModel"] ?? "gemini-embedding-001";
        var chat = _config["Knode:ChatModel"] ?? "gemini-2.5-flash";
        var ragOpts = new KnodeRagOptions
        {
            BookScopeEnabled = _config.GetValue("Knode:BookScope:Enabled", true),
            BookScopeMinTitleChars = _config.GetValue("Knode:BookScope:MinTitleChars", 4),
            YearScopeEnabled = _config.GetValue("Knode:YearScope:Enabled", true),
            RagLoggingEnabled = _config.GetValue("Knode:RagLogging:Enabled", true),
        };
        _rag = new KnodeRagService(emb, chat, ragOpts);
        _oneNoteSettings = OneNoteSettingsStore.Load();
        _oneNoteClientId = _config["Knode:OneNote:ClientId"] ?? "";
        ModelsInfo.Text =
            $"Embeddings: {emb}  ·  Chat: {chat}  ·  Adjust in appsettings.json. " +
            $"Gemini keys: https://aistudio.google.com/apikey";

        var displayVer = GetAppDisplayVersion();
        if (!string.IsNullOrEmpty(displayVer))
        {
            Title = $"Knode · {displayVer}";
            HeaderMetaText.Text = $"v{displayVer} · MVP · Google Gemini";
        }

        try
        {
            var dllPath = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(dllPath))
            {
                var t = File.GetLastWriteTimeUtc(dllPath);
                if (string.IsNullOrEmpty(displayVer))
                {
                    BuildInfoText.Text =
                        $"Built {t:yyyy-MM-dd HH:mm} UTC — use this time to confirm you are not running an old Knode.exe.";
                }
                else
                {
                    BuildInfoText.Text =
                        $"Version {displayVer}\nBuilt {t:yyyy-MM-dd HH:mm} UTC — compare with the installer or GitHub release you expect.";
                }
            }
        }
        catch
        {
            BuildInfoText.Text = string.IsNullOrEmpty(displayVer) ? "" : $"Version {displayVer}";
        }

        var defaultCorpus = _config["Knode:CorpusPath"];
        if (string.IsNullOrWhiteSpace(defaultCorpus))
            defaultCorpus = KnodeUserSettings.TryGetLastCorpusPath();
        if (string.IsNullOrWhiteSpace(defaultCorpus))
        {
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Knode", "corpus.jsonl");
            defaultCorpus = File.Exists(local) ? local : "";
        }

        CorpusPathBox.Text = defaultCorpus ?? "";

        GettingStartedExpander.IsExpanded = !KnodeUserSettings.IsGettingStartedGuideHidden();
        GettingStartedHideCheck.IsChecked = KnodeUserSettings.IsGettingStartedGuideHidden();

        SampleChipCrossBook.Tag = PromptCoach.SampleQuestionChips.CrossBookQuestion;
        SampleChipBookScope.Tag = PromptCoach.SampleQuestionChips.BookScopeQuestion;
        SampleChipYearFilter.Tag = PromptCoach.SampleQuestionChips.YearFilterQuestion;
        SampleChipConnectTopics.Tag = PromptCoach.SampleQuestionChips.ConnectTopicsQuestion;

        var envKey = Environment.GetEnvironmentVariable("AGENT_API_KEY")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
            ?? _config["Knode:Agent:ApiKey"]
            ?? _config["Knode:Gemini:ApiKey"];
        if (!string.IsNullOrEmpty(envKey))
            ApiKeyBox.Password = envKey;

        EnableOneNoteCheck.IsChecked = _oneNoteSettings.Enabled;
        UpdateOneNoteStatus();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_deferShutdownForAsk)
            return;

        if (!_askInFlight)
            return;

        e.Cancel = true;
        _deferShutdownForAsk = true;
        try
        {
            _askCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        _ = Task.Run(async () =>
        {
            try
            {
                for (var i = 0; i < 60; i++)
                {
                    if (!_askInFlight)
                        break;
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
            finally
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Close();
                    }
                    catch
                    {
                        // Window may already be closing.
                    }
                });
            }
        });
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            _askCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        _askCts?.Dispose();
        _askCts = null;
        _client?.Dispose();
        _client = null;
    }

    private void HelpCorpus_Click(object sender, RoutedEventArgs e)
    {
        var w = new CorpusHelpWindow { Owner = this };
        w.ShowDialog();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        MainNavList.SelectedIndex = 0;
        UpdateApiKeySavedHint();
        try
        {
            await MarkdownToHtml.NavigatePlaceholderAsync(
                AnswerWebView,
                "Grounded answers appear here after you Ask. Markdown from the model is formatted for easier reading.",
                "Answer",
                answerTwoColumn: true).ConfigureAwait(true);
            await MarkdownToHtml.NavigatePlaceholderAsync(
                SourcesWebView,
                "Retrieved passages show here with book, location, and match strength.",
                "Sources").ConfigureAwait(true);
        }
        catch
        {
            // WebView2 runtime missing — placeholders stay blank
        }

        await TryRestoreSessionAsync().ConfigureAwait(true);

        ApplySourcesPanelCollapsed(KnodeUserSettings.IsSourcesPanelCollapsed());
    }

    private void SourcesRailCollapse_Click(object sender, RoutedEventArgs e)
    {
        ApplySourcesPanelCollapsed(true);
        KnodeUserSettings.SaveSourcesPanelCollapsed(true);
    }

    private void SourcesRailExpand_Click(object sender, RoutedEventArgs e)
    {
        ApplySourcesPanelCollapsed(false);
        KnodeUserSettings.SaveSourcesPanelCollapsed(false);
    }

    /// <summary>Narrow rail (like left nav) when true; full Sources + splitter when false.</summary>
    private void ApplySourcesPanelCollapsed(bool collapsed)
    {
        if (AskSourcesColumn is null || AskSplitterColumn is null || SourcesSplitter is null
            || SourcesExpandedPanel is null || SourcesNarrowRailPanel is null)
            return;

        const double narrowRailPx = 56;

        if (collapsed)
        {
            _sourcesColumnWidthRestore = AskSourcesColumn.Width;
            AskSourcesColumn.MinWidth = narrowRailPx;
            AskSourcesColumn.MaxWidth = narrowRailPx;
            AskSourcesColumn.Width = new GridLength(narrowRailPx);
            AskSplitterColumn.Width = new GridLength(0);
            SourcesSplitter.Visibility = Visibility.Collapsed;
            SourcesExpandedPanel.Visibility = Visibility.Collapsed;
            SourcesNarrowRailPanel.Visibility = Visibility.Visible;
        }
        else
        {
            AskSourcesColumn.MinWidth = 260;
            AskSourcesColumn.MaxWidth = 960;
            AskSourcesColumn.Width = _sourcesColumnWidthRestore ?? new GridLength(1, GridUnitType.Star);
            AskSplitterColumn.Width = new GridLength(6);
            SourcesSplitter.Visibility = Visibility.Visible;
            SourcesExpandedPanel.Visibility = Visibility.Visible;
            SourcesNarrowRailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void EnsureSourcesPanelVisible()
    {
        if (SourcesNarrowRailPanel is not null && SourcesNarrowRailPanel.Visibility == Visibility.Visible)
            ApplySourcesPanelCollapsed(false);
    }

    private void MainNav_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AskPagePanel is null || SetupPagePanel is null || HelpPagePanel is null)
            return;
        if (MainNavList.SelectedItem is not System.Windows.Controls.ListBoxItem li || li.Tag is not string tag)
            return;
        AskPagePanel.Visibility = tag == "Ask" ? Visibility.Visible : Visibility.Collapsed;
        SetupPagePanel.Visibility = tag == "Setup" ? Visibility.Visible : Visibility.Collapsed;
        HelpPagePanel.Visibility = tag == "Help" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "Ask")
            RefreshAskPageStatus();
    }

    private void NavCollapse_Click(object sender, RoutedEventArgs e)
    {
        _navExpanded = !_navExpanded;
        NavColumn.Width = new GridLength(_navExpanded ? 220 : 56);
        var show = _navExpanded ? Visibility.Visible : Visibility.Collapsed;
        NavAskLabel.Visibility = show;
        NavSetupLabel.Visibility = show;
        NavHelpLabel.Visibility = show;
        NavCollapseBtn.Content = _navExpanded ? "⟨⟨" : "⟩⟩";
        NavCollapseBtn.ToolTip = _navExpanded ? "Narrow sidebar" : "Expand sidebar";
    }

    private void GoSetup_Click(object sender, RoutedEventArgs e) => SelectNavPage("Setup");

    private void GoAsk_Click(object sender, RoutedEventArgs e) => SelectNavPage("Ask");

    private void SelectNavPage(string tag)
    {
        foreach (var o in MainNavList.Items)
        {
            if (o is System.Windows.Controls.ListBoxItem item && item.Tag is string t && t == tag)
            {
                MainNavList.SelectedItem = item;
                return;
            }
        }
    }

    private void RefreshAskPageStatus()
    {
        if (!IsLoaded || AskPageStatus is null)
            return;
        if (_rag.IsReady)
        {
            AskPageStatus.Text = $"Index ready · {_rag.RecordCount} highlights.";
            return;
        }

        var path = CorpusPathBox.Text.Trim();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            AskPageStatus.Text = "No corpus file selected — open Setup and choose corpus.jsonl.";
            return;
        }

        if (string.IsNullOrEmpty(ResolveApiKey()))
        {
            AskPageStatus.Text = "API key not set — open Setup to enter or restore a key.";
            return;
        }

        AskPageStatus.Text = string.IsNullOrWhiteSpace(IndexStatus.Text)
            ? "Index not ready — open Setup and click Build index."
            : IndexStatus.Text;
    }

    private void HelpLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
        e.Handled = true;
    }

    private void SampleQuestionChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b)
            return;
        var s = b.Tag as string;
        if (string.IsNullOrEmpty(s))
            return;
        QuestionBox.Text = s;
        QuestionBox.Focus();
        QuestionBox.CaretIndex = QuestionBox.Text.Length;
    }

    private void GettingStartedHideCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        var hide = GettingStartedHideCheck.IsChecked == true;
        KnodeUserSettings.SaveGettingStartedGuideHidden(hide);
        GettingStartedExpander.IsExpanded = !hide;
    }

    private void EnableOneNoteCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        _oneNoteSettings.Enabled = EnableOneNoteCheck.IsChecked == true;
        OneNoteSettingsStore.Save(_oneNoteSettings);
        UpdateOneNoteStatus();
    }

    private void UpdateApiKeySavedHint()
    {
        ApiKeySavedHint.Visibility = AgentApiKeyStore.HasStoredKey ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task TryRestoreSessionAsync()
    {
        try
        {
            var path = CorpusPathBox.Text.Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                if (!_rag.IsReady)
                    IndexStatus.Text = "Choose corpus.jsonl (Help explains how to create one), then Build index.";
                return;
            }

            var key = ResolveApiKey();
            if (string.IsNullOrEmpty(key))
            {
                if (!_rag.IsReady)
                    IndexStatus.Text = "Enter or save an AI agent API key, then Build index.";
                return;
            }

            IProgress<string> progress = new Progress<string>(s => IndexStatus.Text = s);
            try
            {
                if (await _rag.TryLoadPersistedIndexAsync(path, progress, _oneNoteSettings.LastIndexSignature).ConfigureAwait(true))
                {
                    var baseUrl = _config["Knode:Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/";
                    _client?.Dispose();
                    _client = new GeminiClient(key, baseUrl);
                    AskBtn.IsEnabled = _rag.IsReady && !string.IsNullOrEmpty(key);
                }
                else if (!_rag.IsReady)
                {
                    // TryLoadPersistedIndexAsync may have reported a specific reason via progress (shown in IndexStatus)
                    if (string.IsNullOrWhiteSpace(IndexStatus.Text) ||
                        IndexStatus.Text.StartsWith("Checking saved index", StringComparison.OrdinalIgnoreCase))
                    {
                        IndexStatus.Text = "No saved index for this corpus — click Build index.";
                    }
                }
            }
            catch (Exception ex)
            {
                IndexStatus.Text = "Could not load saved index.";
                MessageBox.Show(ex.Message, "Knode", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            RefreshAskPageStatus();
        }
    }

    private string ResolveApiKey()
    {
        var typed = ApiKeyBox.Password.Trim();
        if (!string.IsNullOrEmpty(typed))
            return typed;
        if (AgentApiKeyStore.TryGet(out var saved))
            return saved;
        return "";
    }

    private string BuildOneNoteIndexSignature(IReadOnlyList<OneNoteSectionSelection> sections)
    {
        if (!_oneNoteSettings.Enabled || sections.Count == 0)
            return "";
        var syncMap = _oneNoteSettings.SectionSyncState
            .Where(static s => !string.IsNullOrWhiteSpace(s.SectionId))
            .ToDictionary(s => s.SectionId, s => s.LastSyncUtc?.UtcDateTime.Ticks ?? 0, StringComparer.Ordinal);
        var parts = sections
            .Select(s =>
            {
                var id = s.Id.Trim();
                var ticks = syncMap.TryGetValue(id, out var t) ? t : 0;
                return $"{id}:{ticks}";
            })
            .Where(static s => s.Length > 1)
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();
        return parts.Length == 0 ? "" : "onenote:" + string.Join(",", parts);
    }

    private static string FormatBuildSummary(BuildIndexStats stats)
    {
        if (stats.LoadedFromExactCache)
        {
            return $"Last build summary: cache hit. Total {stats.TotalRows} rows, embedded 0, reused {stats.ReusedRows}, deleted 0.";
        }

        return $"Last build summary: total {stats.TotalRows} rows, embedded {stats.EmbeddedRows}, reused {stats.ReusedRows}, deleted {stats.DeletedRowsFromBaseline}.";
    }

    private void UpdateOneNoteStatus()
    {
        var hasClientId = !string.IsNullOrWhiteSpace(_oneNoteClientId);
        var selected = _oneNoteSettings.SelectedSections.Count;
        var who = string.IsNullOrWhiteSpace(_oneNoteSettings.AccountUsername)
            ? "Not connected."
            : $"Connected as {_oneNoteSettings.AccountUsername}.";
        var enabled = EnableOneNoteCheck.IsChecked == true;
        OneNoteStatusText.Text = enabled
            ? $"{who} {selected} section(s) selected."
            : $"{who} OneNote ingestion disabled.";
        ConnectOneNoteBtn.IsEnabled = hasClientId;
        SelectOneNoteSectionsBtn.IsEnabled = hasClientId;
        if (!hasClientId)
        {
            OneNoteStatusText.Text = "Add Knode:OneNote:ClientId in appsettings.Local.json to enable OneNote.";
            ConnectOneNoteBtn.Content = "Connect OneNote…";
            ConnectOneNoteBtn.ToolTip = null;
        }
        else
        {
            var signedIn = !string.IsNullOrWhiteSpace(_oneNoteSettings.AccountUsername);
            ConnectOneNoteBtn.Content = signedIn ? "Connected to OneNote" : "Connect OneNote…";
            ConnectOneNoteBtn.ToolTip = signedIn
                ? $"Signed in as {_oneNoteSettings.AccountUsername}. Click to sign in again or switch account."
                : "Sign in with your personal Microsoft account.";
        }
    }

    private async Task<(string Token, string Username)> AcquireOneNoteTokenAsync(bool allowInteractiveWhenNeeded = true)
    {
        _oneNoteAuth ??= new OneNoteAuthService(_oneNoteClientId);
        return await _oneNoteAuth.AcquireTokenAsync(allowInteractiveWhenNeeded).ConfigureAwait(true);
    }

    private async void ConnectOneNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var token = await AcquireOneNoteTokenAsync(allowInteractiveWhenNeeded: true).ConfigureAwait(true);
            _oneNoteSettings.AccountUsername = token.Username;
            OneNoteSettingsStore.Save(_oneNoteSettings);
            UpdateOneNoteStatus();
            MessageBox.Show("OneNote connected.", "Knode", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "OneNote sign-in failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SelectOneNoteSections_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (token, username) = await AcquireOneNoteTokenAsync().ConfigureAwait(true);
            _oneNoteSettings.AccountUsername = username;
            using var graph = new GraphOneNoteClient(token);
            var notebooks = await graph.GetNotebooksAsync().ConfigureAwait(true);
            var sections = await graph.GetSectionsAsync(notebooks).ConfigureAwait(true);
            if (sections.Count == 0)
            {
                MessageBox.Show("No OneNote sections were found for this account.", "Knode", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var preselected = _oneNoteSettings.SelectedSections.Select(static s => s.Id).ToHashSet(StringComparer.Ordinal);
            var picker = new OneNoteSectionPickerWindow(sections, preselected) { Owner = this };
            if (picker.ShowDialog() != true)
                return;

            _oneNoteSettings.SelectedSections = picker.SelectedSections
                .Select(static s => new OneNoteSectionSelection { Id = s.Id, Label = s.Label })
                .ToList();
            _oneNoteSettings.Enabled = EnableOneNoteCheck.IsChecked == true;
            OneNoteSettingsStore.Save(_oneNoteSettings);
            UpdateOneNoteStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "OneNote section picker failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<List<HighlightRecord>> BuildOneNoteRecordsAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        if (EnableOneNoteCheck.IsChecked != true || _oneNoteSettings.SelectedSections.Count == 0)
            return new List<HighlightRecord>();

        var (token, username) = await AcquireOneNoteTokenAsync().ConfigureAwait(true);
        _oneNoteSettings.AccountUsername = username;
        var selectedById = _oneNoteSettings.SelectedSections.ToDictionary(s => s.Id, s => s, StringComparer.Ordinal);
        var snapshot = LoadOneNoteSnapshot()
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value.SourceSectionId)
                         && selectedById.ContainsKey(kv.Value.SourceSectionId))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var syncMap = _oneNoteSettings.SectionSyncState
            .Where(s => !string.IsNullOrWhiteSpace(s.SectionId))
            .ToDictionary(s => s.SectionId, s => s, StringComparer.Ordinal);

        using var graph = new GraphOneNoteClient(token);
        foreach (var section in _oneNoteSettings.SelectedSections)
        {
            ct.ThrowIfCancellationRequested();
            syncMap.TryGetValue(section.Id, out var state);
            progress.Report($"OneNote sync: {section.Label}…");
            var pages = await graph.GetPagesForSectionAsync(section.Id, state?.LastSyncUtc, ct).ConfigureAwait(true);
            var latest = state?.LastSyncUtc ?? DateTimeOffset.MinValue;
            foreach (var p in pages)
            {
                ct.ThrowIfCancellationRequested();
                var text = await graph.GetPagePlainTextAsync(p.ContentUrl, 12000, ct).ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                if (p.LastModified > latest)
                    latest = p.LastModified;

                snapshot[$"onenote:{p.Id}"] = new HighlightRecord
                {
                    Id = $"onenote:{p.Id}",
                    Source = "onenote",
                    SourceSectionId = section.Id,
                    BookTitle = section.Label,
                    Author = string.IsNullOrWhiteSpace(p.Title) ? "(untitled page)" : p.Title.Trim(),
                    Location = p.WebUrl,
                    Text = text,
                    LastAccessed = p.LastModified.UtcDateTime.ToString("yyyy-MM-dd"),
                };
            }

            syncMap[section.Id] = new OneNoteSectionSyncState
            {
                SectionId = section.Id,
                LastSyncUtc = latest == DateTimeOffset.MinValue ? state?.LastSyncUtc : latest,
            };
        }

        _oneNoteSettings.SectionSyncState = syncMap.Values.OrderBy(s => s.SectionId, StringComparer.Ordinal).ToList();
        OneNoteSettingsStore.Save(_oneNoteSettings);
        SaveOneNoteSnapshot(snapshot);
        return snapshot.Values.ToList();
    }

    private static string OneNoteSnapshotPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Knode", "onenote_records.json");

    private static Dictionary<string, HighlightRecord> LoadOneNoteSnapshot()
    {
        if (!File.Exists(OneNoteSnapshotPath))
            return new Dictionary<string, HighlightRecord>(StringComparer.Ordinal);
        try
        {
            var list = HighlightRecordJson.DeserializeRecordsArray(File.ReadAllText(OneNoteSnapshotPath))
                ?? new List<HighlightRecord>();
            return list
                .Where(static r => !string.IsNullOrWhiteSpace(r.Id))
                .ToDictionary(r => r.Id, r => r, StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, HighlightRecord>(StringComparer.Ordinal);
        }
    }

    private static void SaveOneNoteSnapshot(IReadOnlyDictionary<string, HighlightRecord> recordsById)
    {
        var dir = Path.GetDirectoryName(OneNoteSnapshotPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);
        var records = recordsById.Values
            .OrderBy(static r => r.BookTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static r => r.Author, StringComparer.OrdinalIgnoreCase)
            .ToList();
        File.WriteAllText(OneNoteSnapshotPath, System.Text.Json.JsonSerializer.Serialize(records, HighlightRecordJson.Options));
    }

    private void ForgetSavedApiKey_Click(object sender, RoutedEventArgs e)
    {
        if (!AgentApiKeyStore.HasStoredKey)
        {
            MessageBox.Show("There is no saved API key to remove.", "Knode", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(
                "Remove the AI agent API key stored for your Windows account on this PC? You can paste a key again later.",
                "Forget saved key",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        AgentApiKeyStore.Clear();
        UpdateApiKeySavedHint();
        _client?.Dispose();
        _client = null;
        AskBtn.IsEnabled = _rag.IsReady && !string.IsNullOrEmpty(ResolveApiKey());
        IndexStatus.Text = AskBtn.IsEnabled
            ? IndexStatus.Text
            : "Saved key removed. Enter a key (or set AGENT_API_KEY / GEMINI_API_KEY) to use Ask.";
        RefreshAskPageStatus();
    }

    private void BrowseCorpus_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSONL corpus|corpus.jsonl;*.jsonl|All files|*.*",
            Title = "Select corpus.jsonl",
        };
        if (dlg.ShowDialog() == true)
        {
            CorpusPathBox.Text = dlg.FileName;
            KnodeUserSettings.SaveLastCorpusPath(dlg.FileName);
            RefreshAskPageStatus();
        }
    }

    private async void BuildIndex_Click(object sender, RoutedEventArgs e)
    {
        var path = CorpusPathBox.Text.Trim();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            MessageBox.Show("Choose a valid corpus.jsonl file (see Help if you need to create one).", "Knode", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var key = ResolveApiKey();
        if (string.IsNullOrEmpty(key))
        {
            MessageBox.Show(
                "Enter your AI agent API key, save one with “Remember…”, or set AGENT_API_KEY / GEMINI_API_KEY / GOOGLE_API_KEY.",
                "Knode",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        BuildIndexBtn.IsEnabled = false;
        ConnectOneNoteBtn.IsEnabled = false;
        SelectOneNoteSectionsBtn.IsEnabled = false;
        AskBtn.IsEnabled = false;
        IndexStatus.Text = "Working…";
        try
        {
            var baseUrl = _config["Knode:Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/";
            _client?.Dispose();
            _client = new GeminiClient(key, baseUrl);
            IProgress<string> progress = new Progress<string>(s => IndexStatus.Text = s);
            var embedPauseMs = _config.GetValue("Knode:EmbeddingBatchDelayMs", 18500);
            var forceRebuild = ForceFullRebuildCheck.IsChecked == true;
            _oneNoteSettings.Enabled = EnableOneNoteCheck.IsChecked == true;

            List<HighlightRecord>? oneNoteRecords = null;
            if (_oneNoteSettings.Enabled && _oneNoteSettings.SelectedSections.Count == 0)
            {
                throw new InvalidOperationException("OneNote is enabled but no sections are selected. Use Setup > Select sections.");
            }

            if (_oneNoteSettings.Enabled)
            {
                oneNoteRecords = await BuildOneNoteRecordsAsync(progress).ConfigureAwait(true);
                progress.Report($"OneNote sync complete ({oneNoteRecords.Count} page records).");
            }

            var oneNoteSignature = BuildOneNoteIndexSignature(_oneNoteSettings.SelectedSections);
            _oneNoteSettings.LastIndexSignature = oneNoteSignature;
            OneNoteSettingsStore.Save(_oneNoteSettings);

            var buildStats = await _rag.BuildIndexAsync(
                _client,
                path,
                progress,
                embedPauseMs,
                forceRebuild,
                oneNoteRecords,
                oneNoteSignature).ConfigureAwait(true);
            LastBuildSummaryText.Text = FormatBuildSummary(buildStats);
            KnodeUserSettings.SaveLastCorpusPath(path);
            if (RememberApiKeyCheck.IsChecked == true)
            {
                var fromBox = ApiKeyBox.Password.Trim();
                if (!string.IsNullOrEmpty(fromBox))
                    AgentApiKeyStore.Save(fromBox);
            }

            UpdateApiKeySavedHint();
        }
        catch (Exception ex)
        {
            IndexStatus.Text = "Failed.";
            LastBuildSummaryText.Text = "Last build summary unavailable (build failed).";
            MessageBox.Show(ex.Message, "Build index failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BuildIndexBtn.IsEnabled = true;
            ConnectOneNoteBtn.IsEnabled = !string.IsNullOrWhiteSpace(_oneNoteClientId);
            SelectOneNoteSectionsBtn.IsEnabled = !string.IsNullOrWhiteSpace(_oneNoteClientId);
            AskBtn.IsEnabled = _rag.IsReady && !string.IsNullOrEmpty(ResolveApiKey());
            UpdateOneNoteStatus();
            RefreshAskPageStatus();
        }
    }

    private async void Ask_Click(object sender, RoutedEventArgs e)
    {
        if (_askInFlight)
        {
            _askCts?.Cancel();
            return;
        }

        var q = QuestionBox.Text.Trim();
        if (string.IsNullOrEmpty(q) || !_rag.IsReady)
            return;

        var key = ResolveApiKey();
        if (string.IsNullOrEmpty(key))
        {
            MessageBox.Show("Enter or restore an AI agent API key.", "Knode", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var baseUrl = _config["Knode:Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/";
        if (_client is null)
            _client = new GeminiClient(key, baseUrl);

        _askCts?.Dispose();
        _askCts = new CancellationTokenSource();
        var askCt = _askCts.Token;

        _askInFlight = true;
        AskBtn.Content = "Cancel";
        AskBtn.ToolTip = "Stop the current Ask (embedding or Gemini).";
        try
        {
            await MarkdownToHtml.NavigatePlaceholderAsync(AnswerWebView, "Thinking…", "Answer", answerTwoColumn: true).ConfigureAwait(true);
            await MarkdownToHtml.NavigatePlaceholderAsync(SourcesWebView, "Retrieving passages…", "Sources").ConfigureAwait(true);

            var topK = int.TryParse(_config["Knode:TopK"], out var k) ? k : 32;
            var (answer, passages, retrievalBanner) = await _rag.AskAsync(_client, q, topK, askCt).ConfigureAwait(true);
            RetrievalBanner.Text = retrievalBanner;
            var answerMd = AnswerDisplayFormatter.ForDisplay(answer);
            if (string.IsNullOrWhiteSpace(answerMd))
                answerMd = PromptCoach.BlankAnswerPlaceholderMd();
            await MarkdownToHtml.NavigateMarkdownAsync(AnswerWebView, answerMd, "Answer", answerTwoColumn: true).ConfigureAwait(true);
            var sourcesMd = PromptCoach.SourcesPanelMarkdown(passages);
            await MarkdownToHtml.NavigateMarkdownAsync(SourcesWebView, sourcesMd, "Sources").ConfigureAwait(true);
            if (passages.Count == 0)
                EnsureSourcesPanelVisible();
        }
        catch (OperationCanceledException)
        {
            RetrievalBanner.Text = "Ask cancelled.";
            try
            {
                await MarkdownToHtml
                    .NavigateMarkdownAsync(AnswerWebView, PromptCoach.AskCancelledAnswerMd(), "Answer", answerTwoColumn: true)
                    .ConfigureAwait(true);
                await MarkdownToHtml
                    .NavigateMarkdownAsync(SourcesWebView, PromptCoach.AskCancelledSourcesMd(), "Sources")
                    .ConfigureAwait(true);
            }
            catch
            {
                // WebView2 unavailable
            }

            EnsureSourcesPanelVisible();
        }
        catch (Exception ex)
        {
            await MarkdownToHtml.NavigatePlaceholderAsync(AnswerWebView, "", "Answer", answerTwoColumn: true).ConfigureAwait(true);
            MessageBox.Show(PromptCoach.AskFailedUserMessage(ex.Message), "Ask failed", MessageBoxButton.OK,
                MessageBoxImage.Error);
            try
            {
                await MarkdownToHtml
                    .NavigateMarkdownAsync(SourcesWebView, PromptCoach.AskFailedSourcesMd(ex.Message), "Sources")
                    .ConfigureAwait(true);
            }
            catch
            {
                // WebView2 unavailable — MessageBox already shown
            }

            EnsureSourcesPanelVisible();
        }
        finally
        {
            _askInFlight = false;
            AskBtn.Content = "Ask";
            AskBtn.ToolTip = "Ask your library. While a request runs, this becomes Cancel.";
            _askCts?.Dispose();
            _askCts = null;
            AskBtn.IsEnabled = _rag.IsReady && !string.IsNullOrEmpty(ResolveApiKey());
        }
    }

    /// <summary>
    /// Semantic version from the built assembly (from Knode.csproj &lt;Version&gt;); strips +commit suffix when present.
    /// </summary>
    private static string GetAppDisplayVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }

        return asm.GetName().Version?.ToString(3) ?? "";
    }
}

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private GeminiClient? _client;
    private bool _navExpanded = true;

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
        ModelsInfo.Text =
            $"Embeddings: {emb}  ·  Chat: {chat}  ·  Adjust in appsettings.json. " +
            $"Gemini keys: https://aistudio.google.com/apikey";

        try
        {
            var dllPath = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(dllPath))
            {
                var t = File.GetLastWriteTimeUtc(dllPath);
                BuildInfoText.Text =
                    $"This build: {t:yyyy-MM-dd HH:mm} UTC — use this time to confirm you are not running an old Knode.exe.";
            }
        }
        catch
        {
            BuildInfoText.Text = "";
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
                "Answer").ConfigureAwait(true);
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

            var progress = new Progress<string>(s => IndexStatus.Text = s);
            try
            {
                if (await _rag.TryLoadPersistedIndexAsync(path, progress).ConfigureAwait(true))
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
        AskBtn.IsEnabled = false;
        IndexStatus.Text = "Working…";
        try
        {
            var baseUrl = _config["Knode:Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/";
            _client?.Dispose();
            _client = new GeminiClient(key, baseUrl);
            var progress = new Progress<string>(s => IndexStatus.Text = s);
            var embedPauseMs = _config.GetValue("Knode:EmbeddingBatchDelayMs", 18500);
            var forceRebuild = ForceFullRebuildCheck.IsChecked == true;
            await _rag.BuildIndexAsync(_client, path, progress, embedPauseMs, forceRebuild).ConfigureAwait(true);
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
            MessageBox.Show(ex.Message, "Build index failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BuildIndexBtn.IsEnabled = true;
            AskBtn.IsEnabled = _rag.IsReady && !string.IsNullOrEmpty(ResolveApiKey());
            RefreshAskPageStatus();
        }
    }

    private async void Ask_Click(object sender, RoutedEventArgs e)
    {
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

        AskBtn.IsEnabled = false;
        try
        {
            await MarkdownToHtml.NavigatePlaceholderAsync(AnswerWebView, "Thinking…", "Answer").ConfigureAwait(true);
            await MarkdownToHtml.NavigatePlaceholderAsync(SourcesWebView, "Retrieving passages…", "Sources").ConfigureAwait(true);

            var topK = int.TryParse(_config["Knode:TopK"], out var k) ? k : 32;
            var (answer, passages, retrievalBanner) = await _rag.AskAsync(_client, q, topK).ConfigureAwait(true);
            RetrievalBanner.Text = retrievalBanner;
            var answerMd = AnswerDisplayFormatter.ForDisplay(answer);
            if (string.IsNullOrWhiteSpace(answerMd))
                answerMd = PromptCoach.BlankAnswerPlaceholderMd();
            await MarkdownToHtml.NavigateMarkdownAsync(AnswerWebView, answerMd, "Answer").ConfigureAwait(true);
            var sourcesMd = PromptCoach.SourcesPanelMarkdown(passages);
            await MarkdownToHtml.NavigateMarkdownAsync(SourcesWebView, sourcesMd, "Sources").ConfigureAwait(true);
            SourcesExpander.IsExpanded = passages.Count == 0;
        }
        catch (Exception ex)
        {
            await MarkdownToHtml.NavigatePlaceholderAsync(AnswerWebView, "", "Answer").ConfigureAwait(true);
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

            SourcesExpander.IsExpanded = true;
        }
        finally
        {
            AskBtn.IsEnabled = _rag.IsReady && !string.IsNullOrEmpty(ResolveApiKey());
        }
    }
}

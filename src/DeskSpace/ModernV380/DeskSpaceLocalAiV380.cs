using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPasswordBox = System.Windows.Controls.PasswordBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace DeskSpace;

internal enum AiTaskKind
{
    Summarize,
    CleanToCsvTable,
    DraftReply,
    BatchRenamePattern,
    ExcelFormula,
    Custom
}

internal sealed class LocalAiConfigV380
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Ollama";
    public string Endpoint { get; set; } = "http://127.0.0.1:11434";
    public string Model { get; set; } = "llama3.2";
    public string EncryptedApiKeyBase64 { get; set; } = "";
}

internal static class DeskSpaceLocalAiV380
{
    private static readonly object ConfigLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private static readonly HttpClient SharedHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private static LocalAiConfigV380? _config;

    internal static string DefaultStoragePath =>
        System.IO.Path.Combine(WorkspaceStore.Root, "local-ai-v380.json");

    internal static LocalAiConfigV380 Config
    {
        get
        {
            lock (ConfigLock)
                return _config ??= LoadConfig();
        }
        set
        {
            lock (ConfigLock)
                _config = SanitizeConfig(value ?? new LocalAiConfigV380());
        }
    }

    internal static LocalAiConfigV380 LoadConfig(string? storagePath = null)
    {
        string path = storagePath ?? DefaultStoragePath;
        lock (ConfigLock)
        {
            try
            {
                if (!File.Exists(path))
                    return new LocalAiConfigV380();

                var info = new FileInfo(path);
                if (info.Length > 256 * 1024)
                    return new LocalAiConfigV380();

                var loaded = JsonSerializer.Deserialize<LocalAiConfigV380>(
                    File.ReadAllText(path, Encoding.UTF8),
                    JsonOptions);
                return SanitizeConfig(loaded ?? new LocalAiConfigV380());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new LocalAiConfigV380();
            }
        }
    }

    internal static void SaveConfig(LocalAiConfigV380 config, string? storagePath = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        var clean = SanitizeConfig(config);
        string path = storagePath ?? DefaultStoragePath;
        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        lock (ConfigLock)
        {
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(clean, JsonOptions), new UTF8Encoding(false));
                File.Move(tmp, path, true);
                if (storagePath == null)
                    _config = clean;
            }
            finally
            {
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch (IOException) { }
            }
        }
    }

    internal static void SetEncryptedApiKey(LocalAiConfigV380 config, string? plainApiKey)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(plainApiKey))
        {
            config.EncryptedApiKeyBase64 = "";
            return;
        }

        byte[] plain = Encoding.UTF8.GetBytes(plainApiKey.Trim());
        try
        {
            byte[] protectedBlob = DeskSpaceVaultProV380.DpapiProtectCurrentUser(plain);
            config.EncryptedApiKeyBase64 = Convert.ToBase64String(protectedBlob);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    internal static string GetDecryptedApiKey(LocalAiConfigV380 config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(config.EncryptedApiKeyBase64))
            return "";

        byte[] blob = Convert.FromBase64String(config.EncryptedApiKeyBase64);
        byte[] plain = DeskSpaceVaultProV380.DpapiUnprotectCurrentUser(blob);
        try
        {
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static LocalAiConfigV380 SanitizeConfig(LocalAiConfigV380 cfg)
    {
        string provider = string.Equals(cfg.Provider?.Trim(), "OpenAiCompatible", StringComparison.OrdinalIgnoreCase)
            ? "OpenAiCompatible"
            : "Ollama";
        string endpoint = string.IsNullOrWhiteSpace(cfg.Endpoint)
            ? "http://127.0.0.1:11434"
            : cfg.Endpoint.Trim();
        string model = string.IsNullOrWhiteSpace(cfg.Model) ? "llama3.2" : cfg.Model.Trim();

        return new LocalAiConfigV380
        {
            Enabled = cfg.Enabled,
            Provider = provider,
            Endpoint = endpoint,
            Model = model,
            EncryptedApiKeyBase64 = cfg.EncryptedApiKeyBase64 ?? ""
        };
    }

    internal static bool IsValidEndpoint(string? url, out bool isLocalhost, out string reason)
    {
        isLocalhost = false;
        if (string.IsNullOrWhiteSpace(url))
        {
            reason = "Endpoint URL is empty.";
            return false;
        }

        string trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            reason = "Endpoint must be an absolute http:// or https:// URL.";
            return false;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Only http:// and https:// endpoints are allowed.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            reason = "Endpoint URL must specify a valid host without embedded credentials.";
            return false;
        }

        string host = uri.IdnHost.Trim('[', ']');
        isLocalhost = uri.IsLoopback
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.Ordinal)
            || host.Equals("::1", StringComparison.Ordinal)
            || (IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip));

        reason = isLocalhost
            ? "Localhost / Private endpoint (traffic stays on this device)."
            : "External endpoint (data leaves this device when Run is clicked).";
        return true;
    }

    internal static string BuildPrompt(AiTaskKind kind, string inputText, string customInstruction = "")
    {
        string input = (inputText ?? "").Trim();
        string instruction = (customInstruction ?? "").Trim();

        return kind switch
        {
            AiTaskKind.Summarize =>
                "Summarize the following content into concise, accurate bullet points highlighting key numbers, dates, and action items.\n\n" +
                input,

            AiTaskKind.CleanToCsvTable =>
                "Convert the following text into a clean RFC-4180 CSV table with a clear header row. Preserve numbers and dates accurately and return only the CSV output.\n\n" +
                input,

            AiTaskKind.DraftReply =>
                "Draft a clear, polite, professional reply to the following message." +
                (string.IsNullOrEmpty(instruction) ? "" : $"\nAdditional guidance: {instruction}") +
                "\n\nMessage:\n" + input,

            AiTaskKind.BatchRenamePattern =>
                "Suggest a clean, deterministic batch file-renaming rule/pattern (and preview examples) for the following filenames." +
                (string.IsNullOrEmpty(instruction) ? "" : $"\nRule preference: {instruction}") +
                "\n\nFiles:\n" + input,

            AiTaskKind.ExcelFormula =>
                "Provide the exact Microsoft Excel formula for the following requirement, followed by a brief one-line explanation.\n\n" +
                (string.IsNullOrEmpty(instruction) ? "" : $"Context: {instruction}\n") +
                input,

            AiTaskKind.Custom =>
                (string.IsNullOrEmpty(instruction) ? "Process the following input carefully:" : instruction) +
                (string.IsNullOrEmpty(input) ? "" : "\n\n" + input),

            _ => input
        };
    }

    internal static async Task<IReadOnlyList<string>> DetectOllamaModelsAsync(
        string endpoint,
        CancellationToken ct = default)
    {
        if (!IsValidEndpoint(endpoint, out _, out string reason))
            throw new ArgumentException(reason, nameof(endpoint));

        string tagsUrl = endpoint.Trim().TrimEnd('/') + "/api/tags";
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

        using var req = new HttpRequestMessage(HttpMethod.Get, tagsUrl);
        using var resp = await SharedHttp.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        string json = await resp.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var models = new List<string>();
        if (doc.RootElement.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                {
                    string? name = nameEl.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(name) && !models.Contains(name, StringComparer.OrdinalIgnoreCase))
                        models.Add(name);
                }
            }
        }
        return models;
    }

    internal static async Task<string> RunTaskAsync(
        AiTaskKind kind,
        string inputText,
        string customInstruction = "",
        CancellationToken ct = default)
    {
        var cfg = Config;
        if (!cfg.Enabled)
            throw new InvalidOperationException(
                "Local AI is disabled (opt-in required). Enable AI requests before running a task.");

        if (!IsValidEndpoint(cfg.Endpoint, out _, out string reason))
            throw new InvalidOperationException("Invalid AI endpoint: " + reason);

        if (string.IsNullOrWhiteSpace(cfg.Model))
            throw new InvalidOperationException("Model name cannot be empty.");

        if (string.IsNullOrWhiteSpace(inputText) &&
            (kind != AiTaskKind.Custom || string.IsNullOrWhiteSpace(customInstruction)))
            throw new ArgumentException("Input text is required.", nameof(inputText));

        string prompt = BuildPrompt(kind, inputText, customInstruction);
        string baseUrl = cfg.Endpoint.Trim().TrimEnd('/');

        if (cfg.Provider.Equals("OpenAiCompatible", StringComparison.OrdinalIgnoreCase))
        {
            string url = baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                ? baseUrl
                : baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl + "/chat/completions"
                    : baseUrl + "/v1/chat/completions";

            var payload = new
            {
                model = cfg.Model,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.2
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            string apiKey = "";
            try
            {
                apiKey = GetDecryptedApiKey(cfg);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                throw new InvalidOperationException("Saved API key could not be decrypted for the current Windows user.", ex);
            }

            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var resp = await SharedHttp.SendAsync(req, ct).ConfigureAwait(false);
            string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Endpoint returned HTTP {(int)resp.StatusCode}: {TrimForError(body)}");

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    return (content.GetString() ?? "").Trim();
                }
                if (first.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                    return (textEl.GetString() ?? "").Trim();
            }

            throw new InvalidDataException("OpenAI-compatible response did not contain choices[0].message.content.");
        }
        else
        {
            string url = baseUrl.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase)
                ? baseUrl
                : baseUrl + "/api/generate";

            var payload = new
            {
                model = cfg.Model,
                prompt,
                stream = false
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var resp = await SharedHttp.SendAsync(req, ct).ConfigureAwait(false);
            string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Ollama returned HTTP {(int)resp.StatusCode}: {TrimForError(body)}");

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("response", out var respEl) &&
                respEl.ValueKind == JsonValueKind.String)
            {
                return (respEl.GetString() ?? "").Trim();
            }

            throw new InvalidDataException("Ollama response did not contain 'response' text.");
        }
    }

    private static string TrimForError(string? text)
    {
        string s = (text ?? "").Trim();
        return s.Length > 260 ? s[..260] + "…" : s;
    }

    internal static void Show(Window? owner, string? initialInput = null)
    {
        var p = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskSpace · Local AI Assistant (Opt-In)",
            920,
            680,
            owner,
            transient: false);
        win.MinWidth = 740;
        win.MinHeight = 540;

        var cfg = LoadConfig();
        Config = cfg;

        var root = new DockPanel { Margin = new Thickness(16) };

        var statusBar = new TextBlock
        {
            Text = cfg.Enabled
                ? "AI enabled (manual Run only — zero background traffic)."
                : "AI disabled by default. Enable explicitly above and click Run when ready.",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        DockPanel.SetDock(statusBar, Dock.Bottom);
        root.Children.Add(statusBar);

        var body = new StackPanel();

        // Settings & Privacy Card
        var cfgStack = new StackPanel();
        cfgStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Endpoint & Privacy Configuration (Strictly Opt-In)", p));

        var privacyBadge = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6, 10, 6),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var privacyText = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        privacyBadge.Child = privacyText;

        var enableCheck = new WpfCheckBox
        {
            Content = "Enable AI requests (only sends when you explicitly press Run)",
            IsChecked = cfg.Enabled,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var providerCombo = new WpfComboBox
        {
            ItemsSource = new[] { "Ollama", "OpenAiCompatible" },
            SelectedItem = cfg.Provider,
            Width = 170,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var endpointBox = new WpfTextBox { Text = cfg.Endpoint, MinWidth = 280, Margin = new Thickness(0, 0, 8, 0) };
        DeskSpaceDesignV283.Input(endpointBox, p);

        var modelCombo = new WpfComboBox
        {
            IsEditable = true,
            Text = cfg.Model,
            MinWidth = 180,
            Margin = new Thickness(0, 0, 8, 0)
        };
        modelCombo.Items.Add(cfg.Model);

        var apiKeyBox = new WpfPasswordBox
        {
            Width = 210,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Optional API key (stored encrypted via Windows DPAPI)"
        };

        var keyBadge = new TextBlock
        {
            Text = string.IsNullOrEmpty(cfg.EncryptedApiKeyBase64) ? "No API key stored" : "API key stored (DPAPI)",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11.5
        };

        void UpdatePrivacyBadge()
        {
            bool valid = IsValidEndpoint(endpointBox.Text, out bool local, out string reason);
            WpfColor tone = !valid
                ? DeskSpaceDesignV283.Danger(p)
                : local
                    ? DeskSpaceDesignV283.Success(p)
                    : DeskSpaceDesignV283.Warning(p);
            privacyBadge.Background = DeskSpaceDesignV283.Solid(WpfColor.FromArgb(30, tone.R, tone.G, tone.B));
            privacyBadge.BorderBrush = DeskSpaceDesignV283.Solid(tone);
            privacyText.Foreground = DeskSpaceDesignV283.Solid(p.Text);
            privacyText.Text = (!valid ? "Invalid Endpoint: " : local ? "Localhost / Private: " : "External Endpoint: ") + reason;
        }

        endpointBox.TextChanged += (_, _) => UpdatePrivacyBadge();
        UpdatePrivacyBadge();

        void PersistDraft()
        {
            cfg.Enabled = enableCheck.IsChecked == true;
            cfg.Provider = providerCombo.SelectedItem as string ?? "Ollama";
            cfg.Endpoint = endpointBox.Text.Trim();
            cfg.Model = (modelCombo.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(apiKeyBox.Password))
            {
                SetEncryptedApiKey(cfg, apiKeyBox.Password);
                apiKeyBox.Clear();
                keyBadge.Text = "API key stored (DPAPI)";
            }
            SaveConfig(cfg);
        }

        var row1 = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row1.Children.Add(new TextBlock { Text = "Provider:", Foreground = DeskSpaceDesignV283.Solid(p.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        row1.Children.Add(providerCombo);
        row1.Children.Add(new TextBlock { Text = "Endpoint:", Foreground = DeskSpaceDesignV283.Solid(p.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        row1.Children.Add(endpointBox);
        row1.Children.Add(new TextBlock { Text = "Model:", Foreground = DeskSpaceDesignV283.Solid(p.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        row1.Children.Add(modelCombo);

        var row2 = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        row2.Children.Add(new TextBlock { Text = "New API Key:", Foreground = DeskSpaceDesignV283.Solid(p.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        row2.Children.Add(apiKeyBox);

        var saveBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Save Settings", () =>
        {
            try
            {
                PersistDraft();
                statusBar.Text = "Saved Local AI settings (API key encrypted via DPAPI).";
            }
            catch (Exception ex)
            {
                statusBar.Text = "Save failed: " + ex.Message;
            }
        }, false, "Save Local AI configuration");
        saveBtn.Margin = new Thickness(0, 0, 8, 4);

        var clearKeyBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Clear Key", () =>
        {
            apiKeyBox.Clear();
            SetEncryptedApiKey(cfg, null);
            SaveConfig(cfg);
            keyBadge.Text = "No API key stored";
            statusBar.Text = "Cleared stored API key.";
        }, false, "Remove stored DPAPI key");
        clearKeyBtn.Margin = new Thickness(0, 0, 8, 4);

        var detectBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Detect Ollama Models", async () =>
        {
            try
            {
                statusBar.Text = "Querying Ollama /api/tags...";
                var models = await DetectOllamaModelsAsync(endpointBox.Text.Trim());
                modelCombo.Items.Clear();
                foreach (var m in models)
                    modelCombo.Items.Add(m);
                if (models.Count > 0)
                    modelCombo.Text = models[0];
                statusBar.Text = models.Count == 0
                    ? "Connected to Ollama, but no pulled models were found."
                    : $"Detected {models.Count} Ollama model(s).";
            }
            catch (Exception ex)
            {
                statusBar.Text = "Could not detect Ollama models: " + ex.Message;
            }
        }, false, "Fetch installed models from Ollama GET /api/tags");
        detectBtn.Margin = new Thickness(0, 0, 8, 4);

        row2.Children.Add(saveBtn);
        row2.Children.Add(clearKeyBtn);
        row2.Children.Add(detectBtn);
        row2.Children.Add(keyBadge);

        cfgStack.Children.Add(privacyBadge);
        cfgStack.Children.Add(enableCheck);
        cfgStack.Children.Add(row1);
        cfgStack.Children.Add(row2);

        var cfgCard = DeskSpaceFluentBackdropV380.CreateCard(cfgStack, p);
        cfgCard.Margin = new Thickness(0, 0, 0, 12);
        body.Children.Add(cfgCard);

        // Task & Prompt Card
        var taskStack = new StackPanel();
        taskStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Task & Input", p));

        var taskCombo = new WpfComboBox
        {
            ItemsSource = Enum.GetValues<AiTaskKind>(),
            SelectedItem = AiTaskKind.Summarize,
            Width = 200,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var instructionBox = new WpfTextBox
        {
            MinWidth = 360,
            ToolTip = "Optional custom instruction or context"
        };
        DeskSpaceDesignV283.Input(instructionBox, p);

        var taskRow = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        taskRow.Children.Add(new TextBlock { Text = "Task:", Foreground = DeskSpaceDesignV283.Solid(p.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        taskRow.Children.Add(taskCombo);
        taskRow.Children.Add(new TextBlock { Text = "Instruction (optional):", Foreground = DeskSpaceDesignV283.Solid(p.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0) });
        taskRow.Children.Add(instructionBox);

        var inputBox = new WpfTextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 120,
            Text = initialInput ?? "",
            Margin = new Thickness(0, 0, 0, 8)
        };
        DeskSpaceDesignV283.Input(inputBox, p);

        var outputBox = new WpfTextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 140,
            Margin = new Thickness(0, 6, 0, 8)
        };
        DeskSpaceDesignV283.Input(outputBox, p);

        CancellationTokenSource? runCts = null;
        System.Windows.Controls.Button? runBtn = null;
        System.Windows.Controls.Button? cancelBtn = null;

        void DisposeRunCts()
        {
            var prev = Interlocked.Exchange(ref runCts, null);
            if (prev == null)
                return;
            try { prev.Cancel(); } catch (ObjectDisposedException) { }
            try { prev.Dispose(); } catch (ObjectDisposedException) { }
        }

        runBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Run Task", async () =>
        {
            PersistDraft();
            if (!Config.Enabled)
            {
                statusBar.Text = "Enable AI requests checkbox first to opt in.";
                return;
            }

            DisposeRunCts();
            var nextCts = new CancellationTokenSource();
            runCts = nextCts;
            if (runBtn != null) runBtn.IsEnabled = false;
            if (cancelBtn != null) cancelBtn.IsEnabled = true;

            try
            {
                statusBar.Text = $"Running {taskCombo.SelectedItem} with {Config.Model}...";
                var kind = taskCombo.SelectedItem is AiTaskKind k ? k : AiTaskKind.Summarize;
                string result = await RunTaskAsync(kind, inputBox.Text, instructionBox.Text, nextCts.Token);
                outputBox.Text = result;
                statusBar.Text = $"Completed ({result.Length} chars).";
            }
            catch (OperationCanceledException)
            {
                statusBar.Text = "Cancelled.";
            }
            catch (Exception ex)
            {
                statusBar.Text = "AI task failed: " + ex.Message;
            }
            finally
            {
                if (runBtn != null) runBtn.IsEnabled = true;
                if (cancelBtn != null) cancelBtn.IsEnabled = false;
            }
        }, true, "Send explicit request to configured endpoint");
        runBtn.Margin = new Thickness(0, 0, 8, 4);

        cancelBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Cancel", () =>
        {
            var current = Volatile.Read(ref runCts);
            if (current == null) return;
            try { current.Cancel(); } catch (ObjectDisposedException) { }
        }, false, "Cancel running AI request");
        cancelBtn.IsEnabled = false;
        cancelBtn.Margin = new Thickness(0, 0, 8, 4);

        var copyBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy Output", () =>
        {
            if (!string.IsNullOrEmpty(outputBox.Text))
            {
                System.Windows.Clipboard.SetText(outputBox.Text);
                statusBar.Text = "Copied AI output to clipboard.";
            }
        }, false, "Copy result to clipboard");
        copyBtn.Margin = new Thickness(0, 0, 8, 4);

        var saveNoteBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Save as DeskSpace Note", () =>
        {
            if (string.IsNullOrWhiteSpace(outputBox.Text))
                return;
            var store = new WorkspaceStore();
            var ws = store.Load();
            ws.Notes.Insert(0, new NoteItem
            {
                Title = $"AI {taskCombo.SelectedItem} ({DateTime.Now:g})",
                Content = outputBox.Text,
                Updated = DateTime.Now
            });
            store.Save(ws);
            statusBar.Text = "Saved AI output as a new DeskSpace note.";
        }, false, "Save output to Workspace Notes");
        saveNoteBtn.Margin = new Thickness(0, 0, 8, 4);

        var actionRow = new WrapPanel { Orientation = WpfOrientation.Horizontal };
        actionRow.Children.Add(runBtn);
        actionRow.Children.Add(cancelBtn);
        actionRow.Children.Add(copyBtn);
        actionRow.Children.Add(saveNoteBtn);

        taskStack.Children.Add(taskRow);
        taskStack.Children.Add(inputBox);
        taskStack.Children.Add(actionRow);
        taskStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Preview Output", p));
        taskStack.Children.Add(outputBox);

        body.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(taskStack, p));

        win.Closed += (_, _) => DisposeRunCts();

        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body
        });

        win.Content = root;
        win.Show();
        win.Activate();
    }
}

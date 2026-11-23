using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using WpfTextBox = System.Windows.Controls.TextBox;

namespace DeskSpace;

internal sealed record RoutineActionV380(string Kind, string Target, string Label);

internal sealed record RoutineRuleV380(
    Guid Id,
    string Name,
    bool Enabled,
    string TriggerType,
    string TriggerValue,
    List<RoutineActionV380> Actions);

internal sealed record AppProfilePinV380(
    string AppCategory,
    string Label,
    string ActionKind,
    string Target);

internal sealed record RoutineHistoryEntryV380(
    Guid Id,
    Guid RuleId,
    string RuleName,
    DateTime TimestampUtc,
    bool Success,
    string Summary);

internal sealed class AutomationStoreV380
{
    public List<RoutineRuleV380> Rules { get; set; } = new();
    public List<AppProfilePinV380> Pins { get; set; } = new();
}

internal static class DeskSpaceAutomationProV380
{
    private const int MaxHistoryEntries = 30;
    private static readonly object StoreLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> DangerousScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".vbs", ".vbe",
        ".js", ".jse", ".wsf", ".wsh", ".hta", ".scr", ".msi",
        ".msp", ".msix", ".appx", ".appref-ms", ".reg", ".com",
        ".pif", ".dll", ".cpl", ".sct", ".inf", ".jar", ".sh",
        ".py", ".rb", ".pl", ".php"
    };

    internal static readonly string[] SupportedActionKinds =
    {
        "ApplyWindowLayout",
        "StartFocusTimer",
        "SwitchDeskTouchProfile",
        "SwitchThemeMode",
        "OpenAppOrUrl",
        "CopySnippet"
    };

    internal static readonly string[] SupportedTriggerTypes =
    {
        "Manual",
        "AppCategory",
        "MonitorTopology",
        "Schedule"
    };

    internal static string DefaultRoutinesPath =>
        System.IO.Path.Combine(WorkspaceStore.Root, "routines-v380.json");

    internal static string DefaultHistoryPath =>
        System.IO.Path.Combine(WorkspaceStore.Root, "routine-history-v380.json");

    internal static string ClassifyAppExtended(string? processName)
    {
        string n = (processName ?? "").Trim().ToLowerInvariant();
        if (n.EndsWith(".exe", StringComparison.Ordinal))
            n = n[..^4];

        return n switch
        {
            "code" or "devenv" or "studio64" or "rider64" or "wt" or "powershell" or "pwsh" or "cmd" => "Coding",
            "tally" or "wintax" or "wintds" => "Accounting",
            "ms-teams" or "teams" or "zoom" or "webex" => "Meetings",
            "figma" or "photoshop" or "illustrator" or "blender" => "Design",
            _ => DeskSpaceContextSuggestionsV237.Classify(n)
        };
    }

    internal static bool IsActionSafe(RoutineActionV380 action, out string reason)
    {
        if (action == null)
        {
            reason = "Action is null.";
            return false;
        }

        string kind = (action.Kind ?? "").Trim();
        string target = (action.Target ?? "").Trim();

        switch (kind)
        {
            case "ApplyWindowLayout":
                if (!DeskSpaceWindowLayoutsV179.ValidName(target))
                {
                    reason = "Window layout name must be 1–40 safe characters.";
                    return false;
                }
                reason = "Apply saved window layout profile.";
                return true;

            case "StartFocusTimer":
                if (!TryParseFocusTarget(target, action.Label, out int mins, out _))
                {
                    reason = "Focus timer duration must be 1–240 minutes (e.g. '25' or '25|Deep Work').";
                    return false;
                }
                reason = $"Start {mins}-minute focus session.";
                return true;

            case "SwitchDeskTouchProfile":
                if (target.Length is < 1 or > 40 ||
                    !target.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_'))
                {
                    reason = "DeskTouch profile name must be 1–40 letters, digits, spaces, '-' or '_'.";
                    return false;
                }
                reason = $"Switch DeskTouch profile to '{target}'.";
                return true;

            case "SwitchThemeMode":
                if (!TryNormalizeThemeMode(target, out string canonical))
                {
                    reason = "Theme mode must be 'Light', 'Dark', or 'System'.";
                    return false;
                }
                reason = $"Switch DeskSpace theme mode to '{canonical}'.";
                return true;

            case "OpenAppOrUrl":
                return ValidateOpenAppOrUrl(target, out reason);

            case "CopySnippet":
                if (string.IsNullOrEmpty(action.Target) || action.Target.Length > 65536)
                {
                    reason = "Snippet text must be non-empty and at most 65,536 characters.";
                    return false;
                }
                reason = "Copy snippet text to clipboard.";
                return true;

            default:
                reason = $"Unsupported action kind '{kind}'.";
                return false;
        }
    }

    private static readonly HashSet<string> DangerousShellExes = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell.exe", "pwsh.exe", "cmd.exe", "wscript.exe", "cscript.exe",
        "mshta.exe", "reg.exe", "regsvr32.exe", "rundll32.exe"
    };

    private static bool ValidateOpenAppOrUrl(string target, out string reason)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Any(char.IsControl))
        {
            reason = "Target path or URL is empty or contains invalid characters.";
            return false;
        }

        string fileName = "";
        try { fileName = System.IO.Path.GetFileName(target); } catch { }
        if (!string.IsNullOrEmpty(fileName) && DangerousShellExes.Contains(fileName))
        {
            reason = $"Shell runner '{fileName}' cannot be launched automatically from a routine.";
            return false;
        }

        string rawExt = "";
        try { rawExt = System.IO.Path.GetExtension(target); } catch { }
        if (!string.IsNullOrEmpty(rawExt) && DangerousScriptExtensions.Contains(rawExt))
        {
            reason = $"Executable script/installer extension '{rawExt}' is blocked.";
            return false;
        }

        if (System.IO.Path.IsPathFullyQualified(target))
        {
            string mode = (rawExt.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                           rawExt.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                ? "Open application"
                : "Open file";

            if (!DeskSpaceDueShelfV74.IsAllowedAction(mode, target))
            {
                reason = "Target file or application does not exist or failed DeskSpace safety validation.";
                return false;
            }

            reason = $"Open verified {mode.ToLowerInvariant()}: {System.IO.Path.GetFileName(target)}.";
            return true;
        }

        if ((target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
             target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) &&
            DeskSpaceDueShelfV74.IsAllowedAction("Open URL", target))
        {
            reason = $"Open verified HTTP(S) URL: {target}.";
            return true;
        }

        reason = "OpenAppOrUrl requires an http(s) URL or an existing fully-qualified safe file/.exe path.";
        return false;
    }

    internal static string BuildDryRunSummary(RoutineRuleV380 rule)
    {
        if (rule == null)
            return "Invalid routine (null).";

        var sb = new StringBuilder();
        sb.AppendLine($"Routine: {rule.Name}");
        sb.AppendLine($"Status: {(rule.Enabled ? "Enabled" : "Disabled")}  |  Trigger: {rule.TriggerType} ({rule.TriggerValue})");
        sb.AppendLine(new string('-', 56));

        if (rule.Actions == null || rule.Actions.Count == 0)
        {
            sb.AppendLine("No actions defined (zero side effects).");
            return sb.ToString();
        }

        int invalidCount = 0;
        for (int i = 0; i < rule.Actions.Count; i++)
        {
            var act = rule.Actions[i];
            bool safe = IsActionSafe(act, out string reason);
            if (!safe)
                invalidCount++;
            string tag = safe ? "[OK]" : "[WARNING - BLOCKED]";
            string labelPart = string.IsNullOrWhiteSpace(act?.Label) ? "" : $" ({act.Label})";
            sb.AppendLine($"{i + 1}. {tag} {act?.Kind}{labelPart}: {reason}");
        }

        sb.AppendLine(new string('-', 56));
        sb.Append(invalidCount == 0
            ? $"Dry-run passed: {rule.Actions.Count} validated step(s). Nothing was executed."
            : $"Dry-run warning: {invalidCount} invalid step(s) will block execution until fixed.");
        return sb.ToString();
    }

    internal static async Task<string> ExecuteAsync(RoutineRuleV380 rule, bool forceEvenIfDisabled = false)
    {
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));

        if (!rule.Enabled && !forceEvenIfDisabled)
            return $"Skipped '{rule.Name}': routine is disabled.";

        var actions = rule.Actions ?? new List<RoutineActionV380>();
        if (actions.Count == 0)
        {
            string emptyMsg = $"Routine '{rule.Name}' has no actions.";
            AppendHistory(new RoutineHistoryEntryV380(Guid.NewGuid(), rule.Id, rule.Name, DateTime.UtcNow, true, emptyMsg));
            return emptyMsg;
        }

        // Validate every step before running any step.
        for (int i = 0; i < actions.Count; i++)
        {
            if (!IsActionSafe(actions[i], out string invalidReason))
            {
                string blockedMsg = $"Aborted '{rule.Name}' at step {i + 1}: {invalidReason}";
                AppendHistory(new RoutineHistoryEntryV380(Guid.NewGuid(), rule.Id, rule.Name, DateTime.UtcNow, false, blockedMsg));
                return blockedMsg;
            }
        }

        var stepLogs = new List<string>(actions.Count);
        bool allSucceeded = true;

        foreach (var act in actions)
        {
            try
            {
                string stepResult = await ExecuteSingleActionAsync(act).ConfigureAwait(false);
                stepLogs.Add(stepResult);
            }
            catch (Exception ex)
            {
                allSucceeded = false;
                stepLogs.Add($"{act.Kind} failed: {ex.Message}");
            }
        }

        string summary = string.Join("; ", stepLogs);
        AppendHistory(new RoutineHistoryEntryV380(Guid.NewGuid(), rule.Id, rule.Name, DateTime.UtcNow, allSucceeded, summary));
        return summary;
    }

    private static async Task<string> ExecuteSingleActionAsync(RoutineActionV380 act)
    {
        string kind = act.Kind.Trim();
        string target = (act.Target ?? "").Trim();

        switch (kind)
        {
            case "StartFocusTimer":
            {
                TryParseFocusTarget(target, act.Label, out int mins, out string label);
                await RunOnUiOrDirectAsync(() => DeskSpaceFocusRingV380.StartFocus(mins, label)).ConfigureAwait(false);
                return $"Started focus timer ({mins} min: {label})";
            }

            case "ApplyWindowLayout":
            {
                var profile = MonitorTopologyV380.LoadSavedLayoutProfiles()
                    .FirstOrDefault(p => string.Equals(p.Name, target, StringComparison.OrdinalIgnoreCase));
                if (profile == null)
                    return $"Layout '{target}' not found";
                var (restored, skipped) = DeskSpaceWindowLayoutsV179.Restore(profile);
                return $"Layout '{profile.Name}': restored {restored}, skipped {skipped}";
            }

            case "SwitchDeskTouchProfile":
            {
                DeskSpaceDeskTouchBridgeV378.SwitchProfileAsync(target);
                return $"Switched DeskTouch profile to '{target}'";
            }

            case "SwitchThemeMode":
            {
                TryNormalizeThemeMode(target, out string mode);
                var current = DeskSpaceAppearanceV148.Load();
                current.Mode = mode;
                DeskSpaceAppearanceV148.Save(current);
                await RunOnUiOrDirectAsync(() => DeskSpaceFluentBackdropV380.ReapplyLiveTheme()).ConfigureAwait(false);
                return $"Theme mode set to '{mode}'";
            }

            case "OpenAppOrUrl":
            {
                string launchTarget = target;
                if (!System.IO.Path.IsPathFullyQualified(target) &&
                    DeskSpaceReminderBrowserV80.TryUrl(target, out string normalizedUrl))
                {
                    launchTarget = normalizedUrl;
                }
                Process.Start(new ProcessStartInfo(launchTarget) { UseShellExecute = true });
                return $"Launched '{launchTarget}'";
            }

            case "CopySnippet":
            {
                string text = act.Target ?? "";
                await RunOnUiOrDirectAsync(() =>
                {
                    if (Application.Current != null || Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                        System.Windows.Clipboard.SetText(text);
                }).ConfigureAwait(false);
                return $"Copied snippet ({text.Length} chars)";
            }

            default:
                throw new InvalidOperationException($"Unsupported action kind '{kind}'.");
        }
    }

    private static Task RunOnUiOrDirectAsync(Action action)
    {
        var app = Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
            return app.Dispatcher.InvokeAsync(action).Task;
        action();
        return Task.CompletedTask;
    }

    private static bool TryParseFocusTarget(string target, string? fallbackLabel, out int minutes, out string label)
    {
        minutes = 25;
        label = string.IsNullOrWhiteSpace(fallbackLabel) ? "Focus Session" : fallbackLabel.Trim();
        if (string.IsNullOrWhiteSpace(target))
            return false;

        string[] parts = target.Split('|', 2);
        if (!int.TryParse(parts[0].Trim(), out minutes) || minutes < 1 || minutes > 240)
            return false;

        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            label = parts[1].Trim();
        return true;
    }

    private static bool TryNormalizeThemeMode(string target, out string canonical)
    {
        canonical = "";
        switch ((target ?? "").Trim().ToLowerInvariant())
        {
            case "light":
                canonical = "Light";
                return true;
            case "dark":
                canonical = "Dark";
                return true;
            case "system":
                canonical = "System";
                return true;
            default:
                return false;
        }
    }

    internal static AutomationStoreV380 LoadStore(string? storagePath = null)
    {
        string path = storagePath ?? DefaultRoutinesPath;
        lock (StoreLock)
        {
            try
            {
                if (!File.Exists(path))
                    return new AutomationStoreV380();
                var info = new FileInfo(path);
                if (info.Length > 2 * 1024 * 1024)
                    return new AutomationStoreV380();
                var data = JsonSerializer.Deserialize<AutomationStoreV380>(
                    File.ReadAllText(path, Encoding.UTF8),
                    JsonOptions);
                if (data == null)
                    return new AutomationStoreV380();
                data.Rules ??= new List<RoutineRuleV380>();
                data.Pins ??= new List<AppProfilePinV380>();
                return data;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new AutomationStoreV380();
            }
        }
    }

    internal static void SaveStore(AutomationStoreV380 store, string? storagePath = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        string path = storagePath ?? DefaultRoutinesPath;
        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        lock (StoreLock)
        {
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(store, JsonOptions), new UTF8Encoding(false));
                File.Move(tmp, path, true);
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

    internal static IReadOnlyList<RoutineHistoryEntryV380> LoadHistory(string? historyPath = null)
    {
        string path = historyPath ?? DefaultHistoryPath;
        lock (StoreLock)
        {
            try
            {
                if (!File.Exists(path))
                    return Array.Empty<RoutineHistoryEntryV380>();
                var list = JsonSerializer.Deserialize<List<RoutineHistoryEntryV380>>(
                    File.ReadAllText(path, Encoding.UTF8),
                    JsonOptions);
                return list?.Where(x => x != null).Take(MaxHistoryEntries).ToList()
                    ?? new List<RoutineHistoryEntryV380>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return Array.Empty<RoutineHistoryEntryV380>();
            }
        }
    }

    internal static void AppendHistory(RoutineHistoryEntryV380 entry, string? historyPath = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string path = historyPath ?? DefaultHistoryPath;
        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        lock (StoreLock)
        {
            var items = LoadHistory(path).ToList();
            items.Insert(0, entry);
            if (items.Count > MaxHistoryEntries)
                items = items.Take(MaxHistoryEntries).ToList();

            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(items, JsonOptions), new UTF8Encoding(false));
                File.Move(tmp, path, true);
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

    internal static void Show(Window? owner)
    {
        var p = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskSpace · Automation Pro & App Pins",
            960,
            660,
            owner,
            transient: false);
        win.MinWidth = 760;
        win.MinHeight = 520;

        var store = LoadStore();

        var tabs = new TabControl
        {
            Margin = new Thickness(14),
            Background = DeskSpaceDesignV283.Solid(p.Surface),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            ItemContainerStyle = DeskSpaceExcelProV380.BuildTabItemStyle(p)
        };

        var logList = new System.Windows.Controls.ListBox { Margin = new Thickness(0, 0, 0, 8) };
        DeskSpaceDesignV283.Selection(logList, p);

        void RefreshLogs()
        {
            logList.Items.Clear();
            foreach (var h in LoadHistory())
            {
                string icon = h.Success ? "OK" : "ERR";
                logList.Items.Add($"[{icon}] {h.TimestampUtc.ToLocalTime():g}  ·  {h.RuleName}  —  {h.Summary}");
            }
        }

        tabs.Items.Add(BuildRoutinesTab(p, store, RefreshLogs));
        tabs.Items.Add(BuildPinsTab(p, store, RefreshLogs));
        tabs.Items.Add(BuildHistoryTab(p, logList, RefreshLogs));

        RefreshLogs();
        win.Content = tabs;
        win.Show();
        win.Activate();
    }

    private static TabItem BuildRoutinesTab(
        DeskSpaceDesignV283.Palette p,
        AutomationStoreV380 store,
        Action refreshLogs)
    {
        var tab = new TabItem { Header = "Routines" };
        var root = new DockPanel { Margin = new Thickness(10) };

        var status = new TextBlock
        {
            Text = "Build multi-step safe routines. Use Dry-Run to verify all steps before executing.",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        var rulesList = new System.Windows.Controls.ListBox { Width = 260, Margin = new Thickness(0, 0, 12, 0) };
        DeskSpaceDesignV283.Selection(rulesList, p);
        DockPanel.SetDock(rulesList, Dock.Left);
        root.Children.Add(rulesList);

        var nameBox = new WpfTextBox { Margin = new Thickness(0, 0, 0, 6) };
        DeskSpaceDesignV283.Input(nameBox, p);
        var enabledCheck = new WpfCheckBox { Content = "Enabled", IsChecked = true, Foreground = DeskSpaceDesignV283.Solid(p.Text), Margin = new Thickness(0, 0, 0, 6) };
        var triggerTypeCombo = new WpfComboBox { ItemsSource = SupportedTriggerTypes, SelectedIndex = 0, Width = 170, Margin = new Thickness(0, 0, 8, 0) };
        var triggerValBox = new WpfTextBox { MinWidth = 200 };
        DeskSpaceDesignV283.Input(triggerValBox, p);

        var actionKindCombo = new WpfComboBox { ItemsSource = SupportedActionKinds, SelectedIndex = 0, Width = 190, Margin = new Thickness(0, 0, 8, 0) };
        var actionTargetBox = new WpfTextBox { MinWidth = 220, Margin = new Thickness(0, 0, 8, 0) };
        DeskSpaceDesignV283.Input(actionTargetBox, p);
        var actionLabelBox = new WpfTextBox { MinWidth = 140 };
        DeskSpaceDesignV283.Input(actionLabelBox, p);

        var stepsList = new System.Windows.Controls.ListBox { Height = 120, Margin = new Thickness(0, 0, 0, 8) };
        DeskSpaceDesignV283.Selection(stepsList, p);

        var dryRunBox = new WpfTextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Segoe UI")
        };
        DeskSpaceDesignV283.Input(dryRunBox, p);

        var draftActions = new List<RoutineActionV380>();
        Guid? editingId = null;

        void RefreshRulesList()
        {
            rulesList.Items.Clear();
            foreach (var r in store.Rules)
                rulesList.Items.Add($"{(r.Enabled ? "●" : "○")} {r.Name} ({r.Actions.Count})");
        }

        void RefreshDraftSteps()
        {
            stepsList.Items.Clear();
            foreach (var a in draftActions)
                stepsList.Items.Add($"{a.Kind}  →  {a.Target}" + (string.IsNullOrWhiteSpace(a.Label) ? "" : $" [{a.Label}]"));
        }

        RoutineRuleV380 BuildDraftRule() => new(
            editingId ?? Guid.NewGuid(),
            string.IsNullOrWhiteSpace(nameBox.Text) ? "Untitled Routine" : nameBox.Text.Trim(),
            enabledCheck.IsChecked == true,
            triggerTypeCombo.SelectedItem as string ?? "Manual",
            triggerValBox.Text.Trim(),
            draftActions.ToList());

        rulesList.SelectionChanged += (_, _) =>
        {
            int idx = rulesList.SelectedIndex;
            if (idx < 0 || idx >= store.Rules.Count)
                return;
            var r = store.Rules[idx];
            editingId = r.Id;
            nameBox.Text = r.Name;
            enabledCheck.IsChecked = r.Enabled;
            triggerTypeCombo.SelectedItem = r.TriggerType;
            triggerValBox.Text = r.TriggerValue;
            draftActions.Clear();
            if (r.Actions != null)
                draftActions.AddRange(r.Actions);
            RefreshDraftSteps();
            dryRunBox.Text = BuildDryRunSummary(r);
        };

        var editor = new StackPanel();
        editor.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Routine Editor", p));
        editor.Children.Add(new TextBlock { Text = "Routine Name", Foreground = DeskSpaceDesignV283.Solid(p.Muted), FontSize = 11.5 });
        editor.Children.Add(nameBox);
        editor.Children.Add(enabledCheck);

        var trigRow = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        trigRow.Children.Add(new TextBlock { Text = "Trigger:", Foreground = DeskSpaceDesignV283.Solid(p.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        trigRow.Children.Add(triggerTypeCombo);
        trigRow.Children.Add(new TextBlock { Text = "Value:", Foreground = DeskSpaceDesignV283.Solid(p.Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0) });
        trigRow.Children.Add(triggerValBox);
        editor.Children.Add(trigRow);

        editor.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Add Action Step", p));
        var actRow = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        actRow.Children.Add(actionKindCombo);
        actRow.Children.Add(actionTargetBox);
        actRow.Children.Add(actionLabelBox);

        var addStepBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Add Step", () =>
        {
            var act = new RoutineActionV380(
                actionKindCombo.SelectedItem as string ?? "CopySnippet",
                actionTargetBox.Text,
                actionLabelBox.Text.Trim());
            if (!IsActionSafe(act, out string reason))
            {
                status.Text = "Blocked unsafe step: " + reason;
                return;
            }
            draftActions.Add(act);
            RefreshDraftSteps();
            status.Text = "Added validated step: " + reason;
        }, false, "Validate and append action step");
        addStepBtn.Margin = new Thickness(6, 0, 6, 0);

        var remStepBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Remove Step", () =>
        {
            int idx = stepsList.SelectedIndex;
            if (idx >= 0 && idx < draftActions.Count)
            {
                draftActions.RemoveAt(idx);
                RefreshDraftSteps();
            }
        }, false, "Remove selected step");

        actRow.Children.Add(addStepBtn);
        actRow.Children.Add(remStepBtn);
        editor.Children.Add(actRow);
        editor.Children.Add(stepsList);

        var ruleBtns = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var newBtn = DeskSpaceFluentBackdropV380.CreatePillButton("New", () =>
        {
            editingId = null;
            nameBox.Text = "New Routine";
            enabledCheck.IsChecked = true;
            triggerValBox.Text = "";
            draftActions.Clear();
            RefreshDraftSteps();
            dryRunBox.Text = "";
        }, false, "Start a new routine");
        newBtn.Margin = new Thickness(0, 0, 6, 4);

        var saveBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Save Routine", () =>
        {
            var rule = BuildDraftRule();
            int existingIdx = store.Rules.FindIndex(x => x.Id == rule.Id);
            if (existingIdx >= 0)
                store.Rules[existingIdx] = rule;
            else
                store.Rules.Add(rule);
            editingId = rule.Id;
            SaveStore(store);
            RefreshRulesList();
            status.Text = $"Saved routine '{rule.Name}'.";
        }, false, "Persist routine to routines-v380.json");
        saveBtn.Margin = new Thickness(0, 0, 6, 4);

        var dryBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Preview Dry-Run", () =>
        {
            var rule = BuildDraftRule();
            dryRunBox.Text = BuildDryRunSummary(rule);
            status.Text = "Dry-run preview updated (zero side effects).";
        }, false, "Inspect all steps without executing anything");
        dryBtn.Margin = new Thickness(0, 0, 6, 4);

        var runNowBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Run Now", async () =>
        {
            var rule = BuildDraftRule();
            dryRunBox.Text = BuildDryRunSummary(rule);
            string result = await ExecuteAsync(rule, forceEvenIfDisabled: true);
            status.Text = "Execution: " + result;
            refreshLogs();
        }, true, "Validate and execute routine now");
        runNowBtn.Margin = new Thickness(0, 0, 6, 4);

        var delBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Delete", () =>
        {
            int idx = rulesList.SelectedIndex;
            if (idx < 0 || idx >= store.Rules.Count)
                return;
            store.Rules.RemoveAt(idx);
            SaveStore(store);
            editingId = null;
            RefreshRulesList();
            status.Text = "Deleted routine.";
        }, false, "Delete selected routine");
        delBtn.Margin = new Thickness(0, 0, 6, 4);

        ruleBtns.Children.Add(newBtn);
        ruleBtns.Children.Add(saveBtn);
        ruleBtns.Children.Add(dryBtn);
        ruleBtns.Children.Add(runNowBtn);
        ruleBtns.Children.Add(delBtn);

        editor.Children.Add(ruleBtns);
        editor.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Dry-Run Summary", p));
        editor.Children.Add(dryRunBox);

        RefreshRulesList();
        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = DeskSpaceFluentBackdropV380.CreateCard(editor, p)
        });

        tab.Content = root;
        return tab;
    }

    private static TabItem BuildPinsTab(
        DeskSpaceDesignV283.Palette p,
        AutomationStoreV380 store,
        Action refreshLogs)
    {
        var tab = new TabItem { Header = "Per-App Quick Pins" };
        var stack = new StackPanel { Margin = new Thickness(12) };

        string[] categories = { "Coding", "Accounting", "Meetings", "Design", "Excel", "Documents", "Browser", "Files", "Media", "Other" };
        var catCombo = new WpfComboBox { ItemsSource = categories, SelectedIndex = 0, Width = 140, Margin = new Thickness(0, 0, 8, 0) };
        var labelBox = new WpfTextBox { Width = 170, Margin = new Thickness(0, 0, 8, 0), ToolTip = "Button label" };
        DeskSpaceDesignV283.Input(labelBox, p);
        var kindCombo = new WpfComboBox { ItemsSource = SupportedActionKinds, SelectedIndex = 1, Width = 180, Margin = new Thickness(0, 0, 8, 0) };
        var targetBox = new WpfTextBox { MinWidth = 200, Text = "25", ToolTip = "Target value" };
        DeskSpaceDesignV283.Input(targetBox, p);

        var pinsList = new System.Windows.Controls.ListBox { Height = 240, Margin = new Thickness(0, 8, 0, 8) };
        DeskSpaceDesignV283.Selection(pinsList, p);
        var status = new TextBlock { Foreground = DeskSpaceDesignV283.Solid(p.Muted), Text = "Pin quick actions for Coding, Accounting, Meetings, Design, and other app categories." };

        void RefreshPins()
        {
            pinsList.Items.Clear();
            foreach (var pin in store.Pins)
                pinsList.Items.Add($"[{pin.AppCategory}] {pin.Label}  —  {pin.ActionKind}({pin.Target})");
        }

        var row = new WrapPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        row.Children.Add(catCombo);
        row.Children.Add(labelBox);
        row.Children.Add(kindCombo);
        row.Children.Add(targetBox);

        var btns = new WrapPanel { Orientation = WpfOrientation.Horizontal };
        var addPinBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Add Pin", () =>
        {
            string kind = kindCombo.SelectedItem as string ?? "CopySnippet";
            var action = new RoutineActionV380(kind, targetBox.Text, labelBox.Text);
            if (!IsActionSafe(action, out string reason))
            {
                status.Text = "Blocked unsafe pin: " + reason;
                return;
            }
            string cat = catCombo.SelectedItem as string ?? "Coding";
            string lbl = string.IsNullOrWhiteSpace(labelBox.Text) ? $"{cat} Action" : labelBox.Text.Trim();
            store.Pins.Add(new AppProfilePinV380(cat, lbl, kind, targetBox.Text.Trim()));
            SaveStore(store);
            RefreshPins();
            status.Text = $"Added pin '{lbl}' for {cat}.";
        }, true, "Validate and save quick pin");
        addPinBtn.Margin = new Thickness(0, 0, 8, 4);

        var runPinBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Run Selected Pin", async () =>
        {
            int idx = pinsList.SelectedIndex;
            if (idx < 0 || idx >= store.Pins.Count)
                return;
            var pin = store.Pins[idx];
            var rule = new RoutineRuleV380(
                Guid.NewGuid(),
                $"Pin: {pin.Label}",
                true,
                "Manual",
                pin.AppCategory,
                new List<RoutineActionV380> { new(pin.ActionKind, pin.Target, pin.Label) });
            status.Text = await ExecuteAsync(rule, forceEvenIfDisabled: true);
            refreshLogs();
        }, false, "Execute selected quick pin");
        runPinBtn.Margin = new Thickness(0, 0, 8, 4);

        var removePinBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Remove Selected", () =>
        {
            int idx = pinsList.SelectedIndex;
            if (idx < 0 || idx >= store.Pins.Count)
                return;
            store.Pins.RemoveAt(idx);
            SaveStore(store);
            RefreshPins();
            status.Text = "Removed quick pin.";
        }, false, "Delete selected quick pin");
        removePinBtn.Margin = new Thickness(0, 0, 8, 4);

        btns.Children.Add(addPinBtn);
        btns.Children.Add(runPinBtn);
        btns.Children.Add(removePinBtn);

        stack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Per-App Category Quick Pins", p));
        stack.Children.Add(row);
        stack.Children.Add(btns);
        stack.Children.Add(pinsList);
        stack.Children.Add(status);

        RefreshPins();
        tab.Content = DeskSpaceFluentBackdropV380.CreateCard(stack, p);
        return tab;
    }

    private static TabItem BuildHistoryTab(
        DeskSpaceDesignV283.Palette p,
        System.Windows.Controls.ListBox logList,
        Action refreshLogs)
    {
        var tab = new TabItem { Header = "Execution Log" };
        var dock = new DockPanel { Margin = new Thickness(12) };

        var btnRow = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var refreshBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Refresh", refreshLogs, false, "Reload recent execution entries");
        btnRow.Children.Add(refreshBtn);
        DockPanel.SetDock(btnRow, Dock.Top);
        dock.Children.Add(btnRow);
        dock.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(logList, p));

        tab.Content = dock;
        return tab;
    }
}

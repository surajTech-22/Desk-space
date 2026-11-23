using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

internal sealed class InfoRowV380
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public Guid? VaultId { get; set; }
}

internal sealed class InfoCollectionV380
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<InfoRowV380> Rows { get; set; } = new();
}

/// <summary>
/// First-class in-app Information Collections module inside MainWindow (and standalone fallback),
/// backed by the exact same information-collections-v26.json and AES-256-GCM DeskSpaceVaultV10
/// store used by the floating donut Information arc.
/// </summary>
internal static class DeskSpaceInformationTabV380
{
    private const string TabTag = "Information";

    private sealed class LegacyInfoRowDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public string Value { get; set; } = "";
        public string Notes { get; set; } = "";
        public DateTime UpdatedUtc { get; set; }
    }

    private sealed class EditorRowState
    {
        public Guid RowId { get; set; } = Guid.NewGuid();
        public string Type { get; set; } = "UI";
        public string CustomLabel { get; set; } = "";
        public string Value { get; set; } = "";
        public Guid? VaultId { get; set; }
        public string PendingPassword { get; set; } = "";
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _onDispose;
        public ActionDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => System.Threading.Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }

    internal static string DefaultCollectionsPath =>
        System.IO.Path.Combine(WorkspaceStore.Root, "information-collections-v26.json");

    internal static string DefaultLegacyPath =>
        System.IO.Path.Combine(WorkspaceStore.Root, "information-items-v22.json");

    private static DateTime CurrentStamp(string? collectionsPath = null, string? legacyPath = null)
    {
        string primary = collectionsPath ?? DefaultCollectionsPath;
        string legacy = legacyPath ?? DefaultLegacyPath;
        string target = File.Exists(primary) ? primary : legacy;
        try
        {
            return File.Exists(target) ? File.GetLastWriteTimeUtc(target) : DateTime.MinValue;
        }
        catch (IOException)
        {
            return DateTime.MinValue;
        }
        catch (UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }

    internal static List<InfoCollectionV380> LoadCollections(
        string? collectionsPath = null,
        string? legacyPath = null)
    {
        string primary = collectionsPath ?? DefaultCollectionsPath;
        string legacyFile = legacyPath ?? DefaultLegacyPath;

        if (File.Exists(primary))
        {
            var data = JsonSerializer.Deserialize<List<InfoCollectionV380>>(File.ReadAllText(primary))
                ?? throw new InvalidDataException("Invalid information collections.");
            foreach (var c in data)
                c.Rows ??= new List<InfoRowV380>();
            return data.OrderByDescending(c => c.UpdatedUtc).ToList();
        }

        if (!File.Exists(legacyFile))
            return new List<InfoCollectionV380>();

        var legacy = JsonSerializer.Deserialize<List<LegacyInfoRowDto>>(File.ReadAllText(legacyFile))
            ?? new List<LegacyInfoRowDto>();

        return legacy.Select(x => new InfoCollectionV380
        {
            Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
            Name = string.IsNullOrWhiteSpace(x.Title) ? "Imported information" : x.Title,
            UpdatedUtc = x.UpdatedUtc,
            Rows = new List<InfoRowV380>
            {
                new() { Label = "Value", Value = x.Value },
                new() { Label = "Category", Value = x.Category },
                new() { Label = "Notes", Value = x.Notes }
            }.Where(r => !string.IsNullOrWhiteSpace(r.Value)).ToList()
        }).OrderByDescending(c => c.UpdatedUtc).ToList();
    }

    internal static void SaveCollections(
        List<InfoCollectionV380> data,
        string? collectionsPath = null)
    {
        string target = collectionsPath ?? DefaultCollectionsPath;
        string? dir = System.IO.Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        string temp = target + ".pending";
        try
        {
            string json = JsonSerializer.Serialize(
                data,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.Exists(target))
                File.Replace(temp, target, target + ".previous", true);
            else
                File.Move(temp, target);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch
            {
            }
        }
    }

    internal static TabItem AttachToMainWindow(MainWindow window)
    {
        var existing = window.MainTabs.Items.OfType<TabItem>()
            .FirstOrDefault(t => string.Equals(t.Tag as string, TabTag, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var (surface, refreshFromDisk, lifetime) = BuildInformationSurface(() => window);

        var tab = new TabItem
        {
            Header = "ⓘ  Information",
            Tag = TabTag,
            Content = surface
        };

        // Place right after Home / Notes (before Calendar) for quick daily discovery.
        int calIdx = -1;
        for (int i = 0; i < window.MainTabs.Items.Count; i++)
        {
            if (window.MainTabs.Items[i] is TabItem ti &&
                string.Equals(ti.Tag as string, "Calendar", StringComparison.OrdinalIgnoreCase))
            {
                calIdx = i;
                break;
            }
        }

        if (calIdx >= 0)
            window.MainTabs.Items.Insert(calIdx, tab);
        else
            window.MainTabs.Items.Add(tab);

        window.MainTabs.SelectionChanged += (_, _) =>
        {
            if (ReferenceEquals(window.MainTabs.SelectedItem, tab))
                refreshFromDisk();
        };

        window.Closed += (_, _) => lifetime.Dispose();
        return tab;
    }

    internal static void SelectOrOpen(Window? owner)
    {
        var main = Application.Current?.Windows.OfType<MainWindow>()
            .FirstOrDefault(w => w.IsLoaded);
        if (main != null)
        {
            var tab = AttachToMainWindow(main);
            main.Show();
            if (main.WindowState == WindowState.Minimized)
                main.WindowState = WindowState.Normal;
            main.MainTabs.SelectedItem = tab;
            main.Activate();
            return;
        }

        Window? win = null;
        var (surface, _, lifetime) = BuildInformationSurface(() => win);
        win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskSpace · Information Collections",
            880,
            620,
            owner,
            transient: false);
        win.MinWidth = 680;
        win.MinHeight = 460;
        win.Content = surface;
        win.Closed += (_, _) => lifetime.Dispose();
        DeskSpaceFluentBackdropV380.ApplyFadeEntrance(surface);
        win.Show();
        win.Activate();
    }

    private static (FrameworkElement Root, Action RefreshFromDisk, IDisposable Lifetime) BuildInformationSurface(
        Func<Window?> getOwner)
    {
        var host = new Grid();
        var vault = new DeskSpaceVaultV10();
        var revealedIds = new HashSet<Guid>();
        List<InfoCollectionV380> all = new();
        DateTime loadedStamp = DateTime.MinValue;
        DateTime lastVaultAccess = DateTime.UtcNow;

        Guid? selectedId = null;
        string searchQuery = "";
        bool isEditing = false;
        Guid? editingId = null;
        string editorTitle = "";
        List<EditorRowState> editorRows = new();
        string bannerMessage = "";
        bool bannerIsError = false;
        bool vaultPromptVisible = false;
        bool disposed = false;

        void SafeLoadInitial()
        {
            try
            {
                loadedStamp = CurrentStamp();
                all = LoadCollections();
                if (selectedId == null && all.Count > 0)
                    selectedId = all[0].Id;
            }
            catch (Exception ex)
            {
                all = new List<InfoCollectionV380>();
                bannerMessage = "Saved information cannot be read: " + ex.Message;
                bannerIsError = true;
            }
        }

        SafeLoadInitial();

        Action? renderUi = null;

        var lockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        lockTimer.Tick += (_, _) =>
        {
            if (!disposed && vault.IsUnlocked &&
                DateTime.UtcNow - lastVaultAccess > TimeSpan.FromMinutes(3))
            {
                vault.Lock();
                revealedIds.Clear();
                bannerMessage = "Security Vault locked after 3 minutes of inactivity.";
                bannerIsError = false;
                renderUi?.Invoke();
            }
        };
        lockTimer.Start();

        void Render()
        {
            if (disposed)
                return;

            host.Children.Clear();
            var p = DeskSpaceDesignV283.Current;

            var root = new DockPanel { Margin = new Thickness(18) };
            host.Children.Add(root);

            // --- Header Card ---
            var headerDock = new DockPanel { LastChildFill = true };
            var headerActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            void StartNewEditor()
            {
                isEditing = true;
                editingId = null;
                editorTitle = "";
                editorRows = new List<EditorRowState>
                {
                    new() { Type = "UI" },
                    new() { Type = "PW" }
                };
                bannerMessage = "";
                bannerIsError = false;
                Render();
            }

            void ReloadNow(bool showToast = false)
            {
                if (isEditing)
                    return;
                try
                {
                    loadedStamp = CurrentStamp();
                    all = LoadCollections();
                    if (selectedId.HasValue && all.All(c => c.Id != selectedId.Value))
                        selectedId = all.Count > 0 ? all[0].Id : null;
                    else if (!selectedId.HasValue && all.Count > 0)
                        selectedId = all[0].Id;
                    if (showToast)
                    {
                        bannerMessage = $"Synced {all.Count} collections from DeskSpace storage.";
                        bannerIsError = false;
                    }
                }
                catch (Exception ex)
                {
                    bannerMessage = ex.Message;
                    bannerIsError = true;
                }
                Render();
            }

            var newBtn = DeskSpaceFluentBackdropV380.CreatePillButton("+ New Collection", StartNewEditor, primary: true,
                "Create a new Information collection (synced immediately with the Donut arc)");
            newBtn.Margin = new Thickness(0, 0, 8, 0);
            headerActions.Children.Add(newBtn);

            var vaultBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                vault.IsUnlocked ? "Vault Unlocked · Lock" : (vault.IsInitialized ? "Unlock Vault" : "Setup Vault"),
                () =>
                {
                    if (vault.IsUnlocked)
                    {
                        vault.Lock();
                        revealedIds.Clear();
                        vaultPromptVisible = false;
                        bannerMessage = "Security Vault locked.";
                        bannerIsError = false;
                        Render();
                    }
                    else
                    {
                        vaultPromptVisible = !vaultPromptVisible;
                        Render();
                    }
                },
                primary: false,
                tooltip: "Unlock or lock the encrypted AES-256-GCM Security Vault for PW rows");
            vaultBtn.Margin = new Thickness(0, 0, 8, 0);
            headerActions.Children.Add(vaultBtn);

            var refreshBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Refresh", () => ReloadNow(showToast: true),
                primary: false, tooltip: "Reload information collections edited in the floating donut");
            headerActions.Children.Add(refreshBtn);

            DockPanel.SetDock(headerActions, Dock.Right);
            headerDock.Children.Add(headerActions);

            var headerTitleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            headerTitleStack.Children.Add(new TextBlock
            {
                Text = "Information Collections",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = DeskSpaceDesignV283.Solid(p.Text)
            });
            headerTitleStack.Children.Add(new TextBlock
            {
                Text = $"{all.Count} {(all.Count == 1 ? "collection" : "collections")} · Shared live with the floating Donut Information arc & Security Vault",
                FontSize = 12,
                Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            headerDock.Children.Add(headerTitleStack);

            var headerCard = DeskSpaceFluentBackdropV380.CreateCard(headerDock, p);
            headerCard.Padding = new Thickness(16, 13, 16, 13);
            headerCard.Margin = new Thickness(0, 0, 0, 12);
            DockPanel.SetDock(headerCard, Dock.Top);
            root.Children.Add(headerCard);

            // --- Optional Inline Status / Error Banner ---
            if (!string.IsNullOrWhiteSpace(bannerMessage))
            {
                var statusColor = bannerIsError ? DeskSpaceDesignV283.Danger(p) : DeskSpaceDesignV283.AccentText(p);
                var statusBorder = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = DeskSpaceDesignV283.Solid(p.Raised),
                    BorderBrush = DeskSpaceDesignV283.Solid(bannerIsError ? DeskSpaceDesignV283.Danger(p) : p.Border),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = new TextBlock
                    {
                        Text = bannerMessage,
                        FontSize = 12.5,
                        Foreground = DeskSpaceDesignV283.Solid(statusColor),
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                DockPanel.SetDock(statusBorder, Dock.Top);
                root.Children.Add(statusBorder);
            }

            // --- Optional Vault Unlock Bar ---
            if (vaultPromptVisible && !vault.IsUnlocked)
            {
                var unlockPanel = new DockPanel { LastChildFill = true };
                var pwInput = new PasswordBox
                {
                    MinWidth = 220,
                    Height = 32,
                    Padding = new Thickness(8, 5, 8, 5),
                    Background = DeskSpaceDesignV283.Solid(DeskSpaceDesignV283.InputBg(p)),
                    Foreground = DeskSpaceDesignV283.Solid(p.Text),
                    BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                AutomationProperties.SetName(pwInput, "Security Vault master password");

                void DoUnlock()
                {
                    try
                    {
                        vault.Unlock(pwInput.Password);
                        pwInput.Clear();
                        lastVaultAccess = DateTime.UtcNow;
                        vaultPromptVisible = false;
                        bannerMessage = "Security Vault unlocked (auto-locks after 3 min inactivity).";
                        bannerIsError = false;
                        Render();
                    }
                    catch (Exception ex)
                    {
                        bannerMessage = "Unable to unlock Security Vault: " + ex.Message;
                        bannerIsError = true;
                        Render();
                    }
                }

                pwInput.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        DoUnlock();
                    }
                };

                var unlockBtns = new StackPanel { Orientation = Orientation.Horizontal };
                var unlockGo = DeskSpaceFluentBackdropV380.CreatePillButton("Unlock", DoUnlock, primary: true);
                unlockGo.Margin = new Thickness(0, 0, 6, 0);
                var cancelUnlock = DeskSpaceFluentBackdropV380.CreatePillButton("Hide", () =>
                {
                    vaultPromptVisible = false;
                    Render();
                });
                unlockBtns.Children.Add(unlockGo);
                unlockBtns.Children.Add(cancelUnlock);
                DockPanel.SetDock(unlockBtns, Dock.Right);

                var labelPrompt = new TextBlock
                {
                    Text = vault.IsInitialized
                        ? "Master Password to unlock PW rows:"
                        : "Create Vault Master Password (12+ chars):",
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Text),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                DockPanel.SetDock(labelPrompt, Dock.Left);

                unlockPanel.Children.Add(unlockBtns);
                unlockPanel.Children.Add(labelPrompt);
                unlockPanel.Children.Add(pwInput);

                var unlockCard = DeskSpaceFluentBackdropV380.CreateCard(unlockPanel, p);
                unlockCard.Padding = new Thickness(14, 10, 14, 10);
                unlockCard.Margin = new Thickness(0, 0, 0, 12);
                DockPanel.SetDock(unlockCard, Dock.Top);
                root.Children.Add(unlockCard);
            }

            // --- Master-Detail Split Grid ---
            var bodyGrid = new Grid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(285) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(bodyGrid);

            // LEFT PANE: Search + Collection List
            var leftDock = new DockPanel { LastChildFill = true };
            var searchBox = new TextBox
            {
                Text = searchQuery,
                Margin = new Thickness(0, 0, 0, 10),
                ToolTip = "Filter collections by name, label or value"
            };
            DeskSpaceDesignV283.Input(searchBox, p);
            AutomationProperties.SetName(searchBox, "Filter information collections");
            DockPanel.SetDock(searchBox, Dock.Top);
            leftDock.Children.Add(searchBox);

            var listBox = new ListBox
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(4)
            };
            DeskSpaceDesignV283.Selection(listBox, p);
            AutomationProperties.SetName(listBox, "Saved information collections");
            leftDock.Children.Add(listBox);

            void PopulateCollectionList()
            {
                listBox.Items.Clear();
                string q = searchQuery.Trim();
                var filtered = string.IsNullOrEmpty(q)
                    ? all
                    : all.Where(c =>
                        c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        c.Rows.Any(r =>
                            r.Label.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            (r.VaultId == null && r.Value.Contains(q, StringComparison.OrdinalIgnoreCase))))
                    .ToList();

                ListBoxItem? matchingLbi = null;
                foreach (var col in filtered)
                {
                    int pwCount = col.Rows.Count(r => r.VaultId != null);
                    var cardStack = new StackPanel { Margin = new Thickness(6, 5, 6, 5) };
                    cardStack.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(col.Name) ? "Untitled" : col.Name,
                        FontSize = 13.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = DeskSpaceDesignV283.Solid(p.Text),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });

                    string meta = $"{col.Rows.Count} {(col.Rows.Count == 1 ? "row" : "rows")}";
                    if (pwCount > 0)
                        meta += $" · {pwCount} PW";
                    if (col.UpdatedUtc > DateTime.MinValue)
                        meta += " · " + col.UpdatedUtc.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture);

                    cardStack.Children.Add(new TextBlock
                    {
                        Text = meta,
                        FontSize = 11,
                        Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                        Margin = new Thickness(0, 2, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });

                    var lbi = new ListBoxItem
                    {
                        Tag = col.Id,
                        Content = cardStack,
                        Padding = new Thickness(4)
                    };
                    listBox.Items.Add(lbi);
                    if (selectedId == col.Id)
                        matchingLbi = lbi;
                }

                if (matchingLbi != null)
                    listBox.SelectedItem = matchingLbi;
            }

            PopulateCollectionList();

            searchBox.TextChanged += (_, _) =>
            {
                searchQuery = searchBox.Text;
                PopulateCollectionList();
            };

            listBox.SelectionChanged += (_, _) =>
            {
                if (listBox.SelectedItem is ListBoxItem lbi && lbi.Tag is Guid id && selectedId != id)
                {
                    selectedId = id;
                    isEditing = false;
                    editingId = null;
                    revealedIds.Clear();
                    Render();
                }
            };

            var leftCard = DeskSpaceFluentBackdropV380.CreateCard(leftDock, p);
            leftCard.Padding = new Thickness(12);
            Grid.SetColumn(leftCard, 0);
            bodyGrid.Children.Add(leftCard);

            // RIGHT PANE: Detail View or Editor
            var rightScroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var rightContent = new StackPanel();
            rightScroller.Content = rightContent;

            if (isEditing)
            {
                rightContent.Children.Add(new TextBlock
                {
                    Text = editingId.HasValue ? "Edit Information Collection" : "New Information Collection",
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Text),
                    Margin = new Thickness(0, 0, 0, 10)
                });

                rightContent.Children.Add(new TextBlock
                {
                    Text = "Collection Name",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                    Margin = new Thickness(0, 0, 0, 4)
                });

                var titleBox = new TextBox
                {
                    Text = editorTitle,
                    Margin = new Thickness(0, 0, 0, 12),
                    ToolTip = "Name shown on the Donut slice and collection list"
                };
                DeskSpaceDesignV283.Input(titleBox, p);
                titleBox.TextChanged += (_, _) => editorTitle = titleBox.Text;
                rightContent.Children.Add(titleBox);

                rightContent.Children.Add(new TextBlock
                {
                    Text = "Information Rows (UI / PW / Custom)",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                    Margin = new Thickness(0, 0, 0, 6)
                });

                for (int rIdx = 0; rIdx < editorRows.Count; rIdx++)
                {
                    var rowState = editorRows[rIdx];
                    var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var typeCombo = new ComboBox
                    {
                        ItemsSource = new[] { "UI", "PW", "Other" },
                        SelectedItem = rowState.Type,
                        Height = 33,
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Top,
                        Foreground = DeskSpaceDesignV283.Solid(p.HighContrast ? p.Text : Color.FromRgb(15, 23, 42))
                    };
                    Grid.SetColumn(typeCombo, 0);
                    rowGrid.Children.Add(typeCombo);

                    var valueStack = new StackPanel();
                    var customBox = new TextBox
                    {
                        Text = rowState.CustomLabel,
                        Visibility = rowState.Type == "Other" ? Visibility.Visible : Visibility.Collapsed,
                        Margin = new Thickness(0, 0, 0, 6),
                        ToolTip = "Custom field label (e.g. Email, GSTIN, Client ID)"
                    };
                    DeskSpaceDesignV283.Input(customBox, p);
                    customBox.TextChanged += (_, _) => rowState.CustomLabel = customBox.Text;

                    var valBox = new TextBox
                    {
                        Text = rowState.Value,
                        Visibility = rowState.Type == "PW" ? Visibility.Collapsed : Visibility.Visible,
                        ToolTip = "Field value"
                    };
                    DeskSpaceDesignV283.Input(valBox, p);
                    valBox.TextChanged += (_, _) => rowState.Value = valBox.Text;

                    var pwBox = new PasswordBox
                    {
                        Height = 33,
                        Padding = new Thickness(9, 6, 9, 6),
                        Background = DeskSpaceDesignV283.Solid(DeskSpaceDesignV283.InputBg(p)),
                        Foreground = DeskSpaceDesignV283.Solid(p.Text),
                        BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
                        BorderThickness = new Thickness(1),
                        Visibility = rowState.Type == "PW" ? Visibility.Visible : Visibility.Collapsed,
                        ToolTip = rowState.VaultId != null
                            ? "Existing encrypted password preserved unless you enter a new one"
                            : "Stored encrypted inside Security Vault (AES-256-GCM)"
                    };
                    if (!string.IsNullOrEmpty(rowState.PendingPassword))
                        pwBox.Password = rowState.PendingPassword;
                    pwBox.PasswordChanged += (_, _) => rowState.PendingPassword = pwBox.Password;

                    typeCombo.SelectionChanged += (_, _) =>
                    {
                        string nextType = typeCombo.SelectedItem as string ?? "UI";
                        rowState.Type = nextType;
                        customBox.Visibility = nextType == "Other" ? Visibility.Visible : Visibility.Collapsed;
                        valBox.Visibility = nextType == "PW" ? Visibility.Collapsed : Visibility.Visible;
                        pwBox.Visibility = nextType == "PW" ? Visibility.Visible : Visibility.Collapsed;
                        if (nextType == "PW" && !vault.IsUnlocked)
                        {
                            vaultPromptVisible = true;
                            Render();
                        }
                    };

                    valueStack.Children.Add(customBox);
                    valueStack.Children.Add(valBox);
                    valueStack.Children.Add(pwBox);
                    Grid.SetColumn(valueStack, 1);
                    rowGrid.Children.Add(valueStack);

                    var removeBtn = DeskSpaceFluentBackdropV380.CreatePillButton("✕", () =>
                    {
                        editorRows.Remove(rowState);
                        if (editorRows.Count == 0)
                            editorRows.Add(new EditorRowState { Type = "UI" });
                        Render();
                    }, primary: false, tooltip: "Remove row");
                    removeBtn.Padding = new Thickness(9, 4, 9, 4);
                    removeBtn.MinHeight = 30;
                    removeBtn.Margin = new Thickness(8, 1, 0, 0);
                    removeBtn.VerticalAlignment = VerticalAlignment.Top;
                    Grid.SetColumn(removeBtn, 2);
                    rowGrid.Children.Add(removeBtn);

                    rightContent.Children.Add(rowGrid);
                }

                var editActions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
                var addRowBtn = DeskSpaceFluentBackdropV380.CreatePillButton("+ Add Row", () =>
                {
                    editorRows.Add(new EditorRowState { Type = "Other" });
                    Render();
                });
                addRowBtn.Margin = new Thickness(0, 0, 8, 6);

                var saveBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Save Collection", () =>
                {
                    string cleanTitle = editorTitle.Trim();
                    if (cleanTitle.Length == 0)
                    {
                        bannerMessage = "Enter a collection name before saving.";
                        bannerIsError = true;
                        Render();
                        return;
                    }

                    var finalRows = new List<InfoRowV380>();
                    var pendingSecrets = new List<(Guid SecretId, string Password)>();

                    foreach (var entry in editorRows)
                    {
                        string label = entry.Type == "Other" ? entry.CustomLabel.Trim() : entry.Type;
                        bool isPw = entry.Type == "PW";
                        bool hasNewPw = isPw && entry.PendingPassword.Length > 0;

                        if (label.Length == 0 && string.IsNullOrWhiteSpace(entry.Value) && !hasNewPw && entry.VaultId == null)
                            continue;
                        if (label.Length == 0)
                        {
                            bannerMessage = "Enter a label for the custom row.";
                            bannerIsError = true;
                            Render();
                            return;
                        }

                        if (isPw)
                        {
                            if (entry.VaultId == null && !hasNewPw)
                                continue;
                            if (hasNewPw && !vault.IsUnlocked)
                            {
                                vaultPromptVisible = true;
                                bannerMessage = "Unlock the Security Vault to save PW secrets.";
                                bannerIsError = true;
                                Render();
                                return;
                            }
                            Guid vid = entry.VaultId ?? Guid.NewGuid();
                            finalRows.Add(new InfoRowV380
                            {
                                Id = entry.RowId,
                                Label = "PW",
                                Value = "",
                                VaultId = vid
                            });
                            if (hasNewPw)
                                pendingSecrets.Add((vid, entry.PendingPassword));
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(entry.Value))
                                continue;
                            finalRows.Add(new InfoRowV380
                            {
                                Id = entry.RowId,
                                Label = label,
                                Value = entry.Value
                            });
                        }
                    }

                    if (finalRows.Count == 0)
                    {
                        bannerMessage = "Enter at least one non-empty row.";
                        bannerIsError = true;
                        Render();
                        return;
                    }

                    try
                    {
                        foreach (var (secretId, password) in pendingSecrets)
                        {
                            vault.Upsert(new DeskSpaceVaultV10.Secret
                            {
                                Id = secretId,
                                Label = "DeskSpace information / " + cleanTitle,
                                Password = password
                            });
                        }
                        if (pendingSecrets.Count > 0)
                            lastVaultAccess = DateTime.UtcNow;

                        var diskData = LoadCollections();
                        var updated = new InfoCollectionV380
                        {
                            Id = editingId ?? Guid.NewGuid(),
                            Name = cleanTitle,
                            UpdatedUtc = DateTime.UtcNow,
                            Rows = finalRows
                        };

                        int existingIndex = diskData.FindIndex(c => c.Id == updated.Id);
                        if (existingIndex >= 0)
                            diskData[existingIndex] = updated;
                        else
                            diskData.Add(updated);

                        SaveCollections(diskData);
                        loadedStamp = CurrentStamp();
                        all = LoadCollections();
                        selectedId = updated.Id;
                        isEditing = false;
                        editingId = null;
                        revealedIds.Clear();
                        bannerMessage = $"Saved '{cleanTitle}'. Available immediately on the floating Donut too.";
                        bannerIsError = false;
                        Render();
                    }
                    catch (Exception ex)
                    {
                        bannerMessage = ex.Message;
                        bannerIsError = true;
                        Render();
                    }
                }, primary: true);
                saveBtn.Margin = new Thickness(0, 0, 8, 6);

                var cancelBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Cancel", () =>
                {
                    isEditing = false;
                    editingId = null;
                    bannerMessage = "";
                    bannerIsError = false;
                    Render();
                });
                cancelBtn.Margin = new Thickness(0, 0, 8, 6);

                editActions.Children.Add(addRowBtn);
                editActions.Children.Add(saveBtn);
                editActions.Children.Add(cancelBtn);
                rightContent.Children.Add(editActions);
            }
            else
            {
                var current = selectedId.HasValue
                    ? all.FirstOrDefault(c => c.Id == selectedId.Value)
                    : all.FirstOrDefault();

                if (current == null)
                {
                    rightContent.Children.Add(new TextBlock
                    {
                        Text = all.Count == 0
                            ? "No saved Information collections yet"
                            : "Select a collection on the left",
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = DeskSpaceDesignV283.Solid(p.Text),
                        Margin = new Thickness(0, 8, 0, 6)
                    });
                    rightContent.Children.Add(new TextBlock
                    {
                        Text = "Click '+ New Collection' to save user IDs, GST/portal credentials, bank/IFSC details, or encrypted passwords. Collections created here or in the floating Donut automatically stay in sync.",
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                        Margin = new Thickness(0, 0, 0, 12)
                    });
                    rightContent.Children.Add(DeskSpaceFluentBackdropV380.CreatePillButton(
                        "+ Create First Collection", StartNewEditor, primary: true));
                }
                else
                {
                    var detailHeader = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 12) };
                    var detailBtns = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var copyAllBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy All", () =>
                    {
                        var lines = current.Rows
                            .Where(r => r.VaultId == null && !string.IsNullOrEmpty(r.Value))
                            .Select(r => $"{r.Label}: {r.Value}")
                            .ToList();
                        if (lines.Count == 0)
                        {
                            bannerMessage = "No plain text rows in this collection (copy PW individually after unlocking Vault).";
                            bannerIsError = false;
                            Render();
                            return;
                        }
                        Clipboard.SetText(string.Join(Environment.NewLine, lines));
                        bannerMessage = $"Copied {lines.Count} fields from '{current.Name}'.";
                        bannerIsError = false;
                        Render();
                    }, primary: false, tooltip: "Copy all plain rows (excludes encrypted PW rows)");
                    copyAllBtn.Margin = new Thickness(0, 0, 6, 0);

                    var editBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Edit", () =>
                    {
                        isEditing = true;
                        editingId = current.Id;
                        editorTitle = current.Name;
                        editorRows = current.Rows.Select(r => new EditorRowState
                        {
                            RowId = r.Id,
                            Type = (r.VaultId != null || r.Label == "PW") ? "PW" : (r.Label == "UI" ? "UI" : "Other"),
                            CustomLabel = (r.Label != "UI" && r.Label != "PW") ? r.Label : "",
                            Value = r.VaultId == null ? r.Value : "",
                            VaultId = r.VaultId
                        }).ToList();
                        if (editorRows.Count == 0)
                            editorRows.Add(new EditorRowState { Type = "UI" });
                        bannerMessage = "";
                        bannerIsError = false;
                        Render();
                    }, primary: false);
                    editBtn.Margin = new Thickness(0, 0, 6, 0);

                    var deleteBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Delete", () =>
                    {
                        var owner = getOwner();
                        var res = owner != null && owner.IsLoaded && owner.IsVisible
                            ? MessageBox.Show(owner,
                                $"Delete '{current.Name}' and its saved information rows? Any linked password remains separately in the encrypted vault.",
                                "Delete Information Collection", MessageBoxButton.YesNo, MessageBoxImage.Question)
                            : MessageBox.Show(
                                $"Delete '{current.Name}' and its saved information rows? Any linked password remains separately in the encrypted vault.",
                                "Delete Information Collection", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (res != MessageBoxResult.Yes)
                            return;

                        try
                        {
                            var diskData = LoadCollections();
                            diskData.RemoveAll(c => c.Id == current.Id);
                            SaveCollections(diskData);
                            loadedStamp = CurrentStamp();
                            all = LoadCollections();
                            selectedId = all.Count > 0 ? all[0].Id : null;
                            revealedIds.Clear();
                            bannerMessage = $"Deleted '{current.Name}'.";
                            bannerIsError = false;
                            Render();
                        }
                        catch (Exception ex)
                        {
                            bannerMessage = ex.Message;
                            bannerIsError = true;
                            Render();
                        }
                    }, primary: false);

                    detailBtns.Children.Add(copyAllBtn);
                    detailBtns.Children.Add(editBtn);
                    detailBtns.Children.Add(deleteBtn);
                    DockPanel.SetDock(detailBtns, Dock.Right);
                    detailHeader.Children.Add(detailBtns);

                    var detailTitleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    detailTitleStack.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(current.Name) ? "Untitled" : current.Name,
                        FontSize = 18.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = DeskSpaceDesignV283.Solid(p.Text),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                    if (current.UpdatedUtc > DateTime.MinValue)
                    {
                        detailTitleStack.Children.Add(new TextBlock
                        {
                            Text = "Updated " + current.UpdatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                            FontSize = 11.5,
                            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                    }
                    detailHeader.Children.Add(detailTitleStack);
                    rightContent.Children.Add(detailHeader);

                    foreach (var row in current.Rows)
                    {
                        bool isVaultRow = row.VaultId != null;
                        bool isRevealed = isVaultRow && vault.IsUnlocked && revealedIds.Contains(row.Id);
                        string? resolvedSecret = null;
                        bool missingSecret = false;

                        if (isVaultRow && vault.IsUnlocked)
                        {
                            var foundSecret = vault.Entries.FirstOrDefault(s => s.Id == row.VaultId);
                            if (foundSecret != null)
                                resolvedSecret = foundSecret.Password;
                            else
                                missingSecret = true;
                        }

                        var rowGrid = new Grid();
                        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var labelBadge = new Border
                        {
                            CornerRadius = new CornerRadius(7),
                            Background = DeskSpaceDesignV283.Solid(isVaultRow ? p.AccentSoft : p.Navigation),
                            Padding = new Thickness(8, 4, 8, 4),
                            Margin = new Thickness(0, 0, 10, 0),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock
                            {
                                Text = string.IsNullOrWhiteSpace(row.Label) ? "Field" : row.Label,
                                FontSize = 11.5,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = DeskSpaceDesignV283.Solid(
                                    isVaultRow ? DeskSpaceDesignV283.AccentText(p) : p.Text),
                                TextTrimming = TextTrimming.CharacterEllipsis
                            }
                        };
                        Grid.SetColumn(labelBadge, 0);
                        rowGrid.Children.Add(labelBadge);

                        string displayValue = isVaultRow
                            ? (missingSecret ? "(Linked vault entry missing)"
                                : (isRevealed ? (resolvedSecret ?? "") : "••••••••••••"))
                            : row.Value;

                        var valueText = new TextBlock
                        {
                            Text = displayValue,
                            FontSize = 13.5,
                            Foreground = DeskSpaceDesignV283.Solid(missingSecret ? DeskSpaceDesignV283.Danger(p) : p.Text),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 10, 0)
                        };
                        Grid.SetColumn(valueText, 1);
                        rowGrid.Children.Add(valueText);

                        var rowButtons = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        if (isVaultRow)
                        {
                            var revealBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                                isRevealed ? "Hide" : "Reveal",
                                () =>
                                {
                                    if (!vault.IsUnlocked)
                                    {
                                        vaultPromptVisible = true;
                                        bannerMessage = "Unlock the Security Vault above to reveal or copy PW secrets.";
                                        bannerIsError = false;
                                        Render();
                                        return;
                                    }
                                    lastVaultAccess = DateTime.UtcNow;
                                    if (!revealedIds.Add(row.Id))
                                        revealedIds.Remove(row.Id);
                                    Render();
                                },
                                primary: false);
                            revealBtn.Padding = new Thickness(10, 4, 10, 4);
                            revealBtn.MinHeight = 27;
                            revealBtn.FontSize = 11.5;
                            revealBtn.Margin = new Thickness(0, 0, 6, 0);

                            var copyPwBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                                "Copy PW",
                                () =>
                                {
                                    if (!vault.IsUnlocked)
                                    {
                                        vaultPromptVisible = true;
                                        bannerMessage = "Unlock the Security Vault above to copy PW secrets.";
                                        bannerIsError = false;
                                        Render();
                                        return;
                                    }
                                    lastVaultAccess = DateTime.UtcNow;
                                    var sec = vault.Entries.FirstOrDefault(s => s.Id == row.VaultId);
                                    if (sec == null)
                                    {
                                        bannerMessage = "The linked Security Vault entry was not found.";
                                        bannerIsError = true;
                                        Render();
                                        return;
                                    }
                                    DeskSpaceRichClipboardV380.NotifySensitiveCopy(sec.Password);
                                    Clipboard.SetText(sec.Password);
                                    bannerMessage = "Password copied cleanly (excluded from Clipboard History).";
                                    bannerIsError = false;
                                    Render();
                                },
                                primary: true);
                            copyPwBtn.Padding = new Thickness(10, 4, 10, 4);
                            copyPwBtn.MinHeight = 27;
                            copyPwBtn.FontSize = 11.5;

                            rowButtons.Children.Add(revealBtn);
                            rowButtons.Children.Add(copyPwBtn);
                        }
                        else
                        {
                            var copyBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                                "Copy",
                                () =>
                                {
                                    if (!string.IsNullOrEmpty(row.Value))
                                    {
                                        Clipboard.SetText(row.Value);
                                        bannerMessage = $"Copied {row.Label}.";
                                        bannerIsError = false;
                                        Render();
                                    }
                                },
                                primary: true);
                            copyBtn.Padding = new Thickness(11, 4, 11, 4);
                            copyBtn.MinHeight = 27;
                            copyBtn.FontSize = 11.5;
                            rowButtons.Children.Add(copyBtn);
                        }

                        Grid.SetColumn(rowButtons, 2);
                        rowGrid.Children.Add(rowButtons);

                        var rowCard = new Border
                        {
                            CornerRadius = new CornerRadius(10),
                            Background = DeskSpaceDesignV283.Solid(p.Surface),
                            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(12, 9, 12, 9),
                            Margin = new Thickness(0, 0, 0, 8),
                            Child = rowGrid
                        };
                        rightContent.Children.Add(rowCard);
                    }
                }
            }

            var rightCard = DeskSpaceFluentBackdropV380.CreateCard(rightScroller, p);
            rightCard.Padding = new Thickness(16);
            Grid.SetColumn(rightCard, 2);
            bodyGrid.Children.Add(rightCard);
        }

        renderUi = Render;
        Render();

        Action onThemeChanged = () =>
        {
            if (!disposed)
                host.Dispatcher.BeginInvoke(new Action(Render));
        };
        DeskSpaceFluentBackdropV380.ThemeChanged += onThemeChanged;

        Action refreshIfStale = () =>
        {
            if (disposed || isEditing)
                return;
            if (CurrentStamp() != loadedStamp)
            {
                try
                {
                    loadedStamp = CurrentStamp();
                    all = LoadCollections();
                    if (selectedId.HasValue && all.All(c => c.Id != selectedId.Value))
                        selectedId = all.Count > 0 ? all[0].Id : null;
                    else if (!selectedId.HasValue && all.Count > 0)
                        selectedId = all[0].Id;
                    Render();
                }
                catch
                {
                }
            }
        };

        var lifetime = new ActionDisposable(() =>
        {
            disposed = true;
            DeskSpaceFluentBackdropV380.ThemeChanged -= onThemeChanged;
            lockTimer.Stop();
            revealedIds.Clear();
            vault.Dispose();
        });

        return (host, refreshIfStale, lifetime);
    }
}

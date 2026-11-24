using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DeskSpace;

/// <summary>
/// Central wiring hub connecting all ModernV380 modules into MainWindow, WidgetShellWindow,
/// DeskSpaceApplicationsV55, DeskSpaceActionPaletteV197, DeskSpaceFeatureHubV199, and Tray menus
/// without mutating static XAML files or breaking existing R232-R377 tests.
/// </summary>
internal static class DeskSpaceModernHubV380
{
    private static readonly object InitLock = new();
    private static bool _commandsRegistered;
    private static DeskSpaceGlobalHotkeysV380? _activeHotkeys;
    private static Window? _hotkeyOwner;
    private static Func<Workspace>? _latestWorkspaceProvider;

    internal static void EnsureInitialized(Func<Workspace>? workspaceProvider = null)
    {
        if (workspaceProvider != null)
            _latestWorkspaceProvider = workspaceProvider;

        lock (InitLock)
        {
            if (_commandsRegistered)
                return;
            _commandsRegistered = true;
        }

        Func<Workspace> ws = () =>
        {
            try
            {
                return _latestWorkspaceProvider?.Invoke() ?? new WorkspaceStore().Load();
            }
            catch
            {
                return new Workspace();
            }
        };

        DeskSpaceCommandBarV380.RegisterExtraCommands(new[]
        {
            new PaletteCommandV380("v380.information", "Information Collections (Synced with Donut)",
                "View, search, copy & edit saved Information collections & Security Vault PW rows",
                "Workspace", "", owner => DeskSpaceInformationTabV380.SelectOrOpen(owner)),
            new PaletteCommandV380("v380.pinnedfiles", "Pinned App Files & Recent Browser",
                "Show all system files & Tally companies a pinned app can open (Recent first)",
                "Applications", "", owner => ShowPinnedAppsSelector(owner)),
            new PaletteCommandV380("v380.vaultpro", "Vault Pro · Passwords, 2FA & Encrypted Backup",
                "Windows Hello unlock, RFC 6238 TOTP 2FA, generator, AES-256-GCM backup",
                "Privacy", "", owner => DeskSpaceVaultProV380.Show(owner)),
            new PaletteCommandV380("v380.desktouchhub", "DeskTouch Ecosystem Hub & OTP Grabber",
                "Drop files to phone/PC, extract OTPs, notifications & camera OCR to DataTab",
                "Devices", "", owner => DeskSpaceDeskTouchEcosystemV380.ShowHub(owner, ws, null)),
            new PaletteCommandV380("v380.snippets", "Dynamic Snippet Expander",
                "Expand templates with {{date}}, {{clipboard}}, {{prompt:Field}} variables",
                "Workspace", "", owner => DeskSpaceNotesSnippetsV380.ShowSnippetLauncher(owner, ws)),
            new PaletteCommandV380("v380.excelpro", "Excel & Accounting Pro Studio",
                "Pre-paste diff, rollback history, GSTIN Luhn-36 & PAN validator, Tally/Winman prep",
                "Excel", "", owner => DeskSpaceExcelProV380.ShowStudio(owner, ws, null)),
            new PaletteCommandV380("v380.windowpower", "Window Power · Auto-Layouts & Snap Zones",
                "Monitor topology layout switcher, Snap Zones & Always-on-Top window pin",
                "Windows", "Ctrl+Alt+Z", owner => DeskSpaceWindowPowerV380.ShowStudio(owner)),
            new PaletteCommandV380("v380.snapzones", "Snap Active Window to Zone",
                "Visual Snap Zones overlay (Halves, 70/30, Thirds, Quarters, Coding, Ultrawide)",
                "Windows", "Ctrl+Alt+Z", _ => DeskSpaceSnapZonesV380.ShowSnapPickerForForeground()),
            new PaletteCommandV380("v380.topmost", "Toggle Always-on-Top Window Pin",
                "Pin or unpin the active foreground window above other windows",
                "Windows", "Ctrl+Alt+T", owner => DeskSpaceWindowPowerV380.ToggleForegroundTopmost(out _, out _, out _)),
            new PaletteCommandV380("v380.filetoolkit", "File Toolkit · QR, Image EXIF, SHA-256, PDF & ZIP",
                "Offline QR codes, strip image EXIF, verify hashes, export PDF pages, bundle ZIP",
                "Files", "", owner => DeskSpaceFileToolkitV380.Show(owner)),
            new PaletteCommandV380("v380.routines", "Automation Routines & Smart App Profiles",
                "Create multi-step routines with dry-run preview and per-app pinned actions",
                "Automation", "", owner => DeskSpaceAutomationProV380.Show(owner)),
            new PaletteCommandV380("v380.localai", "Local AI Copilot (Opt-In Ollama / BYOK)",
                "Private summarize, OCR-to-CSV, reply drafts, rename patterns & Excel formulas",
                "Tools", "", owner => DeskSpaceLocalAiV380.Show(owner)),
        });

        DeskSpaceBentoDashboardV380.RegisterToolShortcuts(new (string Label, string Tooltip, Action<Window?> Open)[]
        {
            ("Information", "View, copy & edit saved Information collections synced with the Donut & Security Vault",
                owner => DeskSpaceInformationTabV380.SelectOrOpen(owner)),
            ("Pinned App Files", "Browse all system files & Tally companies for pinned apps (Recent first)",
                owner => ShowPinnedAppsSelector(owner)),
            ("Excel & GST Pro", "GSTIN/PAN validator, accounting normalizer & paste rollback history",
                owner => DeskSpaceExcelProV380.ShowStudio(owner, ws, null)),
            ("File Toolkit & QR", "Offline QR generator, EXIF stripper, SHA-256, PDF export & ZIP",
                owner => DeskSpaceFileToolkitV380.Show(owner)),
            ("Window Power", "Snap Zones, Always-on-Top pin & monitor topology auto-layouts",
                owner => DeskSpaceWindowPowerV380.ShowStudio(owner)),
            ("Routines", "Automation routines & per-app quick actions with dry-run preview",
                owner => DeskSpaceAutomationProV380.Show(owner)),
            ("Local AI (Opt-In)", "Local Ollama or BYOK assistant (disabled by default)",
                owner => DeskSpaceLocalAiV380.Show(owner)),
        });
    }

    internal static void AttachMainWindow(
        MainWindow window,
        Func<Workspace> getWorkspace,
        Action saveWorkspace,
        Action refreshDashboard)
    {
        if (window == null) return;
        EnsureInitialized(getWorkspace);
        DeskSpaceFluentBackdropV380.ApplyRuntimeThemeToMainWindow(window);
        DeskSpaceBentoDashboardV380.AttachToHomeTab(window, getWorkspace, saveWorkspace, refreshDashboard);
        DeskSpaceInformationTabV380.AttachToMainWindow(window);
        DeskSpaceNotesSnippetsV380.EnhanceMainWindowNotesAndSnippets(window, getWorkspace, saveWorkspace);
        MonitorTopologyV380.InitializeWatcher(window.Dispatcher);
        DeskSpaceRichClipboardV380.EnsureStartedOnUi(window);
        TryAttachGlobalHotkeys(window, getWorkspace);
    }

    internal static void AddMainTraySubmenu(
        Forms.ContextMenuStrip menu,
        MainWindow window,
        Func<Workspace> getWorkspace)
    {
        if (menu == null) return;
        EnsureInitialized(getWorkspace);
        AddModernTrayItems(menu, window, getWorkspace);
    }

    internal static void AttachWidgetHost(WidgetShellWindow widget, Forms.ContextMenuStrip menu)
    {
        if (widget == null) return;
        Func<Workspace> ws = () =>
        {
            try { return new WorkspaceStore().Load(); }
            catch { return new Workspace(); }
        };
        EnsureInitialized(ws);

        if (!DeskSpaceMultiMonitorV151.IsSatellite(widget))
        {
            DeskSpaceRichClipboardV380.EnsureStartedOnUi(widget);
            MonitorTopologyV380.InitializeWatcher(widget.Dispatcher);
            TryAttachGlobalHotkeys(widget, ws);
        }

        if (menu != null)
            AddModernTrayItems(menu, widget, ws);
    }

    private static void TryAttachGlobalHotkeys(Window owner, Func<Workspace> ws)
    {
        lock (InitLock)
        {
            if (_hotkeyOwner != null && _hotkeyOwner.IsLoaded)
                return;

            _activeHotkeys?.Dispose();
            _hotkeyOwner = owner;
            _activeHotkeys = new DeskSpaceGlobalHotkeysV380(
                owner,
                openCommandBar: () => DeskSpaceCommandBarV380.Show(owner, ws),
                startSnip: () => DeskSpaceScreenToolsV380.StartRegionSnip(owner, ws, null),
                toggleTopmost: () => DeskSpaceWindowPowerV380.ToggleForegroundTopmost(out _, out _, out _),
                showSnapZones: () => DeskSpaceSnapZonesV380.ShowSnapPickerForForeground());

            owner.Closed += (_, _) =>
            {
                lock (InitLock)
                {
                    if (ReferenceEquals(_hotkeyOwner, owner))
                    {
                        _activeHotkeys?.Dispose();
                        _activeHotkeys = null;
                        _hotkeyOwner = null;
                    }
                }
            };
        }
    }

    private static void AddModernTrayItems(Forms.ContextMenuStrip menu, Window owner, Func<Workspace> ws)
    {
        var pinnedFilesMenu = new Forms.ToolStripMenuItem("Pinned Apps · Compatible Files");
        pinnedFilesMenu.DropDownOpening += (_, _) =>
        {
            pinnedFilesMenu.DropDownItems.Clear();
            var pins = LoadUniquePinnedApps();
            if (pins.Count == 0)
            {
                pinnedFilesMenu.DropDownItems.Add("Browse / Choose Application...", null,
                    (_, _) => owner.Dispatcher.Invoke(() => ShowPinnedAppsSelector(owner)));
                return;
            }
            foreach (string appPath in pins)
            {
                string captured = appPath;
                string label = DeskSpacePinnedAppFilesV380.ResolvePinnedAppDisplayName(captured);
                if (string.IsNullOrWhiteSpace(label))
                    label = System.IO.Path.GetFileNameWithoutExtension(captured);
                var appSub = new Forms.ToolStripMenuItem(label);
                appSub.DropDownItems.Add($"Open {label} Directly", null,
                    (_, _) => owner.Dispatcher.Invoke(() => DeskSpacePinnedAppFilesV380.TryLaunchPinnedAppDirect(captured, owner)));
                appSub.DropDownItems.Add("Browse Compatible Files…", null,
                    (_, _) => owner.Dispatcher.Invoke(() => DeskSpacePinnedAppFilesV380.Show(owner, captured)));
                appSub.DropDownItems.Add(new Forms.ToolStripSeparator());
                appSub.DropDownItems.Add($"✕ Unpin {label}", null,
                    (_, _) => owner.Dispatcher.Invoke(() => DeskSpacePinnedAppFilesV380.UnpinApplication(captured)));
                pinnedFilesMenu.DropDownItems.Add(appSub);
            }
            pinnedFilesMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
            pinnedFilesMenu.DropDownItems.Add("Manage / Pick Any App...", null,
                (_, _) => owner.Dispatcher.Invoke(() => ShowPinnedAppsSelector(owner)));
        };

        var modernMenu = new Forms.ToolStripMenuItem("Modern Suite (V380)");
        modernMenu.DropDownItems.Add("Command Bar (Alt+Space / Ctrl+Shift+P)", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceCommandBarV380.Show(owner, ws)));
        modernMenu.DropDownItems.Add("Information Collections (Synced)...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceInformationTabV380.SelectOrOpen(owner)));
        modernMenu.DropDownItems.Add("Pinned App Files Browser...", null,
            (_, _) => owner.Dispatcher.Invoke(() => ShowPinnedAppsSelector(owner)));
        modernMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        modernMenu.DropDownItems.Add("Rich Clipboard History...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceRichClipboardV380.Show(owner)));
        modernMenu.DropDownItems.Add("Screen Snip & Table OCR (Ctrl+Alt+E)", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceScreenToolsV380.StartRegionSnip(owner, ws, null)));
        modernMenu.DropDownItems.Add("Screen Eyedropper...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceScreenToolsV380.StartEyedropper(owner)));
        modernMenu.DropDownItems.Add("Focus Ring Pomodoro Studio...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceFocusRingV380.ShowStudio(owner)));
        modernMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        modernMenu.DropDownItems.Add("Vault Pro (Hello + TOTP + Backup)...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceVaultProV380.Show(owner)));
        modernMenu.DropDownItems.Add("DeskTouch Ecosystem Hub & OTP...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceDeskTouchEcosystemV380.ShowHub(owner, ws, null)));
        modernMenu.DropDownItems.Add("Dynamic Snippet Expander...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceNotesSnippetsV380.ShowSnippetLauncher(owner, ws)));
        modernMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        modernMenu.DropDownItems.Add("Excel & Accounting Pro (GSTIN / Rollback)...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceExcelProV380.ShowStudio(owner, ws, null)));
        modernMenu.DropDownItems.Add("File Toolkit (QR, EXIF, SHA-256, PDF, ZIP)...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceFileToolkitV380.Show(owner)));
        modernMenu.DropDownItems.Add("Window Power (Snap Zones & Auto-Layouts)...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceWindowPowerV380.ShowStudio(owner)));
        modernMenu.DropDownItems.Add("Snap Active Window to Zone (Ctrl+Alt+Z)", null,
            (_, _) => owner.Dispatcher.Invoke(DeskSpaceSnapZonesV380.ShowSnapPickerForForeground));
        modernMenu.DropDownItems.Add("Toggle Active Window Always-on-Top (Ctrl+Alt+T)", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceWindowPowerV380.ToggleForegroundTopmost(out _, out _, out _)));
        modernMenu.DropDownItems.Add("Automation Routines & App Profiles...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceAutomationProV380.Show(owner)));
        modernMenu.DropDownItems.Add("Local AI Copilot (Opt-In)...", null,
            (_, _) => owner.Dispatcher.Invoke(() => DeskSpaceLocalAiV380.Show(owner)));

        menu.Items.Add(pinnedFilesMenu);
        menu.Items.Add(modernMenu);
    }

    internal static void AddFeatureHubButtons(Action<string, string, Action> addButton)
    {
        if (addButton == null) return;
        EnsureInitialized();
        Window? owner = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault(w => w.IsVisible)
            ?? Application.Current?.MainWindow;
        Func<Workspace> ws = () => _latestWorkspaceProvider?.Invoke() ?? new WorkspaceStore().Load();

        addButton("Command Bar (Alt+Space / Ctrl+Shift+P)",
            "Spotlight-style launcher with instant math, GST calculator, unit/date conversion & fuzzy search.",
            () => DeskSpaceCommandBarV380.Show(owner, ws));
        addButton("Information Collections (Synced with Donut)",
            "View, search, copy & edit saved Information collections & Security Vault PW secrets right inside DeskSpace.",
            () => DeskSpaceInformationTabV380.SelectOrOpen(owner));
        addButton("Pinned Apps · Compatible Files Browser",
            "Browse all compatible files & Tally company folders across the PC for any pinned app (Recent first).",
            () => ShowPinnedAppsSelector(owner));
        addButton("Rich Clipboard History & Inline Transforms",
            "Event-driven WM_CLIPBOARDUPDATE history for text, PNG images, hex colors, files & one-click transforms.",
            () => DeskSpaceRichClipboardV380.Show(owner));
        addButton("Screen Snip, Table OCR & Eyedropper",
            "Capture region -> copy OCR text, extract tables to Excel, pin floating screenshot, or sample pixel color.",
            () => DeskSpaceScreenToolsV380.StartRegionSnip(owner, ws, null));
        addButton("Focus Ring Pomodoro Studio",
            "Deep-work focus countdown rendered right around the floating DeskSpace donut.",
            () => DeskSpaceFocusRingV380.ShowStudio(owner));
        addButton("Vault Pro · Hello, 2FA TOTP & Encrypted Backup",
            "Unlock passwords with Windows Hello, generate RFC 6238 TOTP codes, and back up workspace with AES-256-GCM.",
            () => DeskSpaceVaultProV380.Show(owner));
        addButton("DeskTouch Ecosystem Hub & Smart OTP",
            "Switch devices, drop files, grab SMS OTP codes, and import scanned phone tables directly into Data Tabs.",
            () => DeskSpaceDeskTouchEcosystemV380.ShowHub(owner, ws, null));
        addButton("Dynamic Snippet Expander",
            "Expand snippets with {{date}}, {{clipboard}}, {{uuid}} & interactive {{prompt:Name}} fields.",
            () => DeskSpaceNotesSnippetsV380.ShowSnippetLauncher(owner, ws));
        addButton("Excel & Accounting Pro Studio",
            "Normalize Indian numbers/dates, verify GSTIN Luhn-36 checksums & PANs, find duplicate invoices, and roll back pastes.",
            () => DeskSpaceExcelProV380.ShowStudio(owner, ws, null));
        addButton("File Toolkit (QR, EXIF, SHA-256, PDF & ZIP)",
            "Offline QR codes, strip image EXIF tags, verify SHA-256 hashes, export PDF pages to PNG & bundle ZIPs.",
            () => DeskSpaceFileToolkitV380.Show(owner));
        addButton("Window Power · Snap Zones & Topology Layouts",
            "Visual Snap Zones picker, Always-on-Top pin & automatic layout switching per monitor setup.",
            () => DeskSpaceWindowPowerV380.ShowStudio(owner));
        addButton("Automation Routines & Smart App Profiles",
            "Multi-step routines with dry-run verification and customizable per-app quick actions.",
            () => DeskSpaceAutomationProV380.Show(owner));
        addButton("Local AI Copilot (Opt-In Ollama / BYOK)",
            "Private local Ollama or custom endpoint assistant for summarizing, CSV table cleanup & formulas.",
            () => DeskSpaceLocalAiV380.Show(owner));
    }

    internal static void TryExecutePaletteCommand(Window owner, string id)
    {
        EnsureInitialized();
        Func<Workspace> ws = () => _latestWorkspaceProvider?.Invoke() ?? new WorkspaceStore().Load();
        switch (id)
        {
            case "v380_commandbar": DeskSpaceCommandBarV380.Show(owner, ws); break;
            case "v380_information": DeskSpaceInformationTabV380.SelectOrOpen(owner); break;
            case "v380_pinnedfiles": ShowPinnedAppsSelector(owner); break;
            case "v380_clipboard": DeskSpaceRichClipboardV380.Show(owner); break;
            case "v380_snip": DeskSpaceScreenToolsV380.StartRegionSnip(owner, ws, null); break;
            case "v380_eyedropper": DeskSpaceScreenToolsV380.StartEyedropper(owner); break;
            case "v380_focus": DeskSpaceFocusRingV380.ShowStudio(owner); break;
            case "v380_vaultpro": DeskSpaceVaultProV380.Show(owner); break;
            case "v380_desktouch": DeskSpaceDeskTouchEcosystemV380.ShowHub(owner, ws, null); break;
            case "v380_snippets": DeskSpaceNotesSnippetsV380.ShowSnippetLauncher(owner, ws); break;
            case "v380_excelpro": DeskSpaceExcelProV380.ShowStudio(owner, ws, null); break;
            case "v380_filetoolkit": DeskSpaceFileToolkitV380.Show(owner); break;
            case "v380_windowpower": DeskSpaceWindowPowerV380.ShowStudio(owner); break;
            case "v380_snapzones": DeskSpaceSnapZonesV380.ShowSnapPickerForForeground(); break;
            case "v380_topmost": DeskSpaceWindowPowerV380.ToggleForegroundTopmost(out _, out _, out _); break;
            case "v380_routines": DeskSpaceAutomationProV380.Show(owner); break;
            case "v380_localai": DeskSpaceLocalAiV380.Show(owner); break;
        }
    }

    internal static void ShowPinnedAppsSelector(Window? owner)
    {
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskSpace · Pinned Apps & Compatible Files", 660, 490, owner);
        var p = DeskSpaceDesignV283.Current;

        var root = new DockPanel { Margin = new Thickness(18) };
        win.Content = root;

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("PINNED APPS", p));
        header.Children.Add(new TextBlock
        {
            Text = "Open compatible files & companies for any pinned app (Recent items listed first)",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            TextWrapping = TextWrapping.Wrap
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        footer.Children.Add(DeskSpaceFluentBackdropV380.CreatePillButton("Browse Another App (.exe / .lnk)...", () =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose an application to inspect its compatible system files",
                Filter = "Applications & Shortcuts|*.exe;*.lnk|All Files|*.*"
            };
            if (dlg.ShowDialog(win) == true)
            {
                win.Close();
                DeskSpacePinnedAppFilesV380.Show(owner, dlg.FileName);
            }
        }, primary: true));
        footer.Children.Add(new Border { Width = 8 });
        footer.Children.Add(DeskSpaceFluentBackdropV380.CreatePillButton("Close", () => win.Close()));
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel();
        scroller.Content = stack;
        root.Children.Add(scroller);

        void RebuildPinnedCards()
        {
            stack.Children.Clear();
            var currentPins = LoadUniquePinnedApps();
            if (currentPins.Count == 0)
            {
                stack.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(new TextBlock
                {
                    Text = "No pinned applications yet. Pin apps like Tally, Excel, Word, or VS Code in the Donut's Applications -> + Pin ring, or click Browse Another App below.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                    FontSize = 13
                }, p));
                return;
            }

            foreach (string appPath in currentPins)
            {
                string captured = appPath;
                var cap = DeskSpacePinnedAppFilesV380.ResolveCapability(captured);
                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var iconSource = DeskSpaceApplicationIconsV65.Get(captured);
                if (iconSource != null)
                {
                    var img = new Image
                    {
                        Source = iconSource,
                        Width = 28,
                        Height = 28,
                        Margin = new Thickness(0, 0, 12, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(img, 0);
                    rowGrid.Children.Add(img);
                }

                string extPreview = cap.IncludesTallyCompanies
                    ? "Tally Company Data (.1800 / .900) & backups"
                    : string.Join(", ", cap.Extensions.Take(6)) + (cap.Extensions.Count > 6 ? $" (+{cap.Extensions.Count - 6})" : "");

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock
                {
                    Text = cap.DisplayName,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Text)
                });
                info.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(extPreview) ? captured : extPreview,
                    FontSize = 11,
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                Grid.SetColumn(info, 1);
                rowGrid.Children.Add(info);

                var btnRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var launchBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Open App ↗", () =>
                {
                    if (DeskSpacePinnedAppFilesV380.TryLaunchPinnedAppDirect(captured, win))
                        win.Close();
                }, primary: true);
                launchBtn.Margin = new Thickness(0, 0, 8, 0);
                var openBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Files…", () =>
                {
                    win.Close();
                    DeskSpacePinnedAppFilesV380.Show(owner, captured);
                }, primary: false);
                openBtn.Margin = new Thickness(0, 0, 8, 0);
                var unpinBtn = DeskSpaceFluentBackdropV380.CreatePillButton("✕ Unpin", () =>
                {
                    if (DeskSpacePinnedAppFilesV380.UnpinApplication(captured))
                        RebuildPinnedCards();
                }, primary: false, tooltip: $"Remove {cap.DisplayName} from Pinned Applications");
                btnRow.Children.Add(launchBtn);
                btnRow.Children.Add(openBtn);
                btnRow.Children.Add(unpinBtn);
                Grid.SetColumn(btnRow, 2);
                rowGrid.Children.Add(btnRow);

                var card = DeskSpaceFluentBackdropV380.CreateCard(rowGrid, p);
                card.Margin = new Thickness(0, 0, 0, 8);
                stack.Children.Add(card);
            }
        }

        RebuildPinnedCards();

        win.Show();
        win.Activate();
    }

    private static List<string> LoadUniquePinnedApps()
    {
        try
        {
            var settings = WidgetSettingsV3.Load();
            return settings.PinnedApps
                .Where(p => !string.IsNullOrWhiteSpace(p) && System.IO.File.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}

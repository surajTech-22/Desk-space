using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

internal static class DeskSpaceBentoDashboardV380
{
    private const string BentoContainerTag = "DeskSpaceBentoDashboardV380";

    private static readonly object ToolsLock = new();
    private static readonly List<(string Label, string Tooltip, Action<Window?> Open)> RegisteredTools = new();
    private static event Action? ToolsChanged;

    internal static void RegisterToolShortcuts(
        IEnumerable<(string Label, string Tooltip, Action<Window?> Open)> tools)
    {
        if (tools == null) return;
        lock (ToolsLock)
        {
            foreach (var tool in tools)
            {
                if (string.IsNullOrWhiteSpace(tool.Label) || tool.Open == null)
                    continue;
                int existing = RegisteredTools.FindIndex(
                    x => string.Equals(x.Label, tool.Label, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0)
                    RegisteredTools[existing] = tool;
                else
                    RegisteredTools.Add(tool);
            }
        }
        ToolsChanged?.Invoke();
    }

    internal static void AttachToHomeTab(
        MainWindow window,
        Func<Workspace> getWorkspace,
        Action saveWorkspace,
        Action refreshDashboard)
    {
        if (window == null) return;

        var homeTab = window.MainTabs.Items.OfType<TabItem>()
            .FirstOrDefault(t => string.Equals(t.Tag as string, "Home", StringComparison.OrdinalIgnoreCase));
        if (homeTab?.Content is not ScrollViewer scroller || scroller.Content is not StackPanel homeStack)
            return;

        if (homeStack.Children.OfType<FrameworkElement>().Any(el => Equals(el.Tag, BentoContainerTag)))
            return;

        if (window.FindName("StatusText") is TextBlock statusText &&
            (statusText.Text.Contains("not encrypted", StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrWhiteSpace(statusText.Text)))
        {
            statusText.Text = "Ready · Workspace & Modern Suite active (Alt+Space / Ctrl+K)";
        }

        var palette = DeskSpaceDesignV283.Current;
        var bentoRoot = new StackPanel
        {
            Tag = BentoContainerTag,
            Margin = new Thickness(0, 4, 0, 22)
        };

        // 1. Global Search & Command Hero Bar
        var heroButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 12, 16, 12),
            MinHeight = 46,
            Margin = new Thickness(0, 0, 0, 14),
            Background = DeskSpaceDesignV283.Solid(palette.Raised),
            BorderBrush = DeskSpaceDesignV283.Solid(palette.Accent),
            BorderThickness = new Thickness(1.2),
            Cursor = Cursors.Hand,
            ToolTip = "Open DeskSpace Command Bar (Alt+Space / Ctrl+Shift+P)",
            Content = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(4, 2, 4, 2),
                Children =
                {
                    new Border
                    {
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(8, 3, 8, 3),
                        Background = DeskSpaceDesignV283.Solid(palette.AccentSoft),
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = "Alt+Space",
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = DeskSpaceDesignV283.Solid(palette.Accent)
                        }
                    }.Also(b => DockPanel.SetDock(b, Dock.Right)),
                    new TextBlock
                    {
                        Text = "⌕   Search files, apps, notes, calculate 5000+18% gst, or run commands… (Alt+Space / Ctrl+Shift+P)",
                        FontSize = 13.5,
                        Foreground = DeskSpaceDesignV283.Solid(palette.Text),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            }
        };
        DeskSpaceDesignV283.Button(heroButton, primary: false, palette);
        heroButton.BorderBrush = DeskSpaceDesignV283.Solid(palette.Accent);
        AutomationProperties.SetName(heroButton, "Global Search and Command Bar");
        heroButton.Click += (_, _) => DeskSpaceCommandBarV380.Show(window, getWorkspace);
        bentoRoot.Children.Add(heroButton);

        // Row 1: Agenda & Quick Capture Reminder | Focus Ring Pomodoro
        var row1 = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bentoRoot.Children.Add(row1);

        // 2. Today's Agenda & Quick Capture Reminder Card
        var agendaInner = new StackPanel();
        agendaInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Today's Agenda & Quick Reminders", palette));

        var quickRemRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var quickRemBox = new TextBox
        {
            ToolTip = "Type a reminder due in 1 hour and press Enter"
        };
        DeskSpaceDesignV283.Input(quickRemBox, palette);
        AutomationProperties.SetName(quickRemBox, "Quick capture reminder");

        var agendaItemsHost = new StackPanel();

        void RefreshAgendaCard()
        {
            agendaItemsHost.Children.Clear();
            var p = DeskSpaceDesignV283.Current;
            var ws = getWorkspace();

            var upcomingReminders = ws.Reminders
                .Where(r => !r.Done)
                .OrderBy(r => r.Due)
                .Take(5)
                .ToList();

            var todayEvents = ws.Events
                .Where(ev => ev.Date.Date == DateTime.Today)
                .OrderBy(ev => ev.Date)
                .Take(4)
                .ToList();

            if (upcomingReminders.Count == 0 && todayEvents.Count == 0)
            {
                agendaItemsHost.Children.Add(new TextBlock
                {
                    Text = "All clear today — add a quick reminder above.",
                    FontSize = 12,
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                    Margin = new Thickness(0, 4, 0, 2)
                });
                return;
            }

            foreach (var r in upcomingReminders)
            {
                var captured = r;
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
                var doneBtn = DeskSpaceFluentBackdropV380.CreatePillButton("✓", () =>
                {
                    captured.Done = true;
                    window.RemindersList?.Items.Refresh();
                    saveWorkspace();
                    refreshDashboard();
                    RefreshAgendaCard();
                }, primary: false, "Mark reminder done");
                doneBtn.Padding = new Thickness(8, 2, 8, 2);
                doneBtn.MinHeight = 24;
                doneBtn.FontSize = 11;
                doneBtn.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(doneBtn, Dock.Right);
                row.Children.Add(doneBtn);

                bool overdue = r.Due < DateTime.Now;
                row.Children.Add(new TextBlock
                {
                    Text = $"◉  {r.Title}  ·  {r.Due:ddd h:mm tt}",
                    FontSize = 12.5,
                    Foreground = DeskSpaceDesignV283.Solid(overdue ? Color.FromRgb(252, 165, 165) : p.Text),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                agendaItemsHost.Children.Add(row);
            }

            foreach (var ev in todayEvents)
            {
                agendaItemsHost.Children.Add(new TextBlock
                {
                    Text = $"▦  Event today: {ev.Title}",
                    FontSize = 12,
                    Foreground = DeskSpaceDesignV283.Solid(p.Accent),
                    Margin = new Thickness(0, 2, 0, 2),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }
        }

        void AddQuickReminder()
        {
            string title = quickRemBox.Text.Trim();
            if (string.IsNullOrEmpty(title)) return;
            var ws = getWorkspace();
            ws.Reminders.Add(new ReminderItem
            {
                Title = title,
                Due = DateTime.Now.AddHours(1)
            });
            quickRemBox.Clear();
            window.RemindersList?.Items.Refresh();
            saveWorkspace();
            refreshDashboard();
            RefreshAgendaCard();
        }

        var addRemBtn = DeskSpaceFluentBackdropV380.CreatePillButton("+ 1h", AddQuickReminder, primary: true, "Add reminder due in 1 hour");
        addRemBtn.Margin = new Thickness(6, 0, 0, 0);
        DockPanel.SetDock(addRemBtn, Dock.Right);
        quickRemRow.Children.Add(addRemBtn);
        quickRemRow.Children.Add(quickRemBox);
        quickRemBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                AddQuickReminder();
            }
        };

        agendaInner.Children.Add(quickRemRow);
        agendaInner.Children.Add(agendaItemsHost);
        var agendaCard = DeskSpaceFluentBackdropV380.CreateCard(agendaInner, palette);
        Grid.SetColumn(agendaCard, 0);
        row1.Children.Add(agendaCard);

        // 3. Focus Ring Pomodoro Card
        var focusInner = new StackPanel();
        focusInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Focus Ring Pomodoro", palette));

        var focusHeaderRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var focusTimeText = new TextBlock
        {
            Text = DeskSpaceFocusRingV380.FormattedRemaining,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text),
            VerticalAlignment = VerticalAlignment.Center
        };
        var focusModeText = new TextBlock
        {
            Text = $"{DeskSpaceFocusRingV380.ModeName} · {DeskSpaceFocusRingV380.SessionLabel}",
            FontSize = 12,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(focusModeText, Dock.Right);
        focusHeaderRow.Children.Add(focusModeText);
        focusHeaderRow.Children.Add(focusTimeText);
        focusInner.Children.Add(focusHeaderRow);

        var focusProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = DeskSpaceFocusRingV380.ProgressFraction,
            Height = 6,
            Margin = new Thickness(0, 2, 0, 6),
            Foreground = DeskSpaceDesignV283.Solid(palette.Accent),
            Background = DeskSpaceDesignV283.Solid(palette.Surface)
        };
        focusInner.Children.Add(focusProgress);

        var focusStatsText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            Margin = new Thickness(0, 0, 0, 10)
        };
        focusInner.Children.Add(focusStatsText);

        var focusBtns = new WrapPanel();
        Button? focusStartBtn = null;
        Button? focusPauseBtn = null;
        Button? focusStudioBtn = null;

        focusStartBtn = DeskSpaceFluentBackdropV380.CreatePillButton("25m Focus", () =>
        {
            if (DeskSpaceFocusRingV380.IsActive)
                DeskSpaceFocusRingV380.Reset();
            else
                DeskSpaceFocusRingV380.StartFocus(25, "Focus Session");
        }, primary: true, "Start a 25-minute Pomodoro session or reset active ring");
        focusStartBtn.Margin = new Thickness(0, 0, 8, 4);

        focusPauseBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Pause", () =>
        {
            DeskSpaceFocusRingV380.TogglePauseResume();
        }, primary: false, "Pause or resume the current Focus Ring");
        focusPauseBtn.Margin = new Thickness(0, 0, 8, 4);

        focusStudioBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Studio", () =>
        {
            DeskSpaceFocusRingV380.ShowStudio(window);
        }, primary: false, "Open full Focus Ring Studio");
        focusStudioBtn.Margin = new Thickness(0, 0, 0, 4);

        focusBtns.Children.Add(focusStartBtn);
        focusBtns.Children.Add(focusPauseBtn);
        focusBtns.Children.Add(focusStudioBtn);
        focusInner.Children.Add(focusBtns);

        void RefreshFocusCard()
        {
            var p = DeskSpaceDesignV283.Current;
            focusTimeText.Text = DeskSpaceFocusRingV380.FormattedRemaining;
            focusTimeText.Foreground = DeskSpaceDesignV283.Solid(DeskSpaceFocusRingV380.IsActive ? p.Accent : p.Text);
            focusModeText.Text = $"{DeskSpaceFocusRingV380.ModeName} · {DeskSpaceFocusRingV380.SessionLabel}";
            focusProgress.Value = Math.Clamp(DeskSpaceFocusRingV380.ProgressFraction, 0, 1);
            focusStatsText.Text =
                $"Completed today: {DeskSpaceFocusRingV380.CompletedToday} · Streak: {DeskSpaceFocusRingV380.StreakDays}d · {DeskSpaceFocusRingV380.TotalFocusMinutesToday}m focused";

            if (focusStartBtn != null)
                focusStartBtn.Content = DeskSpaceFocusRingV380.IsActive ? "Reset" : "25m Focus";
            if (focusPauseBtn != null)
            {
                focusPauseBtn.IsEnabled = DeskSpaceFocusRingV380.IsActive;
                focusPauseBtn.Content = DeskSpaceFocusRingV380.IsRunning ? "Pause" : "Resume";
            }
        }

        var focusCard = DeskSpaceFluentBackdropV380.CreateCard(focusInner, palette);
        Grid.SetColumn(focusCard, 2);
        row1.Children.Add(focusCard);

        // Row 2: Connected DeskTouch Devices | Quick Scratchpad
        var row2 = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bentoRoot.Children.Add(row2);

        // 4. Connected DeskTouch Devices Card
        var deskTouchInner = new StackPanel();
        var dtHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var openHubBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Open Hub →", () =>
        {
            DeskSpaceDeskTouchEcosystemV380.ShowHub(window, getWorkspace, saveWorkspace);
        }, primary: false, "Open full DeskTouch Ecosystem Hub");
        openHubBtn.Padding = new Thickness(10, 4, 10, 4);
        openHubBtn.MinHeight = 26;
        openHubBtn.FontSize = 11.5;
        DockPanel.SetDock(openHubBtn, Dock.Right);
        dtHeader.Children.Add(openHubBtn);
        dtHeader.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Connected DeskTouch Devices", palette));
        deskTouchInner.Children.Add(dtHeader);

        var dtDevicesWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        var dtHintText = new TextBlock
        {
            Text = "Drop any file onto this card to send it to the active device.",
            FontSize = 11,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            TextWrapping = TextWrapping.Wrap
        };
        deskTouchInner.Children.Add(dtDevicesWrap);
        deskTouchInner.Children.Add(dtHintText);

        var deskTouchCard = DeskSpaceFluentBackdropV380.CreateCard(deskTouchInner, palette);
        deskTouchCard.AllowDrop = true;
        deskTouchCard.DragOver += (_, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                deskTouchCard.BorderBrush = DeskSpaceDesignV283.Solid(DeskSpaceDesignV283.Current.Accent);
                e.Handled = true;
            }
        };
        deskTouchCard.DragLeave += (_, _) =>
        {
            deskTouchCard.BorderBrush = DeskSpaceDesignV283.Solid(DeskSpaceDesignV283.Current.Border);
        };
        deskTouchCard.Drop += (_, e) =>
        {
            deskTouchCard.BorderBrush = DeskSpaceDesignV283.Solid(DeskSpaceDesignV283.Current.Border);
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Length > 0)
            {
                var snap = DeskSpaceDeskTouchBridgeV378.Current;
                int targetId = snap.ActiveTargetId != int.MinValue
                    ? snap.ActiveTargetId
                    : (snap.Devices.FirstOrDefault(d => !d.IsLocal)?.Id ?? 0);
                DeskSpaceDeskTouchBridgeV378.DropFilePathAsync(targetId, files[0]);
                dtHintText.Text = $"Sent '{System.IO.Path.GetFileName(files[0])}' to device #{targetId}.";
                e.Handled = true;
            }
        };

        void RefreshDeskTouchCard()
        {
            var snap = DeskSpaceDeskTouchBridgeV378.Current;
            var p = DeskSpaceDesignV283.Current;
            dtDevicesWrap.Children.Clear();

            if (snap.Devices.Count == 0)
            {
                dtDevicesWrap.Children.Add(new TextBlock
                {
                    Text = snap.IsLive
                        ? "DeskTouch ready · 0 remote peers linked"
                        : "DeskTouch Vision offline — click Open Hub → to launch or check OTP tools.",
                    FontSize = 12,
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 4)
                });
                return;
            }

            foreach (var d in snap.Devices)
            {
                var captured = d;
                bool active = d.IsControlling || (d.IsSelected && d.IsLocal);
                var parts = new List<string> { d.Name };
                if (d.BatteryPercent >= 0)
                    parts.Add((d.IsCharging ? "⚡" : "") + d.BatteryPercent + "%");
                if (d.NotificationCount > 0)
                    parts.Add($"[{d.NotificationCount}]");

                var btn = DeskSpaceFluentBackdropV380.CreatePillButton(
                    string.Join(" · ", parts),
                    () =>
                    {
                        if (captured.IsLocal) DeskSpaceDeskTouchBridgeV378.ReturnLocalAsync();
                        else if (captured.SupportsInput) DeskSpaceDeskTouchBridgeV378.SwitchDeviceAsync(captured.Id);
                    },
                    primary: active,
                    $"{d.Name} ({d.Platform}) · Click to switch control");
                btn.Padding = new Thickness(10, 5, 10, 5);
                btn.MinHeight = 28;
                btn.FontSize = 11.5;
                btn.Margin = new Thickness(0, 0, 6, 6);
                dtDevicesWrap.Children.Add(btn);
            }
        }

        Grid.SetColumn(deskTouchCard, 0);
        row2.Children.Add(deskTouchCard);

        // 5a. Quick Scratchpad Card
        var scratchInner = new StackPanel();
        scratchInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Quick Scratchpad → Notes", palette));

        var scratchTitleBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 6),
            ToolTip = "Quick note title"
        };
        DeskSpaceDesignV283.Input(scratchTitleBox, palette);
        AutomationProperties.SetName(scratchTitleBox, "Scratchpad note title");

        var scratchBodyBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 68,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = "Quick note body (Markdown & #tags supported)"
        };
        DeskSpaceDesignV283.Input(scratchBodyBox, palette);
        AutomationProperties.SetName(scratchBodyBox, "Scratchpad note content");

        var scratchFooter = new DockPanel();
        var scratchFeedback = new TextBlock
        {
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var saveScratchBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Save Note", () =>
        {
            string title = scratchTitleBox.Text.Trim();
            string content = scratchBodyBox.Text;
            if (string.IsNullOrEmpty(title) && string.IsNullOrWhiteSpace(content))
                return;
            var note = new NoteItem
            {
                Title = string.IsNullOrEmpty(title) ? "Quick note" : title,
                Content = content,
                Updated = DateTime.Now
            };
            getWorkspace().Notes.Add(note);
            window.NotesList?.Items.Refresh();
            saveWorkspace();
            refreshDashboard();
            scratchTitleBox.Clear();
            scratchBodyBox.Clear();
            scratchFeedback.Text = $"Saved '{note.Title}' to Notes.";
        }, primary: true, "Save scratchpad text as a DeskSpace Note");
        DockPanel.SetDock(saveScratchBtn, Dock.Right);
        saveScratchBtn.Margin = new Thickness(8, 0, 0, 0);
        scratchFooter.Children.Add(saveScratchBtn);
        scratchFooter.Children.Add(scratchFeedback);

        scratchInner.Children.Add(scratchTitleBox);
        scratchInner.Children.Add(scratchBodyBox);
        scratchInner.Children.Add(scratchFooter);

        var scratchCard = DeskSpaceFluentBackdropV380.CreateCard(scratchInner, palette);
        Grid.SetColumn(scratchCard, 2);
        row2.Children.Add(scratchCard);

        // 5b. Modern Tools Grid Card
        var toolsInner = new StackPanel();
        toolsInner.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Modern Productivity Suite", palette));
        var toolsWrap = new WrapPanel();
        toolsInner.Children.Add(toolsWrap);

        void RebuildToolsGrid()
        {
            toolsWrap.Children.Clear();

            var builtIn = new (string Label, string Tooltip, Action<Window?> Open)[]
            {
                ("Rich Clipboard", "Open searchable clipboard history & pinned snippets", w => DeskSpaceRichClipboardV380.Show(w)),
                ("Screen Snip", "Capture screen region for OCR or DataTab import", w => DeskSpaceScreenToolsV380.StartRegionSnip(w, getWorkspace, saveWorkspace)),
                ("Eyedropper", "Sample any screen pixel color to clipboard", w => DeskSpaceScreenToolsV380.StartEyedropper(w)),
                ("Vault Pro", "Open encrypted password vault, 2FA TOTP & backup studio", w => DeskSpaceVaultProV380.Show(w)),
                ("Focus Studio", "Open Focus Ring timer, sessions & streak stats", w => DeskSpaceFocusRingV380.ShowStudio(w)),
                ("DeskTouch Hub", "Connected devices, OTP grabber & phone scan OCR", w => DeskSpaceDeskTouchEcosystemV380.ShowHub(w, getWorkspace, saveWorkspace)),
                ("Snippet Picker", "Search and expand dynamic snippet templates", w => DeskSpaceNotesSnippetsV380.ShowSnippetLauncher(w, getWorkspace))
            };

            List<(string Label, string Tooltip, Action<Window?> Open)> dynamicTools;
            lock (ToolsLock)
                dynamicTools = RegisteredTools.ToList();

            foreach (var item in builtIn.Concat(dynamicTools))
            {
                var captured = item;
                var tile = DeskSpaceFluentBackdropV380.CreatePillButton(
                    captured.Label,
                    () => captured.Open(window),
                    primary: false,
                    captured.Tooltip);
                tile.Margin = new Thickness(0, 0, 8, 8);
                toolsWrap.Children.Add(tile);
            }
        }

        var toolsCard = DeskSpaceFluentBackdropV380.CreateCard(toolsInner, palette);
        toolsCard.Margin = new Thickness(0, 0, 0, 14);
        bentoRoot.Children.Add(toolsCard);

        // 6. System Glance & Backdrop Selector Card
        var sysInner = new DockPanel { LastChildFill = true };

        var backdropStack = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        DockPanel.SetDock(backdropStack, Dock.Right);
        backdropStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Window Backdrop", palette));
        var backdropWrap = new WrapPanel();
        var backdropButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

        void UpdateBackdropButtons()
        {
            string currentMode = DeskSpaceFluentBackdropV380.GetBackdropPreference();
            var p = DeskSpaceDesignV283.Current;
            foreach (var kv in backdropButtons)
            {
                bool active = string.Equals(kv.Key, currentMode, StringComparison.OrdinalIgnoreCase);
                kv.Value.Background = DeskSpaceDesignV283.Solid(active ? p.Accent : p.Raised);
                kv.Value.Foreground = DeskSpaceDesignV283.Solid(active && !p.HighContrast ? Color.FromRgb(8, 16, 33) : p.Text);
                kv.Value.BorderBrush = DeskSpaceDesignV283.Solid(active ? p.Accent : p.Border);
            }
        }

        foreach (string mode in new[] { "Mica", "Acrylic", "MicaAlt", "Solid" })
        {
            string capturedMode = mode;
            var btn = DeskSpaceFluentBackdropV380.CreatePillButton(capturedMode, () =>
            {
                DeskSpaceFluentBackdropV380.SetBackdropPreference(capturedMode);
                DeskSpaceFluentBackdropV380.ApplyRuntimeThemeToMainWindow(window);
                UpdateBackdropButtons();
            }, primary: false, $"Apply {capturedMode} window backdrop");
            btn.Padding = new Thickness(10, 5, 10, 5);
            btn.MinHeight = 28;
            btn.FontSize = 11.5;
            btn.Margin = new Thickness(0, 0, 6, 4);
            backdropButtons[capturedMode] = btn;
            backdropWrap.Children.Add(btn);
        }
        backdropStack.Children.Add(backdropWrap);
        sysInner.Children.Add(backdropStack);

        var sysLeft = new StackPanel();
        sysLeft.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("System Glance", palette));
        var memoryText = new TextBlock
        {
            FontSize = 12.5,
            Foreground = DeskSpaceDesignV283.Solid(palette.Text),
            Margin = new Thickness(0, 0, 0, 3)
        };
        var netSpeedText = new TextBlock
        {
            Text = "Network: ↓ —    ↑ —",
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 12.5,
            Foreground = DeskSpaceDesignV283.Solid(palette.Muted)
        };
        sysLeft.Children.Add(memoryText);
        sysLeft.Children.Add(netSpeedText);
        sysInner.Children.Add(sysLeft);

        bentoRoot.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(sysInner, palette));

        // Insert right after DashboardSummary so Welcome Hero stays top and Quick Actions remains intact below
        int summaryIdx = window.DashboardSummary != null ? homeStack.Children.IndexOf(window.DashboardSummary) : -1;
        if (summaryIdx >= 0)
            homeStack.Children.Insert(summaryIdx + 1, bentoRoot);
        else
            homeStack.Children.Insert(0, bentoRoot);

        // Visibility & Home-Tab-gated network speed + bridge polling timer
        var netSpeed = new DeskSpaceDeckNetworkSpeedV372();
        var glanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        bool isClosed = false;

        bool ShouldRunLiveGlance()
        {
            if (isClosed || !window.IsVisible) return false;
            return window.MainTabs.SelectedItem is TabItem selected &&
                string.Equals(selected.Tag as string, "Home", StringComparison.OrdinalIgnoreCase);
        }

        void UpdateMemoryReadout()
        {
            double workingMb = Environment.WorkingSet / (1024.0 * 1024.0);
            double managedMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            memoryText.Text = string.Create(
                CultureInfo.InvariantCulture,
                $"Working Set: {workingMb:0.0} MB · Managed Heap: {managedMb:0.0} MB");
        }

        void SyncGlanceTimer()
        {
            if (ShouldRunLiveGlance())
            {
                UpdateMemoryReadout();
                if (!glanceTimer.IsEnabled)
                {
                    netSpeed.Sample();
                    netSpeedText.Text = "Network: " + netSpeed.Display;
                    glanceTimer.Start();
                }
            }
            else
            {
                if (glanceTimer.IsEnabled)
                    glanceTimer.Stop();
                netSpeed.Reset();
                netSpeedText.Text = "Network: paused (inactive tab/hidden)";
            }
        }

        glanceTimer.Tick += (_, _) =>
        {
            if (!ShouldRunLiveGlance())
            {
                glanceTimer.Stop();
                netSpeed.Reset();
                return;
            }
            UpdateMemoryReadout();
            netSpeed.Sample();
            netSpeedText.Text = "Network: " + netSpeed.Display;
            DeskSpaceDeskTouchBridgeV378.PollIfDueAsync();
        };

        void OnFocusStateChanged()
        {
            if (isClosed) return;
            try { window.Dispatcher.BeginInvoke(RefreshFocusCard); }
            catch { }
        }

        void OnBridgeChanged()
        {
            if (isClosed) return;
            try { window.Dispatcher.BeginInvoke(RefreshDeskTouchCard); }
            catch { }
        }

        void OnToolsChanged()
        {
            if (isClosed) return;
            try { window.Dispatcher.BeginInvoke(RebuildToolsGrid); }
            catch { }
        }

        void OnVisibilityChanged(object? s, DependencyPropertyChangedEventArgs e) => SyncGlanceTimer();
        void OnTabSelectionChanged(object? s, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, window.MainTabs)) return;
            SyncGlanceTimer();
            if (ShouldRunLiveGlance())
                RefreshAgendaCard();
        }
        void OnWindowLoaded(object? s, RoutedEventArgs e) => SyncGlanceTimer();

        DeskSpaceFocusRingV380.StateChanged += OnFocusStateChanged;
        DeskSpaceDeskTouchBridgeV378.Changed += OnBridgeChanged;
        ToolsChanged += OnToolsChanged;
        window.IsVisibleChanged += OnVisibilityChanged;
        window.MainTabs.SelectionChanged += OnTabSelectionChanged;
        window.Loaded += OnWindowLoaded;

        window.Closed += (_, _) =>
        {
            isClosed = true;
            glanceTimer.Stop();
            netSpeed.Reset();
            DeskSpaceFocusRingV380.StateChanged -= OnFocusStateChanged;
            DeskSpaceDeskTouchBridgeV378.Changed -= OnBridgeChanged;
            ToolsChanged -= OnToolsChanged;
            window.IsVisibleChanged -= OnVisibilityChanged;
            window.MainTabs.SelectionChanged -= OnTabSelectionChanged;
            window.Loaded -= OnWindowLoaded;
        };

        RefreshAgendaCard();
        RefreshFocusCard();
        RefreshDeskTouchCard();
        RebuildToolsGrid();
        UpdateBackdropButtons();
        UpdateMemoryReadout();
        SyncGlanceTimer();
    }

    private static T Also<T>(this T element, Action<T> configure)
    {
        configure(element);
        return element;
    }
}

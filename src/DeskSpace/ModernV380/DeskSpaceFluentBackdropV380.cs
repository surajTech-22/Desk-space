using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeskSpace;

internal enum FluentBackdropMode
{
    Mica,
    MicaAlt,
    Acrylic,
    Solid
}

internal static class DeskSpaceFluentBackdropV380
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMSBT_NONE = 1;
    private const int DWMSBT_MAINWINDOW = 2;
    private const int DWMSBT_TRANSIENTWINDOW = 3;
    private const int DWMSBT_TABBEDWINDOW = 4;

    private sealed class BackdropPrefsDto
    {
        public string Mode { get; set; } = "Mica";
    }

    private sealed class WindowHookState
    {
        public bool HookAttached;
        public bool Transient;
    }

    private static readonly object PrefLock = new();
    private static readonly object WindowTrackLock = new();
    private static readonly ConditionalWeakTable<Window, WindowHookState> HookedWindows = new();
    private static readonly List<WeakReference<Window>> TrackedWindows = new();
    private static string? _cachedMode;

    internal static event Action? BackdropChanged;

    private static string PrefsPath => System.IO.Path.Combine(WorkspaceStore.Root, "fluent-v380.json");

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    internal static string GetBackdropPreference()
    {
        lock (PrefLock)
        {
            if (_cachedMode != null)
                return _cachedMode;

            try
            {
                if (File.Exists(PrefsPath))
                {
                    var dto = JsonSerializer.Deserialize<BackdropPrefsDto>(File.ReadAllText(PrefsPath));
                    _cachedMode = NormalizeModeName(dto?.Mode);
                    return _cachedMode;
                }
            }
            catch
            {
                // Fall back cleanly to Mica when preferences cannot be read.
            }

            _cachedMode = "Mica";
            return _cachedMode;
        }
    }

    internal static void SetBackdropPreference(string mode)
    {
        string normalized = NormalizeModeName(mode);
        lock (PrefLock)
        {
            Directory.CreateDirectory(WorkspaceStore.Root);
            string tmp = PrefsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                string json = JsonSerializer.Serialize(new BackdropPrefsDto { Mode = normalized });
                File.WriteAllText(tmp, json);
                File.Move(tmp, PrefsPath, true);
                _cachedMode = normalized;
            }
            finally
            {
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch
                {
                }
            }
        }

        BackdropChanged?.Invoke();
        ReapplyLiveTheme();
    }

    internal static FluentBackdropMode ParseMode(string? mode) => NormalizeModeName(mode) switch
    {
        "MicaAlt" => FluentBackdropMode.MicaAlt,
        "Acrylic" => FluentBackdropMode.Acrylic,
        "Solid" => FluentBackdropMode.Solid,
        _ => FluentBackdropMode.Mica
    };

    private static string NormalizeModeName(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return "Mica";

        return mode.Trim().ToLowerInvariant() switch
        {
            "micaalt" or "mica alt" or "tabbed" => "MicaAlt",
            "acrylic" => "Acrylic",
            "solid" or "none" => "Solid",
            _ => "Mica"
        };
    }

    internal static void ApplyToWindow(Window window, bool transient = false)
    {
        if (window == null)
            return;

        var palette = DeskSpaceDesignV283.Current;
        if (!window.AllowsTransparency)
        {
            // Keep clean readable contrast across Light, Dark, and High Contrast.
            DeskSpaceDesignV283.ApplyWindow(window, palette);
        }
        else
        {
            window.Foreground = DeskSpaceDesignV283.Solid(palette.Text);
            window.FontFamily = new FontFamily("Segoe UI Variable, Segoe UI");
            window.UseLayoutRounding = true;
            window.SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(window, TextRenderingMode.ClearType);
        }

        TrackWindow(window, transient);

        var state = HookedWindows.GetOrCreateValue(window);
        state.Transient = transient;

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            ApplyDwmAttributes(window, hwnd, state.Transient);
            return;
        }

        if (!state.HookAttached)
        {
            state.HookAttached = true;
            window.SourceInitialized += OnTrackedWindowSourceInitialized;
        }
    }

    private static void OnTrackedWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;
        window.SourceInitialized -= OnTrackedWindowSourceInitialized;

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        bool transient = HookedWindows.TryGetValue(window, out var state) && state.Transient;
        ApplyDwmAttributes(window, hwnd, transient);
    }

    private static void ApplyDwmAttributes(Window window, IntPtr hwnd, bool transient)
    {
        try
        {
            var palette = DeskSpaceDesignV283.Current;
            int build = Environment.OSVersion.Version.Build;

            int darkValue = palette.Dark ? 1 : 0;
            int darkHr = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkValue, sizeof(int));
            if (darkHr != 0 && build < 22000)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkValue, sizeof(int));

            if (build >= 22000)
            {
                int cornerPref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));
            }

            if (build >= 22621 && !window.AllowsTransparency)
            {
                int backdrop = ResolveDwmBackdropType(palette, transient);
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
        }
        catch
        {
            // Ignore unsupported OS builds or remote/headless DWM composition failures.
        }
    }

    private static int ResolveDwmBackdropType(DeskSpaceDesignV283.Palette palette, bool transient)
    {
        if (palette.HighContrast)
            return DWMSBT_NONE;

        return ParseMode(GetBackdropPreference()) switch
        {
            FluentBackdropMode.Solid => DWMSBT_NONE,
            FluentBackdropMode.Acrylic => DWMSBT_TRANSIENTWINDOW,
            FluentBackdropMode.MicaAlt => transient ? DWMSBT_TRANSIENTWINDOW : DWMSBT_TABBEDWINDOW,
            _ => transient ? DWMSBT_TRANSIENTWINDOW : DWMSBT_MAINWINDOW
        };
    }

    internal static void ApplyRuntimeThemeToMainWindow(MainWindow window)
    {
        if (window == null)
            return;

        ApplyToWindow(window, transient: false);
        var p = DeskSpaceDesignV283.Current;

        try
        {
            ApplyMainWindowResourceStyles(window, p);
            ApplyMainWindowChrome(window, p);
        }
        catch
        {
            // Runtime visual polish must never interrupt Workspace functionality.
        }
    }

    internal static void ReapplyLiveTheme()
    {
        var app = Application.Current;
        if (app == null)
            return;

        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(ReapplyLiveTheme));
            return;
        }

        foreach (Window win in app.Windows)
        {
            if (win is MainWindow main)
                ApplyRuntimeThemeToMainWindow(main);
        }

        List<(Window Window, bool Transient)> snapshot;
        lock (WindowTrackLock)
        {
            snapshot = new List<(Window, bool)>();
            for (int i = TrackedWindows.Count - 1; i >= 0; i--)
            {
                if (TrackedWindows[i].TryGetTarget(out var w))
                {
                    if (w is not MainWindow)
                    {
                        bool transient = HookedWindows.TryGetValue(w, out var st) && st.Transient;
                        snapshot.Add((w, transient));
                    }
                }
                else
                {
                    TrackedWindows.RemoveAt(i);
                }
            }
        }

        foreach (var (win, transient) in snapshot)
        {
            try
            {
                if (win.IsLoaded)
                    ApplyToWindow(win, transient);
            }
            catch
            {
            }
        }
    }

    private static void TrackWindow(Window window, bool transient)
    {
        lock (WindowTrackLock)
        {
            for (int i = TrackedWindows.Count - 1; i >= 0; i--)
            {
                if (!TrackedWindows[i].TryGetTarget(out var existing))
                {
                    TrackedWindows.RemoveAt(i);
                    continue;
                }
                if (ReferenceEquals(existing, window))
                    return;
            }

            TrackedWindows.Add(new WeakReference<Window>(window));
        }

        var state = HookedWindows.GetOrCreateValue(window);
        state.Transient = transient;
    }

    private static void ApplyMainWindowResourceStyles(MainWindow window, DeskSpaceDesignV283.Palette p)
    {
        // Button style matching MainWindow.xaml structure with live palette tokens
        var btnStyle = new Style(typeof(Button));
        btnStyle.Setters.Add(new Setter(Control.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Raised)));
        btnStyle.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Text)));
        btnStyle.Setters.Add(new Setter(Control.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Border)));
        btnStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(p.HighContrast ? 2 : 1)));
        btnStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(13, 9, 13, 9)));
        btnStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        btnStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 33.0));
        btnStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));

        var btnBorder = new FrameworkElementFactory(typeof(Border)) { Name = "chrome" };
        btnBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        btnBorder.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        btnBorder.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        btnBorder.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        btnBorder.SetBinding(Border.PaddingProperty, new Binding(nameof(Control.Padding))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });

        var btnCp = new FrameworkElementFactory(typeof(ContentPresenter));
        btnCp.SetBinding(ContentPresenter.HorizontalAlignmentProperty, new Binding(nameof(Control.HorizontalContentAlignment))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        btnCp.SetBinding(ContentPresenter.VerticalAlignmentProperty, new Binding(nameof(Control.VerticalContentAlignment))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        btnCp.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        btnBorder.AppendChild(btnCp);

        var btnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = btnBorder };
        var btnHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        btnHover.Setters.Add(new Setter(Border.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Hover), "chrome"));
        btnHover.Setters.Add(new Setter(Border.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Accent), "chrome"));
        btnTemplate.Triggers.Add(btnHover);

        var btnFocus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        btnFocus.Setters.Add(new Setter(Border.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Accent), "chrome"));
        btnFocus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "chrome"));
        btnTemplate.Triggers.Add(btnFocus);

        var btnDisabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        btnDisabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.48, "chrome"));
        btnTemplate.Triggers.Add(btnDisabled);

        btnStyle.Setters.Add(new Setter(Control.TemplateProperty, btnTemplate));
        window.Resources[typeof(Button)] = btnStyle;

        // TextBox style
        var tbStyle = new Style(typeof(TextBox));
        tbStyle.Setters.Add(new Setter(Control.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Raised)));
        tbStyle.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Text)));
        tbStyle.Setters.Add(new Setter(Control.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Border)));
        tbStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        tbStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 7, 9, 7)));
        tbStyle.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, DeskSpaceDesignV283.Solid(p.Accent)));
        tbStyle.Setters.Add(new Setter(TextBoxBase.SelectionBrushProperty, DeskSpaceDesignV283.Solid(p.AccentSoft)));
        window.Resources[typeof(TextBox)] = tbStyle;

        // ListBox style
        var lbStyle = new Style(typeof(ListBox));
        lbStyle.Setters.Add(new Setter(Control.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Surface)));
        lbStyle.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Text)));
        lbStyle.Setters.Add(new Setter(Control.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Border)));
        lbStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        window.Resources[typeof(ListBox)] = lbStyle;
    }

    private static void ApplyMainWindowChrome(MainWindow window, DeskSpaceDesignV283.Palette p)
    {
        if (window.Content is DockPanel rootDock)
        {
            foreach (UIElement child in rootDock.Children)
            {
                if (child is Border header && DockPanel.GetDock(header) == Dock.Top)
                {
                    if (p.HighContrast)
                    {
                        header.Background = DeskSpaceDesignV283.Solid(p.Navigation);
                    }
                    else
                    {
                        var c1 = DeskSpaceAppearanceV148.Mix(p.Accent, p.Navigation, p.Dark ? 0.80 : 0.88);
                        var c2 = DeskSpaceAppearanceV148.Mix(p.Accent, p.Raised, p.Dark ? 0.68 : 0.82);
                        var c3 = DeskSpaceAppearanceV148.Mix(p.Accent, p.Surface, p.Dark ? 0.56 : 0.76);
                        var grad = new LinearGradientBrush(
                            new GradientStopCollection
                            {
                                new GradientStop(c1, 0),
                                new GradientStop(c2, 0.66),
                                new GradientStop(c3, 1)
                            },
                            new Point(0, 0),
                            new Point(1, 0));
                        grad.Freeze();
                        header.Background = grad;
                    }
                    header.BorderBrush = DeskSpaceDesignV283.Solid(p.Border);

                    if (header.Child is DockPanel headerDock)
                    {
                        foreach (UIElement hChild in headerDock.Children)
                        {
                            if (hChild is StackPanel leftStack)
                            {
                                foreach (UIElement sItem in leftStack.Children)
                                {
                                    if (sItem is TextBlock titleTb)
                                        titleTb.Foreground = DeskSpaceDesignV283.Solid(p.Text);
                                }
                            }
                            else if (hChild is TextBlock rightTb)
                            {
                                rightTb.Foreground = DeskSpaceDesignV283.Solid(p.Muted);
                            }
                        }
                    }
                }
                else if (child is StatusBar statusBar)
                {
                    statusBar.Background = DeskSpaceDesignV283.Solid(p.Navigation);
                    statusBar.Foreground = DeskSpaceDesignV283.Solid(p.Muted);
                    statusBar.BorderBrush = DeskSpaceDesignV283.Solid(p.Border);
                }
            }
        }

        if (window.VersionButton != null)
        {
            window.VersionButton.Background = DeskSpaceDesignV283.Solid(p.AccentSoft);
            window.VersionButton.Foreground = DeskSpaceDesignV283.Solid(p.Text);
            window.VersionButton.BorderBrush = DeskSpaceDesignV283.Solid(p.Border);
        }

        if (window.StatusText != null)
            window.StatusText.Foreground = DeskSpaceDesignV283.Solid(p.Muted);

        if (window.MainTabs != null)
        {
            window.MainTabs.Background = DeskSpaceDesignV283.Solid(p.Surface);
            window.MainTabs.Foreground = DeskSpaceDesignV283.Solid(p.Text);
            window.MainTabs.BorderBrush = DeskSpaceDesignV283.Solid(p.Border);
            window.MainTabs.ItemContainerStyle = CreateFluentTabItemStyle(p);

            foreach (var tab in window.MainTabs.Items.OfType<TabItem>())
            {
                string? tag = tab.Tag as string;
                if (string.Equals(tag, "Excel", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tag, "DeskSpaceFeatures", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (tab.Content is DependencyObject contentRoot)
                    RetintMainTabTextBlocks(contentRoot, p);
            }
        }

        if (window.GridEditor != null)
        {
            window.GridEditor.Background = DeskSpaceDesignV283.Solid(p.Surface);
            window.GridEditor.Foreground = DeskSpaceDesignV283.Solid(p.Text);
            window.GridEditor.RowBackground = DeskSpaceDesignV283.Solid(p.Surface);
            window.GridEditor.AlternatingRowBackground = DeskSpaceDesignV283.Solid(p.Raised);
            window.GridEditor.BorderBrush = DeskSpaceDesignV283.Solid(p.Border);
            window.GridEditor.HorizontalGridLinesBrush = DeskSpaceDesignV283.Solid(p.Border);
            window.GridEditor.VerticalGridLinesBrush = DeskSpaceDesignV283.Solid(p.Border);

            var hdrStyle = new Style(typeof(DataGridColumnHeader));
            hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Navigation)));
            hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Text)));
            hdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Border)));
            hdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
            window.GridEditor.ColumnHeaderStyle = hdrStyle;

            var rowHdrStyle = new Style(typeof(DataGridRowHeader));
            rowHdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Navigation)));
            rowHdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Muted)));
            rowHdrStyle.Setters.Add(new Setter(Control.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Border)));
            rowHdrStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            window.GridEditor.RowHeaderStyle = rowHdrStyle;
        }

        if (window.GridWarning != null)
        {
            window.GridWarning.Foreground = p.HighContrast
                ? DeskSpaceDesignV283.Solid(p.Text)
                : DeskSpaceDesignV283.Solid(p.Dark ? Color.FromRgb(252, 165, 165) : Color.FromRgb(185, 28, 28));
        }
    }

    private static Style CreateFluentTabItemStyle(DeskSpaceDesignV283.Palette p)
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 182.0));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(17, 13, 17, 13)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(2, 3, 7, 3)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 14.0));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Muted)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Navigation)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));

        var border = new FrameworkElementFactory(typeof(Border)) { Name = "TabChrome" };
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.PaddingProperty, new Binding(nameof(Control.Padding))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });

        var row = new FrameworkElementFactory(typeof(DockPanel));
        var pill = new FrameworkElementFactory(typeof(Border)) { Name = "ActivePill" };
        pill.SetValue(FrameworkElement.WidthProperty, 3.5);
        pill.SetValue(FrameworkElement.HeightProperty, 18.0);
        pill.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        pill.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 9, 0));
        pill.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        pill.SetValue(DockPanel.DockProperty, Dock.Left);
        row.AppendChild(pill);

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        cp.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        row.AppendChild(cp);

        border.AppendChild(row);

        var template = new ControlTemplate(typeof(TabItem)) { VisualTree = border };

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Text)));
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Hover), "TabChrome"));
        template.Triggers.Add(hoverTrigger);

        var selectedTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, DeskSpaceDesignV283.Solid(p.Text)));
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, DeskSpaceDesignV283.Solid(p.AccentSoft), "TabChrome"));
        selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Accent), "TabChrome"));
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Accent), "ActivePill"));
        template.Triggers.Add(selectedTrigger);

        var focusTrigger = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focusTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Accent), "TabChrome"));
        focusTrigger.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "TabChrome"));
        template.Triggers.Add(focusTrigger);

        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static void RetintMainTabTextBlocks(DependencyObject node, DeskSpaceDesignV283.Palette p)
    {
        if (node is TextBlock tb)
        {
            var src = DependencyPropertyHelper.GetValueSource(tb, TextBlock.ForegroundProperty);
            if (src.BaseValueSource == BaseValueSource.Local)
            {
                if (tb.Foreground is SolidColorBrush scb)
                {
                    Color c = scb.Color;
                    // Preserve warning/amber intent while ensuring legible contrast in both themes
                    if (c == Color.FromRgb(0xA4, 0x3E, 0x1C) || c == Color.FromRgb(0xFC, 0xA5, 0xA5) || c == Color.FromRgb(0xB9, 0x1C, 0x1C))
                    {
                        tb.Foreground = DeskSpaceDesignV283.Solid(p.HighContrast ? p.Text : (p.Dark ? Color.FromRgb(252, 165, 165) : Color.FromRgb(185, 28, 28)));
                    }
                    else if (c == Color.FromRgb(0xE3, 0xBD, 0x81) || c == Color.FromRgb(0x92, 0x40, 0x0E))
                    {
                        tb.Foreground = DeskSpaceDesignV283.Solid(p.HighContrast ? p.Text : (p.Dark ? Color.FromRgb(227, 189, 129) : Color.FromRgb(146, 64, 14)));
                    }
                    else
                    {
                        tb.Foreground = DeskSpaceDesignV283.Solid(p.Muted);
                    }
                }
            }
        }

        foreach (object child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is DependencyObject dep)
                RetintMainTabTextBlocks(dep, p);
        }
    }

    internal static Window CreateModernWindow(string title, double width, double height, Window? owner = null, bool transient = false)
    {
        bool hasValidOwner = owner != null && owner.IsLoaded && owner.IsVisible;
        var win = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            ShowInTaskbar = !transient,
            WindowStartupLocation = hasValidOwner ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };
        if (hasValidOwner)
            win.Owner = owner;

        ApplyToWindow(win, transient);
        return win;
    }

    internal static Border CreateCard(UIElement child, DeskSpaceDesignV283.Palette? palette = null)
    {
        return DeskSpaceDesignV283.Card(child, palette);
    }

    internal static Button CreatePillButton(string text, Action onClick, bool primary = false, string? tooltip = null)
    {
        var p = DeskSpaceDesignV283.Current;
        var btn = new Button
        {
            Content = text,
            Padding = new Thickness(15, 7, 15, 7),
            MinHeight = 32,
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = DeskSpaceDesignV283.Solid(primary ? p.Accent : p.Raised),
            Foreground = DeskSpaceDesignV283.Solid(primary && !p.HighContrast ? Color.FromRgb(8, 16, 33) : p.Text),
            BorderBrush = DeskSpaceDesignV283.Solid(primary ? p.Accent : p.Border),
            BorderThickness = new Thickness(p.HighContrast ? 2 : 1),
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Medium,
            ToolTip = string.IsNullOrWhiteSpace(tooltip) ? null : tooltip
        };

        var chrome = new FrameworkElementFactory(typeof(Border)) { Name = "PillChrome" };
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(16));
        chrome.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        chrome.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        chrome.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        chrome.SetBinding(Border.PaddingProperty, new Binding(nameof(Control.Padding))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        chrome.AppendChild(cp);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = chrome };

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        if (!primary)
            hover.Setters.Add(new Setter(Border.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Hover), "PillChrome"));
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Accent), "PillChrome"));
        template.Triggers.Add(hover);

        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Accent), "PillChrome"));
        focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "PillChrome"));
        template.Triggers.Add(focus);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45, "PillChrome"));
        template.Triggers.Add(disabled);

        btn.Template = template;
        AutomationProperties.SetName(btn, text);
        btn.Click += (_, _) => onClick?.Invoke();
        return btn;
    }

    internal static TextBlock CreateSectionHeader(string text, DeskSpaceDesignV283.Palette? palette = null)
    {
        var p = palette ?? DeskSpaceDesignV283.Current;
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Accent),
            Margin = new Thickness(0, 8, 0, 6)
        };
    }

    internal static void ApplyFadeEntrance(FrameworkElement element)
    {
        if (element == null)
            return;

        if (!DeskSpaceReducedMotionV209.Enabled())
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            return;
        }

        element.Opacity = 0;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(UIElement.OpacityProperty, anim);
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace DeskSpace;

internal sealed record TopologyBindingV380(
    string Signature,
    string LayoutProfileName,
    bool LaunchMissingApps,
    List<string> AllowedExePaths);

internal sealed class WindowPowerConfigV380
{
    public bool AutoSwitchEnabled { get; set; }
    public List<TopologyBindingV380> Bindings { get; set; } = new();
}

internal static class MonitorTopologyV380
{
    private static readonly object ConfigLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static int _watcherInitialized;
    private static Dispatcher? _watcherDispatcher;
    private static DispatcherTimer? _debounceTimer;
    private static EventHandler? _displayChangedHandler;

    internal static string DefaultStoragePath =>
        System.IO.Path.Combine(WorkspaceStore.Root, "window-power-v380.json");

    internal static string ComputeSignature(
        IEnumerable<(string Device, int Width, int Height, bool Primary)> screens)
    {
        if (screens == null)
            return "";

        var parts = screens
            .Where(s => s.Width > 0 && s.Height > 0)
            .Select(s => (s.Primary ? "P" : "S") + ":" + s.Width + "x" + s.Height)
            .OrderBy(entry => entry.StartsWith("P:", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(entry => entry, StringComparer.Ordinal)
            .ToArray();

        return string.Join("|", parts);
    }

    internal static string CurrentSignature()
    {
        try
        {
            var screens = Forms.Screen.AllScreens;
            return ComputeSignature(screens.Select(s =>
                (s.DeviceName ?? "", s.Bounds.Width, s.Bounds.Height, s.Primary)));
        }
        catch
        {
            return "";
        }
    }

    internal static WindowPowerConfigV380 LoadConfig(string? storagePath = null)
    {
        string path = storagePath ?? DefaultStoragePath;
        lock (ConfigLock)
        {
            try
            {
                if (!File.Exists(path))
                    return new WindowPowerConfigV380();

                var info = new FileInfo(path);
                if (info.Length > 1024 * 1024)
                    return new WindowPowerConfigV380();

                var cfg = JsonSerializer.Deserialize<WindowPowerConfigV380>(
                    File.ReadAllText(path, Encoding.UTF8),
                    JsonOptions);
                if (cfg == null)
                    return new WindowPowerConfigV380();

                cfg.Bindings ??= new List<TopologyBindingV380>();
                return cfg;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new WindowPowerConfigV380();
            }
        }
    }

    internal static void SaveConfig(WindowPowerConfigV380 config, string? storagePath = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        string path = storagePath ?? DefaultStoragePath;
        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        lock (ConfigLock)
        {
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions), new UTF8Encoding(false));
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

    internal static IReadOnlyList<DeskSpaceWindowLayoutsV179.Profile> LoadSavedLayoutProfiles()
    {
        try
        {
            string layoutFile = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeskSpace",
                "WindowLayouts",
                "profiles-v1.json");

            if (!File.Exists(layoutFile))
                return Array.Empty<DeskSpaceWindowLayoutsV179.Profile>();

            var info = new FileInfo(layoutFile);
            if (info.Length > 1024 * 1024)
                return Array.Empty<DeskSpaceWindowLayoutsV179.Profile>();

            var list = JsonSerializer.Deserialize<List<DeskSpaceWindowLayoutsV179.Profile>>(
                File.ReadAllText(layoutFile, Encoding.UTF8));
            if (list == null)
                return Array.Empty<DeskSpaceWindowLayoutsV179.Profile>();

            return list.Where(DeskSpaceWindowLayoutsV179.ValidProfile).Take(12).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Array.Empty<DeskSpaceWindowLayoutsV179.Profile>();
        }
    }

    internal static void InitializeWatcher(Dispatcher ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        if (Interlocked.CompareExchange(ref _watcherInitialized, 1, 0) != 0)
            return;

        _watcherDispatcher = ui;
        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background, ui)
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer?.Stop();
            ApplyBindingForCurrentTopology();
        };

        _displayChangedHandler = (_, _) =>
        {
            var disp = _watcherDispatcher;
            if (disp == null || disp.HasShutdownStarted || disp.HasShutdownFinished)
                return;

            disp.BeginInvoke(new Action(() =>
            {
                if (_debounceTimer == null)
                    return;
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }));
        };

        SystemEvents.DisplaySettingsChanged += _displayChangedHandler;
        ui.ShutdownStarted += (_, _) => ShutdownWatcher();
        if (Application.Current != null)
            Application.Current.Exit += (_, _) => ShutdownWatcher();
    }

    internal static void ShutdownWatcher()
    {
        if (Interlocked.Exchange(ref _watcherInitialized, 0) == 0)
            return;

        if (_displayChangedHandler != null)
        {
            try
            {
                SystemEvents.DisplaySettingsChanged -= _displayChangedHandler;
            }
            catch { }
            _displayChangedHandler = null;
        }

        _debounceTimer?.Stop();
        _debounceTimer = null;
        _watcherDispatcher = null;
    }

    internal static string ApplyBindingForCurrentTopology(bool forceIgnoreAutoSwitch = false)
    {
        var cfg = LoadConfig();
        if (!forceIgnoreAutoSwitch && !cfg.AutoSwitchEnabled)
            return "Auto-Switch is disabled.";

        string sig = CurrentSignature();
        if (string.IsNullOrEmpty(sig))
            return "No monitor topology detected.";

        var binding = cfg.Bindings.FirstOrDefault(b =>
            string.Equals(b.Signature, sig, StringComparison.OrdinalIgnoreCase));
        if (binding == null)
            return $"No binding configured for topology '{sig}'.";

        var sb = new StringBuilder();
        var profile = LoadSavedLayoutProfiles().FirstOrDefault(p =>
            string.Equals(p.Name, binding.LayoutProfileName, StringComparison.OrdinalIgnoreCase));
        if (profile != null)
        {
            var (restored, skipped) = DeskSpaceWindowLayoutsV179.Restore(profile);
            sb.Append($"Restored {restored} window(s) (skipped {skipped}) for '{profile.Name}'.");
        }
        else
        {
            sb.Append($"Layout profile '{binding.LayoutProfileName}' was not found.");
        }

        if (binding.LaunchMissingApps && binding.AllowedExePaths != null)
        {
            int launched = 0;
            foreach (string rawExe in binding.AllowedExePaths)
            {
                if (TryLaunchExeIfNotRunning(rawExe))
                    launched++;
            }
            if (launched > 0)
                sb.Append($" Launched {launched} missing app(s).");
        }

        return sb.ToString();
    }

    private static bool TryLaunchExeIfNotRunning(string? rawExe)
    {
        if (string.IsNullOrWhiteSpace(rawExe))
            return false;

        string path = rawExe.Trim();
        if (!System.IO.Path.IsPathFullyQualified(path))
            return false;
        if (!System.IO.Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!File.Exists(path))
            return false;

        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return false;

            string procName = System.IO.Path.GetFileNameWithoutExtension(path);
            var running = Process.GetProcessesByName(procName);
            try
            {
                if (running.Length > 0)
                    return false;
            }
            finally
            {
                foreach (var p in running)
                    p.Dispose();
            }

            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(path) ?? ""
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal static class DeskSpaceSnapZonesV380
{
    internal static readonly string[] TemplateIds =
    {
        "Halves", "Focus70_30", "Thirds", "Quarters", "Coding", "Ultrawide"
    };

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int SW_RESTORE = 9;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    internal static IReadOnlyList<Drawing.Rectangle> ComputeZoneRects(string templateId, Drawing.Rectangle workArea)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
            return Array.Empty<Drawing.Rectangle>();

        int gap = (workArea.Width >= 200 && workArea.Height >= 150) ? 8 : 0;

        static (int Start, int Size)[] SplitLinear(int origin, int total, double[] fractions, int innerGap)
        {
            int count = fractions.Length;
            int effectiveGap = total >= count * (innerGap + 2) ? innerGap : 0;
            int avail = Math.Max(count, total - (count - 1) * effectiveGap);
            var res = new (int Start, int Size)[count];
            int used = 0;
            int cursor = origin;
            for (int i = 0; i < count; i++)
            {
                int remainingSlots = count - 1 - i;
                int size = (i == count - 1)
                    ? Math.Max(1, avail - used)
                    : Math.Clamp((int)Math.Floor(avail * fractions[i]), 1, avail - used - remainingSlots);
                res[i] = (cursor, size);
                used += size;
                cursor += size + effectiveGap;
            }
            return res;
        }

        string id = (templateId ?? "Halves").Trim();
        var zones = new List<Drawing.Rectangle>();

        switch (id)
        {
            case "Focus70_30":
            {
                var cols = SplitLinear(workArea.Left, workArea.Width, new[] { 0.70, 0.30 }, gap);
                zones.Add(new Drawing.Rectangle(cols[0].Start, workArea.Top, cols[0].Size, workArea.Height));
                zones.Add(new Drawing.Rectangle(cols[1].Start, workArea.Top, cols[1].Size, workArea.Height));
                break;
            }
            case "Thirds":
            {
                var cols = SplitLinear(workArea.Left, workArea.Width, new[] { 1.0 / 3, 1.0 / 3, 1.0 / 3 }, gap);
                foreach (var c in cols)
                    zones.Add(new Drawing.Rectangle(c.Start, workArea.Top, c.Size, workArea.Height));
                break;
            }
            case "Quarters":
            {
                var cols = SplitLinear(workArea.Left, workArea.Width, new[] { 0.5, 0.5 }, gap);
                var rows = SplitLinear(workArea.Top, workArea.Height, new[] { 0.5, 0.5 }, gap);
                zones.Add(new Drawing.Rectangle(cols[0].Start, rows[0].Start, cols[0].Size, rows[0].Size));
                zones.Add(new Drawing.Rectangle(cols[1].Start, rows[0].Start, cols[1].Size, rows[0].Size));
                zones.Add(new Drawing.Rectangle(cols[0].Start, rows[1].Start, cols[0].Size, rows[1].Size));
                zones.Add(new Drawing.Rectangle(cols[1].Start, rows[1].Start, cols[1].Size, rows[1].Size));
                break;
            }
            case "Coding":
            {
                var cols = SplitLinear(workArea.Left, workArea.Width, new[] { 0.60, 0.40 }, gap);
                var rows = SplitLinear(workArea.Top, workArea.Height, new[] { 0.5, 0.5 }, gap);
                zones.Add(new Drawing.Rectangle(cols[0].Start, workArea.Top, cols[0].Size, workArea.Height));
                zones.Add(new Drawing.Rectangle(cols[1].Start, rows[0].Start, cols[1].Size, rows[0].Size));
                zones.Add(new Drawing.Rectangle(cols[1].Start, rows[1].Start, cols[1].Size, rows[1].Size));
                break;
            }
            case "Ultrawide":
            {
                var cols = SplitLinear(workArea.Left, workArea.Width, new[] { 0.25, 0.50, 0.25 }, gap);
                foreach (var c in cols)
                    zones.Add(new Drawing.Rectangle(c.Start, workArea.Top, c.Size, workArea.Height));
                break;
            }
            default: // "Halves"
            {
                var cols = SplitLinear(workArea.Left, workArea.Width, new[] { 0.5, 0.5 }, gap);
                zones.Add(new Drawing.Rectangle(cols[0].Start, workArea.Top, cols[0].Size, workArea.Height));
                zones.Add(new Drawing.Rectangle(cols[1].Start, workArea.Top, cols[1].Size, workArea.Height));
                break;
            }
        }

        // Clamp strictly within workArea
        for (int i = 0; i < zones.Count; i++)
        {
            var r = Drawing.Rectangle.Intersect(zones[i], workArea);
            zones[i] = r;
        }

        return zones;
    }

    internal static void ShowSnapPickerForForeground()
    {
        IntPtr target = FindSnapTargetWindow(out string targetTitle);
        if (target == IntPtr.Zero)
        {
            System.Windows.MessageBox.Show(
                "No eligible foreground window was found to snap.",
                "DeskSpace · Snap Zones",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var screen = Forms.Screen.FromHandle(target) ?? Forms.Screen.PrimaryScreen;
        var workArea = screen?.WorkingArea ?? new Drawing.Rectangle(0, 0, 1920, 1080);

        var p = DeskSpaceDesignV283.Current;
        string currentTemplate = "Halves";

        var overlay = new Window
        {
            Title = "DeskSpace · Snap Zones",
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(WpfColor.FromArgb(145, 10, 15, 28)),
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        overlay.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(overlay).Handle;
            if (hwnd != IntPtr.Zero)
            {
                SetWindowPos(hwnd, new IntPtr(-1), workArea.Left, workArea.Top, workArea.Width, workArea.Height, 0);
            }
        };

        var root = new DockPanel { Margin = new Thickness(18) };
        var topBar = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        };

        var titleText = new TextBlock
        {
            Text = $"Snap '{targetTitle}'  ·  Press 1-9 or click a zone  ·  Esc to cancel",
            Foreground = WpfBrushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };
        topBar.Children.Add(titleText);

        var zoneHost = new Canvas();
        IReadOnlyList<Drawing.Rectangle> currentZones = Array.Empty<Drawing.Rectangle>();

        void SnapToZone(int index)
        {
            if (index < 0 || index >= currentZones.Count)
                return;
            var rect = currentZones[index];
            overlay.Close();
            if (IsWindow(target))
            {
                if (IsIconic(target))
                    ShowWindow(target, SW_RESTORE);
                SetWindowPos(target, IntPtr.Zero, rect.Left, rect.Top, rect.Width, rect.Height, SWP_NOZORDER | SWP_NOACTIVATE);
            }
        }

        void RenderZones()
        {
            zoneHost.Children.Clear();
            currentZones = ComputeZoneRects(currentTemplate, workArea);
            double hostW = zoneHost.ActualWidth;
            double hostH = zoneHost.ActualHeight;
            if (hostW <= 10 || hostH <= 10 || workArea.Width <= 0 || workArea.Height <= 0)
                return;

            for (int i = 0; i < currentZones.Count; i++)
            {
                int idx = i;
                var z = currentZones[i];
                double relX = (double)(z.Left - workArea.Left) / workArea.Width;
                double relY = (double)(z.Top - workArea.Top) / workArea.Height;
                double relW = (double)z.Width / workArea.Width;
                double relH = (double)z.Height / workArea.Height;

                var zoneBtn = new System.Windows.Controls.Button
                {
                    Width = Math.Max(40, relW * hostW),
                    Height = Math.Max(40, relH * hostH),
                    Background = new SolidColorBrush(WpfColor.FromArgb(110, p.Accent.R, p.Accent.G, p.Accent.B)),
                    BorderBrush = DeskSpaceDesignV283.Solid(p.Accent),
                    BorderThickness = new Thickness(2),
                    Foreground = WpfBrushes.White,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Content = new StackPanel
                    {
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = (idx + 1).ToString(),
                                FontSize = 36,
                                FontWeight = FontWeights.Bold,
                                Foreground = WpfBrushes.White,
                                HorizontalAlignment = WpfHorizontalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = $"{z.Width}×{z.Height}",
                                FontSize = 13,
                                Foreground = WpfBrushes.Gainsboro,
                                HorizontalAlignment = WpfHorizontalAlignment.Center
                            }
                        }
                    }
                };
                zoneBtn.Click += (_, _) => SnapToZone(idx);
                Canvas.SetLeft(zoneBtn, relX * hostW);
                Canvas.SetTop(zoneBtn, relY * hostH);
                zoneHost.Children.Add(zoneBtn);
            }
        }

        foreach (string tid in TemplateIds)
        {
            string captured = tid;
            var btn = DeskSpaceFluentBackdropV380.CreatePillButton(
                captured,
                () =>
                {
                    currentTemplate = captured;
                    RenderZones();
                },
                captured == currentTemplate,
                $"Switch snap layout to {captured}");
            btn.Margin = new Thickness(0, 0, 6, 0);
            topBar.Children.Add(btn);
        }

        var cancelBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Cancel (Esc)", () => overlay.Close(), false, "Close overlay");
        topBar.Children.Add(cancelBtn);

        var barBorder = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(220, 18, 25, 44)),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 8, 14, 8),
            Child = topBar,
            HorizontalAlignment = WpfHorizontalAlignment.Center
        };
        DockPanel.SetDock(barBorder, Dock.Top);
        root.Children.Add(barBorder);
        root.Children.Add(zoneHost);

        overlay.Content = root;
        zoneHost.SizeChanged += (_, _) => RenderZones();
        overlay.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                overlay.Close();
                e.Handled = true;
                return;
            }
            int num = e.Key switch
            {
                >= Key.D1 and <= Key.D9 => e.Key - Key.D1,
                >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad1,
                _ => -1
            };
            if (num >= 0 && num < currentZones.Count)
            {
                SnapToZone(num);
                e.Handled = true;
            }
        };

        overlay.Show();
        overlay.Activate();
    }

    private static IntPtr FindSnapTargetWindow(out string title)
    {
        title = "";
        int ownPid = Environment.ProcessId;

        bool IsEligible(IntPtr hwnd, out string foundTitle)
        {
            foundTitle = "";
            if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd))
                return false;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0 || pid == ownPid)
                return false;

            var cls = new StringBuilder(64);
            GetClassName(hwnd, cls, cls.Capacity);
            string c = cls.ToString();
            if (c is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow")
                return false;

            var txt = new StringBuilder(256);
            GetWindowText(hwnd, txt, txt.Capacity);
            foundTitle = txt.ToString().Trim();
            if (string.IsNullOrEmpty(foundTitle))
            {
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    foundTitle = proc.ProcessName;
                }
                catch
                {
                    foundTitle = "Window";
                }
            }
            return true;
        }

        IntPtr fg = GetForegroundWindow();
        if (IsEligible(fg, out title))
            return fg;

        IntPtr fallback = IntPtr.Zero;
        string fallbackTitle = "";
        EnumWindows((hwnd, _) =>
        {
            if (IsEligible(hwnd, out string t))
            {
                fallback = hwnd;
                fallbackTitle = t;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        title = fallbackTitle;
        return fallback;
    }
}

internal static class DeskSpaceWindowPowerV380
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x0008L;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static readonly HashSet<string> IgnoredShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow"
    };

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    private static IntPtr GetWindowLongPtrCompat(IntPtr hwnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, nIndex) : new IntPtr(GetWindowLong32(hwnd, nIndex));

    internal static bool ToggleForegroundTopmost(out string windowTitle, out bool isTopmost, out string error)
    {
        windowTitle = "";
        isTopmost = false;
        error = "";

        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            error = "No foreground window detected.";
            return false;
        }

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId)
        {
            error = "DeskSpace cannot toggle Always-on-Top on its own window.";
            return false;
        }

        var cls = new StringBuilder(96);
        if (GetClassName(hwnd, cls, cls.Capacity) > 0 && IgnoredShellClasses.Contains(cls.ToString()))
        {
            error = $"Ignored shell window ({cls}).";
            return false;
        }

        int len = Math.Clamp(GetWindowTextLength(hwnd), 0, 512);
        var titleBuf = new StringBuilder(len + 2);
        if (len > 0)
            GetWindowText(hwnd, titleBuf, titleBuf.Capacity);
        windowTitle = titleBuf.ToString().Trim();
        if (windowTitle.Length == 0)
        {
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                windowTitle = proc.ProcessName;
            }
            catch
            {
                windowTitle = "Active Window";
            }
        }

        long exStyle = GetWindowLongPtrCompat(hwnd, GWL_EXSTYLE).ToInt64();
        bool wasTopmost = (exStyle & WS_EX_TOPMOST) != 0;
        IntPtr targetOrder = wasTopmost ? HWND_NOTOPMOST : HWND_TOPMOST;

        if (!SetWindowPos(hwnd, targetOrder, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE))
        {
            int winErr = Marshal.GetLastWin32Error();
            error = $"SetWindowPos failed (Win32 error {winErr}).";
            return false;
        }

        isTopmost = !wasTopmost;
        ShowTopmostPill(windowTitle, isTopmost);
        return true;
    }

    private static void ShowTopmostPill(string windowTitle, bool pinned)
    {
        var app = Application.Current;
        if (app == null)
            return;

        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var p = DeskSpaceDesignV283.Current;
                string shortTitle = windowTitle.Length > 42 ? windowTitle[..42] + "…" : windowTitle;
                var pill = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = WpfBrushes.Transparent,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Topmost = true,
                    Focusable = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Content = new Border
                    {
                        CornerRadius = new CornerRadius(18),
                        Background = DeskSpaceDesignV283.Solid(WpfColor.FromArgb(235, p.Raised.R, p.Raised.G, p.Raised.B)),
                        BorderBrush = DeskSpaceDesignV283.Solid(pinned ? p.Accent : p.Border),
                        BorderThickness = new Thickness(1.5),
                        Padding = new Thickness(16, 8, 16, 8),
                        Child = new TextBlock
                        {
                            Text = (pinned ? "Pinned Always-on-Top: " : "Unpinned Topmost: ") + shortTitle,
                            Foreground = DeskSpaceDesignV283.Solid(p.Text),
                            FontSize = 13,
                            FontWeight = FontWeights.SemiBold
                        }
                    }
                };

                var area = SystemParameters.WorkArea;
                pill.Loaded += (_, _) =>
                {
                    pill.Left = area.Left + (area.Width - pill.ActualWidth) / 2;
                    pill.Top = area.Top + 26;
                };
                pill.Show();

                var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
                closeTimer.Tick += (_, _) =>
                {
                    closeTimer.Stop();
                    pill.Close();
                };
                closeTimer.Start();
            }
            catch { }
        }));
    }

    internal static void ShowStudio(Window? owner)
    {
        var p = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            "DeskSpace · Window Power Studio",
            860,
            620,
            owner,
            transient: false);
        win.MinWidth = 680;
        win.MinHeight = 500;

        var config = MonitorTopologyV380.LoadConfig();
        var root = new DockPanel { Margin = new Thickness(16) };

        var statusText = new TextBlock
        {
            Text = "Configure monitor topology auto-layouts, Snap Zones (Ctrl+Alt+Z), and Always-on-Top pin (Ctrl+Alt+T).",
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };
        DockPanel.SetDock(statusText, Dock.Bottom);
        root.Children.Add(statusText);

        var body = new StackPanel();

        // Quick Actions Card
        var quickStack = new StackPanel();
        quickStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Quick Window Actions", p));
        var quickRow = new WrapPanel { Orientation = WpfOrientation.Horizontal };
        var snapBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Launch Snap Zones (Ctrl+Alt+Z)", () =>
        {
            DeskSpaceSnapZonesV380.ShowSnapPickerForForeground();
        }, true, "Pick a snap zone on the target monitor");
        snapBtn.Margin = new Thickness(0, 0, 8, 6);
        quickRow.Children.Add(snapBtn);

        var pinBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Pin Active Window Topmost (Ctrl+Alt+T)", () =>
        {
            if (ToggleForegroundTopmost(out string title, out bool topmost, out string err))
                statusText.Text = (topmost ? "Pinned: " : "Unpinned: ") + title;
            else
                statusText.Text = err;
        }, false, "Toggle Always-on-Top on the active foreground window");
        pinBtn.Margin = new Thickness(0, 0, 8, 6);
        quickRow.Children.Add(pinBtn);

        var openLayoutsBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Open Saved Layout Profiles...", () =>
        {
            DeskSpaceWindowLayoutsV179.Show(win);
        }, false, "Capture or edit DeskSpaceWindowLayoutsV179 profiles");
        openLayoutsBtn.Margin = new Thickness(0, 0, 8, 6);
        quickRow.Children.Add(openLayoutsBtn);

        quickStack.Children.Add(quickRow);
        var quickCard = DeskSpaceFluentBackdropV380.CreateCard(quickStack, p);
        quickCard.Margin = new Thickness(0, 0, 0, 12);
        body.Children.Add(quickCard);

        // Current Monitor Topology Card
        var topoStack = new StackPanel();
        topoStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Current Monitor Topology", p));
        string currentSig = MonitorTopologyV380.CurrentSignature();
        var sigLabel = new TextBlock
        {
            Text = $"Topology Signature: {currentSig}",
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            Margin = new Thickness(0, 0, 0, 6)
        };
        topoStack.Children.Add(sigLabel);

        foreach (var scr in Forms.Screen.AllScreens)
        {
            topoStack.Children.Add(new TextBlock
            {
                Text = $"• {scr.DeviceName}  —  {scr.Bounds.Width}×{scr.Bounds.Height}  {(scr.Primary ? "(Primary)" : "(Secondary)")}  WorkArea: {scr.WorkingArea.Width}×{scr.WorkingArea.Height}",
                Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                FontSize = 12,
                Margin = new Thickness(0, 1, 0, 1)
            });
        }

        var autoSwitchCheck = new System.Windows.Controls.CheckBox
        {
            Content = "Auto-apply matching layout when monitor topology changes (1.5s debounce)",
            IsChecked = config.AutoSwitchEnabled,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            Margin = new Thickness(0, 10, 0, 0)
        };
        autoSwitchCheck.Checked += (_, _) =>
        {
            config.AutoSwitchEnabled = true;
            MonitorTopologyV380.SaveConfig(config);
            MonitorTopologyV380.InitializeWatcher(win.Dispatcher);
            statusText.Text = "Auto-Switch enabled.";
        };
        autoSwitchCheck.Unchecked += (_, _) =>
        {
            config.AutoSwitchEnabled = false;
            MonitorTopologyV380.SaveConfig(config);
            statusText.Text = "Auto-Switch disabled.";
        };
        topoStack.Children.Add(autoSwitchCheck);

        var topoCard = DeskSpaceFluentBackdropV380.CreateCard(topoStack, p);
        topoCard.Margin = new Thickness(0, 0, 0, 12);
        body.Children.Add(topoCard);

        // Topology -> Layout Binding Editor Card
        var bindStack = new StackPanel();
        bindStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("Topology → Layout Bindings", p));

        var sigBox = new System.Windows.Controls.TextBox { Text = currentSig, Margin = new Thickness(0, 0, 0, 6) };
        DeskSpaceDesignV283.Input(sigBox, p);

        var profileCombo = new WpfComboBox { Margin = new Thickness(0, 0, 0, 6) };
        void RefreshProfileCombo()
        {
            profileCombo.ItemsSource = MonitorTopologyV380.LoadSavedLayoutProfiles().Select(x => x.Name).ToArray();
            if (profileCombo.Items.Count > 0 && profileCombo.SelectedIndex < 0)
                profileCombo.SelectedIndex = 0;
        }
        RefreshProfileCombo();

        var launchAppsCheck = new System.Windows.Controls.CheckBox
        {
            Content = "Launch missing allowed .exe apps if not running",
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            Margin = new Thickness(0, 4, 0, 6)
        };

        var exePathsBox = new System.Windows.Controls.TextBox
        {
            AcceptsReturn = true,
            Height = 64,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ToolTip = "One full .exe path per line",
            Margin = new Thickness(0, 0, 0, 8)
        };
        DeskSpaceDesignV283.Input(exePathsBox, p);

        var bindingsList = new System.Windows.Controls.ListBox { Height = 115, Margin = new Thickness(0, 0, 0, 8) };
        DeskSpaceDesignV283.Selection(bindingsList, p);

        void RefreshBindingsList()
        {
            bindingsList.Items.Clear();
            foreach (var b in config.Bindings)
                bindingsList.Items.Add($"{b.Signature}  →  {b.LayoutProfileName}  (Launch apps: {(b.LaunchMissingApps ? b.AllowedExePaths.Count : 0)})");
        }
        RefreshBindingsList();

        bindingsList.SelectionChanged += (_, _) =>
        {
            int idx = bindingsList.SelectedIndex;
            if (idx < 0 || idx >= config.Bindings.Count)
                return;
            var b = config.Bindings[idx];
            sigBox.Text = b.Signature;
            profileCombo.SelectedItem = b.LayoutProfileName;
            launchAppsCheck.IsChecked = b.LaunchMissingApps;
            exePathsBox.Text = string.Join(Environment.NewLine, b.AllowedExePaths ?? new List<string>());
        };

        var bindBtns = new WrapPanel { Orientation = WpfOrientation.Horizontal };
        var addExeBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Add .exe...", () =>
        {
            var dlg = new OpenFileDialog
            {
                Title = "Choose allowed executable (.exe)",
                Filter = "Executables (*.exe)|*.exe",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dlg.ShowDialog() == true)
            {
                string existing = exePathsBox.Text.TrimEnd();
                exePathsBox.Text = string.IsNullOrEmpty(existing)
                    ? dlg.FileName
                    : existing + Environment.NewLine + dlg.FileName;
            }
        }, false, "Browse for an application executable");
        addExeBtn.Margin = new Thickness(0, 0, 8, 6);

        var saveBindBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Save Binding", () =>
        {
            string sig = sigBox.Text.Trim();
            string prof = (profileCombo.SelectedItem as string ?? "").Trim();
            if (string.IsNullOrEmpty(sig) || string.IsNullOrEmpty(prof))
            {
                statusText.Text = "Enter a topology signature and choose a saved layout profile.";
                return;
            }
            var exes = exePathsBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => System.IO.Path.IsPathFullyQualified(s) &&
                            System.IO.Path.GetExtension(s).Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            config.Bindings.RemoveAll(b => string.Equals(b.Signature, sig, StringComparison.OrdinalIgnoreCase));
            config.Bindings.Add(new TopologyBindingV380(sig, prof, launchAppsCheck.IsChecked == true, exes));
            MonitorTopologyV380.SaveConfig(config);
            RefreshBindingsList();
            statusText.Text = $"Saved binding: '{sig}' → '{prof}'.";
        }, true, "Save topology binding");
        saveBindBtn.Margin = new Thickness(0, 0, 8, 6);

        var applyNowBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Apply Current Binding Now", () =>
        {
            statusText.Text = MonitorTopologyV380.ApplyBindingForCurrentTopology(forceIgnoreAutoSwitch: true);
        }, false, "Apply the saved layout for the current monitors");
        applyNowBtn.Margin = new Thickness(0, 0, 8, 6);

        var removeBindBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Remove Selected", () =>
        {
            int idx = bindingsList.SelectedIndex;
            if (idx < 0 || idx >= config.Bindings.Count)
                return;
            config.Bindings.RemoveAt(idx);
            MonitorTopologyV380.SaveConfig(config);
            RefreshBindingsList();
            statusText.Text = "Removed topology binding.";
        }, false, "Delete selected binding");
        removeBindBtn.Margin = new Thickness(0, 0, 8, 6);

        bindBtns.Children.Add(addExeBtn);
        bindBtns.Children.Add(saveBindBtn);
        bindBtns.Children.Add(applyNowBtn);
        bindBtns.Children.Add(removeBindBtn);

        bindStack.Children.Add(new TextBlock { Text = "Topology Signature", Foreground = DeskSpaceDesignV283.Solid(p.Muted), FontSize = 11.5 });
        bindStack.Children.Add(sigBox);
        bindStack.Children.Add(new TextBlock { Text = "Target Layout Profile", Foreground = DeskSpaceDesignV283.Solid(p.Muted), FontSize = 11.5 });
        bindStack.Children.Add(profileCombo);
        bindStack.Children.Add(launchAppsCheck);
        bindStack.Children.Add(new TextBlock { Text = "Allowed .exe paths (one per line)", Foreground = DeskSpaceDesignV283.Solid(p.Muted), FontSize = 11.5 });
        bindStack.Children.Add(exePathsBox);
        bindStack.Children.Add(bindBtns);
        bindStack.Children.Add(bindingsList);

        body.Children.Add(DeskSpaceFluentBackdropV380.CreateCard(bindStack, p));

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

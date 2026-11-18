using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace DeskSpace;

internal sealed record DeskTouchDeviceItemV378(
    int Id,
    string Name,
    string Platform,
    bool IsLocal,
    bool IsSelected,
    bool IsControlling,
    bool SupportsInput,
    int NotificationCount,
    string StatusText,
    double LayoutX = 0,
    double LayoutY = 0,
    double LayoutW = 0,
    double LayoutH = 0,
    int ArrangementOrder = 0,
    int BatteryPercent = -1,
    bool IsCharging = false,
    string SignalLabel = "");

internal sealed record DeskTouchBridgeSnapshotV378(
    bool IsLive,
    int Version,
    long UpdatedUtcMs,
    string LocalName,
    int LocalNotificationCount,
    int ActiveTargetId,
    bool IsRemoteActive,
    string ActiveDeviceName,
    int ConnectedCount,
    int TotalNotificationCount,
    bool HasAnyKnownNotificationCount,
    IReadOnlyList<DeskTouchDeviceItemV378> Devices,
    string ActiveProfile = "Work",
    bool ClipboardSyncEnabled = true,
    string ActiveForegroundApp = "",
    string ActiveShortcutPreset = "Default");

internal static class DeskSpaceDeskTouchBridgeV378
{
    internal const string PipeName = "DeskTouch.Vision.DeckBridge.V1";

    internal static string SnapshotFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskTouchVision", "live-devices.json");

    private static readonly object StateLock = new();
    private static DeskTouchBridgeSnapshotV378 _current = CreateEmpty(live: false);
    private static string _lastSignature = "";
    private static long _lastPollTicks;
    private static int _polling;
    private static bool _sectionMinimized;
    private static bool _sectionDismissed;
    private static int _lastConnectedCount;

    internal static event Action? Changed;

    internal static DeskTouchBridgeSnapshotV378 Current
    {
        get { lock (StateLock) return _current; }
    }

    internal static bool HasConnectedDevices => Current.ConnectedCount > 0;
    internal static bool IsSectionMinimized { get { lock (StateLock) return _sectionMinimized; } }
    internal static bool IsSectionDismissed { get { lock (StateLock) return _sectionDismissed; } }

    internal static void ToggleMinimized()
    {
        lock (StateLock)
            _sectionMinimized = !_sectionMinimized;
        Changed?.Invoke();
    }

    internal static void DismissSection()
    {
        lock (StateLock)
            _sectionDismissed = true;
        Changed?.Invoke();
    }

    internal static void ResetSectionVisibility()
    {
        lock (StateLock)
        {
            _sectionMinimized = false;
            _sectionDismissed = false;
        }
        Changed?.Invoke();
    }

    internal static DeskTouchBridgeSnapshotV378 CreateEmpty(bool live) =>
        new(
            IsLive: live,
            Version: 1,
            UpdatedUtcMs: 0,
            LocalName: Environment.MachineName,
            LocalNotificationCount: -1,
            ActiveTargetId: int.MinValue,
            IsRemoteActive: false,
            ActiveDeviceName: Environment.MachineName,
            ConnectedCount: 0,
            TotalNotificationCount: 0,
            HasAnyKnownNotificationCount: false,
            Devices: Array.Empty<DeskTouchDeviceItemV378>());

    internal static string FormatCountBadge(int count) =>
        count >= 0 ? count.ToString() : "—";

    internal static DeskTouchBridgeSnapshotV378 ParseSnapshotJson(string? json, bool requireFreshTimestamp)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CreateEmpty(live: false);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int version = root.TryGetProperty("version", out var vEl) && vEl.TryGetInt32(out int v) ? v : 1;
            long updatedMs = root.TryGetProperty("updatedUtcMs", out var uEl) && uEl.TryGetInt64(out long u) ? u : 0;
            if (requireFreshTimestamp && updatedMs > 0)
            {
                long ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - updatedMs;
                if (ageMs > 60000)
                    return CreateEmpty(live: false);
            }
            string localName = root.TryGetProperty("localName", out var lnEl) && lnEl.ValueKind == JsonValueKind.String
                ? (lnEl.GetString() ?? Environment.MachineName) : Environment.MachineName;
            int localNotif = root.TryGetProperty("localNotificationCount", out var lncEl) && lncEl.TryGetInt32(out int lnc) ? lnc : -1;
            int activeTargetId = root.TryGetProperty("activeTargetId", out var atEl) && atEl.TryGetInt32(out int at) ? at : int.MinValue;
            bool isRemoteActive = root.TryGetProperty("isRemoteActive", out var irEl) && irEl.ValueKind == JsonValueKind.True;
            string activeDeviceName = root.TryGetProperty("activeDeviceName", out var adEl) && adEl.ValueKind == JsonValueKind.String
                ? (adEl.GetString() ?? localName) : localName;
            int connectedCount = root.TryGetProperty("connectedCount", out var ccEl) && ccEl.TryGetInt32(out int cc) ? Math.Max(0, cc) : 0;
            int totalNotif = root.TryGetProperty("totalNotificationCount", out var tnEl) && tnEl.TryGetInt32(out int tn) ? Math.Max(0, tn) : 0;
            bool hasKnown = root.TryGetProperty("hasAnyKnownNotificationCount", out var hkEl) && hkEl.ValueKind == JsonValueKind.True;

            var list = new List<DeskTouchDeviceItemV378>();
            if (root.TryGetProperty("devices", out var devsEl) && devsEl.ValueKind == JsonValueKind.Array)
            {
                int fallbackIdx = 0;
                foreach (var d in devsEl.EnumerateArray())
                {
                    int id = d.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out int iv) ? iv : 0;
                    string name = d.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String
                        ? (nEl.GetString() ?? "Device") : "Device";
                    string platform = d.TryGetProperty("platform", out var pEl) && pEl.ValueKind == JsonValueKind.String
                        ? (pEl.GetString() ?? "Windows") : "Windows";
                    bool isLocal = d.TryGetProperty("isLocal", out var lEl) && lEl.ValueKind == JsonValueKind.True;
                    bool isSel = d.TryGetProperty("isSelected", out var sEl) && sEl.ValueKind == JsonValueKind.True;
                    bool isCtrl = d.TryGetProperty("isControlling", out var cEl) && cEl.ValueKind == JsonValueKind.True;
                    bool supports = !d.TryGetProperty("supportsInput", out var supEl) || supEl.ValueKind != JsonValueKind.False;
                    int notif = d.TryGetProperty("notificationCount", out var ncEl) && ncEl.TryGetInt32(out int ncv) ? ncv : -1;
                    string status = d.TryGetProperty("statusText", out var stEl) && stEl.ValueKind == JsonValueKind.String
                        ? (stEl.GetString() ?? "") : "";
                    double lx = d.TryGetProperty("layoutX", out var lxEl) && lxEl.TryGetDouble(out double lxv) && double.IsFinite(lxv) ? lxv : 0;
                    double ly = d.TryGetProperty("layoutY", out var lyEl) && lyEl.TryGetDouble(out double lyv) && double.IsFinite(lyv) ? lyv : 0;
                    double lw = d.TryGetProperty("layoutW", out var lwEl) && lwEl.TryGetDouble(out double lwv) && double.IsFinite(lwv) ? lwv : 0;
                    double lh = d.TryGetProperty("layoutH", out var lhEl) && lhEl.TryGetDouble(out double lhv) && double.IsFinite(lhv) ? lhv : 0;
                    int order = d.TryGetProperty("arrangementOrder", out var aoEl) && aoEl.TryGetInt32(out int aov) ? aov : fallbackIdx;
                    int batt = d.TryGetProperty("batteryPercent", out var bpEl) && bpEl.TryGetInt32(out int bpv) ? bpv : -1;
                    bool charging = d.TryGetProperty("isCharging", out var chgEl) && chgEl.ValueKind == JsonValueKind.True;
                    string sig = d.TryGetProperty("signalLabel", out var sigEl) && sigEl.ValueKind == JsonValueKind.String ? (sigEl.GetString() ?? "") : "";
                    list.Add(new DeskTouchDeviceItemV378(id, name, platform, isLocal, isSel, isCtrl, supports, notif, status, lx, ly, lw, lh, order, batt, charging, sig));
                    fallbackIdx++;
                }
                list.Sort((a, b) => a.ArrangementOrder.CompareTo(b.ArrangementOrder));
            }
            string activeProfile = root.TryGetProperty("activeProfile", out var apEl) && apEl.ValueKind == JsonValueKind.String
                ? (apEl.GetString() ?? "Work") : "Work";
            bool clipEnabled = !root.TryGetProperty("clipboardSyncEnabled", out var csEl) || csEl.ValueKind != JsonValueKind.False;
            string activeFg = root.TryGetProperty("activeForegroundApp", out var fgEl) && fgEl.ValueKind == JsonValueKind.String
                ? (fgEl.GetString() ?? "") : "";
            string activePreset = root.TryGetProperty("activeShortcutPreset", out var spEl) && spEl.ValueKind == JsonValueKind.String
                ? (spEl.GetString() ?? "Default") : "Default";
            return new DeskTouchBridgeSnapshotV378(
                IsLive: list.Count > 0,
                Version: version,
                UpdatedUtcMs: updatedMs,
                LocalName: localName,
                LocalNotificationCount: localNotif,
                ActiveTargetId: activeTargetId,
                IsRemoteActive: isRemoteActive,
                ActiveDeviceName: activeDeviceName,
                ConnectedCount: connectedCount,
                TotalNotificationCount: totalNotif,
                HasAnyKnownNotificationCount: hasKnown,
                Devices: list,
                ActiveProfile: activeProfile,
                ClipboardSyncEnabled: clipEnabled,
                ActiveForegroundApp: activeFg,
                ActiveShortcutPreset: activePreset);
        }
        catch
        {
            return CreateEmpty(live: false);
        }
    }

    private static string BuildSignature(DeskTouchBridgeSnapshotV378 s)
    {
        var sb = new StringBuilder(160);
        sb.Append(s.IsLive ? '1' : '0').Append('|')
            .Append(s.ConnectedCount).Append('|')
            .Append(s.IsRemoteActive ? '1' : '0').Append('|')
            .Append(s.ActiveTargetId).Append('|')
            .Append(s.TotalNotificationCount).Append('|')
            .Append(s.ActiveProfile).Append('|')
            .Append(s.ClipboardSyncEnabled ? '1' : '0');
        foreach (var d in s.Devices)
        {
            sb.Append(';').Append(d.Id).Append(':')
                .Append(d.Name).Append(':')
                .Append(d.ArrangementOrder).Append(':')
                .Append(Math.Round(d.LayoutX, 3)).Append(',')
                .Append(Math.Round(d.LayoutY, 3)).Append(',')
                .Append(Math.Round(d.LayoutW, 3)).Append(',')
                .Append(Math.Round(d.LayoutH, 3)).Append(':')
                .Append(d.IsControlling ? '1' : '0')
                .Append(d.IsSelected ? '1' : '0')
                .Append(d.SupportsInput ? '1' : '0').Append(':')
                .Append(d.NotificationCount).Append(':')
                .Append(d.BatteryPercent).Append(':')
                .Append(d.IsCharging ? '1' : '0');
        }
        return sb.ToString();
    }

    internal static void PollIfDueAsync(bool force = false)
    {
        long now = Environment.TickCount64;
        if (!force && now - Volatile.Read(ref _lastPollTicks) < 850)
            return;
        if (Interlocked.CompareExchange(ref _polling, 1, 0) != 0)
            return;
        Volatile.Write(ref _lastPollTicks, now);
        Task.Run(() =>
        {
            try
            {
                var next = FetchLiveSnapshot();
                ApplySnapshot(next);
            }
            catch { }
            finally
            {
                Volatile.Write(ref _polling, 0);
            }
        });
    }

    internal static DeskTouchBridgeSnapshotV378 FetchLiveSnapshot()
    {
        string? raw = SendPipeRequest("GET", timeoutMs: 85);
        if (!string.IsNullOrWhiteSpace(raw))
            return ParseSnapshotJson(raw, requireFreshTimestamp: false);
        try
        {
            string file = SnapshotFilePath;
            if (File.Exists(file))
                return ParseSnapshotJson(File.ReadAllText(file, Encoding.UTF8), requireFreshTimestamp: true);
        }
        catch { }
        return CreateEmpty(live: false);
    }

    private static void ApplySnapshot(DeskTouchBridgeSnapshotV378 next)
    {
        string sig = BuildSignature(next);
        bool changed;
        lock (StateLock)
        {
            _current = next;
            if ((next.ConnectedCount > 0 && _lastConnectedCount == 0)
                || (!next.IsLive && _lastConnectedCount > 0))
            {
                _sectionDismissed = false;
            }
            _lastConnectedCount = next.ConnectedCount;
            changed = !string.Equals(_lastSignature, sig, StringComparison.Ordinal);
            _lastSignature = sig;
        }
        if (changed)
            Changed?.Invoke();
    }

    private static string? SendPipeRequest(string command, int timeoutMs = 150)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(timeoutMs);
            byte[] req = Encoding.UTF8.GetBytes(command);
            pipe.Write(req, 0, req.Length);
            pipe.Flush();
            using var ms = new MemoryStream();
            var buf = new byte[2048];
            int n;
            while ((n = pipe.Read(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > 65536) break;
            }
            return ms.Length > 0 ? Encoding.UTF8.GetString(ms.ToArray()) : null;
        }
        catch
        {
            return null;
        }
    }

    internal static void SwitchDeviceAsync(int targetId) => DispatchActionAsync($"SWITCH:{targetId}");
    internal static void ReturnLocalAsync() => DispatchActionAsync("RETURN");
    internal static void TogglePayModeAsync(int peerId) => DispatchActionAsync($"TOGGLE_PAY:{peerId}");
    internal static void OpenDeskTouchAsync() => DispatchActionAsync("SHOW");
    internal static void OpenNotificationsAsync(int targetId) => DispatchActionAsync($"OPEN_NOTIFS:{targetId}");
    internal static void ToggleClipboardAsync() => DispatchActionAsync("TOGGLE_CLIPBOARD");
    internal static void DropFilePathAsync(int targetId, string? path) => DispatchActionAsync(
        string.IsNullOrWhiteSpace(path) ? $"DROP_FILE:{targetId}" : $"DROP_FILE_PATH:{targetId}|{path}");
    internal static void SendPowerAsync(int targetId, string action) => DispatchActionAsync($"POWER:{targetId}|{action}");
    internal static void SendMediaAsync(int targetId, string action) => DispatchActionAsync($"MEDIA:{targetId}|{action}");
    internal static void SwitchProfileAsync(string profileName) => DispatchActionAsync($"PROFILE:{profileName}");

    private static void DispatchActionAsync(string command)
    {
        Task.Run(async () =>
        {
            try
            {
                string? raw = SendPipeRequest(command, timeoutMs: 220);
                if (!string.IsNullOrWhiteSpace(raw))
                    ApplySnapshot(ParseSnapshotJson(raw, requireFreshTimestamp: false));
                await Task.Delay(130).ConfigureAwait(false);
                ApplySnapshot(FetchLiveSnapshot());
            }
            catch { }
        });
    }

    internal static IReadOnlyList<IReadOnlyList<DeskTouchDeviceItemV378>> ComputeCompactRows(
        IReadOnlyList<DeskTouchDeviceItemV378> devices)
    {
        if (devices == null || devices.Count == 0)
            return Array.Empty<IReadOnlyList<DeskTouchDeviceItemV378>>();

        var ordered = devices
            .OrderBy(d => d.ArrangementOrder)
            .ThenBy(d => Math.Round(d.LayoutY + (d.LayoutH * 0.5), 2))
            .ThenBy(d => d.LayoutX)
            .ToArray();

        bool hasBounds = ordered.All(d => d.LayoutW > 0.001 && d.LayoutH > 0.001);
        if (!hasBounds || ordered.Length <= 1)
            return new IReadOnlyList<DeskTouchDeviceItemV378>[] { ordered };

        var byRow = ordered
            .OrderBy(d => d.LayoutY + (d.LayoutH * 0.5))
            .ThenBy(d => d.LayoutX)
            .ToArray();

        var rows = new List<IReadOnlyList<DeskTouchDeviceItemV378>>();
        var current = new List<DeskTouchDeviceItemV378>();
        double rowMinY = 0;
        double rowMaxY = 0;

        foreach (var d in byRow)
        {
            double top = d.LayoutY;
            double bottom = d.LayoutY + d.LayoutH;
            if (current.Count == 0)
            {
                current.Add(d);
                rowMinY = top;
                rowMaxY = bottom;
            }
            else
            {
                double overlap = Math.Min(rowMaxY, bottom) - Math.Max(rowMinY, top);
                double minH = Math.Max(0.01, Math.Min(rowMaxY - rowMinY, d.LayoutH));
                if (overlap > minH * 0.30)
                {
                    current.Add(d);
                    rowMinY = Math.Min(rowMinY, top);
                    rowMaxY = Math.Max(rowMaxY, bottom);
                }
                else
                {
                    rows.Add(current.OrderBy(x => x.LayoutX).ToArray());
                    current = new List<DeskTouchDeviceItemV378> { d };
                    rowMinY = top;
                    rowMaxY = bottom;
                }
            }
        }
        if (current.Count > 0)
            rows.Add(current.OrderBy(x => x.LayoutX).ToArray());

        return rows;
    }

    private static ControlTemplate CreateBorderlessTemplate()
    {
        var template = new ControlTemplate(typeof(WpfButton));
        var root = new FrameworkElementFactory(typeof(Border));
        root.SetValue(Border.BackgroundProperty, WpfBrushes.Transparent);
        root.SetValue(Border.SnapsToDevicePixelsProperty, true);
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, WpfHorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(cp);
        template.VisualTree = root;
        return template;
    }

    private static FrameworkElement CreateDeviceLogo(bool isMobile, WpfColor strokeColor)
    {
        var strokeBrush = new SolidColorBrush(strokeColor);
        var host = new Grid
        {
            Width = 22,
            Height = 20,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };

        if (isMobile)
        {
            var body = new Border
            {
                Width = 11,
                Height = 18,
                CornerRadius = new CornerRadius(2.2),
                BorderThickness = new Thickness(1.5),
                BorderBrush = strokeBrush,
                Background = WpfBrushes.Transparent,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };
            var inner = new Grid();
            inner.Children.Add(new Border
            {
                Width = 3.8,
                Height = 1.2,
                CornerRadius = new CornerRadius(0.6),
                Background = strokeBrush,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1.8, 0, 0)
            });
            inner.Children.Add(new Border
            {
                Width = 4.5,
                Height = 1.2,
                CornerRadius = new CornerRadius(0.6),
                Background = strokeBrush,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1.6)
            });
            body.Child = inner;
            host.Children.Add(body);
            return host;
        }

        var monitor = new StackPanel
        {
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        monitor.Children.Add(new Border
        {
            Width = 18,
            Height = 12,
            CornerRadius = new CornerRadius(1.8),
            BorderThickness = new Thickness(1.5),
            BorderBrush = strokeBrush,
            Background = WpfBrushes.Transparent,
            SnapsToDevicePixels = true
        });
        monitor.Children.Add(new Border
        {
            Width = 2.0,
            Height = 2.2,
            Background = strokeBrush,
            HorizontalAlignment = WpfHorizontalAlignment.Center
        });
        monitor.Children.Add(new Border
        {
            Width = 9.0,
            Height = 1.5,
            CornerRadius = new CornerRadius(0.75),
            Background = strokeBrush,
            HorizontalAlignment = WpfHorizontalAlignment.Center
        });
        host.Children.Add(monitor);
        return host;
    }

    private static WpfButton CreateSectionHeaderButton(string symbol, string tooltip, Action click)
    {
        var btn = new WpfButton
        {
            Content = symbol,
            Width = 20,
            Height = 20,
            MinWidth = 20,
            MinHeight = 20,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 0, 0),
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            FontFamily = new WpfFontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(184, 207, 239)),
            Background = WpfBrushes.Transparent,
            BorderThickness = new Thickness(0),
            Focusable = false,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = WpfHorizontalAlignment.Center,
            ToolTip = tooltip
        };
        btn.Click += (_, _) => click();
        return btn;
    }

    internal static void AddDeckSection(StackPanel list)
    {
        var snap = Current;
        bool minimized, dismissed;
        lock (StateLock)
        {
            minimized = _sectionMinimized;
            dismissed = _sectionDismissed;
        }
        if (dismissed || (!snap.IsLive && snap.ConnectedCount == 0))
            return;

        var container = new StackPanel();
        var header = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, 0, 0, minimized ? 0 : 4)
        };

        var closeBtn = CreateSectionHeaderButton("×", "Hide Devices section", DismissSection);
        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(closeBtn);

        var minBtn = CreateSectionHeaderButton(
            minimized ? "▢" : "−",
            minimized ? "Expand Devices section" : "Minimize Devices section",
            ToggleMinimized);
        DockPanel.SetDock(minBtn, Dock.Right);
        header.Children.Add(minBtn);

        string headerText = snap.ConnectedCount == 1
            ? "Devices · 1 connected"
            : $"Devices · {snap.ConnectedCount} connected";
        header.Children.Add(new TextBlock
        {
            Text = headerText,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new WpfFontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(186, 226, 252)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        container.Children.Add(header);

        if (!minimized && snap.Devices.Count > 0)
        {
            var rows = ComputeCompactRows(snap.Devices);
            var diagram = new StackPanel
            {
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 1)
            };
            var borderlessTemplate = CreateBorderlessTemplate();

            for (int r = 0; r < rows.Count; r++)
            {
                var rowItems = rows[r];
                var rowPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    Margin = r > 0 ? new Thickness(0, 4, 0, 0) : new Thickness(0)
                };

                for (int i = 0; i < rowItems.Count; i++)
                {
                    var device = rowItems[i];
                    bool active = device.IsControlling || (device.IsSelected && device.IsLocal);
                    bool isMobile = !device.IsLocal && string.Equals(device.Platform, "Android", StringComparison.OrdinalIgnoreCase);

                    var unit = new StackPanel
                    {
                        MinWidth = isMobile ? 46 : 34,
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        Margin = i > 0 ? new Thickness(6, 0, 0, 0) : new Thickness(0)
                    };

                    var captured = device;
                    bool hasUnread = device.NotificationCount > 0;
                    var badge = new Border
                    {
                        CornerRadius = new CornerRadius(7),
                        Padding = new Thickness(5, 0.5, 5, 0.5),
                        Margin = new Thickness(0, 0, 0, 2),
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Background = new SolidColorBrush(hasUnread
                            ? WpfColor.FromRgb(56, 189, 248)
                            : WpfColor.FromRgb(47, 64, 96)),
                        ToolTip = device.NotificationCount >= 0
                            ? $"{device.NotificationCount} active notifications — click to view/reply"
                            : "Click to view notifications"
                    };
                    badge.MouseLeftButtonUp += (_, e) =>
                    {
                        e.Handled = true;
                        OpenNotificationsAsync(captured.Id);
                    };
                    badge.Child = new TextBlock
                    {
                        Text = FormatCountBadge(device.NotificationCount),
                        FontSize = 8.5,
                        FontWeight = FontWeights.Bold,
                        FontFamily = new WpfFontFamily("Segoe UI"),
                        Foreground = new SolidColorBrush(hasUnread
                            ? WpfColor.FromRgb(9, 17, 30)
                            : WpfColor.FromRgb(220, 232, 248)),
                        HorizontalAlignment = WpfHorizontalAlignment.Center
                    };
                    unit.Children.Add(badge);

                    var strokeColor = active
                        ? WpfColor.FromRgb(56, 189, 248)
                        : WpfColor.FromRgb(212, 224, 244);

                    var btn = new WpfButton
                    {
                        Width = 26,
                        Height = 22,
                        MinWidth = 26,
                        MinHeight = 22,
                        Padding = new Thickness(0),
                        Background = WpfBrushes.Transparent,
                        BorderBrush = WpfBrushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Template = borderlessTemplate,
                        Focusable = false,
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        IsEnabled = device.SupportsInput,
                        Cursor = device.SupportsInput ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                        ToolTip = device.IsLocal
                            ? $"Return mouse & keyboard to this computer ({device.Name})"
                            : $"{device.Name} ({device.Platform}) · {device.StatusText} (Drop file to send)",
                        Content = CreateDeviceLogo(isMobile, strokeColor)
                    };
                    btn.Click += (_, _) =>
                    {
                        if (captured.IsLocal) ReturnLocalAsync();
                        else if (captured.SupportsInput) SwitchDeviceAsync(captured.Id);
                    };
                    unit.AllowDrop = true;
                    unit.DragOver += (_, e) =>
                    {
                        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                        {
                            e.Effects = System.Windows.DragDropEffects.Copy;
                            e.Handled = true;
                        }
                    };
                    unit.Drop += (_, e) =>
                    {
                        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) &&
                            e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files &&
                            files.Length > 0)
                        {
                            DropFilePathAsync(captured.Id, files[0]);
                            e.Handled = true;
                        }
                    };
                    unit.Children.Add(btn);

                    unit.Children.Add(new Border
                    {
                        Width = 12,
                        Height = 2,
                        CornerRadius = new CornerRadius(1),
                        Margin = new Thickness(0, 1, 0, 0),
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        Background = active
                            ? new SolidColorBrush(WpfColor.FromRgb(56, 189, 248))
                            : WpfBrushes.Transparent
                    });

                    if (device.BatteryPercent >= 0)
                    {
                        var battColor = device.IsCharging
                            ? WpfColor.FromRgb(56, 189, 248)
                            : (device.BatteryPercent <= 20
                                ? WpfColor.FromRgb(252, 165, 165)
                                : WpfColor.FromRgb(134, 239, 172));
                        unit.Children.Add(new TextBlock
                        {
                            Text = (device.IsCharging ? "⚡" : "") + device.BatteryPercent + "%",
                            FontSize = 7.5,
                            FontWeight = FontWeights.SemiBold,
                            FontFamily = new WpfFontFamily("Segoe UI"),
                            Foreground = new SolidColorBrush(battColor),
                            HorizontalAlignment = WpfHorizontalAlignment.Center,
                            Margin = new Thickness(0, 1, 0, 0)
                        });
                    }

                    if (isMobile && device.SupportsInput)
                    {
                        var pay = new WpfButton
                        {
                            Content = "Pay Off",
                            FontSize = 8,
                            Padding = new Thickness(4, 0.5, 4, 0.5),
                            Margin = new Thickness(0, 2, 0, 0),
                            HorizontalAlignment = WpfHorizontalAlignment.Center,
                            Foreground = new SolidColorBrush(WpfColor.FromRgb(251, 207, 232)),
                            Background = WpfBrushes.Transparent,
                            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(219, 92, 156)),
                            BorderThickness = new Thickness(1),
                            Cursor = System.Windows.Input.Cursors.Hand,
                            ToolTip = "Pause phone Accessibility for payments"
                        };
                        pay.Click += (_, _) => TogglePayModeAsync(captured.Id);
                        unit.Children.Add(pay);
                    }

                    rowPanel.Children.Add(unit);
                }

                diagram.Children.Add(rowPanel);
            }

            container.Children.Add(diagram);

            var ecoStrip = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            ecoStrip.Children.Add(CreateQuickPill(
                snap.ClipboardSyncEnabled ? "Clip On" : "Clip Off",
                "Toggle shared clipboard sync",
                ToggleClipboardAsync));
            int dropTargetId = snap.ActiveTargetId != int.MinValue
                ? snap.ActiveTargetId
                : (snap.Devices.FirstOrDefault(d => !d.IsLocal)?.Id ?? 0);
            ecoStrip.Children.Add(CreateQuickPill("File", "Send a file to active device", () => DropFilePathAsync(dropTargetId, null)));
            ecoStrip.Children.Add(CreateQuickPill("⏯", "Play / Pause active device", () => SendMediaAsync(dropTargetId, "play-pause")));
            ecoStrip.Children.Add(CreateQuickPill("🔒", "Lock all linked computers", () => SendPowerAsync(0, "lock-all")));
            string nextProfile = string.Equals(snap.ActiveProfile, "Work", StringComparison.OrdinalIgnoreCase)
                ? "Presentation"
                : (string.Equals(snap.ActiveProfile, "Presentation", StringComparison.OrdinalIgnoreCase) ? "Away" : "Work");
            ecoStrip.Children.Add(CreateQuickPill(
                string.IsNullOrWhiteSpace(snap.ActiveProfile) ? "Work" : snap.ActiveProfile,
                $"Profile: {snap.ActiveProfile} (click to switch to {nextProfile})",
                () => SwitchProfileAsync(nextProfile)));
            container.Children.Add(ecoStrip);
        }

        list.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(WpfColor.FromArgb(165, 25, 41, 70)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(95, 93, 149, 207)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2, 0, 2, 5),
            Padding = new Thickness(7, 5, 7, 5),
            Child = container
        });
    }

    internal static void AddTraySubmenu(Forms.ContextMenuStrip menu, Dispatcher dispatcher)
    {
        var devicesMenu = new Forms.ToolStripMenuItem("DeskTouch Devices");
        devicesMenu.DropDownOpening += (_, _) =>
        {
            devicesMenu.DropDownItems.Clear();
            var snap = FetchLiveSnapshot();
            ApplySnapshot(snap);

            if (!snap.IsLive && snap.ConnectedCount == 0)
            {
                devicesMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("DeskTouch Vision not active")
                {
                    Enabled = false
                });
                return;
            }

            devicesMenu.DropDownItems.Add(new Forms.ToolStripMenuItem(
                $"{snap.ConnectedCount} connected") { Enabled = false });
            devicesMenu.DropDownItems.Add(new Forms.ToolStripSeparator());

            foreach (var device in snap.Devices)
            {
                string countTag = device.NotificationCount >= 0
                    ? $" [{device.NotificationCount}]" : "";
                string label = device.IsLocal
                    ? $"This Computer ({device.Name}){countTag}"
                    : $"{device.Name} ({device.Platform}){countTag}";
                var item = new Forms.ToolStripMenuItem(label)
                {
                    Checked = device.IsControlling || (device.IsSelected && device.IsLocal),
                    Enabled = device.SupportsInput
                };
                var captured = device;
                item.Click += (_, _) => dispatcher.Invoke(() =>
                {
                    if (captured.IsLocal) ReturnLocalAsync();
                    else if (captured.SupportsInput) SwitchDeviceAsync(captured.Id);
                });
                devicesMenu.DropDownItems.Add(item);
            }

            var readyPhone = snap.Devices.FirstOrDefault(
                d => !d.IsLocal && d.Platform == "Android" && d.SupportsInput);
            if (readyPhone != null)
            {
                devicesMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
                devicesMenu.DropDownItems.Add("Pause Phone Accessibility (Payments)", null,
                    (_, _) => dispatcher.Invoke(() => TogglePayModeAsync(readyPhone.Id)));
                devicesMenu.DropDownItems.Add($"View {readyPhone.Name} Notifications...", null,
                    (_, _) => dispatcher.Invoke(() => OpenNotificationsAsync(readyPhone.Id)));
            }

            devicesMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
            devicesMenu.DropDownItems.Add(
                snap.ClipboardSyncEnabled ? "Clipboard Sync: On (Click to Pause)" : "Clipboard Sync: Paused (Click to Resume)",
                null,
                (_, _) => dispatcher.Invoke(ToggleClipboardAsync));
            devicesMenu.DropDownItems.Add("Send File to Active Device...", null,
                (_, _) => dispatcher.Invoke(() => DropFilePathAsync(
                    snap.ActiveTargetId != int.MinValue ? snap.ActiveTargetId : (snap.Devices.FirstOrDefault(d => !d.IsLocal)?.Id ?? 0),
                    null)));
            var profileMenu = new Forms.ToolStripMenuItem($"Profile ({snap.ActiveProfile})");
            foreach (string profName in new[] { "Work", "Presentation", "Away" })
            {
                string capturedProf = profName;
                var pItem = new Forms.ToolStripMenuItem(capturedProf)
                {
                    Checked = string.Equals(snap.ActiveProfile, capturedProf, StringComparison.OrdinalIgnoreCase)
                };
                pItem.Click += (_, _) => dispatcher.Invoke(() => SwitchProfileAsync(capturedProf));
                profileMenu.DropDownItems.Add(pItem);
            }
            devicesMenu.DropDownItems.Add(profileMenu);
            devicesMenu.DropDownItems.Add("Lock All Linked Computers", null,
                (_, _) => dispatcher.Invoke(() => SendPowerAsync(0, "lock-all")));
            devicesMenu.DropDownItems.Add("Wake Linked Computers", null,
                (_, _) => dispatcher.Invoke(() => SendPowerAsync(0, "wake")));

            devicesMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
            if (IsSectionDismissed)
            {
                devicesMenu.DropDownItems.Add("Show Devices Section on Deck", null,
                    (_, _) => dispatcher.Invoke(ResetSectionVisibility));
            }
            devicesMenu.DropDownItems.Add("Open DeskTouch Vision", null,
                (_, _) => dispatcher.Invoke(OpenDeskTouchAsync));
        };
        menu.Items.Add(devicesMenu);
    }

    private static WpfButton CreateQuickPill(string text, string tooltip, Action click)
    {
        var pill = new WpfButton
        {
            Content = text,
            FontSize = 8,
            Padding = new Thickness(4.5, 1, 4.5, 1),
            Margin = new Thickness(1.5, 0, 1.5, 0),
            Foreground = new SolidColorBrush(WpfColor.FromRgb(210, 228, 250)),
            Background = new SolidColorBrush(WpfColor.FromArgb(140, 36, 56, 92)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(140, 82, 132, 198)),
            BorderThickness = new Thickness(0.8),
            Focusable = false,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = tooltip
        };
        pill.Click += (_, _) => click();
        return pill;
    }

    private static string ShortLabel(string name, int maxLen)
    {
        string clean = (name ?? "Device").Trim();
        return clean.Length <= maxLen ? clean : clean.Substring(0, maxLen - 1) + "…";
    }
}

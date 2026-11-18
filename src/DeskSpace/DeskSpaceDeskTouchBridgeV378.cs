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
    string StatusText);

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
    IReadOnlyList<DeskTouchDeviceItemV378> Devices);

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

    internal static event Action? Changed;

    internal static DeskTouchBridgeSnapshotV378 Current
    {
        get { lock (StateLock) return _current; }
    }

    internal static bool HasConnectedDevices => Current.ConnectedCount > 0;

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
                    list.Add(new DeskTouchDeviceItemV378(id, name, platform, isLocal, isSel, isCtrl, supports, notif, status));
                }
            }
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
                Devices: list);
        }
        catch
        {
            return CreateEmpty(live: false);
        }
    }

    private static string BuildSignature(DeskTouchBridgeSnapshotV378 s)
    {
        var sb = new StringBuilder(128);
        sb.Append(s.IsLive ? '1' : '0').Append('|')
            .Append(s.ConnectedCount).Append('|')
            .Append(s.IsRemoteActive ? '1' : '0').Append('|')
            .Append(s.ActiveTargetId).Append('|')
            .Append(s.TotalNotificationCount);
        foreach (var d in s.Devices)
        {
            sb.Append(';').Append(d.Id).Append(':')
                .Append(d.Name).Append(':')
                .Append(d.IsControlling ? '1' : '0')
                .Append(d.IsSelected ? '1' : '0')
                .Append(d.SupportsInput ? '1' : '0').Append(':')
                .Append(d.NotificationCount);
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

    internal static void AddDeckSection(StackPanel list)
    {
        var snap = Current;
        if (!snap.IsLive && snap.ConnectedCount == 0)
            return;

        var container = new StackPanel();
        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 4) };

        var rightAction = new WpfButton
        {
            Content = snap.IsRemoteActive ? "Return PC" : "Open",
            FontSize = 10,
            Padding = new Thickness(6, 2, 6, 2),
            MinWidth = 42,
            Foreground = WpfBrushes.White,
            Background = new SolidColorBrush(snap.IsRemoteActive
                ? WpfColor.FromRgb(14, 116, 144)
                : WpfColor.FromRgb(42, 61, 96)),
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(93, 141, 194)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = snap.IsRemoteActive
                ? "Return mouse & keyboard to this computer"
                : "Open DeskTouch Vision Displays window"
        };
        rightAction.Click += (_, _) =>
        {
            if (snap.IsRemoteActive) ReturnLocalAsync();
            else OpenDeskTouchAsync();
        };
        DockPanel.SetDock(rightAction, Dock.Right);
        header.Children.Add(rightAction);

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

        if (snap.Devices.Count > 0)
        {
            var wrap = new WrapPanel
            {
                HorizontalAlignment = WpfHorizontalAlignment.Left,
                Margin = new Thickness(0, 1, 0, 0)
            };
            foreach (var device in snap.Devices)
            {
                var tile = new StackPanel
                {
                    Margin = new Thickness(0, 2, 5, 3),
                    HorizontalAlignment = WpfHorizontalAlignment.Center
                };

                // Numeric notification count badge directly ABOVE each device switch button.
                bool hasUnread = device.NotificationCount > 0;
                var badge = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(7, 1, 7, 1),
                    Margin = new Thickness(0, 0, 0, 2),
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    Background = new SolidColorBrush(hasUnread
                        ? WpfColor.FromRgb(56, 189, 248)
                        : WpfColor.FromRgb(47, 64, 96)),
                    ToolTip = device.NotificationCount >= 0
                        ? $"{device.NotificationCount} active notifications"
                        : "Notification count unavailable"
                };
                badge.Child = new TextBlock
                {
                    Text = FormatCountBadge(device.NotificationCount),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new WpfFontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(hasUnread
                        ? WpfColor.FromRgb(9, 17, 30)
                        : WpfColor.FromRgb(220, 232, 248)),
                    HorizontalAlignment = WpfHorizontalAlignment.Center
                };
                tile.Children.Add(badge);

                bool active = device.IsControlling || (device.IsSelected && device.IsLocal);
                string shortName = device.IsLocal ? "This PC" : ShortLabel(device.Name, 12);
                string prefix = device.IsLocal ? "" : (device.Platform == "Android" ? "📱 " : "💻 ");

                var btn = new WpfButton
                {
                    Content = prefix + shortName,
                    FontSize = 10.5,
                    FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                    Padding = new Thickness(7, 3, 7, 3),
                    MinWidth = 62,
                    IsEnabled = device.SupportsInput,
                    Cursor = device.SupportsInput ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                    Foreground = WpfBrushes.White,
                    Background = new SolidColorBrush(active
                        ? WpfColor.FromRgb(29, 78, 216)
                        : WpfColor.FromRgb(37, 52, 84)),
                    BorderBrush = new SolidColorBrush(active
                        ? WpfColor.FromRgb(56, 189, 248)
                        : WpfColor.FromRgb(78, 102, 146)),
                    BorderThickness = new Thickness(active ? 1.5 : 1.0),
                    ToolTip = device.IsLocal
                        ? "Return mouse & keyboard to this PC"
                        : $"{device.Name} ({device.Platform}) · {device.StatusText}"
                };
                var captured = device;
                btn.Click += (_, _) =>
                {
                    if (captured.IsLocal) ReturnLocalAsync();
                    else if (captured.SupportsInput) SwitchDeviceAsync(captured.Id);
                };
                tile.Children.Add(btn);

                if (!device.IsLocal && device.Platform == "Android" && device.SupportsInput)
                {
                    var pay = new WpfButton
                    {
                        Content = "Pay Off",
                        FontSize = 9,
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(0, 2, 0, 0),
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        Foreground = new SolidColorBrush(WpfColor.FromRgb(251, 207, 232)),
                        Background = new SolidColorBrush(WpfColor.FromRgb(72, 34, 54)),
                        BorderBrush = new SolidColorBrush(WpfColor.FromRgb(219, 92, 156)),
                        BorderThickness = new Thickness(1),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        ToolTip = "Pause phone Accessibility for payments"
                    };
                    pay.Click += (_, _) => TogglePayModeAsync(captured.Id);
                    tile.Children.Add(pay);
                }

                wrap.Children.Add(tile);
            }
            container.Children.Add(wrap);
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
            }

            devicesMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
            devicesMenu.DropDownItems.Add("Open DeskTouch Vision", null,
                (_, _) => dispatcher.Invoke(OpenDeskTouchAsync));
        };
        menu.Items.Add(devicesMenu);
    }

    private static string ShortLabel(string name, int maxLen)
    {
        string clean = (name ?? "Device").Trim();
        return clean.Length <= maxLen ? clean : clean.Substring(0, maxLen - 1) + "…";
    }
}

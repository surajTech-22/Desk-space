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
    int ArrangementOrder = 0);

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

internal sealed record DeckDevicePlacementV378(
    DeskTouchDeviceItemV378 Device,
    double X,
    double Y,
    double Width,
    double Height,
    double UnitX,
    double UnitTopY,
    double UnitWidth,
    double UnitHeight);

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
                    list.Add(new DeskTouchDeviceItemV378(id, name, platform, isLocal, isSel, isCtrl, supports, notif, status, lx, ly, lw, lh, order));
                    fallbackIdx++;
                }
                list.Sort((a, b) => a.ArrangementOrder.CompareTo(b.ArrangementOrder));
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
        var sb = new StringBuilder(160);
        sb.Append(s.IsLive ? '1' : '0').Append('|')
            .Append(s.ConnectedCount).Append('|')
            .Append(s.IsRemoteActive ? '1' : '0').Append('|')
            .Append(s.ActiveTargetId).Append('|')
            .Append(s.TotalNotificationCount);
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

    internal static IReadOnlyList<DeckDevicePlacementV378> ComputeDeckPlacements(
        IReadOnlyList<DeskTouchDeviceItemV378> devices,
        double canvasWidth,
        out double canvasHeight)
    {
        canvasHeight = 0;
        if (devices == null || devices.Count == 0)
            return Array.Empty<DeckDevicePlacementV378>();

        const double padX = 8.0;
        const double padTop = 6.0;
        const double padBottom = 6.0;
        const double badgeHeadroom = 20.0;
        const double payFootroom = 18.0;
        const double minGap = 6.0;

        double usableW = Math.Max(140.0, canvasWidth - (2.0 * padX));
        bool hasNormalized = devices.All(d => d.LayoutW > 0.001 && d.LayoutH > 0.001);

        var items = new DeskTouchDeviceItemV378[devices.Count];
        var rawRects = new Rect[devices.Count];
        for (int i = 0; i < devices.Count; i++)
        {
            items[i] = devices[i];
            if (hasNormalized)
            {
                rawRects[i] = new Rect(items[i].LayoutX, items[i].LayoutY, items[i].LayoutW, items[i].LayoutH);
            }
            else
            {
                bool isMobile = string.Equals(items[i].Platform, "Android", StringComparison.OrdinalIgnoreCase);
                double rw = isMobile ? 0.19 : 0.34;
                double rh = isMobile ? 0.32 : 0.22;
                double rx = i * (rw + 0.06);
                double ry = (0.34 - rh) * 0.5;
                rawRects[i] = new Rect(rx, Math.Max(0, ry), rw, rh);
            }
        }

        // Preserve stable spatial ordering from DeskTouch arrangement metadata.
        var orderIdx = Enumerable.Range(0, items.Length)
            .OrderBy(i => items[i].ArrangementOrder)
            .ThenBy(i => Math.Round(rawRects[i].Y, 2))
            .ThenBy(i => rawRects[i].X)
            .ToArray();

        double minX = rawRects.Min(r => r.Left);
        double maxX = rawRects.Max(r => r.Right);
        double minY = rawRects.Min(r => r.Top);
        double maxY = rawRects.Max(r => r.Bottom);
        double spanX = Math.Max(0.05, maxX - minX);
        double spanY = Math.Max(0.05, maxY - minY);

        // Scale proportionally to canvas width while keeping vertical stacks compact.
        double scale = usableW / spanX;
        if (spanY * scale > 130.0)
            scale = 130.0 / spanY;

        int n = orderIdx.Length;
        var sortedItems = new DeskTouchDeviceItemV378[n];
        var bx = new double[n];
        var by = new double[n];
        var bw = new double[n];
        var bh = new double[n];
        var hasPay = new bool[n];
        var normCx = new double[n];
        var normCy = new double[n];

        for (int k = 0; k < n; k++)
        {
            int src = orderIdx[k];
            var dev = items[src];
            var nr = rawRects[src];
            sortedItems[k] = dev;

            bool isMobile = !dev.IsLocal && string.Equals(dev.Platform, "Android", StringComparison.OrdinalIgnoreCase);
            hasPay[k] = isMobile && dev.SupportsInput;

            double minTileW = isMobile ? 32.0 : 48.0;
            double maxTileW = isMobile ? 46.0 : 88.0;
            double minTileH = isMobile ? 44.0 : 32.0;
            double maxTileH = isMobile ? 64.0 : 54.0;

            double w = Math.Clamp(nr.Width * scale, minTileW, maxTileW);
            double h = Math.Clamp(nr.Height * scale, minTileH, maxTileH);
            if (isMobile && h < w + 8.0)
                h = Math.Clamp(w + 12.0, minTileH, maxTileH);

            double cx = (nr.Left + (nr.Width * 0.5) - minX) * scale;
            double cy = (nr.Top + (nr.Height * 0.5) - minY) * scale;
            normCx[k] = nr.Left + (nr.Width * 0.5);
            normCy[k] = nr.Top + (nr.Height * 0.5);

            bw[k] = w;
            bh[k] = h;
            bx[k] = cx - (w * 0.5);
            by[k] = cy - (h * 0.5);
        }

        // Resolve overlaps iteratively without inverting relative left/right or top/bottom order.
        for (int pass = 0; pass < 4; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                double iLeft = bx[i] - Math.Max(0, (HasPayWidth(hasPay[i], bw[i]) - bw[i]) * 0.5);
                double iRight = iLeft + HasPayWidth(hasPay[i], bw[i]);
                double iTop = by[i] - badgeHeadroom;
                double iBottom = by[i] + bh[i] + (hasPay[i] ? payFootroom : 0.0);

                for (int j = i + 1; j < n; j++)
                {
                    double jUnitW = HasPayWidth(hasPay[j], bw[j]);
                    double jLeft = bx[j] - Math.Max(0, (jUnitW - bw[j]) * 0.5);
                    double jRight = jLeft + jUnitW;
                    double jTop = by[j] - badgeHeadroom;
                    double jBottom = by[j] + bh[j] + (hasPay[j] ? payFootroom : 0.0);

                    bool overlapX = jLeft < iRight + minGap && iLeft < jRight + minGap;
                    bool overlapY = jTop < iBottom + minGap && iTop < jBottom + minGap;
                    if (!overlapX || !overlapY)
                        continue;

                    double ndx = Math.Abs(normCx[j] - normCx[i]);
                    double ndy = Math.Abs(normCy[j] - normCy[i]);
                    if (ndx >= ndy)
                    {
                        double shift = (iRight + minGap) - jLeft;
                        bx[j] += shift;
                    }
                    else
                    {
                        double shift = (iBottom + minGap) - jTop;
                        by[j] += shift;
                    }
                }
            }
        }

        // Normalize coordinates to fit inside [padX, canvasWidth - padX] centered nicely.
        double totalMinX = double.MaxValue;
        double totalMaxX = double.MinValue;
        for (int i = 0; i < n; i++)
        {
            double uw = HasPayWidth(hasPay[i], bw[i]);
            double uLeft = bx[i] - Math.Max(0, (uw - bw[i]) * 0.5);
            if (uLeft < totalMinX) totalMinX = uLeft;
            if (uLeft + uw > totalMaxX) totalMaxX = uLeft + uw;
        }
        double curSpanW = Math.Max(1.0, totalMaxX - totalMinX);
        if (curSpanW > usableW)
        {
            double fitScale = usableW / curSpanW;
            for (int i = 0; i < n; i++)
            {
                double uw = HasPayWidth(hasPay[i], bw[i]);
                double uLeft = bx[i] - Math.Max(0, (uw - bw[i]) * 0.5);
                double centerOffset = (uLeft - totalMinX) * fitScale;
                bw[i] = Math.Max(26.0, bw[i] * fitScale);
                double nextUw = HasPayWidth(hasPay[i], bw[i]);
                bx[i] = centerOffset + Math.Max(0, (nextUw - bw[i]) * 0.5);
            }
            totalMinX = 0;
            totalMaxX = usableW;
        }

        double offsetX = padX + Math.Max(0.0, (usableW - (totalMaxX - totalMinX)) * 0.5) - totalMinX;
        double minUnitTop = Enumerable.Range(0, n).Min(i => by[i] - badgeHeadroom);
        double offsetY = padTop - minUnitTop;

        double maxBottom = 0;
        var result = new List<DeckDevicePlacementV378>(n);
        for (int i = 0; i < n; i++)
        {
            double tx = Math.Round(bx[i] + offsetX, 1);
            double ty = Math.Round(by[i] + offsetY, 1);
            double uw = Math.Round(HasPayWidth(hasPay[i], bw[i]), 1);
            double ux = Math.Round(tx - Math.Max(0, (uw - bw[i]) * 0.5), 1);
            double utop = Math.Round(ty - badgeHeadroom, 1);
            double uh = Math.Round(badgeHeadroom + bh[i] + (hasPay[i] ? payFootroom : 0.0), 1);
            if (utop + uh > maxBottom) maxBottom = utop + uh;

            result.Add(new DeckDevicePlacementV378(
                sortedItems[i],
                tx,
                ty,
                Math.Round(bw[i], 1),
                Math.Round(bh[i], 1),
                ux,
                utop,
                uw,
                uh));
        }

        canvasHeight = Math.Round(maxBottom + padBottom, 1);
        return result;
    }

    private static double HasPayWidth(bool hasPay, double tileWidth) =>
        Math.Max(tileWidth, hasPay ? 46.0 : 30.0);

    private static FrameworkElement CreateDeviceLogo(bool isMobile, double tileWidth, double tileHeight)
    {
        var white = new SolidColorBrush(WpfColor.FromRgb(241, 246, 255));
        if (isMobile)
        {
            var body = new Border
            {
                Width = 12,
                Height = 19,
                CornerRadius = new CornerRadius(2.4),
                BorderThickness = new Thickness(1.6),
                BorderBrush = white,
                Background = new SolidColorBrush(WpfColor.FromArgb(48, 255, 255, 255)),
                SnapsToDevicePixels = true
            };
            var inner = new Grid();
            inner.Children.Add(new Border
            {
                Width = 4.6,
                Height = 1.4,
                CornerRadius = new CornerRadius(0.7),
                Background = white,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1.8, 0, 0)
            });
            inner.Children.Add(new Border
            {
                Width = 5.4,
                Height = 1.3,
                CornerRadius = new CornerRadius(0.65),
                Background = new SolidColorBrush(WpfColor.FromArgb(205, 241, 246, 255)),
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1.8)
            });
            body.Child = inner;
            return new Viewbox
            {
                Stretch = Stretch.Uniform,
                MaxWidth = Math.Max(10.0, tileWidth - 10.0),
                MaxHeight = Math.Max(14.0, tileHeight - 10.0),
                Child = body
            };
        }

        var monitor = new StackPanel
        {
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        monitor.Children.Add(new Border
        {
            Width = 20,
            Height = 13,
            CornerRadius = new CornerRadius(2),
            BorderThickness = new Thickness(1.7),
            BorderBrush = white,
            Background = new SolidColorBrush(WpfColor.FromArgb(44, 255, 255, 255)),
            SnapsToDevicePixels = true
        });
        monitor.Children.Add(new Border
        {
            Width = 3,
            Height = 2.4,
            Background = white,
            HorizontalAlignment = WpfHorizontalAlignment.Center
        });
        monitor.Children.Add(new Border
        {
            Width = 10,
            Height = 1.8,
            CornerRadius = new CornerRadius(0.9),
            Background = white,
            HorizontalAlignment = WpfHorizontalAlignment.Center
        });
        return new Viewbox
        {
            Stretch = Stretch.Uniform,
            MaxWidth = Math.Max(14.0, tileWidth - 10.0),
            MaxHeight = Math.Max(12.0, tileHeight - 8.0),
            Child = monitor
        };
    }

    internal static void AddDeckSection(StackPanel list)
    {
        var snap = Current;
        if (!snap.IsLive && snap.ConnectedCount == 0)
            return;

        var container = new StackPanel();
        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 5) };

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
            const double arrangementWidth = 258.0;
            var placements = ComputeDeckPlacements(snap.Devices, arrangementWidth, out double arrangementHeight);
            var canvas = new Canvas
            {
                Width = arrangementWidth,
                Height = Math.Max(64.0, arrangementHeight),
                ClipToBounds = false
            };

            foreach (var p in placements)
            {
                var device = p.Device;
                bool active = device.IsControlling || (device.IsSelected && device.IsLocal);
                bool isMobile = !device.IsLocal && string.Equals(device.Platform, "Android", StringComparison.OrdinalIgnoreCase);

                var unit = new StackPanel
                {
                    Width = p.UnitWidth,
                    HorizontalAlignment = WpfHorizontalAlignment.Center
                };

                bool hasUnread = device.NotificationCount > 0;
                var badge = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(0, 0, 0, 3),
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
                    FontSize = 9.0,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new WpfFontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(hasUnread
                        ? WpfColor.FromRgb(9, 17, 30)
                        : WpfColor.FromRgb(220, 232, 248)),
                    HorizontalAlignment = WpfHorizontalAlignment.Center
                };
                unit.Children.Add(badge);

                var fillColor = device.IsLocal
                    ? WpfColor.FromRgb(66, 119, 203)
                    : WpfColor.FromRgb(54, 136, 131);
                var borderColor = active
                    ? WpfColor.FromRgb(56, 189, 248)
                    : (device.IsLocal ? WpfColor.FromRgb(126, 174, 245) : WpfColor.FromRgb(106, 179, 175));

                var btn = new WpfButton
                {
                    Width = p.Width,
                    Height = p.Height,
                    MinWidth = p.Width,
                    MinHeight = p.Height,
                    Padding = new Thickness(0),
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    IsEnabled = device.SupportsInput,
                    Cursor = device.SupportsInput ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                    Background = new SolidColorBrush(fillColor),
                    BorderBrush = new SolidColorBrush(borderColor),
                    BorderThickness = new Thickness(active ? 1.8 : 1.0),
                    ToolTip = device.IsLocal
                        ? $"Return mouse & keyboard to this computer ({device.Name})"
                        : $"{device.Name} ({device.Platform}) · {device.StatusText}",
                    Content = CreateDeviceLogo(isMobile, p.Width, p.Height)
                };
                var captured = device;
                btn.Click += (_, _) =>
                {
                    if (captured.IsLocal) ReturnLocalAsync();
                    else if (captured.SupportsInput) SwitchDeviceAsync(captured.Id);
                };
                unit.Children.Add(btn);

                if (isMobile && device.SupportsInput)
                {
                    var pay = new WpfButton
                    {
                        Content = "Pay Off",
                        FontSize = 8.5,
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(0, 3, 0, 0),
                        HorizontalAlignment = WpfHorizontalAlignment.Center,
                        Foreground = new SolidColorBrush(WpfColor.FromRgb(251, 207, 232)),
                        Background = new SolidColorBrush(WpfColor.FromRgb(72, 34, 54)),
                        BorderBrush = new SolidColorBrush(WpfColor.FromRgb(219, 92, 156)),
                        BorderThickness = new Thickness(1),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        ToolTip = "Pause phone Accessibility for payments"
                    };
                    pay.Click += (_, _) => TogglePayModeAsync(captured.Id);
                    unit.Children.Add(pay);
                }

                Canvas.SetLeft(unit, p.UnitX);
                Canvas.SetTop(unit, p.UnitTopY);
                canvas.Children.Add(unit);
            }

            container.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(WpfColor.FromRgb(22, 25, 31)),
                BorderBrush = new SolidColorBrush(WpfColor.FromRgb(42, 49, 61)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2),
                Child = canvas
            });
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

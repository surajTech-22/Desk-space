using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DeskSpace;

internal sealed record PaletteCommandV380(
    string Id,
    string Title,
    string Subtitle,
    string Category,
    string Shortcut,
    Action<Window?> Execute);

internal sealed record CommandSearchHit(
    string Category,
    string Title,
    string Subtitle,
    string Badge,
    int Score,
    Action<Window?> Run,
    string? CopyValue = null);

internal sealed class DeskSpaceGlobalHotkeysV380 : IDisposable
{
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int WM_HOTKEY = 0x0312;

    internal const int HotkeyAltSpaceId = 0x4454;
    internal const int HotkeyCommandBarId = 0x4455;
    internal const int HotkeySnipId = 0x4456;
    internal const int HotkeyTopmostId = 0x4457;
    internal const int HotkeySnapZonesId = 0x4458;

    private readonly Window _owner;
    private readonly Action _openCommandBar;
    private readonly Action _startSnip;
    private readonly Action _toggleTopmost;
    private readonly Action _showSnapZones;
    private readonly HashSet<int> _registeredIds = new();
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private bool _disposed;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal DeskSpaceGlobalHotkeysV380(
        Window owner,
        Action openCommandBar,
        Action startSnip,
        Action toggleTopmost,
        Action showSnapZones)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _openCommandBar = openCommandBar ?? throw new ArgumentNullException(nameof(openCommandBar));
        _startSnip = startSnip ?? throw new ArgumentNullException(nameof(startSnip));
        _toggleTopmost = toggleTopmost ?? throw new ArgumentNullException(nameof(toggleTopmost));
        _showSnapZones = showSnapZones ?? throw new ArgumentNullException(nameof(showSnapZones));

        _owner.Closed += OnOwnerClosed;

        IntPtr handle = new WindowInteropHelper(_owner).Handle;
        if (handle != IntPtr.Zero)
            AttachAndRegister(handle);
        else
            _owner.SourceInitialized += OnOwnerSourceInitialized;
    }

    private void OnOwnerSourceInitialized(object? sender, EventArgs e)
    {
        _owner.SourceInitialized -= OnOwnerSourceInitialized;
        if (_disposed)
            return;
        IntPtr handle = new WindowInteropHelper(_owner).Handle;
        if (handle != IntPtr.Zero)
            AttachAndRegister(handle);
    }

    private void OnOwnerClosed(object? sender, EventArgs e) => Dispose();

    private void AttachAndRegister(IntPtr hwnd)
    {
        if (_disposed || _hwnd != IntPtr.Zero)
            return;

        _hwnd = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);

        TryRegister(HotkeyAltSpaceId, MOD_ALT | MOD_NOREPEAT, 0x20);                 // Alt+Space (graceful if taken)
        TryRegister(HotkeyCommandBarId, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 0x50); // Ctrl+Shift+P
        TryRegister(HotkeySnipId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x45);         // Ctrl+Alt+E
        TryRegister(HotkeyTopmostId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x54);      // Ctrl+Alt+T
        TryRegister(HotkeySnapZonesId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x5A);    // Ctrl+Alt+Z
    }

    private void TryRegister(int id, uint mods, uint vk)
    {
        try
        {
            if (RegisterHotKey(_hwnd, id, mods, vk))
                _registeredIds.Add(id);
        }
        catch
        {
            // Ignore registration failures when OS or another utility already holds a hotkey.
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && !_disposed)
        {
            int id = wParam.ToInt32();
            try
            {
                switch (id)
                {
                    case HotkeyAltSpaceId:
                    case HotkeyCommandBarId:
                        _openCommandBar();
                        handled = true;
                        break;
                    case HotkeySnipId:
                        _startSnip();
                        handled = true;
                        break;
                    case HotkeyTopmostId:
                        _toggleTopmost();
                        handled = true;
                        break;
                    case HotkeySnapZonesId:
                        _showSnapZones();
                        handled = true;
                        break;
                }
            }
            catch
            {
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _owner.SourceInitialized -= OnOwnerSourceInitialized;
        _owner.Closed -= OnOwnerClosed;

        if (_hwndSource != null)
        {
            try { _hwndSource.RemoveHook(WndProc); } catch { }
            _hwndSource = null;
        }

        if (_hwnd != IntPtr.Zero)
        {
            foreach (int id in _registeredIds)
            {
                try { UnregisterHotKey(_hwnd, id); } catch { }
            }
        }
        _registeredIds.Clear();
    }
}

internal static class DeskSpaceCommandBarV380
{
    private static readonly object RegistryLock = new();
    private static readonly List<PaletteCommandV380> ExtraCommands = new();
    private static Window? _activeBar;

    private static readonly (string Name, string Uri)[] SystemSettingsShortcuts =
    {
        ("Display Settings", "ms-settings:display"),
        ("Sound Settings", "ms-settings:sound"),
        ("Bluetooth & Devices", "ms-settings:bluetooth"),
        ("Network & Internet", "ms-settings:network"),
        ("Wi-Fi Settings", "ms-settings:network-wifi"),
        ("Windows Update", "ms-settings:windowsupdate"),
        ("Power & Battery", "ms-settings:powersleep"),
        ("Task Manager", "taskmgr.exe")
    };

    private static readonly Regex ReverseGstRegex = new(
        @"^([0-9][0-9,]*(?:\.[0-9]+)?)\s+(?:excl|excluding|ex)\s+(?:([0-9]+(?:\.[0-9]+)?)\s*%?\s*gst|gst\s*([0-9]+(?:\.[0-9]+)?)\s*%?)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ForwardGstRegex = new(
        @"^([0-9][0-9,]*(?:\.[0-9]+)?)\s*(?:(?:\+|plus)?\s*([0-9]+(?:\.[0-9]+)?)\s*%?\s*gst|(?:\+|plus)?\s*gst\s*([0-9]+(?:\.[0-9]+)?)\s*%?)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PercentOpRegex = new(
        @"^([0-9][0-9,]*(?:\.[0-9]+)?)\s*([+\-*×/÷])\s*([0-9]+(?:\.[0-9]+)?)\s*%$",
        RegexOptions.Compiled);

    private static readonly Regex PercentOfRegex = new(
        @"^([0-9]+(?:\.[0-9]+)?)\s*%\s+of\s+([0-9][0-9,]*(?:\.[0-9]+)?)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DaysBetweenRegex = new(
        @"^days\s+(?:between|from)\s+(.+?)\s+(?:and|to)\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DateOffsetRegex = new(
        @"^(today|now|tomorrow|yesterday|\d{4}-\d{1,2}-\d{1,2})\s*([+-])\s*(\d+)\s*(d|day|days|w|wk|wks|week|weeks|m|mo|mos|month|months|y|yr|yrs|year|years)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BasePrefixRegex = new(
        @"^(hex|bin|oct|dec)\s+([+-]?(?:0x[0-9a-f]+|0b[01]+|0o[0-7]+|[0-9]+))$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BaseConvertRegex = new(
        @"^([+-]?(?:0x[0-9a-f]+|0b[01]+|0o[0-7]+|[0-9]+))\s+(?:to|in)\s+(hex|bin|binary|oct|octal|dec|decimal)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UnitConvertRegex = new(
        @"^([+-]?[0-9][0-9,]*(?:\.[0-9]+)?)\s*°?\s*([a-zA-Z]+)\s+(?:to|in)\s+°?\s*([a-zA-Z]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static void RegisterExtraCommands(IEnumerable<PaletteCommandV380> commands)
    {
        if (commands == null)
            return;

        lock (RegistryLock)
        {
            foreach (var cmd in commands)
            {
                if (cmd == null || string.IsNullOrWhiteSpace(cmd.Id))
                    continue;
                int existing = ExtraCommands.FindIndex(c => string.Equals(c.Id, cmd.Id, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0)
                    ExtraCommands[existing] = cmd;
                else
                    ExtraCommands.Add(cmd);
            }
        }
    }

    private static List<PaletteCommandV380> BuildAllCommands(Func<Workspace>? workspaceProvider)
    {
        var list = new List<PaletteCommandV380>
        {
            new("v380.clipboard", "Rich Clipboard History", "Browse text, images, colors, files & smart transforms", "Tools", "Ctrl+Shift+V",
                owner => DeskSpaceRichClipboardV380.Show(owner)),
            new("v380.snip", "Screen Snip & Table OCR", "Capture screen region to clipboard, OCR text, Excel TSV, or pin", "Screen", "Ctrl+Alt+E",
                owner => DeskSpaceScreenToolsV380.StartRegionSnip(owner, workspaceProvider, null)),
            new("v380.eyedropper", "Color Eyedropper", "Pick #RRGGBB, rgb(), and hsl() from anywhere on screen", "Screen", "",
                owner => DeskSpaceScreenToolsV380.StartEyedropper(owner)),
            new("v380.focus", "Focus Studio (Pomodoro)", "25/5, 50/10, 90/15 sessions with live donut ring & streak", "Focus", "",
                owner => DeskSpaceFocusRingV380.ShowStudio(owner)),
            new("v380.focus.start25", "Focus: Start 25m Deep Work", "Begin a 25-minute focus timer immediately", "Focus", "",
                _ => DeskSpaceFocusRingV380.StartFocus(25, "Deep Work")),
            new("v380.focus.pause", "Focus: Pause / Resume", "Toggle active Pomodoro timer", "Focus", "",
                _ => DeskSpaceFocusRingV380.TogglePauseResume()),
            new("v380.backdrop.mica", "Backdrop: Windows 11 Mica", "Use subtle Mica backdrop for DeskSpace windows", "Theme", "",
                _ => DeskSpaceFluentBackdropV380.SetBackdropPreference("Mica")),
            new("v380.backdrop.micaalt", "Backdrop: Mica Alt (Tabbed)", "Use deeper Mica Alt material for DeskSpace windows", "Theme", "",
                _ => DeskSpaceFluentBackdropV380.SetBackdropPreference("MicaAlt")),
            new("v380.backdrop.acrylic", "Backdrop: Acrylic", "Use translucent Acrylic backdrop for DeskSpace windows", "Theme", "",
                _ => DeskSpaceFluentBackdropV380.SetBackdropPreference("Acrylic")),
            new("v380.backdrop.solid", "Backdrop: Solid", "Use clean solid background without translucency", "Theme", "",
                _ => DeskSpaceFluentBackdropV380.SetBackdropPreference("Solid")),
            new("v380.search", "Universal Search", "Search files across drives, apps, settings, and notes", "Search", "",
                owner =>
                {
                    var search = new UniversalSearchWindow(workspaceProvider ?? (() => new WorkspaceStore().Load()), WidgetSettingsV3.Load());
                    if (owner != null && owner.IsLoaded && owner.IsVisible)
                    {
                        search.Owner = owner;
                        search.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }
                    search.Show();
                }),
            new("v380.settings", "Donut & Appearance Settings", "Open DeskSpace Control Center & Theme Studio", "Settings", "",
                owner =>
                {
                    var settings = WidgetSettingsV3.Load();
                    DeskSpaceSettingsStudioV148.Show(
                        owner ?? System.Windows.Application.Current?.MainWindow!,
                        settings,
                        () => { settings.Save(); settings.ApplyWindowsStartup(); DeskSpaceFluentBackdropV380.ReapplyLiveTheme(); },
                        () => new WidgetOptionsWindow(settings, () => { settings.Save(); settings.ApplyWindowsStartup(); }).Show());
                }),
            new("v380.desktouch", "Open DeskTouch Vision", "Launch or focus paired devices bridge", "Devices", "",
                _ => DeskSpaceDeskTouchBridgeV378.OpenDeskTouchAsync())
        };

        lock (RegistryLock)
        {
            foreach (var extra in ExtraCommands)
            {
                if (!list.Any(c => string.Equals(c.Id, extra.Id, StringComparison.OrdinalIgnoreCase)))
                    list.Add(extra);
            }
        }

        return list;
    }

    internal static bool TryEvaluateExpression(string? query, out string result, out string detail)
    {
        result = string.Empty;
        detail = string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return false;

        string q = query.Trim();
        if (q.StartsWith('=') && q.Length > 1)
            q = q.Substring(1).Trim();
        if (q.Length == 0 || q.Length > 160)
            return false;

        if (TryEvalGst(q, out result, out detail))
            return true;
        if (TryEvalPercentOp(q, out result, out detail))
            return true;
        if (TryEvalDateMath(q, out result, out detail))
            return true;
        if (TryEvalBaseConversion(q, out result, out detail))
            return true;
        if (TryEvalUnitConversion(q, out result, out detail))
            return true;
        if (TryEvalArithmetic(q, out result, out detail))
            return true;

        return false;
    }

    private static bool TryEvalGst(string q, out string result, out string detail)
    {
        result = string.Empty;
        detail = string.Empty;

        var rev = ReverseGstRegex.Match(q);
        if (rev.Success)
        {
            string totalRaw = rev.Groups[1].Value.Replace(",", "");
            string rateRaw = rev.Groups[2].Success ? rev.Groups[2].Value : rev.Groups[3].Value;
            if (decimal.TryParse(totalRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal total) &&
                decimal.TryParse(rateRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal rate) &&
                rate >= 0 && rate <= 1000 && total >= 0)
            {
                decimal divisor = 1m + (rate / 100m);
                decimal baseVal = Math.Round(total / divisor, 2, MidpointRounding.AwayFromZero);
                decimal gst = total - baseVal;
                result = baseVal.ToString("F2", CultureInfo.InvariantCulture);
                detail = $"Base {baseVal.ToString("N2", CultureInfo.InvariantCulture)} + {FormatCleanDecimal(rate)}% GST {gst.ToString("N2", CultureInfo.InvariantCulture)}";
                return true;
            }
        }

        var fwd = ForwardGstRegex.Match(q);
        if (fwd.Success)
        {
            string baseRaw = fwd.Groups[1].Value.Replace(",", "");
            string rateRaw = fwd.Groups[2].Success ? fwd.Groups[2].Value : fwd.Groups[3].Value;
            if (decimal.TryParse(baseRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal baseVal) &&
                decimal.TryParse(rateRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal rate) &&
                rate >= 0 && rate <= 1000 && baseVal >= 0)
            {
                decimal gst = Math.Round(baseVal * rate / 100m, 2, MidpointRounding.AwayFromZero);
                decimal total = baseVal + gst;
                result = total.ToString("F2", CultureInfo.InvariantCulture);
                detail = $"{baseVal.ToString("N2", CultureInfo.InvariantCulture)} + {FormatCleanDecimal(rate)}% GST ({gst.ToString("N2", CultureInfo.InvariantCulture)}) = {total.ToString("N2", CultureInfo.InvariantCulture)}";
                return true;
            }
        }

        return false;
    }

    private static bool TryEvalPercentOp(string q, out string result, out string detail)
    {
        result = string.Empty;
        detail = string.Empty;

        var m = PercentOpRegex.Match(q);
        if (m.Success)
        {
            string lhsRaw = m.Groups[1].Value.Replace(",", "");
            string op = m.Groups[2].Value;
            string pctRaw = m.Groups[3].Value;
            if (decimal.TryParse(lhsRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal lhs) &&
                decimal.TryParse(pctRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal pct))
            {
                decimal portion = lhs * pct / 100m;
                decimal value;
                switch (op)
                {
                    case "+":
                        value = lhs + portion;
                        detail = $"{FormatNumberGrouped(lhs)} + {FormatCleanDecimal(pct)}% ({FormatNumberGrouped(portion)}) = {FormatNumberGrouped(value)}";
                        break;
                    case "-":
                        value = lhs - portion;
                        detail = $"{FormatNumberGrouped(lhs)} - {FormatCleanDecimal(pct)}% ({FormatNumberGrouped(portion)}) = {FormatNumberGrouped(value)}";
                        break;
                    case "*":
                    case "×":
                        value = portion;
                        detail = $"{FormatNumberGrouped(lhs)} × {FormatCleanDecimal(pct)}% = {FormatNumberGrouped(value)}";
                        break;
                    case "/":
                    case "÷":
                        if (pct == 0m) return false;
                        value = lhs / (pct / 100m);
                        detail = $"{FormatNumberGrouped(lhs)} ÷ {FormatCleanDecimal(pct)}% = {FormatNumberGrouped(value)}";
                        break;
                    default:
                        return false;
                }

                result = FormatCleanDecimal(value);
                return true;
            }
        }

        var ofMatch = PercentOfRegex.Match(q);
        if (ofMatch.Success)
        {
            if (decimal.TryParse(ofMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal pct) &&
                decimal.TryParse(ofMatch.Groups[2].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal rhs))
            {
                decimal value = rhs * pct / 100m;
                result = FormatCleanDecimal(value);
                detail = $"{FormatCleanDecimal(pct)}% of {FormatNumberGrouped(rhs)} = {FormatNumberGrouped(value)}";
                return true;
            }
        }

        return false;
    }

    private static bool TryEvalDateMath(string q, out string result, out string detail)
    {
        result = string.Empty;
        detail = string.Empty;

        var between = DaysBetweenRegex.Match(q);
        if (between.Success)
        {
            if (TryParseDateToken(between.Groups[1].Value, out DateTime d1) &&
                TryParseDateToken(between.Groups[2].Value, out DateTime d2))
            {
                int days = Math.Abs((d2.Date - d1.Date).Days);
                int weeks = days / 7;
                int remDays = days % 7;
                result = $"{days} days";
                detail = $"{d1:yyyy-MM-dd} to {d2:yyyy-MM-dd} · {days:N0} days ({weeks}w {remDays}d)";
                return true;
            }
        }

        var offset = DateOffsetRegex.Match(q);
        if (offset.Success)
        {
            if (TryParseDateToken(offset.Groups[1].Value, out DateTime baseDate) &&
                int.TryParse(offset.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
            {
                int sign = offset.Groups[2].Value == "-" ? -1 : 1;
                int delta = sign * amount;
                string unit = offset.Groups[4].Value.ToLowerInvariant();

                DateTime target;
                try
                {
                    target = unit[0] switch
                    {
                        'd' => baseDate.AddDays(delta),
                        'w' => baseDate.AddDays(delta * 7),
                        'm' => baseDate.AddMonths(delta),
                        'y' => baseDate.AddYears(delta),
                        _ => baseDate
                    };
                }
                catch
                {
                    return false;
                }

                result = target.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                detail = $"{baseDate:yyyy-MM-dd} {offset.Groups[2].Value} {amount}{unit} → {target:dddd, dd MMM yyyy}";
                return true;
            }
        }

        return false;
    }

    private static bool TryParseDateToken(string raw, out DateTime dt)
    {
        string s = raw.Trim().ToLowerInvariant();
        if (s is "today" or "now")
        {
            dt = DateTime.Today;
            return true;
        }
        if (s == "tomorrow")
        {
            dt = DateTime.Today.AddDays(1);
            return true;
        }
        if (s == "yesterday")
        {
            dt = DateTime.Today.AddDays(-1);
            return true;
        }

        return DateTime.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dt) ||
               DateTime.TryParse(raw.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt);
    }

    private static bool TryEvalBaseConversion(string q, out string result, out string detail)
    {
        result = string.Empty;
        detail = string.Empty;

        var prefixMatch = BasePrefixRegex.Match(q);
        if (prefixMatch.Success)
        {
            string target = NormalizeBaseName(prefixMatch.Groups[1].Value);
            if (TryParseIntegerToken(prefixMatch.Groups[2].Value, out long val))
            {
                return FormatBaseResult(val, target, out result, out detail);
            }
        }

        var convMatch = BaseConvertRegex.Match(q);
        if (convMatch.Success)
        {
            string target = NormalizeBaseName(convMatch.Groups[2].Value);
            if (TryParseIntegerToken(convMatch.Groups[1].Value, out long val))
            {
                return FormatBaseResult(val, target, out result, out detail);
            }
        }

        // Direct 0xFF or 0b101010 literal
        if (q.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && q.Length > 2 &&
            TryParseIntegerToken(q, out long hexVal))
        {
            return FormatBaseResult(hexVal, "dec", out result, out detail);
        }
        if (q.StartsWith("0b", StringComparison.OrdinalIgnoreCase) && q.Length > 2 &&
            TryParseIntegerToken(q, out long binVal))
        {
            return FormatBaseResult(binVal, "dec", out result, out detail);
        }

        return false;
    }

    private static string NormalizeBaseName(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "hex" or "hexadecimal" => "hex",
        "bin" or "binary" => "bin",
        "oct" or "octal" => "oct",
        _ => "dec"
    };

    private static bool TryParseIntegerToken(string raw, out long value)
    {
        value = 0;
        string s = raw.Trim();
        bool neg = s.StartsWith('-');
        if (neg || s.StartsWith('+'))
            s = s.Substring(1);

        try
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                ulong u = ulong.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                value = neg ? -unchecked((long)u) : unchecked((long)u);
                return true;
            }
            if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                ulong u = Convert.ToUInt64(s.Substring(2), 2);
                value = neg ? -unchecked((long)u) : unchecked((long)u);
                return true;
            }
            if (s.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
            {
                ulong u = Convert.ToUInt64(s.Substring(2), 8);
                value = neg ? -unchecked((long)u) : unchecked((long)u);
                return true;
            }
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long d))
            {
                value = neg ? -d : d;
                return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    private static bool FormatBaseResult(long val, string targetBase, out string result, out string detail)
    {
        string hexStr = val < 0 ? "-0x" + (-val).ToString("X", CultureInfo.InvariantCulture) : "0x" + val.ToString("X", CultureInfo.InvariantCulture);
        string binStr = val < 0 ? "-0b" + Convert.ToString(-val, 2) : "0b" + Convert.ToString(val, 2);
        string octStr = val < 0 ? "-0o" + Convert.ToString(-val, 8) : "0o" + Convert.ToString(val, 8);
        string decStr = val.ToString(CultureInfo.InvariantCulture);

        result = targetBase switch
        {
            "hex" => hexStr,
            "bin" => binStr,
            "oct" => octStr,
            _ => decStr
        };
        detail = $"Dec {decStr} · Hex {hexStr} · Bin {binStr}";
        return true;
    }

    private static bool TryEvalUnitConversion(string q, out string result, out string detail)
    {
        result = string.Empty;
        detail = string.Empty;

        var m = UnitConvertRegex.Match(q);
        if (!m.Success)
            return false;

        if (!double.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return false;

        string u1 = NormalizeUnit(m.Groups[2].Value);
        string u2 = NormalizeUnit(m.Groups[3].Value);

        // Temperature
        if (IsTempUnit(u1) && IsTempUnit(u2))
        {
            double c = u1 switch
            {
                "c" => value,
                "f" => (value - 32.0) * 5.0 / 9.0,
                "k" => value - 273.15,
                _ => value
            };
            double converted = u2 switch
            {
                "c" => c,
                "f" => (c * 9.0 / 5.0) + 32.0,
                "k" => c + 273.15,
                _ => c
            };
            string disp2 = u2 == "k" ? "K" : "°" + u2.ToUpperInvariant();
            string disp1 = u1 == "k" ? "K" : "°" + u1.ToUpperInvariant();
            result = $"{FormatCleanDouble(converted)} {disp2}";
            detail = $"{FormatCleanDouble(value)} {disp1} = {FormatCleanDouble(converted)} {disp2}";
            return true;
        }

        // Data units (binary 1024 & decimal 1000 clearly labeled)
        if (TryGetDataPower(u1, out int pow1, out bool iec1) && TryGetDataPower(u2, out int pow2, out bool iec2))
        {
            double bytesBin = value * Math.Pow(1024, pow1);
            double valBin = bytesBin / Math.Pow(1024, pow2);
            double bytesDec = value * Math.Pow(iec1 ? 1024 : 1000, pow1);
            double valDec = bytesDec / Math.Pow(iec2 ? 1024 : 1000, pow2);

            string u1Label = u1.ToUpperInvariant();
            string u2Label = u2.ToUpperInvariant();
            if (iec1 || iec2)
            {
                result = $"{FormatCleanDouble(valBin)} {u2Label}";
                detail = $"{FormatCleanDouble(value)} {u1Label} = {FormatCleanDouble(valBin)} {u2Label} (1024-based)";
            }
            else
            {
                result = $"{FormatCleanDouble(valBin)} {u2Label}";
                detail = $"Binary (1024): {FormatCleanDouble(valBin)} {u2Label} · Decimal (1000): {FormatCleanDouble(valDec)} {u2Label}";
            }
            return true;
        }

        // Time units (allow 'm' when paired with explicit time unit)
        if (TryGetTimeSecondsFactor(u1, out double ts1, out string tDisp1) &&
            TryGetTimeSecondsFactor(u2, out double ts2, out string tDisp2))
        {
            double outVal = value * ts1 / ts2;
            result = $"{FormatCleanDouble(outVal)} {tDisp2}";
            detail = $"{FormatCleanDouble(value)} {tDisp1} = {FormatCleanDouble(outVal)} {tDisp2}";
            return true;
        }

        // Length units
        if (TryGetLengthMetersFactor(u1, out double lm1, out string lDisp1) &&
            TryGetLengthMetersFactor(u2, out double lm2, out string lDisp2))
        {
            double outVal = value * lm1 / lm2;
            result = $"{FormatCleanDouble(outVal)} {lDisp2}";
            detail = $"{FormatCleanDouble(value)} {lDisp1} = {FormatCleanDouble(outVal)} {lDisp2}";
            return true;
        }

        // Mass units
        if (TryGetMassGramsFactor(u1, out double mg1, out string mDisp1) &&
            TryGetMassGramsFactor(u2, out double mg2, out string mDisp2))
        {
            double outVal = value * mg1 / mg2;
            result = $"{FormatCleanDouble(outVal)} {mDisp2}";
            detail = $"{FormatCleanDouble(value)} {mDisp1} = {FormatCleanDouble(outVal)} {mDisp2}";
            return true;
        }

        return false;
    }

    private static string NormalizeUnit(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "kilometer" or "kilometers" or "kilometre" or "kilometres" => "km",
        "meter" or "meters" or "metre" or "metres" => "m",
        "centimeter" or "centimeters" or "centimetre" or "centimetres" => "cm",
        "millimeter" or "millimeters" or "millimetre" or "millimetres" => "mm",
        "mile" or "miles" => "mi",
        "foot" or "feet" => "ft",
        "inch" or "inches" => "in",
        "yard" or "yards" => "yd",
        "kilogram" or "kilograms" or "kgs" => "kg",
        "gram" or "grams" => "g",
        "milligram" or "milligrams" => "mg",
        "pound" or "pounds" or "lbs" => "lb",
        "ounce" or "ounces" => "oz",
        "celsius" or "centigrade" or "degc" => "c",
        "fahrenheit" or "degf" => "f",
        "kelvin" => "k",
        "byte" or "bytes" => "b",
        "kilobyte" or "kilobytes" => "kb",
        "megabyte" or "megabytes" => "mb",
        "gigabyte" or "gigabytes" => "gb",
        "terabyte" or "terabytes" => "tb",
        "hour" or "hours" or "hr" or "hrs" or "h" => "hrs",
        "minute" or "minutes" or "mins" or "min" => "min",
        "second" or "seconds" or "secs" or "sec" or "s" => "sec",
        "day" or "days" => "days",
        "week" or "weeks" or "wk" or "wks" => "wks",
        var other => other
    };

    private static bool IsTempUnit(string u) => u is "c" or "f" or "k";

    private static bool TryGetDataPower(string u, out int power, out bool isIec)
    {
        isIec = false;
        switch (u)
        {
            case "b": power = 0; return true;
            case "kb": power = 1; return true;
            case "mb": power = 2; return true;
            case "gb": power = 3; return true;
            case "tb": power = 4; return true;
            case "kib": power = 1; isIec = true; return true;
            case "mib": power = 2; isIec = true; return true;
            case "gib": power = 3; isIec = true; return true;
            case "tib": power = 4; isIec = true; return true;
            default: power = 0; return false;
        }
    }

    private static bool TryGetLengthMetersFactor(string u, out double factor, out string label)
    {
        switch (u)
        {
            case "mm": factor = 0.001; label = "mm"; return true;
            case "cm": factor = 0.01; label = "cm"; return true;
            case "m": factor = 1.0; label = "m"; return true;
            case "km": factor = 1000.0; label = "km"; return true;
            case "in": factor = 0.0254; label = "in"; return true;
            case "ft": factor = 0.3048; label = "ft"; return true;
            case "yd": factor = 0.9144; label = "yd"; return true;
            case "mi": factor = 1609.344; label = "mi"; return true;
            default: factor = 0; label = ""; return false;
        }
    }

    private static bool TryGetMassGramsFactor(string u, out double factor, out string label)
    {
        switch (u)
        {
            case "mg": factor = 0.001; label = "mg"; return true;
            case "g": factor = 1.0; label = "g"; return true;
            case "kg": factor = 1000.0; label = "kg"; return true;
            case "oz": factor = 28.349523125; label = "oz"; return true;
            case "lb": factor = 453.59237; label = "lb"; return true;
            default: factor = 0; label = ""; return false;
        }
    }

    private static bool TryGetTimeSecondsFactor(string u, out double factor, out string label)
    {
        switch (u)
        {
            case "ms": factor = 0.001; label = "ms"; return true;
            case "sec": factor = 1.0; label = "sec"; return true;
            case "min": factor = 60.0; label = "min"; return true;
            case "hrs": factor = 3600.0; label = "hrs"; return true;
            case "days": factor = 86400.0; label = "days"; return true;
            case "wks": factor = 604800.0; label = "wks"; return true;
            default: factor = 0; label = ""; return false;
        }
    }

    private enum TokenKind { Number, Plus, Minus, Star, Slash, Percent, Caret, LParen, RParen }
    private readonly record struct ArithToken(TokenKind Kind, decimal Value);

    private static bool TryEvalArithmetic(string q, out string result, out string detail)
    {
        result = string.Empty;
        detail = string.Empty;

        string normalized = q.Replace("×", "*").Replace("÷", "/").Replace("−", "-").Replace("**", "^");
        if (!TokenizeArithmetic(normalized, out var tokens, out bool hasOperatorOrParen) || !hasOperatorOrParen || tokens.Count == 0)
            return false;

        try
        {
            int pos = 0;
            decimal value = ParseExpr(tokens, ref pos);
            if (pos != tokens.Count)
                return false;

            result = FormatCleanDecimal(value);
            detail = $"{q} = {FormatNumberGrouped(value)}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TokenizeArithmetic(string input, out List<ArithToken> tokens, out bool hasOperatorOrParen)
    {
        tokens = new List<ArithToken>();
        hasOperatorOrParen = false;
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                bool seenDot = c == '.';
                i++;
                while (i < input.Length)
                {
                    char ch = input[i];
                    if (char.IsDigit(ch))
                    {
                        i++;
                    }
                    else if (ch == '.' && !seenDot)
                    {
                        seenDot = true;
                        i++;
                    }
                    else if (ch == ',' && i + 1 < input.Length && char.IsDigit(input[i + 1]))
                    {
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }

                string numStr = input.Substring(start, i - start).Replace(",", "");
                if (!decimal.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal num))
                    return false;
                tokens.Add(new ArithToken(TokenKind.Number, num));
                continue;
            }

            TokenKind? tk = c switch
            {
                '+' => TokenKind.Plus,
                '-' => TokenKind.Minus,
                '*' => TokenKind.Star,
                '/' => TokenKind.Slash,
                '%' => TokenKind.Percent,
                '^' => TokenKind.Caret,
                '(' => TokenKind.LParen,
                ')' => TokenKind.RParen,
                _ => null
            };
            if (!tk.HasValue)
                return false;

            if (tk.Value != TokenKind.Number)
            {
                // Require at least one binary/paren token beyond a lone leading sign like "-5"
                if (tokens.Count > 0 || tk.Value is TokenKind.LParen)
                    hasOperatorOrParen = true;
            }
            tokens.Add(new ArithToken(tk.Value, 0m));
            i++;
        }

        return true;
    }

    private static decimal ParseExpr(List<ArithToken> tokens, ref int pos)
    {
        decimal left = ParseTerm(tokens, ref pos);
        while (pos < tokens.Count && (tokens[pos].Kind == TokenKind.Plus || tokens[pos].Kind == TokenKind.Minus))
        {
            var op = tokens[pos++].Kind;
            decimal right = ParseTerm(tokens, ref pos);
            left = op == TokenKind.Plus ? left + right : left - right;
        }
        return left;
    }

    private static decimal ParseTerm(List<ArithToken> tokens, ref int pos)
    {
        decimal left = ParseUnary(tokens, ref pos);
        while (pos < tokens.Count &&
               (tokens[pos].Kind == TokenKind.Star || tokens[pos].Kind == TokenKind.Slash || tokens[pos].Kind == TokenKind.Percent))
        {
            var op = tokens[pos++].Kind;
            decimal right = ParseUnary(tokens, ref pos);
            if (op == TokenKind.Star)
                left *= right;
            else if (op == TokenKind.Slash)
            {
                if (right == 0m) throw new DivideByZeroException();
                left /= right;
            }
            else
            {
                if (right == 0m) throw new DivideByZeroException();
                left %= right;
            }
        }
        return left;
    }

    private static decimal ParseUnary(List<ArithToken> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos].Kind == TokenKind.Plus)
        {
            pos++;
            return ParseUnary(tokens, ref pos);
        }
        if (pos < tokens.Count && tokens[pos].Kind == TokenKind.Minus)
        {
            pos++;
            return -ParseUnary(tokens, ref pos);
        }
        return ParsePower(tokens, ref pos);
    }

    private static decimal ParsePower(List<ArithToken> tokens, ref int pos)
    {
        decimal left = ParsePrimary(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos].Kind == TokenKind.Percent)
        {
            bool hasFollowingOperand = pos + 1 < tokens.Count &&
                tokens[pos + 1].Kind is TokenKind.Number or TokenKind.LParen;
            if (hasFollowingOperand)
                break;
            left /= 100m;
            pos++;
        }
        if (pos < tokens.Count && tokens[pos].Kind == TokenKind.Caret)
        {
            pos++;
            decimal right = ParseUnary(tokens, ref pos);
            double pow = Math.Pow((double)left, (double)right);
            if (!double.IsFinite(pow))
                throw new OverflowException();
            return Convert.ToDecimal(pow);
        }
        return left;
    }

    private static decimal ParsePrimary(List<ArithToken> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
            throw new FormatException("Unexpected end of expression.");

        var t = tokens[pos];
        if (t.Kind == TokenKind.Number)
        {
            pos++;
            return t.Value;
        }

        if (t.Kind == TokenKind.LParen)
        {
            pos++;
            decimal inside = ParseExpr(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos].Kind != TokenKind.RParen)
                throw new FormatException("Missing closing parenthesis.");
            pos++;
            return inside;
        }

        throw new FormatException("Unexpected token.");
    }

    private static string FormatCleanDecimal(decimal value)
    {
        decimal rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        string raw = rounded.ToString("0.######", CultureInfo.InvariantCulture);
        return raw == "-0" ? "0" : raw;
    }

    private static string FormatNumberGrouped(decimal value)
    {
        decimal rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        if (rounded == Math.Truncate(rounded))
            return rounded.ToString("#,##0", CultureInfo.InvariantCulture);
        return rounded.ToString("#,##0.######", CultureInfo.InvariantCulture);
    }

    private static string FormatCleanDouble(double value)
    {
        if (!double.IsFinite(value))
            return "0";
        double rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        string raw = rounded.ToString("0.######", CultureInfo.InvariantCulture);
        return raw == "-0" ? "0" : raw;
    }

    internal static IReadOnlyList<CommandSearchHit> Search(string query, Workspace? workspace = null)
    {
        string raw = (query ?? string.Empty).Trim();
        var hits = new List<CommandSearchHit>();

        // 1. Calculator / Unit / Base / Date Math at top (Score = 1000)
        if (!string.IsNullOrEmpty(raw) && TryEvaluateExpression(raw, out string calcResult, out string calcDetail))
        {
            string copyVal = calcResult;
            hits.Add(new CommandSearchHit(
                "Calculator",
                calcResult,
                calcDetail,
                "=",
                1000,
                _ => SafeSetClipboardText(copyVal),
                copyVal));
        }

        char prefix = raw.Length > 0 && raw[0] is '>' or ';' or '#' or '@' ? raw[0] : '\0';
        string term = prefix != '\0' ? raw.Substring(1).Trim() : raw;
        bool hasTerm = term.Length > 0;

        int ScoreMatch(string primary, string secondary, int baseWeight)
        {
            if (!hasTerm)
                return baseWeight;
            if (primary.Equals(term, StringComparison.OrdinalIgnoreCase))
                return baseWeight + 300;
            if (primary.StartsWith(term, StringComparison.OrdinalIgnoreCase))
                return baseWeight + 200;
            if (primary.Contains(term, StringComparison.OrdinalIgnoreCase))
                return baseWeight + 120;
            if (!string.IsNullOrEmpty(secondary) && secondary.Contains(term, StringComparison.OrdinalIgnoreCase))
                return baseWeight + 60;
            return -1;
        }

        // 2. Registered Commands ('>' prefix boosts)
        var allCommands = BuildAllCommands(workspace != null ? () => workspace : null);
        int cmdBoost = prefix == '>' ? 260 : 0;
        if (prefix is '\0' or '>')
        {
            foreach (var cmd in allCommands)
            {
                int score = ScoreMatch(cmd.Title, cmd.Subtitle + " " + cmd.Category, 520 + cmdBoost);
                if (score < 0)
                    continue;
                var captured = cmd;
                hits.Add(new CommandSearchHit(
                    captured.Category,
                    captured.Title,
                    captured.Subtitle,
                    string.IsNullOrWhiteSpace(captured.Shortcut) ? "Command" : captured.Shortcut,
                    score,
                    captured.Execute));
            }
        }

        // 3. Connected DeskTouch Devices ('@' prefix boosts)
        var bridgeSnap = DeskSpaceDeskTouchBridgeV378.Current;
        if (prefix is '\0' or '@')
        {
            int devBoost = prefix == '@' ? 280 : 0;
            foreach (var dev in bridgeSnap.Devices)
            {
                string sub = $"{dev.Platform} · {dev.StatusText}".Trim(' ', '·');
                int score = ScoreMatch(dev.Name, sub + " " + dev.Platform, 460 + devBoost);
                if (score < 0)
                    continue;
                var captured = dev;
                hits.Add(new CommandSearchHit(
                    "Devices",
                    captured.IsLocal ? $"Switch to {captured.Name} (This PC)" : $"Switch to {captured.Name}",
                    sub,
                    captured.IsControlling ? "ACTIVE" : captured.Platform,
                    score,
                    _ =>
                    {
                        if (captured.IsLocal)
                            DeskSpaceDeskTouchBridgeV378.ReturnLocalAsync();
                        else if (captured.SupportsInput)
                            DeskSpaceDeskTouchBridgeV378.SwitchDeviceAsync(captured.Id);
                    }));
            }
        }

        // 4. Quick Snippets (';' prefix boosts)
        if (workspace != null && prefix is '\0' or ';')
        {
            int snipBoost = prefix == ';' ? 270 : 0;
            foreach (var snip in workspace.Snippets)
            {
                int score = ScoreMatch(snip.Name, snip.Text, 440 + snipBoost);
                if (score < 0)
                    continue;
                string preview = snip.Text.Replace('\r', ' ').Replace('\n', ' ');
                if (preview.Length > 90) preview = preview.Substring(0, 90) + "…";
                string textVal = snip.Text;
                hits.Add(new CommandSearchHit(
                    "Snippet",
                    snip.Name,
                    string.IsNullOrWhiteSpace(preview) ? "Empty snippet" : preview,
                    "Copy",
                    score,
                    _ => SafeSetClipboardText(textVal),
                    textVal));
            }
        }

        // 5. Notes ('#' prefix boosts)
        if (workspace != null && prefix is '\0' or '#')
        {
            int noteBoost = prefix == '#' ? 270 : 0;
            foreach (var note in workspace.Notes)
            {
                int score = ScoreMatch(note.Title, note.Content, 420 + noteBoost);
                if (score < 0)
                    continue;
                string preview = note.Content.Replace('\r', ' ').Replace('\n', ' ');
                if (preview.Length > 90) preview = preview.Substring(0, 90) + "…";
                var captured = note;
                hits.Add(new CommandSearchHit(
                    "Note",
                    captured.Title,
                    string.IsNullOrWhiteSpace(preview) ? "Note in DeskSpace" : preview,
                    "Open",
                    score,
                    _ => OpenNoteOrCopy(captured),
                    captured.Content));
            }
        }

        // 6. Reminders
        if (workspace != null && prefix == '\0' && hasTerm)
        {
            foreach (var rem in workspace.Reminders.Where(r => !r.Done))
            {
                int score = ScoreMatch(rem.Title, rem.Due.ToString("g", CultureInfo.CurrentCulture), 380);
                if (score < 0)
                    continue;
                hits.Add(new CommandSearchHit(
                    "Reminder",
                    rem.Title,
                    $"Due {rem.Due:g}",
                    "Task",
                    score,
                    _ => NavigateMainWindow("REMINDERS")));
            }
        }

        // 7. Rich Clipboard items
        if (prefix == '\0' && (hasTerm || DeskSpaceRichClipboardV380.Items.Count > 0))
        {
            foreach (var clip in DeskSpaceRichClipboardV380.Items.Take(hasTerm ? 40 : 6))
            {
                string clipTitle = clip.Kind == ClipKind.Color ? clip.ColorHex : clip.Text.Replace('\r', ' ').Replace('\n', ' ');
                if (clipTitle.Length > 72) clipTitle = clipTitle.Substring(0, 72) + "…";
                int score = ScoreMatch(clipTitle, clip.SourceSummary, hasTerm ? 360 : 250);
                if (score < 0)
                    continue;
                var captured = clip;
                string copyStr = captured.Kind == ClipKind.Color ? captured.ColorHex : captured.Text;
                hits.Add(new CommandSearchHit(
                    "Clipboard",
                    string.IsNullOrWhiteSpace(clipTitle) ? captured.SourceSummary : clipTitle,
                    captured.SourceSummary,
                    captured.Pinned ? "PIN" : "Clip",
                    score,
                    _ => DeskSpaceRichClipboardV380.PasteAsPlainText(copyStr),
                    copyStr));
            }
        }

        // 8. System Settings & Builtin shortcuts
        if (prefix == '\0' && hasTerm)
        {
            foreach (var (name, uri) in SystemSettingsShortcuts)
            {
                int score = ScoreMatch(name, uri, 320);
                if (score < 0)
                    continue;
                string target = uri;
                hits.Add(new CommandSearchHit(
                    "System",
                    name,
                    "Open Windows Settings / tool",
                    "Open",
                    score,
                    _ =>
                    {
                        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
                        catch { }
                    }));
            }
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Title, StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToArray();
    }

    private static void SafeSetClipboardText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text ?? string.Empty);
        }
        catch
        {
        }
    }

    private static void OpenNoteOrCopy(NoteItem note)
    {
        var main = System.Windows.Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
        if (main != null)
        {
            main.ShowFeatureDestinationV200("NOTES");
            if (main.NotesList != null)
            {
                var matched = main.NotesList.Items.OfType<NoteItem>().FirstOrDefault(n => n.Id == note.Id);
                if (matched != null)
                    main.NotesList.SelectedItem = matched;
            }
            return;
        }

        SafeSetClipboardText(note.Content);
    }

    private static void NavigateMainWindow(string destination)
    {
        var main = System.Windows.Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
        main?.ShowFeatureDestinationV200(destination);
    }

    internal static void Show(Window? owner, Func<Workspace>? workspaceProvider = null)
    {
        if (_activeBar != null)
        {
            try
            {
                _activeBar.Activate();
                return;
            }
            catch
            {
                _activeBar = null;
            }
        }

        DeskSpaceDeskTouchBridgeV378.PollIfDueAsync();

        var p = DeskSpaceDesignV283.Current;
        Workspace? cachedWorkspace = null;
        try
        {
            cachedWorkspace = (workspaceProvider ?? (() => new WorkspaceStore().Load())).Invoke();
        }
        catch
        {
        }

        var win = new Window
        {
            Title = "DeskSpace Command Bar",
            Width = 680,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 580,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        DeskSpaceFluentBackdropV380.ApplyToWindow(win, transient: true);

        // Center horizontally and place in upper third of active monitor
        PositionOnActiveScreen(win);

        _activeBar = win;
        bool closing = false;
        CancellationTokenSource? asyncSearchCts = null;

        void DisposeSearchCts()
        {
            var prev = Interlocked.Exchange(ref asyncSearchCts, null);
            if (prev == null)
                return;
            try { prev.Cancel(); } catch (ObjectDisposedException) { }
            try { prev.Dispose(); } catch (ObjectDisposedException) { }
        }

        void CloseCleanly()
        {
            if (closing)
                return;
            closing = true;
            DisposeSearchCts();
            try { win.Close(); } catch { }
        }

        var outerBorder = new Border
        {
            CornerRadius = new CornerRadius(16),
            Margin = new Thickness(14),
            Background = DeskSpaceDesignV283.Solid(p.HighContrast
                ? p.Canvas
                : Color.FromArgb(245, p.Canvas.R, p.Canvas.G, p.Canvas.B)),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            BorderThickness = new Thickness(p.HighContrast ? 2 : 1.2),
            Effect = p.HighContrast ? null : new DropShadowEffect
            {
                Color = Color.FromRgb(2, 6, 18),
                BlurRadius = 24,
                ShadowDepth = 8,
                Opacity = 0.55,
                Direction = 270
            }
        };

        var layout = new DockPanel { LastChildFill = true };

        // Top Search Box + Hint Bar
        var topBlock = new StackPanel { Margin = new Thickness(16, 14, 16, 8) };
        var inputBox = new TextBox
        {
            FontSize = 18,
            FontWeight = FontWeights.Medium,
            Background = Brushes.Transparent,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            BorderThickness = new Thickness(0),
            CaretBrush = DeskSpaceDesignV283.Solid(p.Accent),
            SelectionBrush = DeskSpaceDesignV283.Solid(p.AccentSoft),
            Padding = new Thickness(2, 4, 2, 4)
        };
        topBlock.Children.Add(inputBox);

        var hintBar = new WrapPanel { Margin = new Thickness(2, 6, 2, 0) };
        foreach (string hint in new[]
        {
            "> commands",
            "# notes",
            "; snippets",
            "@ devices",
            "5900 excl 18% gst · 5 km to mi · today + 45d"
        })
        {
            hintBar.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = DeskSpaceDesignV283.Solid(p.Raised),
                BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 0, 6, 4),
                Child = new TextBlock
                {
                    Text = hint,
                    FontSize = 11,
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted)
                }
            });
        }
        topBlock.Children.Add(hintBar);

        topBlock.Children.Add(new Border
        {
            Height = 1,
            Background = DeskSpaceDesignV283.Solid(p.Border),
            Margin = new Thickness(0, 6, 0, 0)
        });

        DockPanel.SetDock(topBlock, Dock.Top);
        layout.Children.Add(topBlock);

        var footerBar = new DockPanel { Margin = new Thickness(16, 4, 16, 12), LastChildFill = false };
        var statusText = new TextBlock
        {
            Text = "↑↓ Navigate · Enter Run / Copy · Esc Close",
            FontSize = 11,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted)
        };
        DockPanel.SetDock(statusText, Dock.Left);
        footerBar.Children.Add(statusText);
        DockPanel.SetDock(footerBar, Dock.Bottom);
        layout.Children.Add(footerBar);

        var resultsList = new ListBox
        {
            MaxHeight = 360,
            Margin = new Thickness(10, 0, 10, 6),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(resultsList, ScrollBarVisibility.Disabled);
        layout.Children.Add(resultsList);

        outerBorder.Child = layout;
        win.Content = outerBorder;
        DeskSpaceFluentBackdropV380.ApplyFadeEntrance(outerBorder);

        var currentHits = new List<CommandSearchHit>();

        void RenderHits(IReadOnlyList<CommandSearchHit> hits)
        {
            currentHits.Clear();
            currentHits.AddRange(hits);
            resultsList.Items.Clear();

            foreach (var hit in currentHits)
            {
                bool isCalc = string.Equals(hit.Category, "Calculator", StringComparison.Ordinal);
                var row = new DockPanel { LastChildFill = true, Margin = new Thickness(4, 3, 4, 3) };

                var badgeBorder = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = DeskSpaceDesignV283.Solid(isCalc ? p.AccentSoft : p.Navigation),
                    BorderBrush = DeskSpaceDesignV283.Solid(isCalc ? p.Accent : p.Border),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = isCalc ? "Enter to copy" : hit.Badge,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = DeskSpaceDesignV283.Solid(isCalc ? p.Accent : p.Muted)
                    }
                };
                DockPanel.SetDock(badgeBorder, Dock.Right);
                row.Children.Add(badgeBorder);

                var catPill = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = DeskSpaceDesignV283.Solid(isCalc ? p.Accent : p.Raised),
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 76,
                    Child = new TextBlock
                    {
                        Text = hit.Category,
                        FontSize = 10.5,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = DeskSpaceDesignV283.Solid(isCalc
                            ? DeskSpaceDesignV283.OnAccent(p.Accent, p.HighContrast)
                            : p.Text)
                    }
                };
                DockPanel.SetDock(catPill, Dock.Left);
                row.Children.Add(catPill);

                var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textCol.Children.Add(new TextBlock
                {
                    Text = hit.Title,
                    FontSize = isCalc ? 17 : 13.5,
                    FontWeight = isCalc ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Text),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                if (!string.IsNullOrWhiteSpace(hit.Subtitle))
                {
                    textCol.Children.Add(new TextBlock
                    {
                        Text = hit.Subtitle,
                        FontSize = 11.5,
                        Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                }
                row.Children.Add(textCol);

                resultsList.Items.Add(new ListBoxItem
                {
                    Tag = hit,
                    Content = row,
                    Padding = new Thickness(6, 4, 6, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                });
            }

            if (resultsList.Items.Count > 0)
                resultsList.SelectedIndex = 0;

            statusText.Text = currentHits.Count == 0
                ? "No matches — try math, unit conversion, >command, #note, ;snippet, or @device"
                : $"{currentHits.Count} result(s) · ↑↓ Navigate · Enter Execute · Esc Close";
        }

        void ExecuteSelected()
        {
            CommandSearchHit? hit = null;
            if (resultsList.SelectedItem is ListBoxItem lbi && lbi.Tag is CommandSearchHit tagged)
                hit = tagged;
            else if (currentHits.Count > 0)
                hit = currentHits[0];

            if (hit == null)
                return;

            CloseCleanly();
            try
            {
                hit.Run(owner);
            }
            catch
            {
            }
        }

        void UpdateQuery(string q)
        {
            DisposeSearchCts();

            var baseHits = Search(q, cachedWorkspace).ToList();
            RenderHits(baseHits);

            string trimmed = q.Trim();
            if (trimmed.Length >= 2 &&
                trimmed[0] is not ('>' or ';' or '@' or '#') &&
                !TryEvaluateExpression(trimmed, out _, out _) &&
                cachedWorkspace != null)
            {
                var cts = new CancellationTokenSource();
                asyncSearchCts = cts;
                var token = cts.Token;
                var wsSnapshot = cachedWorkspace;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(140, token).ConfigureAwait(false);
                        var fileHits = DeskSpaceSearchIndexV13.Search(trimmed, wsSnapshot, token);
                        if (token.IsCancellationRequested || closing)
                            return;

                        var merged = new List<CommandSearchHit>(baseHits);
                        foreach (var f in fileHits.Take(12))
                        {
                            if (f.Category is "Notes" or "Reminders" or "Settings")
                                continue;
                            var captured = f;
                            merged.Add(new CommandSearchHit(
                                captured.Category,
                                captured.Name,
                                captured.Location,
                                "Open",
                                210,
                                _ =>
                                {
                                    if (!string.IsNullOrWhiteSpace(captured.OpenTarget))
                                    {
                                        try { Process.Start(new ProcessStartInfo(captured.OpenTarget) { UseShellExecute = true }); }
                                        catch { }
                                    }
                                }));
                        }

                        if (!win.Dispatcher.HasShutdownStarted)
                        {
                            await win.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (!token.IsCancellationRequested && !closing && inputBox.Text.Trim() == trimmed)
                                {
                                    int prevIdx = resultsList.SelectedIndex;
                                    RenderHits(merged);
                                    if (prevIdx >= 0 && prevIdx < resultsList.Items.Count)
                                        resultsList.SelectedIndex = prevIdx;
                                }
                            }));
                        }
                    }
                    catch
                    {
                    }
                }, token);
            }
        }

        inputBox.TextChanged += (_, _) => UpdateQuery(inputBox.Text);

        win.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseCleanly();
                return;
            }

            if (e.Key == Key.Down)
            {
                if (resultsList.Items.Count > 0)
                {
                    int next = Math.Min(resultsList.Items.Count - 1, resultsList.SelectedIndex + 1);
                    resultsList.SelectedIndex = next;
                    resultsList.ScrollIntoView(resultsList.SelectedItem);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                if (resultsList.Items.Count > 0)
                {
                    int prev = Math.Max(0, resultsList.SelectedIndex - 1);
                    resultsList.SelectedIndex = prev;
                    resultsList.ScrollIntoView(resultsList.SelectedItem);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteSelected();
            }
        };

        resultsList.MouseDoubleClick += (_, _) => ExecuteSelected();
        win.Deactivated += (_, _) => CloseCleanly();
        win.Closed += (_, _) =>
        {
            closing = true;
            DisposeSearchCts();
            if (ReferenceEquals(_activeBar, win))
                _activeBar = null;
        };
        win.Loaded += (_, _) =>
        {
            PositionOnActiveScreen(win);
            inputBox.Focus();
            Keyboard.Focus(inputBox);
        };

        UpdateQuery(string.Empty);
        win.Show();
        win.Activate();
    }

    private static void PositionOnActiveScreen(Window win)
    {
        try
        {
            var cursor = Forms.Cursor.Position;
            var screen = Forms.Screen.FromPoint(cursor);
            var area = screen.WorkingArea;

            double scaleX = 1.0;
            double scaleY = 1.0;
            var src = PresentationSource.FromVisual(win);
            if (src?.CompositionTarget != null)
            {
                scaleX = src.CompositionTarget.TransformToDevice.M11;
                scaleY = src.CompositionTarget.TransformToDevice.M22;
            }
            if (scaleX <= 0) scaleX = 1.0;
            if (scaleY <= 0) scaleY = 1.0;

            double areaLeft = area.Left / scaleX;
            double areaTop = area.Top / scaleY;
            double areaWidth = area.Width / scaleX;
            double areaHeight = area.Height / scaleY;

            win.Left = areaLeft + Math.Max(0, (areaWidth - win.Width) * 0.5);
            win.Top = areaTop + Math.Max(24, areaHeight * 0.16);
        }
        catch
        {
            var area = SystemParameters.WorkArea;
            win.Left = area.Left + Math.Max(0, (area.Width - win.Width) * 0.5);
            win.Top = area.Top + Math.Max(24, area.Height * 0.16);
        }
    }
}

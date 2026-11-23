using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DeskSpace;

internal enum ClipKind
{
    Text,
    Image,
    Color,
    Files
}

internal sealed record RichClipItem(
    Guid Id,
    ClipKind Kind,
    DateTime CapturedUtc,
    bool Pinned,
    string Text,
    string ColorHex,
    byte[]? ImagePngBytes,
    List<string> FilePaths,
    string SourceSummary);

internal static class DeskSpaceRichClipboardV380
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private const int MaxHistoryItems = 60;
    private const int MaxTextChars = 65536;
    private const int MaxImageBytes = 2 * 1024 * 1024;
    private const int MaxImageDimension = 1280;
    private const int MaxFileDropEntries = 32;

    private sealed class ClipboardPrefsDto
    {
        public bool CaptureEnabled { get; set; }
    }

    private static readonly object StateLock = new();
    private static readonly object PrefLock = new();
    private static readonly List<RichClipItem> History = new();
    private static readonly Dictionary<string, DateTime> SensitiveHashes = new(StringComparer.OrdinalIgnoreCase);

    private static bool? _captureEnabledCache;
    private static HwndSource? _listenerSource;
    private static bool _listenerRegistered;
    private static bool _exitHooked;
    private static Window? _openWindow;

    private static readonly Regex HexColorRegex = new(
        @"^#(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$",
        RegexOptions.Compiled);

    private static readonly Regex RgbColorRegex = new(
        @"^rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RgbaColorRegex = new(
        @"^rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*([01](?:\.\d+)?|\.\d+)\s*\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UrlRegex = new(
        @"https?://[^\s<>""'\)\]]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled);

    private static string PrefsPath => System.IO.Path.Combine(WorkspaceStore.Root, "clipboard-v380-prefs.json");

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    internal static event Action? ItemsChanged;

    internal static IReadOnlyList<RichClipItem> Items
    {
        get
        {
            lock (StateLock)
                return History.ToArray();
        }
    }

    internal static bool CaptureEnabled
    {
        get
        {
            lock (PrefLock)
            {
                if (_captureEnabledCache.HasValue)
                    return _captureEnabledCache.Value;

                try
                {
                    if (File.Exists(PrefsPath))
                    {
                        var dto = JsonSerializer.Deserialize<ClipboardPrefsDto>(File.ReadAllText(PrefsPath));
                        _captureEnabledCache = dto?.CaptureEnabled ?? false;
                        return _captureEnabledCache.Value;
                    }
                }
                catch
                {
                }

                _captureEnabledCache = false;
                return false;
            }
        }
    }

    internal static void SetCaptureEnabled(bool enabled, Window? hostWindow = null)
    {
        lock (PrefLock)
        {
            Directory.CreateDirectory(WorkspaceStore.Root);
            string tmp = PrefsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(new ClipboardPrefsDto { CaptureEnabled = enabled }));
                File.Move(tmp, PrefsPath, true);
                _captureEnabledCache = enabled;
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

        if (enabled)
            EnsureStartedOnUi(hostWindow);
        else
            Shutdown();
    }

    internal static void EnsureStartedOnUi(Window? hostWindow)
    {
        if (!CaptureEnabled)
            return;

        var dispatcher = hostWindow?.Dispatcher ?? System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => EnsureStartedOnUi(hostWindow)));
            return;
        }

        if (_listenerSource != null && !_listenerSource.IsDisposed && _listenerRegistered)
            return;

        ShutdownCore();

        try
        {
            var parameters = new HwndSourceParameters("DeskSpaceRichClipboardV380")
            {
                ParentWindow = new IntPtr(-3), // HWND_MESSAGE
                Width = 0,
                Height = 0,
                WindowStyle = 0
            };
            _listenerSource = new HwndSource(parameters);
            _listenerSource.AddHook(ClipboardWndProc);
            _listenerRegistered = AddClipboardFormatListener(_listenerSource.Handle);

            if (!_exitHooked && System.Windows.Application.Current != null)
            {
                _exitHooked = true;
                System.Windows.Application.Current.Exit += (_, _) => Shutdown();
            }
        }
        catch
        {
            ShutdownCore();
        }
    }

    internal static void Shutdown()
    {
        var source = _listenerSource;
        if (source == null)
            return;

        var dispatcher = source.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess() && !dispatcher.HasShutdownStarted)
        {
            dispatcher.BeginInvoke(new Action(ShutdownCore));
            return;
        }

        ShutdownCore();
    }

    private static void ShutdownCore()
    {
        if (_listenerSource != null)
        {
            try
            {
                if (_listenerRegistered && _listenerSource.Handle != IntPtr.Zero)
                    RemoveClipboardFormatListener(_listenerSource.Handle);
            }
            catch
            {
            }

            try
            {
                _listenerSource.RemoveHook(ClipboardWndProc);
            }
            catch
            {
            }

            try
            {
                _listenerSource.Dispose();
            }
            catch
            {
            }

            _listenerSource = null;
            _listenerRegistered = false;
        }
    }

    private static IntPtr ClipboardWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE && CaptureEnabled)
        {
            _listenerSource?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                if (CaptureEnabled)
                    CaptureNow();
            }), DispatcherPriority.Background);
        }
        return IntPtr.Zero;
    }

    internal static void NotifySensitiveCopy(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
            return;

        string hash = ComputeSha256Hex(secret);
        DateTime expires = DateTime.UtcNow.AddSeconds(45);
        lock (StateLock)
        {
            PruneExpiredSensitiveLocked(DateTime.UtcNow);
            SensitiveHashes[hash] = expires;
        }
    }

    private static string ComputeSha256Hex(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] digest = SHA256.HashData(bytes);
        return Convert.ToHexString(digest);
    }

    private static void PruneExpiredSensitiveLocked(DateTime now)
    {
        if (SensitiveHashes.Count == 0)
            return;
        var expired = SensitiveHashes.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToArray();
        foreach (string key in expired)
            SensitiveHashes.Remove(key);
    }

    private static bool IsSensitiveText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        string hash = ComputeSha256Hex(text);
        lock (StateLock)
        {
            PruneExpiredSensitiveLocked(DateTime.UtcNow);
            return SensitiveHashes.ContainsKey(hash);
        }
    }

    internal static bool CaptureNow()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            return false;

        try
        {
            if (System.Windows.Clipboard.ContainsImage())
            {
                var img = System.Windows.Clipboard.GetImage();
                if (img != null && TryEncodeResizedPng(img, out byte[]? pngBytes, out int w, out int h) && pngBytes != null)
                {
                    var item = new RichClipItem(
                        Guid.NewGuid(),
                        ClipKind.Image,
                        DateTime.UtcNow,
                        false,
                        $"Image ({w} × {h})",
                        "",
                        pngBytes,
                        new List<string>(),
                        $"Image · {w}×{h} · {FormatBytes(pngBytes.Length)}");
                    AddOrPromote(item);
                    return true;
                }
            }

            if (System.Windows.Clipboard.ContainsFileDropList())
            {
                var files = System.Windows.Clipboard.GetFileDropList();
                if (files != null && files.Count > 0)
                {
                    var paths = new List<string>(Math.Min(files.Count, MaxFileDropEntries));
                    foreach (string? p in files)
                    {
                        if (!string.IsNullOrWhiteSpace(p))
                        {
                            paths.Add(p);
                            if (paths.Count >= MaxFileDropEntries)
                                break;
                        }
                    }

                    if (paths.Count > 0)
                    {
                        string summary = paths.Count == 1
                            ? $"File · {System.IO.Path.GetFileName(paths[0])}"
                            : $"{paths.Count} files";
                        var item = new RichClipItem(
                            Guid.NewGuid(),
                            ClipKind.Files,
                            DateTime.UtcNow,
                            false,
                            string.Join(Environment.NewLine, paths),
                            "",
                            null,
                            paths,
                            summary);
                        AddOrPromote(item);
                        return true;
                    }
                }
            }

            if (System.Windows.Clipboard.ContainsText())
            {
                string raw = System.Windows.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(raw))
                    return false;
                if (IsSensitiveText(raw))
                    return false;

                string text = raw.Length > MaxTextChars ? raw.Substring(0, MaxTextChars) : raw;
                string trimmed = text.Trim();

                if (TryParseColorHex(trimmed, out string normalizedHex))
                {
                    var colorItem = new RichClipItem(
                        Guid.NewGuid(),
                        ClipKind.Color,
                        DateTime.UtcNow,
                        false,
                        trimmed,
                        normalizedHex,
                        null,
                        new List<string>(),
                        $"Color · {normalizedHex}");
                    AddOrPromote(colorItem);
                    return true;
                }

                var textItem = new RichClipItem(
                    Guid.NewGuid(),
                    ClipKind.Text,
                    DateTime.UtcNow,
                    false,
                    text,
                    "",
                    null,
                    new List<string>(),
                    BuildTextSummary(text, "Clipboard"));
                AddOrPromote(textItem);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    internal static bool TryParseColorHex(string raw, out string normalizedHex)
    {
        normalizedHex = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string s = raw.Trim();
        if (HexColorRegex.IsMatch(s))
        {
            if (s.Length == 4)
            {
                char r = char.ToUpperInvariant(s[1]);
                char g = char.ToUpperInvariant(s[2]);
                char b = char.ToUpperInvariant(s[3]);
                normalizedHex = $"#{r}{r}{g}{g}{b}{b}";
                return true;
            }

            normalizedHex = s.ToUpperInvariant();
            return true;
        }

        var rgbMatch = RgbColorRegex.Match(s);
        if (rgbMatch.Success)
        {
            if (int.TryParse(rgbMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) &&
                int.TryParse(rgbMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int g) &&
                int.TryParse(rgbMatch.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int b) &&
                r is >= 0 and <= 255 && g is >= 0 and <= 255 && b is >= 0 and <= 255)
            {
                normalizedHex = $"#{r:X2}{g:X2}{b:X2}";
                return true;
            }
            return false;
        }

        var rgbaMatch = RgbaColorRegex.Match(s);
        if (rgbaMatch.Success)
        {
            if (int.TryParse(rgbaMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) &&
                int.TryParse(rgbaMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int g) &&
                int.TryParse(rgbaMatch.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int b) &&
                double.TryParse(rgbaMatch.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double a) &&
                r is >= 0 and <= 255 && g is >= 0 and <= 255 && b is >= 0 and <= 255 &&
                a is >= 0.0 and <= 1.0)
            {
                int alphaByte = (int)Math.Round(a * 255.0);
                normalizedHex = alphaByte == 255
                    ? $"#{r:X2}{g:X2}{b:X2}"
                    : $"#{alphaByte:X2}{r:X2}{g:X2}{b:X2}";
                return true;
            }
        }

        return false;
    }

    internal static void AddColorEntry(string hex)
    {
        if (!TryParseColorHex(hex, out string normalized))
            return;

        var item = new RichClipItem(
            Guid.NewGuid(),
            ClipKind.Color,
            DateTime.UtcNow,
            false,
            normalized,
            normalized,
            null,
            new List<string>(),
            $"Eyedropper · {normalized}");
        AddOrPromote(item);
    }

    internal static void AddTextEntry(string text, string source = "DeskSpace")
    {
        if (string.IsNullOrWhiteSpace(text) || IsSensitiveText(text))
            return;

        string clamped = text.Length > MaxTextChars ? text.Substring(0, MaxTextChars) : text;
        string trimmed = clamped.Trim();
        if (TryParseColorHex(trimmed, out string normalizedHex))
        {
            AddOrPromote(new RichClipItem(
                Guid.NewGuid(),
                ClipKind.Color,
                DateTime.UtcNow,
                false,
                trimmed,
                normalizedHex,
                null,
                new List<string>(),
                $"{source} · {normalizedHex}"));
            return;
        }

        AddOrPromote(new RichClipItem(
            Guid.NewGuid(),
            ClipKind.Text,
            DateTime.UtcNow,
            false,
            clamped,
            "",
            null,
            new List<string>(),
            BuildTextSummary(clamped, source)));
    }

    internal static void TogglePin(Guid id)
    {
        bool changed = false;
        lock (StateLock)
        {
            int idx = History.FindIndex(x => x.Id == id);
            if (idx >= 0)
            {
                var cur = History[idx];
                History[idx] = cur with { Pinned = !cur.Pinned };
                changed = true;
            }
        }

        if (changed)
            ItemsChanged?.Invoke();
    }

    internal static void Remove(Guid id)
    {
        bool removed;
        lock (StateLock)
            removed = History.RemoveAll(x => x.Id == id) > 0;

        if (removed)
            ItemsChanged?.Invoke();
    }

    internal static void ClearUnpinned()
    {
        bool removed;
        lock (StateLock)
            removed = History.RemoveAll(x => !x.Pinned) > 0;

        if (removed)
            ItemsChanged?.Invoke();
    }

    internal static void PasteAsPlainText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text ?? string.Empty);
        }
        catch
        {
        }
    }

    internal static string TransformQuick(string text, string op)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(op);

        return op switch
        {
            "Format JSON" => DeskSpaceTextConversionsV198.Convert(text, "Format JSON"),
            "Minify JSON" => DeskSpaceTextConversionsV198.Convert(text, "Minify JSON"),
            "CSV to Markdown" or "CSV to Markdown table" or "CSV -> Markdown" =>
                DeskSpaceTextConversionsV198.Convert(text, "CSV to Markdown table"),
            "Unique lines" => DeskSpaceTextConversionsV198.Convert(text, "Unique lines"),
            "Sort lines" => DeskSpaceTextConversionsV198.Convert(text, "Sort lines"),
            "Clean Whitespace" => CleanWhitespace(text),
            "Extract URLs" => ExtractMatches(text, UrlRegex, trimTrailingPunctuation: true, caseInsensitiveDistinct: false),
            "Extract Emails" => ExtractMatches(text, EmailRegex, trimTrailingPunctuation: false, caseInsensitiveDistinct: true),
            "UPPERCASE" => text.ToUpper(CultureInfo.CurrentCulture),
            "lowercase" => text.ToLower(CultureInfo.CurrentCulture),
            "Title Case" => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower(CultureInfo.CurrentCulture)),
            "snake_case" => TransformIdentifiersByLine(text, toCamel: false),
            "camelCase" => TransformIdentifiersByLine(text, toCamel: true),
            _ => throw new ArgumentOutOfRangeException(nameof(op), $"Unsupported clipboard transform '{op}'.")
        };
    }

    private static string CleanWhitespace(string text)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var rawLines = normalized.Split('\n');
        var output = new List<string>(rawLines.Length);
        bool prevBlank = true; // trims leading blank lines

        foreach (string line in rawLines)
        {
            string trimmedEnd = line.TrimEnd(' ', '\t');
            if (trimmedEnd.Length == 0)
            {
                if (!prevBlank)
                {
                    output.Add(string.Empty);
                    prevBlank = true;
                }
            }
            else
            {
                output.Add(trimmedEnd);
                prevBlank = false;
            }
        }

        while (output.Count > 0 && output[^1].Length == 0)
            output.RemoveAt(output.Count - 1);

        return string.Join(Environment.NewLine, output);
    }

    private static string ExtractMatches(string text, Regex regex, bool trimTrailingPunctuation, bool caseInsensitiveDistinct)
    {
        var seen = new HashSet<string>(caseInsensitiveDistinct ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var list = new List<string>();
        foreach (Match match in regex.Matches(text))
        {
            string val = match.Value;
            if (trimTrailingPunctuation)
                val = TrimUrlTrailingPunctuation(val);
            if (!string.IsNullOrWhiteSpace(val) && seen.Add(val))
                list.Add(val);
        }
        return string.Join(Environment.NewLine, list);
    }

    private static string TrimUrlTrailingPunctuation(string url)
    {
        while (url.Length > 0)
        {
            char last = url[^1];
            if (last is '.' or ',' or ';' or ':' or '!' or '?')
            {
                url = url.Substring(0, url.Length - 1);
                continue;
            }
            if (last == ')' && url.Count(c => c == ')') > url.Count(c => c == '('))
            {
                url = url.Substring(0, url.Length - 1);
                continue;
            }
            break;
        }
        return url;
    }

    private static string TransformIdentifiersByLine(string text, bool toCamel)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        var converted = new string[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            var tokens = SplitIdentifierWords(lines[i]);
            if (tokens.Count == 0)
            {
                converted[i] = string.Empty;
                continue;
            }

            if (!toCamel)
            {
                converted[i] = string.Join("_", tokens.Select(t => t.ToLowerInvariant()));
            }
            else
            {
                var sb = new StringBuilder();
                for (int w = 0; w < tokens.Count; w++)
                {
                    string word = tokens[w].ToLowerInvariant();
                    if (w == 0)
                    {
                        sb.Append(word);
                    }
                    else
                    {
                        sb.Append(char.ToUpperInvariant(word[0]));
                        if (word.Length > 1)
                            sb.Append(word.Substring(1));
                    }
                }
                converted[i] = sb.ToString();
            }
        }
        return string.Join(Environment.NewLine, converted);
    }

    private static List<string> SplitIdentifierWords(string line)
    {
        var words = new List<string>();
        var current = new StringBuilder();

        void Flush()
        {
            if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (!char.IsLetterOrDigit(c))
            {
                Flush();
                continue;
            }

            if (current.Length > 0)
            {
                char prev = current[^1];
                bool prevLower = char.IsLower(prev);
                bool prevUpper = char.IsUpper(prev);
                bool prevDigit = char.IsDigit(prev);
                bool curUpper = char.IsUpper(c);
                bool curDigit = char.IsDigit(c);
                bool nextLower = i + 1 < line.Length && char.IsLower(line[i + 1]);

                if ((prevLower && curUpper) ||
                    (prevUpper && curUpper && nextLower) ||
                    (prevDigit != curDigit))
                {
                    Flush();
                }
            }

            current.Append(c);
        }

        Flush();
        return words;
    }

    private static void AddOrPromote(RichClipItem item)
    {
        lock (StateLock)
        {
            int existingIdx = History.FindIndex(e => AreItemsEqual(e, item));
            if (existingIdx >= 0)
            {
                var existing = History[existingIdx];
                var updated = item with
                {
                    Id = existing.Id,
                    Pinned = existing.Pinned,
                    CapturedUtc = DateTime.UtcNow
                };
                History.RemoveAt(existingIdx);
                History.Insert(0, updated);
            }
            else
            {
                History.Insert(0, item);
            }

            while (History.Count > MaxHistoryItems)
            {
                int removeIdx = History.FindLastIndex(x => !x.Pinned);
                if (removeIdx < 0)
                    removeIdx = History.Count - 1;
                History.RemoveAt(removeIdx);
            }
        }

        ItemsChanged?.Invoke();
    }

    private static bool AreItemsEqual(RichClipItem a, RichClipItem b)
    {
        if (a.Kind != b.Kind)
            return false;

        return a.Kind switch
        {
            ClipKind.Text => string.Equals(a.Text, b.Text, StringComparison.Ordinal),
            ClipKind.Color => string.Equals(a.ColorHex, b.ColorHex, StringComparison.OrdinalIgnoreCase),
            ClipKind.Files => a.FilePaths.SequenceEqual(b.FilePaths, StringComparer.OrdinalIgnoreCase),
            ClipKind.Image => a.ImagePngBytes != null && b.ImagePngBytes != null &&
                              a.ImagePngBytes.AsSpan().SequenceEqual(b.ImagePngBytes),
            _ => false
        };
    }

    private static bool TryEncodeResizedPng(BitmapSource source, out byte[]? pngBytes, out int width, out int height)
    {
        pngBytes = null;
        width = source.PixelWidth;
        height = source.PixelHeight;
        if (width <= 0 || height <= 0)
            return false;

        try
        {
            BitmapSource working = source;
            if (width > MaxImageDimension || height > MaxImageDimension)
            {
                double scale = Math.Min((double)MaxImageDimension / width, (double)MaxImageDimension / height);
                working = new TransformedBitmap(source, new ScaleTransform(scale, scale));
                width = working.PixelWidth;
                height = working.PixelHeight;
            }

            byte[]? encoded = EncodePngCore(working);
            if (encoded == null)
            {
                var converted = new FormatConvertedBitmap(working, PixelFormats.Bgra32, null, 0);
                encoded = EncodePngCore(converted);
            }

            if (encoded == null || encoded.Length == 0 || encoded.Length > MaxImageBytes)
                return false;

            pngBytes = encoded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[]? EncodePngCore(BitmapSource source)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildTextSummary(string text, string prefix)
    {
        int lines = text.Split('\n').Length;
        return $"{prefix} · {text.Length:N0} chars · {lines} {(lines == 1 ? "line" : "lines")}";
    }

    private static string FormatBytes(int bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }

    internal static void Show(Window? owner)
    {
        if (_openWindow != null)
        {
            try
            {
                if (_openWindow.WindowState == WindowState.Minimized)
                    _openWindow.WindowState = WindowState.Normal;
                _openWindow.Show();
                _openWindow.Activate();
                return;
            }
            catch
            {
                _openWindow = null;
            }
        }

        var p = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow("DeskSpace · Rich Clipboard", 920, 640, owner, transient: false);
        win.MinWidth = 720;
        win.MinHeight = 480;
        _openWindow = win;

        string activeCategory = "All";
        string searchQuery = string.Empty;
        Guid? selectedId = null;

        var root = new DockPanel { Margin = new Thickness(18) };

        // Header & toolbar
        var topPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        var titleRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 10) };

        var titleStack = new StackPanel { Orientation = Orientation.Vertical };
        titleStack.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("DESKSPACE  /  RICH CLIPBOARD", p));
        titleStack.Children.Add(new TextBlock
        {
            Text = "Clipboard History & Smart Transforms",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(p.Text)
        });
        DockPanel.SetDock(titleStack, Dock.Left);
        titleRow.Children.Add(titleStack);

        var controlsWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var liveToggle = new CheckBox
        {
            Content = "Live Capture (WM_CLIPBOARDUPDATE)",
            IsChecked = CaptureEnabled,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            ToolTip = "Opt-in Windows clipboard listener. Secrets copied from Vault are excluded automatically."
        };
        controlsWrap.Children.Add(liveToggle);

        var captureNowBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Capture Now", () =>
        {
            CaptureNow();
        }, primary: true, tooltip: "Read current Windows clipboard item once");
        captureNowBtn.Margin = new Thickness(0, 0, 8, 0);
        controlsWrap.Children.Add(captureNowBtn);

        var clearBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Clear Unpinned", () =>
        {
            ClearUnpinned();
        }, primary: false, tooltip: "Remove all unpinned items while keeping pinned clips");
        controlsWrap.Children.Add(clearBtn);

        DockPanel.SetDock(controlsWrap, Dock.Right);
        titleRow.Children.Add(controlsWrap);
        topPanel.Children.Add(titleRow);

        var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var searchBox = new TextBox
        {
            FontSize = 14,
            ToolTip = "Filter clipboard items by text, hex color, or filename"
        };
        DeskSpaceDesignV283.Input(searchBox, p);
        searchRow.Children.Add(searchBox);
        topPanel.Children.Add(searchRow);

        var chipPanel = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        var categoryButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        string[] categories = { "All", "Pinned", "Text", "Images", "Colors", "Files" };

        var statusBanner = new TextBlock
        {
            FontSize = 12,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            Margin = new Thickness(0, 6, 0, 0),
            Text = CaptureEnabled
                ? "Live capture is ON (Vault secrets excluded for 45s)."
                : "Live capture is OFF by default. Click Capture Now or toggle Live Capture."
        };

        var itemsList = new ListBox
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6)
        };
        DeskSpaceDesignV283.Selection(itemsList, p);

        // Selected item inline action bar
        var actionBarContent = new StackPanel();
        var actionBar = DeskSpaceFluentBackdropV380.CreateCard(actionBarContent, p);
        actionBar.Margin = new Thickness(0, 10, 0, 0);
        actionBar.Padding = new Thickness(12);

        RichClipItem? GetSelectedItem()
        {
            if (itemsList.SelectedItem is ListBoxItem lbi && lbi.Tag is Guid id)
                return Items.FirstOrDefault(x => x.Id == id);
            return selectedId.HasValue ? Items.FirstOrDefault(x => x.Id == selectedId.Value) : null;
        }

        void RefreshActionBar()
        {
            actionBarContent.Children.Clear();
            var item = GetSelectedItem();
            if (item == null)
            {
                actionBarContent.Children.Add(new TextBlock
                {
                    Text = "Select a clipboard item above to copy plain text, pin, run OCR, transform, or send to a paired DeskTouch device.",
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            var mainActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };

            void AddActionBtn(string label, Action act, bool primary = false, string? tip = null)
            {
                var b = DeskSpaceFluentBackdropV380.CreatePillButton(label, act, primary, tip);
                b.Margin = new Thickness(0, 0, 6, 6);
                b.Padding = new Thickness(11, 5, 11, 5);
                b.MinHeight = 28;
                mainActions.Children.Add(b);
            }

            if (item.Kind == ClipKind.Image && item.ImagePngBytes != null)
            {
                AddActionBtn("Copy Image", () =>
                {
                    try
                    {
                        var bmpImg = DecodeBitmapImage(item.ImagePngBytes);
                        if (bmpImg != null)
                        {
                            System.Windows.Clipboard.SetImage(bmpImg);
                            statusBanner.Text = "Image copied to Windows clipboard.";
                        }
                    }
                    catch (Exception ex)
                    {
                        statusBanner.Text = "Copy failed: " + ex.Message;
                    }
                }, primary: true);

                AddActionBtn("Extract OCR", async () =>
                {
                    try
                    {
                        statusBanner.Text = "Running Windows OCR on image...";
                        string recognized = await DeskSpaceScreenToolsV380.RecognizePngBytesAsync(item.ImagePngBytes);
                        if (string.IsNullOrWhiteSpace(recognized))
                        {
                            statusBanner.Text = "No readable text found in image.";
                            return;
                        }
                        PasteAsPlainText(recognized);
                        AddTextEntry(recognized, "Image OCR");
                        statusBanner.Text = $"Extracted {recognized.Length} chars via OCR and copied to clipboard.";
                    }
                    catch (Exception ex)
                    {
                        statusBanner.Text = "OCR unavailable: " + ex.Message;
                    }
                }, primary: false, tip: "Extract text from image using WinRT OCR");
            }
            else if (item.Kind == ClipKind.Files && item.FilePaths.Count > 0)
            {
                AddActionBtn("Copy Plain", () =>
                {
                    PasteAsPlainText(string.Join(Environment.NewLine, item.FilePaths));
                    statusBanner.Text = "Copied file path(s) as plain text.";
                }, primary: true);

                AddActionBtn("Copy File Drop", () =>
                {
                    try
                    {
                        var sc = new System.Collections.Specialized.StringCollection();
                        foreach (string fp in item.FilePaths)
                        {
                            if (File.Exists(fp) || Directory.Exists(fp))
                                sc.Add(fp);
                        }
                        if (sc.Count > 0)
                        {
                            System.Windows.Clipboard.SetFileDropList(sc);
                            statusBanner.Text = $"Copied {sc.Count} file(s) for Explorer paste.";
                        }
                    }
                    catch (Exception ex)
                    {
                        statusBanner.Text = "File copy failed: " + ex.Message;
                    }
                });
            }
            else
            {
                string valueToCopy = item.Kind == ClipKind.Color ? item.ColorHex : item.Text;
                AddActionBtn("Copy Plain", () =>
                {
                    PasteAsPlainText(valueToCopy);
                    statusBanner.Text = "Copied plain text to clipboard.";
                }, primary: true);
            }

            AddActionBtn(item.Pinned ? "Unpin" : "Pin", () =>
            {
                TogglePin(item.Id);
                statusBanner.Text = item.Pinned ? "Unpinned item." : "Pinned item (protected from Clear Unpinned and cap eviction).";
            });

            AddActionBtn("Send to Device", () =>
            {
                SendItemToDevice(item, statusBanner);
            }, tip: "Send item to active connected DeskTouch device");

            AddActionBtn("Delete", () =>
            {
                Remove(item.Id);
                statusBanner.Text = "Item removed from clipboard history.";
            });

            actionBarContent.Children.Add(mainActions);

            if (item.Kind is ClipKind.Text or ClipKind.Color)
            {
                var transformPanel = new WrapPanel();
                transformPanel.Children.Add(new TextBlock
                {
                    Text = "Transform →",
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Accent),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 4)
                });

                string[] quickOps =
                {
                    "Format JSON", "Minify JSON", "Clean Whitespace", "Extract URLs", "Extract Emails",
                    "Title Case", "snake_case", "camelCase", "CSV -> Markdown", "UPPERCASE", "lowercase",
                    "Unique lines", "Sort lines"
                };

                foreach (string op in quickOps)
                {
                    string capturedOp = op;
                    var chip = DeskSpaceFluentBackdropV380.CreatePillButton(capturedOp, () =>
                    {
                        try
                        {
                            string transformed = TransformQuick(item.Text, capturedOp);
                            if (string.IsNullOrEmpty(transformed))
                            {
                                statusBanner.Text = $"'{capturedOp}' produced no output.";
                                return;
                            }
                            PasteAsPlainText(transformed);
                            AddTextEntry(transformed, capturedOp);
                            statusBanner.Text = $"Applied '{capturedOp}' and copied result to clipboard.";
                        }
                        catch (Exception ex)
                        {
                            statusBanner.Text = $"Cannot apply '{capturedOp}': {ex.Message}";
                        }
                    });
                    chip.FontSize = 11;
                    chip.Padding = new Thickness(9, 4, 9, 4);
                    chip.MinHeight = 25;
                    chip.Margin = new Thickness(0, 0, 5, 4);
                    transformPanel.Children.Add(chip);
                }

                actionBarContent.Children.Add(transformPanel);
            }
        }

        void RefreshList()
        {
            var all = Items;
            IEnumerable<RichClipItem> filtered = activeCategory switch
            {
                "Pinned" => all.Where(x => x.Pinned),
                "Text" => all.Where(x => x.Kind == ClipKind.Text),
                "Images" => all.Where(x => x.Kind == ClipKind.Image),
                "Colors" => all.Where(x => x.Kind == ClipKind.Color),
                "Files" => all.Where(x => x.Kind == ClipKind.Files),
                _ => all
            };

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                string q = searchQuery.Trim();
                filtered = filtered.Where(x =>
                    x.Text.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.ColorHex.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.SourceSummary.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.FilePaths.Any(f => f.Contains(q, StringComparison.OrdinalIgnoreCase)));
            }

            var listSnapshot = filtered.ToList();
            itemsList.Items.Clear();

            ListBoxItem? toSelect = null;
            foreach (var item in listSnapshot)
            {
                var card = BuildRichListItem(item, p);
                var lbi = new ListBoxItem
                {
                    Tag = item.Id,
                    Content = card,
                    Padding = new Thickness(4),
                    Margin = new Thickness(0, 0, 0, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                itemsList.Items.Add(lbi);
                if (selectedId.HasValue && item.Id == selectedId.Value)
                    toSelect = lbi;
            }

            if (toSelect != null)
                itemsList.SelectedItem = toSelect;
            else if (itemsList.Items.Count > 0)
                itemsList.SelectedIndex = 0;
            else
                selectedId = null;

            RefreshActionBar();
        }

        void UpdateCategoryStyles()
        {
            foreach (var pair in categoryButtons)
            {
                bool active = pair.Key == activeCategory;
                pair.Value.Background = DeskSpaceDesignV283.Solid(active ? p.AccentSoft : p.Raised);
                pair.Value.BorderBrush = DeskSpaceDesignV283.Solid(active ? p.Accent : p.Border);
                pair.Value.FontWeight = active ? FontWeights.SemiBold : FontWeights.Medium;
            }
        }

        foreach (string cat in categories)
        {
            string capturedCat = cat;
            var chip = DeskSpaceFluentBackdropV380.CreatePillButton(capturedCat, () =>
            {
                activeCategory = capturedCat;
                UpdateCategoryStyles();
                RefreshList();
            });
            chip.Padding = new Thickness(12, 5, 12, 5);
            chip.MinHeight = 28;
            chip.Margin = new Thickness(0, 0, 6, 4);
            categoryButtons[capturedCat] = chip;
            chipPanel.Children.Add(chip);
        }
        UpdateCategoryStyles();
        topPanel.Children.Add(chipPanel);
        topPanel.Children.Add(statusBanner);

        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        DockPanel.SetDock(actionBar, Dock.Bottom);
        root.Children.Add(actionBar);

        root.Children.Add(itemsList);

        win.Content = root;
        DeskSpaceFluentBackdropV380.ApplyFadeEntrance(root);

        liveToggle.Checked += (_, _) =>
        {
            SetCaptureEnabled(true, win);
            statusBanner.Text = "Live capture is ON (WM_CLIPBOARDUPDATE listener active).";
        };
        liveToggle.Unchecked += (_, _) =>
        {
            SetCaptureEnabled(false, win);
            statusBanner.Text = "Live capture paused. Use Capture Now for manual capture.";
        };

        searchBox.TextChanged += (_, _) =>
        {
            searchQuery = searchBox.Text;
            RefreshList();
        };

        itemsList.SelectionChanged += (_, _) =>
        {
            if (itemsList.SelectedItem is ListBoxItem lbi && lbi.Tag is Guid id)
                selectedId = id;
            RefreshActionBar();
        };

        itemsList.MouseDoubleClick += (_, _) =>
        {
            var item = GetSelectedItem();
            if (item == null)
                return;
            if (item.Kind is ClipKind.Text or ClipKind.Color)
            {
                PasteAsPlainText(item.Kind == ClipKind.Color ? item.ColorHex : item.Text);
                statusBanner.Text = "Copied plain text to clipboard.";
            }
        };

        void OnItemsChanged()
        {
            if (!win.Dispatcher.HasShutdownStarted)
                win.Dispatcher.BeginInvoke(new Action(RefreshList));
        }

        ItemsChanged += OnItemsChanged;
        win.Closed += (_, _) =>
        {
            ItemsChanged -= OnItemsChanged;
            if (ReferenceEquals(_openWindow, win))
                _openWindow = null;
        };

        RefreshList();
        win.Show();
        win.Activate();
    }

    private static FrameworkElement BuildRichListItem(RichClipItem item, DeskSpaceDesignV283.Palette p)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = DeskSpaceDesignV283.Solid(p.Raised),
            BorderBrush = DeskSpaceDesignV283.Solid(item.Pinned ? p.Accent : p.Border),
            BorderThickness = new Thickness(item.Pinned ? 1.5 : 1),
            Padding = new Thickness(10, 8, 10, 8)
        };

        var stack = new StackPanel();

        var metaRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 5) };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal };

        if (item.Pinned)
        {
            badgeRow.Children.Add(CreateTagPill("PINNED", p.AccentSoft, p.Accent));
        }

        badgeRow.Children.Add(CreateTagPill(item.Kind.ToString().ToUpperInvariant(), p.Navigation, p.Muted));

        badgeRow.Children.Add(new TextBlock
        {
            Text = item.SourceSummary,
            FontSize = 11,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        });
        DockPanel.SetDock(badgeRow, Dock.Left);
        metaRow.Children.Add(badgeRow);

        var timeLabel = new TextBlock
        {
            Text = item.CapturedUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture),
            FontSize = 11,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(timeLabel, Dock.Right);
        metaRow.Children.Add(timeLabel);
        stack.Children.Add(metaRow);

        if (item.Kind == ClipKind.Color)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            Color swatchColor = Colors.Transparent;
            try
            {
                if (ColorConverter.ConvertFromString(item.ColorHex) is Color parsed)
                    swatchColor = parsed;
            }
            catch
            {
            }

            row.Children.Add(new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(swatchColor),
                BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0)
            });

            row.Children.Add(new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = item.ColorHex,
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = DeskSpaceDesignV283.Solid(p.Text)
                    },
                    new TextBlock
                    {
                        Text = $"rgb({swatchColor.R}, {swatchColor.G}, {swatchColor.B})",
                        FontSize = 11.5,
                        Foreground = DeskSpaceDesignV283.Solid(p.Muted)
                    }
                }
            });
            stack.Children.Add(row);
        }
        else if (item.Kind == ClipKind.Image && item.ImagePngBytes != null)
        {
            var bmp = DecodeBitmapImage(item.ImagePngBytes);
            if (bmp != null)
            {
                stack.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(6),
                    BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new Image
                    {
                        Source = bmp,
                        MaxHeight = 90,
                        MaxWidth = 220,
                        Stretch = Stretch.Uniform
                    }
                });
            }
        }
        else if (item.Kind == ClipKind.Files)
        {
            string preview = string.Join(Environment.NewLine, item.FilePaths.Take(3));
            if (item.FilePaths.Count > 3)
                preview += $"{Environment.NewLine}+{item.FilePaths.Count - 3} more";

            stack.Children.Add(new TextBlock
            {
                Text = preview,
                FontSize = 12.5,
                Foreground = DeskSpaceDesignV283.Solid(p.Text),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 64
            });
        }
        else
        {
            string preview = item.Text.Length > 320 ? item.Text.Substring(0, 320) + "…" : item.Text;
            stack.Children.Add(new TextBlock
            {
                Text = preview,
                FontSize = 12.5,
                Foreground = DeskSpaceDesignV283.Solid(p.Text),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 62
            });
        }

        border.Child = stack;
        return border;
    }

    private static Border CreateTagPill(string label, Color bg, Color fg)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = DeskSpaceDesignV283.Solid(bg),
            Padding = new Thickness(6, 1.5, 6, 1.5),
            Margin = new Thickness(0, 0, 5, 0),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = DeskSpaceDesignV283.Solid(fg)
            }
        };
    }

    private static BitmapImage? DecodeBitmapImage(byte[] pngBytes)
    {
        try
        {
            using var ms = new MemoryStream(pngBytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static void SendItemToDevice(RichClipItem item, TextBlock statusBanner)
    {
        try
        {
            DeskSpaceDeskTouchBridgeV378.PollIfDueAsync(force: true);
            var snap = DeskSpaceDeskTouchBridgeV378.Current;
            var target = snap.Devices.FirstOrDefault(d => d.Id == snap.ActiveTargetId && !d.IsLocal && d.SupportsInput)
                         ?? snap.Devices.FirstOrDefault(d => !d.IsLocal && d.SupportsInput);

            if (target == null)
            {
                statusBanner.Text = "No connected DeskTouch device found. Open DeskTouch Vision to pair a device.";
                return;
            }

            if (item.Kind == ClipKind.Files && item.FilePaths.Count > 0)
            {
                DeskSpaceDeskTouchBridgeV378.DropFilePathAsync(target.Id, item.FilePaths[0]);
                statusBanner.Text = $"Sending file to {target.Name}...";
                return;
            }

            if (item.Kind == ClipKind.Image && item.ImagePngBytes != null)
            {
                string tmpImg = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"deskspace_clip_{Guid.NewGuid():N}.png");
                File.WriteAllBytes(tmpImg, item.ImagePngBytes);
                DeskSpaceDeskTouchBridgeV378.DropFilePathAsync(target.Id, tmpImg);
                statusBanner.Text = $"Sending image to {target.Name}...";
                return;
            }

            string payload = item.Kind == ClipKind.Color ? item.ColorHex : item.Text;
            string tmpTxt = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"deskspace_clip_{Guid.NewGuid():N}.txt");
            File.WriteAllText(tmpTxt, payload, Encoding.UTF8);
            DeskSpaceDeskTouchBridgeV378.DropFilePathAsync(target.Id, tmpTxt);
            statusBanner.Text = $"Sent clip to {target.Name}.";
        }
        catch (Exception ex)
        {
            statusBanner.Text = "Send to device failed: " + ex.Message;
        }
    }
}

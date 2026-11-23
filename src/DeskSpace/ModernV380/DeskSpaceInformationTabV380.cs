using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;

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

internal sealed record AppInfoEligibilityV380(
    string AppPath,
    string AppDisplayName,
    string ExeName,
    bool IsSupported,
    string CategoryLabel,
    string Reason,
    IReadOnlyList<string> SupportedExtensions);

internal sealed record AppInfoHeaderFieldV380(
    string Header,
    int MatchingFileCount,
    int TotalFileCount,
    string SampleValue,
    bool IsDefaultVisible);

internal sealed record AppInfoFileRecordV380(
    string DisplayName,
    string FullPath,
    string KindBadge,
    DateTime ModifiedUtc,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<KeyValuePair<string, string>> OrderedFields);

internal sealed record AppInfoDiscoveryResultV380(
    AppInfoEligibilityV380 Eligibility,
    int TotalFilesScanned,
    int QualifyingFileCount,
    bool MeetsMinimumTwoFilesRule,
    string StatusMessage,
    IReadOnlyList<AppInfoHeaderFieldV380> MatchedHeaders,
    IReadOnlyList<AppInfoFileRecordV380> MatchedFiles,
    IReadOnlyList<string> VisibleHeaders);

/// <summary>
/// First-class in-app Information Collections & Application Inside-File Header->Value module
/// inside MainWindow (and standalone fallback), backed by information-collections-v26.json,
/// app-information-preferences-v380.json, and AES-256-GCM DeskSpaceVaultV10.
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

    internal static string DefaultAppInfoPreferencesPath =>
        System.IO.Path.Combine(WorkspaceStore.Root, "app-information-preferences-v380.json");

    private static readonly object PreferencesLock = new();

    // Exact 95-character printable ASCII (0x20..0x7E) bijection used by Winman .wgst and .tax records.
    private static readonly (byte Cipher, char Plain)[] WinmanCipherPairs =
    {
        (0x7A, '('), (0x7B, 'm'), (0x7C, 'k'), (0x7D, 'n'), (0x7E, 'G'),
        (0x75, '$'), (0x76, '%'), (0x77, '^'), (0x78, '&'), (0x79, '*'),
        (0x70, '~'), (0x71, '`'), (0x72, '!'), (0x73, '@'), (0x74, '#'),
        (0x6B, 'V'), (0x6C, 'W'), (0x6D, 'X'), (0x6E, 'Y'), (0x6F, 'Z'),
        (0x66, '5'), (0x67, '6'), (0x68, '7'), (0x69, '8'), (0x6A, '9'),
        (0x61, '0'), (0x62, '1'), (0x63, '2'), (0x64, '3'), (0x65, '4'),
        (0x5C, 'l'), (0x5D, 'p'), (0x5E, 'b'), (0x5F, 'h'), (0x60, 'H'),
        (0x56, 'B'), (0x57, 'C'), (0x58, 'D'), (0x59, 'E'), (0x5A, 'F'), (0x5B, 'o'),
        (0x50, '<'), (0x51, ','), (0x52, '>'), (0x53, '.'), (0x54, '?'), (0x55, '/'),
        (0x4B, ']'), (0x4C, ':'), (0x4D, ';'), (0x4E, '"'), (0x4F, '\''),
        (0x46, '|'), (0x47, '\\'), (0x48, '{'), (0x49, '}'), (0x4A, '['),
        (0x40, 'J'), (0x41, ' '), (0x42, '-'), (0x43, '_'), (0x44, '+'), (0x45, '='),
        (0x3B, 'r'), (0x3C, 'u'), (0x3D, 'j'), (0x3E, 'w'), (0x3F, 'y'),
        (0x36, 'T'), (0x37, 'L'), (0x38, 'R'), (0x39, 'S'), (0x3A, 'q'),
        (0x30, 'U'), (0x31, 'N'), (0x32, 'Q'), (0x33, 'P'), (0x34, 'O'), (0x35, 'M'),
        (0x2B, 'i'), (0x2C, 'v'), (0x2D, 'g'), (0x2E, 'x'), (0x2F, 'z'),
        (0x25, 'a'), (0x26, 'c'), (0x27, 't'), (0x28, 'e'), (0x29, 'f'), (0x2A, 'd'),
        (0x20, 'A'), (0x21, 'I'), (0x22, 's'), (0x23, 'K'), (0x24, ')')
    };

    private static readonly char[] WinmanDecodeMap = BuildWinmanDecodeMap();
    private static readonly byte[] WinmanEncodeMap = BuildWinmanEncodeMap();

    private static readonly Dictionary<string, string> IndianGstStateCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = "Jammu & Kashmir",
        ["02"] = "Himachal Pradesh",
        ["03"] = "Punjab",
        ["04"] = "Chandigarh",
        ["05"] = "Uttarakhand",
        ["06"] = "Haryana",
        ["07"] = "Delhi",
        ["08"] = "Rajasthan",
        ["09"] = "Uttar Pradesh",
        ["10"] = "Bihar",
        ["11"] = "Sikkim",
        ["12"] = "Arunachal Pradesh",
        ["13"] = "Nagaland",
        ["14"] = "Manipur",
        ["15"] = "Mizoram",
        ["16"] = "Tripura",
        ["17"] = "Meghalaya",
        ["18"] = "Assam",
        ["19"] = "West Bengal",
        ["20"] = "Jharkhand",
        ["21"] = "Odisha",
        ["22"] = "Chhattisgarh",
        ["23"] = "Madhya Pradesh",
        ["24"] = "Gujarat",
        ["25"] = "Daman & Diu",
        ["26"] = "Dadra & Nagar Haveli",
        ["27"] = "Maharashtra",
        ["28"] = "Andhra Pradesh (Old)",
        ["29"] = "Karnataka",
        ["30"] = "Goa",
        ["31"] = "Lakshadweep",
        ["32"] = "Kerala",
        ["33"] = "Tamil Nadu",
        ["34"] = "Puducherry",
        ["35"] = "Andaman & Nicobar Islands",
        ["36"] = "Telangana",
        ["37"] = "Andhra Pradesh",
        ["38"] = "Ladakh"
    };

    private static readonly HashSet<string> DefaultPriorityHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Legal Name", "Trade Name", "Assessee Name", "Company Name", "Mailing Name",
        "GSTIN", "PAN", "PAN / Income Tax No.", "State & State Code", "State",
        "Financial Year", "Assessment Year", "Taxpayer / Dealer Type", "Constitution / Status",
        "Status / Category", "Return Form", "Filing Type", "Company Code", "Tally Edition",
        "Mobile Number", "Phone / Mobile", "Email ID", "PIN Code", "City / Town",
        "Date of Birth / Incorporation", "Gross Total Income", "Total Taxable Income",
        "Net Tax", "Tax Payable / Refund", "Outward Supplies", "Inward Supplies", "Total Tax Payable"
    };

    private static readonly HashSet<string> UnsupportedProseOrMediaExes = new(StringComparer.OrdinalIgnoreCase)
    {
        "winword.exe", "wordpad.exe", "swriter.exe",
        "powerpnt.exe", "simpress.exe",
        "vlc.exe", "wmplayer.exe", "mpc-hc64.exe", "potplayermini64.exe",
        "mspaint.exe", "photoshop.exe", "paintdotnet.exe",
        "acrord32.exe", "acrobat.exe", "foxitpdfreader.exe", "sumatrapdf.exe",
        "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe",
        "7zfm.exe", "winrar.exe"
    };

    private static readonly HashSet<string> UnstructuredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".docm", ".dotx", ".rtf", ".odt", ".txt", ".md", ".log",
        ".ppt", ".pptx", ".pptm", ".ppsx", ".odp",
        ".pdf", ".html", ".htm",
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".mp3", ".wav", ".flac", ".m4a", ".aac",
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".svg", ".psd",
        ".zip", ".7z", ".rar", ".tar", ".gz", ".xz"
    };

    private static char[] BuildWinmanDecodeMap()
    {
        char[] table = new char[256];
        for (int i = 0; i < 256; i++)
            table[i] = (char)i;
        foreach (var (cipher, plain) in WinmanCipherPairs)
            table[cipher] = plain;
        return table;
    }

    private static byte[] BuildWinmanEncodeMap()
    {
        byte[] table = new byte[256];
        for (int i = 0; i < 256; i++)
            table[i] = (byte)i;
        foreach (var (cipher, plain) in WinmanCipherPairs)
            table[(byte)plain] = cipher;
        return table;
    }

    internal static string DecodeWinmanString(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return string.Empty;
        char[] chars = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
            chars[i] = WinmanDecodeMap[bytes[i]];
        return new string(chars);
    }

    internal static byte[] EncodeWinmanString(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return Array.Empty<byte>();
        byte[] result = new byte[plainText.Length];
        for (int i = 0; i < plainText.Length; i++)
        {
            char c = plainText[i];
            result[i] = c < 256 ? WinmanEncodeMap[(byte)c] : (byte)'?';
        }
        return result;
    }

    internal static byte[] BuildSyntheticWinmanRecord(IReadOnlyList<string?> fields35)
    {
        using var ms = new MemoryStream();
        for (int i = 0; i < 35; i++)
        {
            string val = (fields35 != null && i < fields35.Count && fields35[i] != null)
                ? fields35[i]!
                : string.Empty;
            byte[] encoded = EncodeWinmanString(val);
            ushort len = (ushort)Math.Min(ushort.MaxValue, encoded.Length);
            ms.WriteByte((byte)(len & 0xFF));
            ms.WriteByte((byte)((len >> 8) & 0xFF));
            if (len > 0)
                ms.Write(encoded, 0, len);
        }
        return ms.ToArray();
    }

    internal static string? TryReadWinmanInAppDisplayName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            int toRead = (int)Math.Min(1024, fs.Length);
            if (toRead < 80)
                return null;
            byte[] buf = new byte[toRead];
            int read = 0;
            while (read < toRead)
            {
                int n = fs.Read(buf, read, toRead - read);
                if (n <= 0) break;
                read += n;
            }
            if (!TryReadWinmanClientInfoRecord(buf, read, 0, out string[] row0, out _))
                return null;
            if (!row0[0].StartsWith("CLIENT-", StringComparison.OrdinalIgnoreCase))
                return null;

            string primaryName = row0[15].Trim();
            return DeskSpacePinnedAppFilesV380.CleanAndValidateInAppTitle(primaryName, allowShort: true);
        }
        catch
        {
            return null;
        }
    }

    // =========================================================================
    // Application Eligibility & Inside-File Header -> Value Discovery Engine
    // =========================================================================

    internal static AppInfoEligibilityV380 EvaluateAppEligibility(string appOrShortcutPath)
    {
        if (string.IsNullOrWhiteSpace(appOrShortcutPath))
        {
            return new AppInfoEligibilityV380(
                string.Empty,
                "Unknown Application",
                string.Empty,
                false,
                "Unsupported",
                "Select a valid application or shortcut (.exe / .lnk).",
                Array.Empty<string>());
        }

        var cap = DeskSpacePinnedAppFilesV380.ResolveCapability(appOrShortcutPath);
        string exeLower = (cap.ExeName ?? string.Empty).Trim().ToLowerInvariant();
        string pinBase = System.IO.Path.GetFileNameWithoutExtension(appOrShortcutPath.Trim().Trim('"'));
        string displayName = !string.IsNullOrWhiteSpace(cap.DisplayName) ? cap.DisplayName : pinBase;

        // 1. Explicitly reject unstructured prose / presentation / media / graphics / browser / archive apps
        if (UnsupportedProseOrMediaExes.Contains(exeLower) ||
            pinBase.Contains("word", StringComparison.OrdinalIgnoreCase) ||
            pinBase.Contains("powerpoint", StringComparison.OrdinalIgnoreCase))
        {
            string reason = exeLower switch
            {
                "winword.exe" or "wordpad.exe" or "swriter.exe" =>
                    $"{displayName} stores free-form prose paragraphs rather than structured Header → Value records.",
                "powerpnt.exe" or "simpress.exe" =>
                    $"{displayName} opens presentation slides rather than structured Header → Value data files.",
                "vlc.exe" or "wmplayer.exe" or "mpc-hc64.exe" or "potplayermini64.exe" =>
                    $"{displayName} plays audio/video streams rather than structured Header → Value files.",
                "mspaint.exe" or "photoshop.exe" or "paintdotnet.exe" =>
                    $"{displayName} edits raster/vector images rather than structured Header → Value records.",
                "acrord32.exe" or "acrobat.exe" or "foxitpdfreader.exe" or "sumatrapdf.exe" =>
                    $"{displayName} opens unstructured PDF prose documents rather than structured Header → Value records.",
                "chrome.exe" or "msedge.exe" or "firefox.exe" or "brave.exe" =>
                    $"{displayName} opens unstructured web pages and documents.",
                "7zfm.exe" or "winrar.exe" =>
                    $"{displayName} opens compressed archives rather than structured Header → Value records.",
                _ => $"{displayName} opens unstructured prose/media documents and does not support Header → Value extraction."
            };
            return new AppInfoEligibilityV380(
                appOrShortcutPath,
                displayName,
                cap.ExeName ?? string.Empty,
                false,
                "Unstructured Prose / Media (Not Supported)",
                reason,
                cap.Extensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToArray());
        }

        // 2. TallyPrime & Tally.ERP 9 Accounting Companies (.1800 / .900)
        if (cap.Kind == PinnedAppKindV380.TallyAccounting || cap.IncludesTallyCompanies)
        {
            string[] tallyFormats = cap.TallyEdition switch
            {
                TallyEditionModeV380.TallyPrime1800 => new[] { "Company.1800" },
                TallyEditionModeV380.TallyErp900 => new[] { "Company.900" },
                _ => new[] { "Company.1800", "Company.900" }
            };
            string cat = cap.TallyEdition switch
            {
                TallyEditionModeV380.TallyPrime1800 => "TallyPrime Company Master (Company.1800)",
                TallyEditionModeV380.TallyErp900 => "Tally.ERP 9 Company Master (Company.900)",
                _ => "Tally Company Master (Company.1800 / Company.900)"
            };
            return new AppInfoEligibilityV380(
                appOrShortcutPath,
                displayName,
                cap.ExeName ?? "tally.exe",
                true,
                cat,
                $"Supports structured inside-file Header → Value master tags ({string.Join(", ", tallyFormats)}).",
                tallyFormats);
        }

        // 3. Winman GST / Winman CA-ERP / Winman TDS
        if (cap.Kind == PinnedAppKindV380.WinmanAccounting ||
            exeLower is "wintax.exe" or "winman_launcher.exe" or "wintds.exe" ||
            pinBase.Contains("winman", StringComparison.OrdinalIgnoreCase))
        {
            bool isGstShortcut = pinBase.Contains("gst", StringComparison.OrdinalIgnoreCase) &&
                                 !pinBase.Contains("ca", StringComparison.OrdinalIgnoreCase) &&
                                 !pinBase.Contains("erp", StringComparison.OrdinalIgnoreCase) &&
                                 !pinBase.Contains("saral", StringComparison.OrdinalIgnoreCase);
            bool isCaErpShortcut = (pinBase.Contains("ca", StringComparison.OrdinalIgnoreCase) ||
                                    pinBase.Contains("erp", StringComparison.OrdinalIgnoreCase) ||
                                    pinBase.Contains("saral", StringComparison.OrdinalIgnoreCase)) &&
                                   !pinBase.Contains("gst", StringComparison.OrdinalIgnoreCase);
            bool isTdsShortcut = exeLower == "wintds.exe" ||
                                 pinBase.Contains("tds", StringComparison.OrdinalIgnoreCase);

            if (isGstShortcut)
            {
                return new AppInfoEligibilityV380(
                    appOrShortcutPath,
                    "Winman GST",
                    cap.ExeName ?? "WinTax.exe",
                    true,
                    "Winman GST Return (.wgst)",
                    "Supports structured inside-file Winman GST Header → Value records (.wgst).",
                    new[] { ".wgst" });
            }
            if (isTdsShortcut)
            {
                return new AppInfoEligibilityV380(
                    appOrShortcutPath,
                    "Winman TDS",
                    cap.ExeName ?? "WinTDS.exe",
                    true,
                    "Winman TDS Return (.24Q / .26Q / .27Q)",
                    "Supports structured inside-file Winman TDS Header → Value records (.24q, .26q, .27q, .27eq, .7eq).",
                    new[] { ".24q", ".26q", ".27q", ".27eq", ".7eq" });
            }
            if (isCaErpShortcut)
            {
                return new AppInfoEligibilityV380(
                    appOrShortcutPath,
                    "Winman CA ERP",
                    cap.ExeName ?? "WinTax.exe",
                    true,
                    "Winman CA-ERP Income Tax (.tax)",
                    "Supports structured inside-file Winman CA-ERP Header → Value records (.tax).",
                    new[] { ".tax" });
            }

            return new AppInfoEligibilityV380(
                appOrShortcutPath,
                displayName,
                cap.ExeName ?? "WinTax.exe",
                true,
                "Winman GST & Income Tax (.wgst / .tax)",
                "Supports structured inside-file Winman Header → Value records (.wgst, .tax, .24q, .26q).",
                new[] { ".wgst", ".tax", ".24q", ".26q", ".27q", ".27eq", ".7eq" });
        }

        // 4. Microsoft Excel / Spreadsheets (.xlsx / .xlsm / .csv / .tsv)
        if (exeLower is "excel.exe" or "scalc.exe" ||
            cap.Extensions.Contains(".xlsx") ||
            cap.Extensions.Contains(".csv"))
        {
            var sheetExts = cap.Extensions
                .Where(e => e is ".xlsx" or ".xlsm" or ".csv" or ".tsv")
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (sheetExts.Count == 0)
                sheetExts.AddRange(new[] { ".xlsx", ".csv" });

            return new AppInfoEligibilityV380(
                appOrShortcutPath,
                displayName,
                cap.ExeName ?? "EXCEL.EXE",
                true,
                "Structured Spreadsheet Table (.xlsx / .csv)",
                "Supports structured column & key-value Header → Value pairs inside workbooks and CSV tables.",
                sheetExts);
        }

        // 5. Generic structured key-value apps (.json / .ini / .cfg / .xml / .yaml / .csproj)
        var structuredExts = cap.Extensions
            .Where(e => e is ".json" or ".ini" or ".cfg" or ".xml" or ".yaml" or ".yml" or ".toml" or ".csproj" or ".csv" or ".tsv" or ".xlsx" or ".wgst" or ".tax")
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToList();

        bool onlyUnstructured = cap.Extensions.Count > 0 &&
                                cap.Extensions.All(UnstructuredExtensions.Contains);
        if (!onlyUnstructured && structuredExts.Count > 0)
        {
            return new AppInfoEligibilityV380(
                appOrShortcutPath,
                displayName,
                cap.ExeName ?? string.Empty,
                true,
                $"Structured Key → Value Data ({string.Join(", ", structuredExts.Take(4))})",
                $"Supports structured inside-file Header → Value properties ({string.Join(", ", structuredExts)}).",
                structuredExts);
        }

        return new AppInfoEligibilityV380(
            appOrShortcutPath,
            displayName,
            cap.ExeName ?? string.Empty,
            false,
            "Unstructured Document Application",
            $"{displayName} does not use structured Header → Value data files.",
            cap.Extensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    internal static bool IsValidHeaderValuePair(string? rawHeader, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawHeader) || string.IsNullOrWhiteSpace(rawValue))
            return false;

        string header = rawHeader.Trim();
        string value = Regex.Replace(rawValue.Trim(), @"\s+", " ");

        if (header.Length is < 2 or > 52)
            return false;
        if (value.Length is < 1 or > 180)
            return false;

        // Header must have at least 2 letters and not be a file path, URL, or pure number
        int headerLetters = 0;
        foreach (char c in header)
        {
            if (c == '\uFFFD' || (char.IsControl(c) && c != '\t'))
                return false;
            if (char.IsLetter(c))
                headerLetters++;
        }
        if (headerLetters < 2)
            return false;
        if (header.Contains(":\\", StringComparison.Ordinal) ||
            header.StartsWith(@"\\", StringComparison.Ordinal) ||
            header.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            header.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Value must contain at least 1 letter or digit, no binary control garbage, and not be random multi-sentence prose
        int valAlnum = 0;
        foreach (char c in value)
        {
            if (c == '\uFFFD' || (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                return false;
            if (char.IsLetterOrDigit(c))
                valAlnum++;
        }
        if (valAlnum == 0)
            return false;

        if (value.Equals("-", StringComparison.Ordinal) ||
            value.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("`", StringComparison.Ordinal) ||
            value.StartsWith("%%F%%", StringComparison.Ordinal) ||
            value.StartsWith("[IF(", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("S#$X=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reject prose paragraphs (e.g., > 18 words or multiple sentence endings)
        int wordCount = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > 18)
            return false;

        return true;
    }

    internal static bool TryExtractFileHeaderValues(
        string path,
        out List<KeyValuePair<string, string>> fields,
        out string displayName,
        out string kindBadge)
    {
        fields = new List<KeyValuePair<string, string>>();
        displayName = string.Empty;
        kindBadge = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            if (Directory.Exists(path))
            {
                return TryExtractTallyCompanyFolderHeaders(path, out fields, out displayName, out kindBadge);
            }

            if (!File.Exists(path))
                return false;

            string fileName = System.IO.Path.GetFileName(path);
            if (fileName.Equals("Company.1800", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("Company.900", StringComparison.OrdinalIgnoreCase))
            {
                string? parentDir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parentDir))
                    return TryExtractTallyCompanyFolderHeaders(parentDir, out fields, out displayName, out kindBadge);
            }

            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".wgst" => TryExtractWinmanFileHeaders(path, isGst: true, out fields, out displayName, out kindBadge),
                ".tax" => TryExtractWinmanFileHeaders(path, isGst: false, out fields, out displayName, out kindBadge),
                ".24q" or ".26q" or ".27q" or ".27eq" or ".7eq" =>
                    TryExtractWinmanTdsFileHeaders(path, ext, out fields, out displayName, out kindBadge),
                ".csv" or ".tsv" =>
                    TryExtractCsvOrTsvFileHeaders(path, ext == ".tsv" ? '\t' : ',', out fields, out displayName, out kindBadge),
                ".xlsx" or ".xlsm" =>
                    TryExtractXlsxFileHeaders(path, out fields, out displayName, out kindBadge),
                ".json" =>
                    TryExtractJsonFileHeaders(path, out fields, out displayName, out kindBadge),
                ".ini" or ".cfg" =>
                    TryExtractIniFileHeaders(path, out fields, out displayName, out kindBadge),
                ".xml" or ".csproj" =>
                    TryExtractXmlFileHeaders(path, out fields, out displayName, out kindBadge),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static void AddFieldIfValid(
        List<KeyValuePair<string, string>> list,
        HashSet<string> seenHeaders,
        string header,
        string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return;
        string cleanedVal = Regex.Replace(rawValue.Replace("\r\n", ", ").Replace('\n', ' ').Trim(), @"\s+", " ");
        if (!IsValidHeaderValuePair(header, cleanedVal))
            return;
        if (seenHeaders.Add(header))
            list.Add(new KeyValuePair<string, string>(header, cleanedVal));
    }

    private static bool TryReadWinmanClientInfoRecord(
        byte[] buffer,
        int bufferLen,
        int startPos,
        out string[] fields,
        out int nextPos)
    {
        fields = new string[35];
        nextPos = startPos;
        for (int i = 0; i < 35; i++)
            fields[i] = string.Empty;

        int pos = startPos;
        for (int f = 0; f < 35; f++)
        {
            if (pos + 2 > bufferLen)
                return false;
            ushort len = BitConverter.ToUInt16(buffer, pos);
            pos += 2;
            if (len > 32768 || pos + len > bufferLen)
                return false;
            fields[f] = len > 0
                ? DecodeWinmanString(buffer.AsSpan(pos, len)).Trim()
                : string.Empty;
            pos += len;
        }

        nextPos = pos;
        return true;
    }

    private static string? FormatWinman20DigitTimestamp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length < 8)
            return null;
        for (int i = 0; i < 8; i++)
        {
            if (!char.IsDigit(raw[i]))
                return null;
        }
        string dd = raw.Substring(0, 2);
        string mm = raw.Substring(2, 2);
        string yyyy = raw.Substring(4, 4);
        if (!int.TryParse(dd, out int day) || day is < 1 or > 31)
            return null;
        if (!int.TryParse(mm, out int month) || month is < 1 or > 12)
            return null;
        if (!int.TryParse(yyyy, out int year) || year is < 2000 or > 2100)
            return null;
        return $"{dd}-{mm}-{yyyy}";
    }

    private static (string CleanGstin, string? Pan, string? StateDisplay) ParseGstinComponents(string? rawGstin)
    {
        if (string.IsNullOrWhiteSpace(rawGstin))
            return (string.Empty, null, null);

        string compact = Regex.Replace(rawGstin.Trim().ToUpperInvariant(), @"\s+", "");
        if (compact.Length == 15 &&
            char.IsDigit(compact[0]) &&
            char.IsDigit(compact[1]) &&
            Regex.IsMatch(compact.Substring(2, 10), @"^[A-Z]{5}[0-9]{4}[A-Z]$"))
        {
            string stateCode = compact.Substring(0, 2);
            string pan = compact.Substring(2, 10);
            string stateLabel = IndianGstStateCodes.TryGetValue(stateCode, out string? stateName)
                ? $"{stateCode} · {stateName}"
                : stateCode;
            return (compact, pan, stateLabel);
        }

        return (rawGstin.Trim(), null, null);
    }

    private static bool TryExtractWinmanFileHeaders(
        string filePath,
        bool isGst,
        out List<KeyValuePair<string, string>> fields,
        out string displayName,
        out string kindBadge)
    {
        fields = new List<KeyValuePair<string, string>>();
        displayName = DeskSpacePinnedAppFilesV380.FormatFriendlyFileName(System.IO.Path.GetFileName(filePath), isDirectory: false);
        kindBadge = isGst ? "Winman GST (.WGST)" : "Winman Income Tax (.TAX)";

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        int toRead = (int)Math.Min(131072, fs.Length);
        if (toRead < 70)
            return false;

        byte[] buf = new byte[toRead];
        int totalRead = 0;
        while (totalRead < toRead)
        {
            int n = fs.Read(buf, totalRead, toRead - totalRead);
            if (n <= 0) break;
            totalRead += n;
        }

        if (!TryReadWinmanClientInfoRecord(buf, totalRead, 0, out string[] row0, out int pos))
            return false;

        if (!row0[0].StartsWith("CLIENT-", StringComparison.OrdinalIgnoreCase))
            return false;

        var jsonMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var scheduleTotals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[]? row9 = null;

        for (int r = 1; r <= 240 && pos + 70 <= totalRead; r++)
        {
            if (!TryReadWinmanClientInfoRecord(buf, totalRead, pos, out string[] row, out pos))
                break;

            if (r == 2 && row[30].StartsWith('{'))
            {
                foreach (Match m in Regex.Matches(row[30], @"""([A-Za-z0-9_]+)""\s*:\s*""([^""]*)"""))
                {
                    string k = m.Groups[1].Value.Trim();
                    string v = m.Groups[2].Value.Trim();
                    if (k.Length > 0 && v.Length > 0 && v != "-" && !v.Equals("NA", StringComparison.OrdinalIgnoreCase) && !v.StartsWith('`'))
                        jsonMap[k] = v;
                }
            }
            else if (r == 9)
            {
                row9 = row;
            }
            else if (r > 2)
            {
                string idxCode = row[10];
                string rowLabel = row[2];
                if (isGst)
                {
                    if (idxCode == "5.30.16.00.00.00" && !string.IsNullOrWhiteSpace(row[4]) && row[4] != "-")
                        scheduleTotals["Outward Supplies"] = row[4];
                    else if (idxCode == "5.30.28.00.00.00" && !string.IsNullOrWhiteSpace(row[4]) && row[4] != "-")
                        scheduleTotals["Inward Supplies"] = row[4];
                    else if (idxCode == "5.30.30.00.00.00" || rowLabel.Equals("Total Tax payable", StringComparison.OrdinalIgnoreCase))
                    {
                        string taxTot = ExtractLastBackslashCurrency(row[4], row[3]);
                        if (!string.IsNullOrEmpty(taxTot))
                            scheduleTotals["Total Tax Payable"] = taxTot;
                    }
                    else if (idxCode == "5.30.34.00.00.00" || rowLabel.Equals("Tax & Interest payable", StringComparison.OrdinalIgnoreCase))
                    {
                        string taxInt = ExtractLastBackslashCurrency(row[4], row[3]);
                        if (!string.IsNullOrEmpty(taxInt))
                            scheduleTotals["Tax & Interest Payable"] = taxInt;
                    }
                }
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (isGst)
        {
            string legalName = jsonMap.TryGetValue("Legal_name", out string? jLegal) && !string.IsNullOrWhiteSpace(jLegal)
                ? jLegal
                : row0[15];
            string tradeName = jsonMap.TryGetValue("Trade_Name", out string? jTrade) && !string.IsNullOrWhiteSpace(jTrade)
                ? jTrade
                : (!string.Equals(row0[17], row0[15], StringComparison.OrdinalIgnoreCase) &&
                   !row0[17].Contains("Supplier", StringComparison.OrdinalIgnoreCase) &&
                   !row0[17].Equals("Regular", StringComparison.OrdinalIgnoreCase)
                    ? row0[17]
                    : legalName);
            string dealerType = jsonMap.TryGetValue("Taxpayer_type", out string? jDealer) && !string.IsNullOrWhiteSpace(jDealer)
                ? jDealer
                : row0[17];
            string rawGstin = jsonMap.TryGetValue("GSTIN", out string? jGstin) && !string.IsNullOrWhiteSpace(jGstin)
                ? jGstin
                : row0[22];
            var (cleanGstin, derivedPan, stateDisplay) = ParseGstinComponents(rawGstin);
            string finYear = jsonMap.TryGetValue("Fin_Year", out string? jFy) && !string.IsNullOrWhiteSpace(jFy)
                ? jFy
                : row0[23];
            string constitution = !string.IsNullOrWhiteSpace(row0[26])
                ? row0[26]
                : (row0[0].Length > 7 ? row0[0].Substring(7).Trim() : string.Empty);
            string returnForm = !string.IsNullOrWhiteSpace(row0[29]) && row0[29] != "0"
                ? row0[29]
                : (row9 != null && !string.IsNullOrWhiteSpace(row9[29]) ? row9[29] : string.Empty);
            string filingType = jsonMap.TryGetValue("MISPriority", out string? jPrio) && !string.IsNullOrWhiteSpace(jPrio)
                ? jPrio
                : row0[8];
            string mobile = jsonMap.TryGetValue("Mobile_number", out string? jMob) && !string.IsNullOrWhiteSpace(jMob)
                ? jMob
                : row0[33];

            if (!string.IsNullOrWhiteSpace(legalName))
                displayName = !string.IsNullOrWhiteSpace(finYear) ? $"{legalName} ({finYear})" : legalName;
            if (!string.IsNullOrWhiteSpace(returnForm))
                kindBadge = $"Winman GST · {returnForm}";

            AddFieldIfValid(fields, seen, "Legal Name", legalName);
            AddFieldIfValid(fields, seen, "Trade Name", tradeName);
            AddFieldIfValid(fields, seen, "GSTIN", cleanGstin);
            AddFieldIfValid(fields, seen, "PAN", derivedPan);
            AddFieldIfValid(fields, seen, "State & State Code", stateDisplay);
            AddFieldIfValid(fields, seen, "Financial Year", finYear);
            AddFieldIfValid(fields, seen, "Taxpayer / Dealer Type", dealerType);
            AddFieldIfValid(fields, seen, "Constitution / Status", constitution);
            AddFieldIfValid(fields, seen, "Return Form", returnForm);
            AddFieldIfValid(fields, seen, "Filing Type", filingType);
            AddFieldIfValid(fields, seen, "Mobile Number", mobile);
            if (jsonMap.TryGetValue("EMail_ID", out string? jEmail))
                AddFieldIfValid(fields, seen, "Email ID", jEmail);
            if (jsonMap.TryGetValue("Address", out string? jAddr))
                AddFieldIfValid(fields, seen, "Address", jAddr);
            if (jsonMap.TryGetValue("Quarterly_Return", out string? jQr))
                AddFieldIfValid(fields, seen, "Quarterly Return", jQr);

            foreach (var kv in scheduleTotals)
                AddFieldIfValid(fields, seen, kv.Key, kv.Value);

            string? createdBy = jsonMap.TryGetValue("Created_by_PC_User", out string? jCb) ? jCb : (row9?[14]);
            string? modifiedBy = jsonMap.TryGetValue("Modified_by_PC_User", out string? jMb) ? jMb : (row9?[15]);
            AddFieldIfValid(fields, seen, "Created By User", createdBy);
            AddFieldIfValid(fields, seen, "Modified By User", modifiedBy);
            AddFieldIfValid(fields, seen, "Created Date", FormatWinman20DigitTimestamp(row0[4]));
            AddFieldIfValid(fields, seen, "Last Saved Date", FormatWinman20DigitTimestamp(row0[6]));
            AddFieldIfValid(fields, seen, "Winman Version", row0[27]);
        }
        else
        {
            string assesseeName = jsonMap.TryGetValue("Name", out string? jName) && !string.IsNullOrWhiteSpace(jName)
                ? jName
                : row0[15];
            string rawPan = jsonMap.TryGetValue("PAN", out string? jPan) && !string.IsNullOrWhiteSpace(jPan)
                ? jPan
                : row0[13];
            string cleanPan = Regex.Replace(rawPan.Trim().ToUpperInvariant(), @"\s+", "");
            string status = jsonMap.TryGetValue("Status", out string? jStatus) && !string.IsNullOrWhiteSpace(jStatus)
                ? jStatus
                : (!string.IsNullOrWhiteSpace(row0[26]) ? row0[26] : (row0[0].Length > 7 ? row0[0].Substring(7).Trim() : string.Empty));
            string asstYear = jsonMap.TryGetValue("Asstyear", out string? jAy) && !string.IsNullOrWhiteSpace(jAy)
                ? jAy
                : row0[23];
            string dob = jsonMap.TryGetValue("DOB__DOI__DOF", out string? jDob) && !string.IsNullOrWhiteSpace(jDob)
                ? jDob
                : row0[17];
            string resStatus = jsonMap.TryGetValue("Residential_Status", out string? jRes) && !string.IsNullOrWhiteSpace(jRes)
                ? jRes
                : row0[12];
            string mobile = jsonMap.TryGetValue("Mobile_Number", out string? jMob) && !string.IsNullOrWhiteSpace(jMob)
                ? jMob
                : row0[33];
            string city = jsonMap.TryGetValue("City", out string? jCity) && !string.IsNullOrWhiteSpace(jCity)
                ? jCity
                : row0[10];
            string area = jsonMap.TryGetValue("Area", out string? jArea) && !string.IsNullOrWhiteSpace(jArea)
                ? jArea
                : row0[32];
            string pinCode = jsonMap.TryGetValue("Pincode", out string? jPin) && !string.IsNullOrWhiteSpace(jPin)
                ? jPin
                : row0[3];
            string address = jsonMap.TryGetValue("Address", out string? jAddr) && !string.IsNullOrWhiteSpace(jAddr)
                ? jAddr
                : (!string.IsNullOrWhiteSpace(row0[22]) ? row0[22] : row0[21]);
            string dueDate = jsonMap.TryGetValue("Due_date", out string? jDue) && !string.IsNullOrWhiteSpace(jDue)
                ? jDue
                : row0[20];
            string filingType = jsonMap.TryGetValue("MISPriority", out string? jPrio) && !string.IsNullOrWhiteSpace(jPrio)
                ? jPrio
                : row0[8];

            if (!string.IsNullOrWhiteSpace(assesseeName))
                displayName = !string.IsNullOrWhiteSpace(asstYear) ? $"{assesseeName} (AY {asstYear})" : assesseeName;
            if (!string.IsNullOrWhiteSpace(status))
                kindBadge = $"Winman Tax · {status}";

            AddFieldIfValid(fields, seen, "Assessee Name", assesseeName);
            if (jsonMap.TryGetValue("Father_name", out string? jFather))
                AddFieldIfValid(fields, seen, "Father Name", jFather);
            AddFieldIfValid(fields, seen, "PAN", cleanPan);
            AddFieldIfValid(fields, seen, "Status / Category", status);
            AddFieldIfValid(fields, seen, "Assessment Year", asstYear);
            AddFieldIfValid(fields, seen, "Date of Birth / Incorporation", dob);
            AddFieldIfValid(fields, seen, "Residential Status", resStatus);
            AddFieldIfValid(fields, seen, "Mobile Number", mobile);
            if (jsonMap.TryGetValue("Telephone_No", out string? jTel))
                AddFieldIfValid(fields, seen, "Telephone Number", jTel);
            if (jsonMap.TryGetValue("Email_ID", out string? jEmail))
                AddFieldIfValid(fields, seen, "Email ID", jEmail);
            if (jsonMap.TryGetValue("Aadhar_No", out string? jAadhaar))
                AddFieldIfValid(fields, seen, "Aadhaar Number", jAadhaar);
            AddFieldIfValid(fields, seen, "Address", address);
            AddFieldIfValid(fields, seen, "Area / Locality", area);
            AddFieldIfValid(fields, seen, "City / Town", city);
            AddFieldIfValid(fields, seen, "State", row0[14]);
            AddFieldIfValid(fields, seen, "PIN Code", pinCode);
            if (jsonMap.TryGetValue("Password_OF_ITR_V", out string? jItrPw))
                AddFieldIfValid(fields, seen, "ITR-V Password", jItrPw);
            AddFieldIfValid(fields, seen, "Return Due Date", dueDate);
            if (jsonMap.TryGetValue("Tax_audit_case", out string? jAudit))
                AddFieldIfValid(fields, seen, "Tax Audit Case", jAudit);
            if (jsonMap.TryGetValue("Category", out string? jCat))
                AddFieldIfValid(fields, seen, "Audit Category", jCat);
            if (jsonMap.TryGetValue("GSTIN", out string? jGstin))
            {
                var (cleanGstin, _, _) = ParseGstinComponents(jGstin);
                AddFieldIfValid(fields, seen, "GSTIN", cleanGstin);
            }
            if (jsonMap.TryGetValue("Salary", out string? jSal))
                AddFieldIfValid(fields, seen, "Income from Salaries", jSal);
            if (jsonMap.TryGetValue("House_Property", out string? jHp))
                AddFieldIfValid(fields, seen, "Income from House Property", jHp);
            if (jsonMap.TryGetValue("Business", out string? jBus))
                AddFieldIfValid(fields, seen, "Business / Profession Income", jBus);
            if (jsonMap.TryGetValue("Capital_Gain", out string? jCg))
                AddFieldIfValid(fields, seen, "Capital Gain", jCg);
            if (jsonMap.TryGetValue("Other_Sources", out string? jOs))
                AddFieldIfValid(fields, seen, "Other Sources Income", jOs);
            if (jsonMap.TryGetValue("Gross_Total_Income", out string? jGti))
                AddFieldIfValid(fields, seen, "Gross Total Income", jGti);
            if (jsonMap.TryGetValue("Deduction_U__C_VIA", out string? jDed))
                AddFieldIfValid(fields, seen, "Chapter VI-A Deductions", jDed);
            if (jsonMap.TryGetValue("Total_Income__Loss", out string? jTi))
                AddFieldIfValid(fields, seen, "Total Taxable Income", jTi);
            if (jsonMap.TryGetValue("Tax_on_Total_Income", out string? jTti))
                AddFieldIfValid(fields, seen, "Tax on Total Income", jTti);
            if (jsonMap.TryGetValue("Rebate_and_Relief", out string? jReb))
                AddFieldIfValid(fields, seen, "Rebate & Relief", jReb);
            if (jsonMap.TryGetValue("Net_Tax", out string? jNet))
                AddFieldIfValid(fields, seen, "Net Tax", jNet);
            if (jsonMap.TryGetValue("TDS", out string? jTds))
                AddFieldIfValid(fields, seen, "TDS Claimed", jTds);
            if (jsonMap.TryGetValue("Advance_Tax_paid", out string? jAdv))
                AddFieldIfValid(fields, seen, "Advance Tax Paid", jAdv);
            if (jsonMap.TryGetValue("Self_asst_tax_paid", out string? jSa))
                AddFieldIfValid(fields, seen, "Self-Assessment Tax Paid", jSa);
            if (jsonMap.TryGetValue("Tax_Payable__Refund", out string? jRef))
                AddFieldIfValid(fields, seen, "Tax Payable / Refund", jRef);
            if (jsonMap.TryGetValue("Gross_Receipts_excluding_44AE_income", out string? jGr))
                AddFieldIfValid(fields, seen, "Gross Receipts", jGr);
            AddFieldIfValid(fields, seen, "Filing Type", filingType);
            if (jsonMap.TryGetValue("Created_by_PC_User", out string? jCb))
                AddFieldIfValid(fields, seen, "Created By User", jCb);
            if (jsonMap.TryGetValue("Modified_by_PC_User", out string? jMb))
                AddFieldIfValid(fields, seen, "Modified By User", jMb);
            AddFieldIfValid(fields, seen, "Created Date", FormatWinman20DigitTimestamp(row0[4]));
            AddFieldIfValid(fields, seen, "Last Saved Date", FormatWinman20DigitTimestamp(row0[6]));
            AddFieldIfValid(fields, seen, "Winman Version", row0[27]);
        }

        return fields.Count >= 2;
    }

    private static string ExtractLastBackslashCurrency(string primary, string fallback)
    {
        foreach (string candidate in new[] { primary, fallback })
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate == "-")
                continue;
            string[] tokens = candidate.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            for (int i = tokens.Length - 1; i >= 0; i--)
            {
                string t = tokens[i].Trim();
                if (t.Length > 0 && t != "-" && char.IsDigit(t[0]))
                    return t;
            }
        }
        return string.Empty;
    }

    private static bool TryExtractWinmanTdsFileHeaders(
        string filePath,
        string ext,
        out List<KeyValuePair<string, string>> fields,
        out string displayName,
        out string kindBadge)
    {
        if (TryExtractWinmanFileHeaders(filePath, isGst: false, out fields, out displayName, out kindBadge))
        {
            kindBadge = $"Winman TDS ({ext.ToUpperInvariant()})";
            return true;
        }

        fields = new List<KeyValuePair<string, string>>();
        displayName = DeskSpacePinnedAppFilesV380.FormatFriendlyFileName(System.IO.Path.GetFileName(filePath), isDirectory: false);
        kindBadge = $"Winman TDS ({ext.ToUpperInvariant()})";

        // NSDL FVU text / caret-delimited batch header fallback
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] buf = new byte[(int)Math.Min(16384, fs.Length)];
        int read = fs.Read(buf, 0, buf.Length);
        if (read <= 0)
            return false;

        string text = Encoding.UTF8.GetString(buf, 0, read);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddFieldIfValid(fields, seen, "Return Form", ext.TrimStart('.').ToUpperInvariant());
        var tanMatch = Regex.Match(text, @"\b([A-Z]{4}[0-9]{5}[A-Z])\b");
        if (tanMatch.Success)
            AddFieldIfValid(fields, seen, "TAN", tanMatch.Groups[1].Value);
        var panMatch = Regex.Match(text, @"\b([A-Z]{5}[0-9]{4}[A-Z])\b");
        if (panMatch.Success)
            AddFieldIfValid(fields, seen, "PAN", panMatch.Groups[1].Value);
        var fyMatch = Regex.Match(text, @"\b(20\d{2}-?(?:20)?\d{2})\b");
        if (fyMatch.Success)
            AddFieldIfValid(fields, seen, "Financial Year", fyMatch.Groups[1].Value);
        var qMatch = Regex.Match(text, @"\b(Q[1-4])\b", RegexOptions.IgnoreCase);
        if (qMatch.Success)
            AddFieldIfValid(fields, seen, "Quarter", qMatch.Groups[1].Value.ToUpperInvariant());

        return fields.Count >= 2;
    }

    private static bool TryExtractTallyCompanyFolderHeaders(
        string folderPath,
        out List<KeyValuePair<string, string>> fields,
        out string displayName,
        out string kindBadge)
    {
        fields = new List<KeyValuePair<string, string>>();
        displayName = string.Empty;
        kindBadge = string.Empty;

        if (!DeskSpacePinnedAppFilesV380.TryParseTallyCompanyFolder(folderPath, out var tallyItem) || tallyItem == null)
            return false;

        displayName = tallyItem.DisplayName;
        kindBadge = tallyItem.KindBadge;

        string? metaPath = new[] { "Company.1800", "CmpSave.1800", "Company.900", "CmpSave.900" }
            .Select(fn => System.IO.Path.Combine(folderPath, fn))
            .FirstOrDefault(File.Exists);
        if (metaPath == null)
            return false;

        bool is1800 = metaPath.EndsWith(".1800", StringComparison.OrdinalIgnoreCase);
        byte[] rawBytes = File.ReadAllBytes(metaPath);
        byte[] buf = DeskSpacePinnedAppFilesV380.TryUnpageTallyBlocks(rawBytes, rawBytes.Length, rawBytes.Length) ?? rawBytes;

        string? companyName = null;
        string? mailingName = null;
        var addressLines = new List<string>();
        string? state = null;
        string? pinCode = null;
        string? country = null;
        string? pan = null;
        string? phone = null;
        string? email = null;
        string? guid = null;
        string? gstin = null;

        for (int i = 0; i <= buf.Length - 10; i++)
        {
            if (buf[i] == 0x02 && buf[i + 1] == 0x10 && buf[i + 3] == 0x00 && buf[i + 4] == 0x00 && buf[i + 5] == 0x0F)
            {
                byte tag = buf[i + 2];
                ushort byteLen = BitConverter.ToUInt16(buf, i + 6);
                if (byteLen < 2 || byteLen > 512 || (byteLen % 2) != 0 || i + 8 + byteLen > buf.Length)
                    continue;

                string rawStr = Encoding.Unicode.GetString(buf, i + 8, byteLen);
                int nul = rawStr.IndexOf('\0');
                if (nul >= 0)
                    rawStr = rawStr.Substring(0, nul);
                string cleanStr = Regex.Replace(rawStr, @"\s+", " ").Trim();
                if (cleanStr.Length == 0)
                    continue;

                switch (tag)
                {
                    case 0x1B:
                        companyName ??= cleanStr;
                        break;
                    case 0x1C:
                        mailingName ??= cleanStr;
                        break;
                    case 0x1D:
                        if (addressLines.Count < 5 && !addressLines.Contains(cleanStr, StringComparer.OrdinalIgnoreCase))
                            addressLines.Add(cleanStr);
                        break;
                    case 0x1E:
                        if (cleanStr.Length >= 8)
                            guid ??= cleanStr;
                        break;
                    case 0x65:
                    case 0x69:
                    case 0xCD:
                    case 0xD8:
                        if (cleanStr.Any(char.IsDigit) && cleanStr.Length >= 6)
                            phone ??= cleanStr;
                        break;
                    case 0x66:
                        if (cleanStr.Contains('@'))
                            email ??= cleanStr;
                        break;
                    case 0x67:
                    case 0x04:
                        if (cleanStr.Length >= 2)
                            state ??= cleanStr;
                        break;
                    case 0x68:
                        if (cleanStr.Any(char.IsDigit))
                            pinCode ??= cleanStr;
                        break;
                    case 0xCA:
                        if (Regex.IsMatch(cleanStr, @"^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.IgnoreCase))
                            pan ??= cleanStr.ToUpperInvariant();
                        break;
                    case 0xD2:
                        country ??= cleanStr;
                        break;
                }

                if (gstin == null)
                {
                    var gstMatch = Regex.Match(cleanStr, @"\b([0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]{3})\b");
                    if (gstMatch.Success)
                        gstin = gstMatch.Groups[1].Value;
                }
            }
        }

        if (gstin == null)
        {
            string fullU16 = Encoding.Unicode.GetString(buf);
            var gstMatch = Regex.Match(fullU16, @"\b([0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]{3})\b");
            if (gstMatch.Success)
                gstin = gstMatch.Groups[1].Value;
        }

        if (pan == null && gstin != null && gstin.Length == 15)
            pan = gstin.Substring(2, 10);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFieldIfValid(fields, seen, "Company Name", companyName ?? tallyItem.DisplayName);
        AddFieldIfValid(fields, seen, "Mailing Name", mailingName ?? companyName);
        AddFieldIfValid(fields, seen, "Company Code", tallyItem.TallyCompanyCode);
        AddFieldIfValid(fields, seen, "Tally Edition", is1800 ? "TallyPrime (.1800)" : "Tally.ERP 9 (.900)");
        AddFieldIfValid(fields, seen, "GSTIN", gstin);
        AddFieldIfValid(fields, seen, "PAN / Income Tax No.", pan);
        AddFieldIfValid(fields, seen, "State", state);
        AddFieldIfValid(fields, seen, "PIN Code", pinCode);
        AddFieldIfValid(fields, seen, "Country", country);
        if (addressLines.Count > 0)
            AddFieldIfValid(fields, seen, "Address", string.Join(", ", addressLines));
        AddFieldIfValid(fields, seen, "Phone / Mobile", phone);
        AddFieldIfValid(fields, seen, "Email ID", email);
        AddFieldIfValid(fields, seen, "Company GUID", guid);
        if (!string.IsNullOrWhiteSpace(tallyItem.ParentFolder))
            AddFieldIfValid(fields, seen, "Data Directory", tallyItem.ParentFolder);
        if (tallyItem.SortTimeUtc > DateTime.MinValue)
            AddFieldIfValid(fields, seen, "Last Modified Date", tallyItem.SortTimeUtc.ToLocalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));

        return fields.Count >= 2;
    }

    private static bool TryExtractCsvOrTsvFileHeaders(
        string filePath,
        char delimiter,
        out List<KeyValuePair<string, string>> fields,
        out string displayName,
        out string kindBadge)
    {
        fields = new List<KeyValuePair<string, string>>();
        displayName = DeskSpacePinnedAppFilesV380.FormatFriendlyFileName(System.IO.Path.GetFileName(filePath), isDirectory: false);
        kindBadge = delimiter == '\t' ? "TSV Table" : "CSV Table";

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var rows = new List<string[]>();
        while (!sr.EndOfStream && rows.Count < 25)
        {
            string? line = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;
            string[] cols = ParseDelimitedLine(line, delimiter);
            if (cols.Any(c => !string.IsNullOrWhiteSpace(c)))
                rows.Add(cols);
        }

        if (rows.Count == 0)
            return false;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Mode A: 2-column vertical Header -> Value sheet
        if (rows.Count >= 2 && rows.All(r => r.Length == 2))
        {
            foreach (var r in rows)
                AddFieldIfValid(fields, seen, r[0], r[1]);
        }

        // Mode B: Horizontal table (Row 0 = column headers, Row 1 = first data record)
        if (fields.Count < 2 && rows.Count >= 2)
        {
            string[] headers = rows[0];
            string[] firstData = rows[1];
            int count = Math.Min(headers.Length, Math.Min(firstData.Length, 24));
            for (int c = 0; c < count; c++)
                AddFieldIfValid(fields, seen, headers[c], firstData[c]);
        }

        return fields.Count >= 2;
    }

    private static string[] ParseDelimitedLine(string line, char delimiter)
    {
        var result = new List<string>();
        var cur = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cur.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(cur.ToString().Trim());
                cur.Clear();
            }
            else
            {
                cur.Append(c);
            }
        }
        result.Add(cur.ToString().Trim());
        return result.ToArray();
    }

    private static bool TryExtractXlsxFileHeaders(
        string filePath,
        out List<KeyValuePair<string, string>> fields,
        out string displayName,
        out string kindBadge)
    {
        fields = new List<KeyValuePair<string, string>>();
        displayName = DeskSpacePinnedAppFilesV380.ResolveInAppDisplayName(filePath, isDirectory: false);
        kindBadge = "Excel Workbook (.XLSX)";

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (fs.Length <= 0 || fs.Length > 25L * 1024 * 1024)
            return false;

        using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var xmlSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        // 1. Shared strings table (up to 512 entries)
        var sharedStrings = new List<string>();
        var ssEntry = zip.Entries.FirstOrDefault(e =>
            e.FullName.Replace('\\', '/').Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));
        if (ssEntry != null && ssEntry.Length <= 2 * 1024 * 1024)
        {
            using var ssStream = ssEntry.Open();
            using var xr = XmlReader.Create(ssStream, xmlSettings);
            while (xr.Read() && sharedStrings.Count < 512)
            {
                if (xr.NodeType == XmlNodeType.Element && xr.LocalName == "si")
                {
                    using var sub = xr.ReadSubtree();
                    var sb = new StringBuilder();
                    while (sub.Read())
                    {
                        if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "t")
                            sb.Append(sub.ReadElementContentAsString());
                    }
                    sharedStrings.Add(sb.ToString().Trim());
                }
            }
        }

        // 2. First worksheet cells (rows 1..20)
        var sheetEntry = zip.Entries
            .Where(e => e.FullName.Replace('\\', '/').StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (sheetEntry != null && sheetEntry.Length <= 4 * 1024 * 1024)
        {
            var rowCells = new Dictionary<int, Dictionary<int, string>>();
            using var shStream = sheetEntry.Open();
            using var xr = XmlReader.Create(shStream, xmlSettings);
            while (xr.Read())
            {
                if (xr.NodeType == XmlNodeType.Element && xr.LocalName == "c")
                {
                    string? cellRef = xr.GetAttribute("r");
                    string? cellType = xr.GetAttribute("t");
                    if (string.IsNullOrEmpty(cellRef) || !TryParseCellRef(cellRef, out int rowIdx, out int colIdx) || rowIdx > 25 || colIdx > 25)
                        continue;

                    string cellVal = string.Empty;
                    using var sub = xr.ReadSubtree();
                    while (sub.Read())
                    {
                        if (sub.NodeType == XmlNodeType.Element && (sub.LocalName == "v" || sub.LocalName == "t"))
                        {
                            cellVal = sub.ReadElementContentAsString().Trim();
                            break;
                        }
                    }

                    if (cellType == "s" && int.TryParse(cellVal, out int ssIdx) && ssIdx >= 0 && ssIdx < sharedStrings.Count)
                        cellVal = sharedStrings[ssIdx];

                    if (!string.IsNullOrWhiteSpace(cellVal))
                    {
                        if (!rowCells.TryGetValue(rowIdx, out var cols))
                        {
                            cols = new Dictionary<int, string>();
                            rowCells[rowIdx] = cols;
                        }
                        cols[colIdx] = cellVal;
                    }
                }
            }

            // Horizontal header row (first non-empty row -> next non-empty row)
            var orderedRowNums = rowCells.Keys.OrderBy(k => k).ToList();
            if (orderedRowNums.Count >= 2)
            {
                var headerRow = rowCells[orderedRowNums[0]];
                var valueRow = rowCells[orderedRowNums[1]];
                if (headerRow.Count >= 2)
                {
                    foreach (var kv in headerRow.OrderBy(k => k.Key))
                    {
                        if (valueRow.TryGetValue(kv.Key, out string? v))
                            AddFieldIfValid(fields, seen, kv.Value, v);
                    }
                }

                // Vertical 2-column key-value table fallback
                if (fields.Count < 2)
                {
                    foreach (int rNum in orderedRowNums)
                    {
                        var rDict = rowCells[rNum];
                        if (rDict.TryGetValue(1, out string? k) && rDict.TryGetValue(2, out string? v))
                            AddFieldIfValid(fields, seen, k, v);
                    }
                }
            }
        }

        return fields.Count >= 2;
    }

    private static bool TryParseCellRef(string cellRef, out int row, out int col)
    {
        row = 0;
        col = 0;
        int i = 0;
        while (i < cellRef.Length && char.IsLetter(cellRef[i]))
        {
            col = col * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1);
            i++;
        }
        return col > 0 && i < cellRef.Length && int.TryParse(cellRef.Substring(i), out row) && row > 0;
    }

    private static bool TryExtractJsonFileHeaders(
        string filePath,
        out List<KeyValuePair<string, string>> fields,
        out string displayName,
        out string kindBadge)
    {
        fields = new List<KeyValuePair<string, string>>();
        displayName = DeskSpacePinnedAppFilesV380.FormatFriendlyFileName(System.IO.Path.GetFileName(filePath), isDirectory: false);
        kindBadge = "JSON Data";

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (fs.Length <= 2 || fs.Length > 2 * 1024 * 1024)
            return false;

        using var doc = JsonDocument.Parse(fs, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        JsonElement target = doc.RootElement;
        if (target.ValueKind == JsonValueKind.Array && target.GetArrayLength() > 0)
            target = target[0];
        if (target.ValueKind != JsonValueKind.Object)
            return false;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in target.EnumerateObject())
        {
            if (prop.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                AddFieldIfValid(fields, seen, prop.Name, prop.Value.ToString());
                if (fields.Count >= 24)
                    break;
            }
        }
        return fields.Count >= 2;
    }

    private static bool TryExtractIniFileHeaders(
        string filePath,
        out List<KeyValuePair<string, string>> fields,
        out string displayName,
        out string kindBadge)
    {
        fields = new List<KeyValuePair<string, string>>();
        displayName = DeskSpacePinnedAppFilesV380.FormatFriendlyFileName(System.IO.Path.GetFileName(filePath), isDirectory: false);
        kindBadge = "INI / Config";

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string rawLine in File.ReadLines(filePath).Take(120))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] is ';' or '#' or '[')
                continue;
            int eq = line.IndexOf('=');
            if (eq <= 1)
                continue;
            string k = line.Substring(0, eq).Trim();
            string v = line.Substring(eq + 1).Trim().Trim('"');
            AddFieldIfValid(fields, seen, k, v);
            if (fields.Count >= 24)
                break;
        }
        return fields.Count >= 2;
    }

    private static bool TryExtractXmlFileHeaders(
        string filePath,
        out List<KeyValuePair<string, string>> fields,
        out string displayName,
        out string kindBadge)
    {
        fields = new List<KeyValuePair<string, string>>();
        displayName = DeskSpacePinnedAppFilesV380.FormatFriendlyFileName(System.IO.Path.GetFileName(filePath), isDirectory: false);
        kindBadge = System.IO.Path.GetExtension(filePath).ToUpperInvariant();

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };
        using var xr = XmlReader.Create(fs, settings);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (xr.Read() && fields.Count < 24)
        {
            if (xr.NodeType == XmlNodeType.Element && !xr.IsEmptyElement && xr.Depth is >= 1 and <= 2)
            {
                string elemName = xr.LocalName;
                if (xr.Read() && xr.NodeType == XmlNodeType.Text)
                {
                    string val = xr.Value.Trim();
                    if (!val.Contains("$("))
                        AddFieldIfValid(fields, seen, elemName, val);
                }
            }
        }
        return fields.Count >= 2;
    }

    internal static AppInfoDiscoveryResultV380 DiscoverAppInformation(
        string appOrShortcutPath,
        IEnumerable<string>? explicitCandidateFiles = null,
        string? preferencesPath = null)
    {
        var eligibility = EvaluateAppEligibility(appOrShortcutPath);
        if (!eligibility.IsSupported)
        {
            return new AppInfoDiscoveryResultV380(
                eligibility,
                TotalFilesScanned: 0,
                QualifyingFileCount: 0,
                MeetsMinimumTwoFilesRule: false,
                StatusMessage: eligibility.Reason,
                MatchedHeaders: Array.Empty<AppInfoHeaderFieldV380>(),
                MatchedFiles: Array.Empty<AppInfoFileRecordV380>(),
                VisibleHeaders: Array.Empty<string>());
        }

        var cap = DeskSpacePinnedAppFilesV380.ResolveCapability(appOrShortcutPath);
        var candidatePaths = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAddCandidate(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return;
            string full;
            try
            {
                full = System.IO.Path.GetFullPath(rawPath.Trim().Trim('"'))
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return;
            }
            if (!IsPathCompatibleWithEligibility(full, cap, eligibility))
                return;
            if (seenPaths.Add(full))
                candidatePaths.Add(full);
        }

        if (explicitCandidateFiles != null)
        {
            foreach (string path in explicitCandidateFiles)
                TryAddCandidate(path);
        }
        else
        {
            foreach (string path in DiscoverCandidateFilesForApp(appOrShortcutPath, cap, eligibility))
                TryAddCandidate(path);
        }

        var parsedFiles = new List<(string DisplayName, string FullPath, string KindBadge, DateTime ModifiedUtc, List<KeyValuePair<string, string>> Pairs)>();
        var headerOrder = new List<string>();
        var headerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerSample = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string candidate in candidatePaths.Take(80))
        {
            if (!TryExtractFileHeaderValues(candidate, out var pairs, out string disp, out string badge) || pairs.Count == 0)
                continue;

            DateTime modUtc = Directory.Exists(candidate)
                ? SafeGetDirWriteTimeUtc(candidate)
                : SafeGetFileWriteTimeUtc(candidate);

            parsedFiles.Add((disp, candidate, badge, modUtc, pairs));

            var uniqueInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in pairs)
            {
                if (!uniqueInFile.Add(kv.Key))
                    continue;
                if (!headerCounts.TryGetValue(kv.Key, out int curr))
                {
                    headerOrder.Add(kv.Key);
                    headerCounts[kv.Key] = 1;
                    headerSample[kv.Key] = kv.Value;
                }
                else
                {
                    headerCounts[kv.Key] = curr + 1;
                }
            }
        }

        // Strict >= 2 similar files with matching Header -> Value rule
        var qualifyingHeaderSet = new HashSet<string>(
            headerOrder.Where(h => headerCounts.TryGetValue(h, out int c) && c >= 2),
            StringComparer.OrdinalIgnoreCase);

        var matchedFiles = new List<AppInfoFileRecordV380>();
        foreach (var pf in parsedFiles)
        {
            var filteredPairs = pf.Pairs
                .Where(kv => qualifyingHeaderSet.Contains(kv.Key))
                .ToList();
            if (filteredPairs.Count == 0)
                continue;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in filteredPairs)
                dict[kv.Key] = kv.Value;

            matchedFiles.Add(new AppInfoFileRecordV380(
                pf.DisplayName,
                pf.FullPath,
                pf.KindBadge,
                pf.ModifiedUtc,
                dict,
                filteredPairs));
        }

        if (matchedFiles.Count < 2 || qualifyingHeaderSet.Count == 0)
        {
            return new AppInfoDiscoveryResultV380(
                eligibility,
                TotalFilesScanned: candidatePaths.Count,
                QualifyingFileCount: matchedFiles.Count,
                MeetsMinimumTwoFilesRule: false,
                StatusMessage: $"{eligibility.AppDisplayName}: Requires at least 2 compatible files with matching Header → Value information (found {matchedFiles.Count}).",
                MatchedHeaders: Array.Empty<AppInfoHeaderFieldV380>(),
                MatchedFiles: Array.Empty<AppInfoFileRecordV380>(),
                VisibleHeaders: Array.Empty<string>());
        }

        // Recompute exact counts across the qualifying matched files
        var matchedHeaders = new List<AppInfoHeaderFieldV380>();
        int defaultVisibleCount = 0;
        foreach (string h in headerOrder)
        {
            if (!qualifyingHeaderSet.Contains(h))
                continue;
            int count = matchedFiles.Count(f => f.Fields.ContainsKey(h));
            if (count < 2)
                continue;
            bool isDef = (DefaultPriorityHeaders.Contains(h) && defaultVisibleCount < 8) ||
                         (defaultVisibleCount < 5);
            if (isDef)
                defaultVisibleCount++;

            matchedHeaders.Add(new AppInfoHeaderFieldV380(
                Header: h,
                MatchingFileCount: count,
                TotalFileCount: matchedFiles.Count,
                SampleValue: headerSample.TryGetValue(h, out string? sVal) ? sVal : string.Empty,
                IsDefaultVisible: isDef));
        }

        if (matchedHeaders.Count > 0 && matchedHeaders.All(h => !h.IsDefaultVisible))
        {
            for (int i = 0; i < Math.Min(6, matchedHeaders.Count); i++)
                matchedHeaders[i] = matchedHeaders[i] with { IsDefaultVisible = true };
        }

        var savedVisible = LoadVisibleHeadersPreference(appOrShortcutPath, preferencesPath);
        List<string> activeVisible;
        if (savedVisible.Count > 0)
        {
            var validSet = new HashSet<string>(matchedHeaders.Select(m => m.Header), StringComparer.OrdinalIgnoreCase);
            activeVisible = savedVisible.Where(validSet.Contains).ToList();
            if (activeVisible.Count == 0)
                activeVisible = matchedHeaders.Where(m => m.IsDefaultVisible).Select(m => m.Header).ToList();
        }
        else
        {
            activeVisible = matchedHeaders.Where(m => m.IsDefaultVisible).Select(m => m.Header).ToList();
        }

        string status = $"{eligibility.AppDisplayName} · {matchedFiles.Count} files share {matchedHeaders.Count} verified Header → Value fields ({activeVisible.Count} visible).";
        return new AppInfoDiscoveryResultV380(
            eligibility,
            TotalFilesScanned: candidatePaths.Count,
            QualifyingFileCount: matchedFiles.Count,
            MeetsMinimumTwoFilesRule: true,
            StatusMessage: status,
            MatchedHeaders: matchedHeaders,
            MatchedFiles: matchedFiles,
            VisibleHeaders: activeVisible);
    }

    internal static IReadOnlyList<KeyValuePair<string, string>> GetVisibleFieldsForFile(
        AppInfoFileRecordV380 file,
        IEnumerable<string> visibleHeaders)
    {
        if (file == null || visibleHeaders == null)
            return Array.Empty<KeyValuePair<string, string>>();

        var result = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string header in visibleHeaders)
        {
            if (string.IsNullOrWhiteSpace(header) || !seen.Add(header))
                continue;
            if (file.Fields.TryGetValue(header, out string? val) && !string.IsNullOrWhiteSpace(val))
                result.Add(new KeyValuePair<string, string>(header, val));
        }
        return result;
    }

    internal static string GetAppPreferenceKey(string appOrShortcutPath)
    {
        var elig = EvaluateAppEligibility(appOrShortcutPath);
        if (!string.IsNullOrWhiteSpace(elig.AppDisplayName))
            return elig.AppDisplayName.Trim().ToLowerInvariant();
        return (appOrShortcutPath ?? string.Empty).Trim().ToLowerInvariant();
    }

    internal static List<string> LoadVisibleHeadersPreference(
        string appOrShortcutPath,
        string? preferencesPath = null)
    {
        string target = string.IsNullOrWhiteSpace(preferencesPath) ? DefaultAppInfoPreferencesPath : preferencesPath;
        string key = GetAppPreferenceKey(appOrShortcutPath);
        lock (PreferencesLock)
        {
            try
            {
                if (!File.Exists(target))
                    return new List<string>();
                var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(target));
                if (map != null)
                {
                    foreach (var kv in map)
                    {
                        if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) && kv.Value != null)
                            return kv.Value.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    }
                }
            }
            catch
            {
            }
            return new List<string>();
        }
    }

    internal static void SaveVisibleHeadersPreference(
        string appOrShortcutPath,
        IEnumerable<string> visibleHeaders,
        string? preferencesPath = null)
    {
        string target = string.IsNullOrWhiteSpace(preferencesPath) ? DefaultAppInfoPreferencesPath : preferencesPath;
        string key = GetAppPreferenceKey(appOrShortcutPath);
        var cleanList = (visibleHeaders ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (PreferencesLock)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(target))
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(target));
                    if (loaded != null)
                    {
                        foreach (var kv in loaded)
                            map[kv.Key] = kv.Value ?? new List<string>();
                    }
                }
            }
            catch
            {
            }

            map[key] = cleanList;

            string? dir = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            string temp = target + ".pending";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                File.Move(temp, target, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }
    }

    private static bool IsPathCompatibleWithEligibility(
        string fullPath,
        AppFileCapabilityV380 cap,
        AppInfoEligibilityV380 eligibility)
    {
        if (Directory.Exists(fullPath))
        {
            if (!cap.IncludesTallyCompanies)
                return false;
            return DeskSpacePinnedAppFilesV380.TryParseTallyCompanyFolder(fullPath, out var tItem) &&
                   tItem != null &&
                   DeskSpacePinnedAppFilesV380.IsTallyCompanyCompatibleWithCapability(cap, tItem);
        }

        if (!File.Exists(fullPath))
            return false;

        string fileName = System.IO.Path.GetFileName(fullPath);
        if (cap.IncludesTallyCompanies &&
            (fileName.Equals("Company.1800", StringComparison.OrdinalIgnoreCase) ||
             fileName.Equals("Company.900", StringComparison.OrdinalIgnoreCase)))
        {
            string? dir = System.IO.Path.GetDirectoryName(fullPath);
            return !string.IsNullOrEmpty(dir) &&
                   DeskSpacePinnedAppFilesV380.TryParseTallyCompanyFolder(dir, out var tItem) &&
                   tItem != null &&
                   DeskSpacePinnedAppFilesV380.IsTallyCompanyCompatibleWithCapability(cap, tItem);
        }

        if (fullPath.EndsWith(".tax.wb", StringComparison.OrdinalIgnoreCase) ||
            fullPath.EndsWith(".wgst.wb", StringComparison.OrdinalIgnoreCase) ||
            fullPath.EndsWith(".wt", StringComparison.OrdinalIgnoreCase) ||
            fullPath.EndsWith(".tbk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string ext = System.IO.Path.GetExtension(fullPath);
        return eligibility.SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> DiscoverCandidateFilesForApp(
        string appOrShortcutPath,
        AppFileCapabilityV380 cap,
        AppInfoEligibilityV380 eligibility)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfNew(string? p)
        {
            if (string.IsNullOrWhiteSpace(p))
                return;
            string clean = p.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            if (seen.Add(clean))
                results.Add(clean);
        }

        if (DeskSpacePinnedAppFilesV380.TryGetCachedQuickArcFiles(appOrShortcutPath, out var cached))
        {
            foreach (var item in cached)
                AddIfNew(item.FullPath);
        }

        foreach (var rec in DeskSpacePinnedAppFilesV380.DiscoverRecentItems(cap))
            AddIfNew(rec.FullPath);

        // Scan ExtraDataRoots and Winman install/data directories
        var roots = new List<string>(cap.ExtraDataRoots);
        if (cap.Kind == PinnedAppKindV380.WinmanAccounting)
        {
            foreach (string wDir in new[] { @"C:\Winman", @"D:\Winman", @"E:\Winman", @"F:\Winman" })
            {
                if (Directory.Exists(wDir) && !roots.Contains(wDir, StringComparer.OrdinalIgnoreCase))
                    roots.Add(wDir);
            }
        }

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;
            try
            {
                if (cap.IncludesTallyCompanies)
                {
                    foreach (string sub in Directory.EnumerateDirectories(root).Take(120))
                    {
                        if (DeskSpacePinnedAppFilesV380.TryParseTallyCompanyFolder(sub, out var tItem) &&
                            tItem != null &&
                            DeskSpacePinnedAppFilesV380.IsTallyCompanyCompatibleWithCapability(cap, tItem))
                        {
                            AddIfNew(sub);
                            continue;
                        }
                        try
                        {
                            foreach (string nested in Directory.EnumerateDirectories(sub).Take(60))
                            {
                                if (DeskSpacePinnedAppFilesV380.TryParseTallyCompanyFolder(nested, out var nItem) &&
                                    nItem != null &&
                                    DeskSpacePinnedAppFilesV380.IsTallyCompanyCompatibleWithCapability(cap, nItem))
                                {
                                    AddIfNew(nested);
                                }
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    foreach (string ext in eligibility.SupportedExtensions)
                    {
                        if (!ext.StartsWith('.'))
                            continue;
                        foreach (string f in Directory.EnumerateFiles(root, "*" + ext, SearchOption.TopDirectoryOnly).Take(40))
                            AddIfNew(f);
                        foreach (string sub in Directory.EnumerateDirectories(root).Take(30))
                        {
                            try
                            {
                                foreach (string f in Directory.EnumerateFiles(sub, "*" + ext, SearchOption.AllDirectories).Take(40))
                                    AddIfNew(f);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        if (results.Count < 60)
        {
            try
            {
                var fetched = DeskSpacePinnedAppFilesV380.GetQuickArcFilesAsync(appOrShortcutPath, 80)
                    .GetAwaiter().GetResult();
                foreach (var item in fetched)
                    AddIfNew(item.FullPath);
            }
            catch
            {
            }
        }

        return results;
    }

    internal static List<(string Label, string Path)> GetAvailableAppsForInformation()
    {
        var list = new List<(string Label, string Path)>();
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAddApp(string? path, string? overrideLabel = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;
            string full;
            try { full = System.IO.Path.GetFullPath(path); }
            catch { return; }
            if (!seenPaths.Add(full))
                return;

            var elig = EvaluateAppEligibility(full);
            string label = !string.IsNullOrWhiteSpace(overrideLabel)
                ? overrideLabel
                : (!string.IsNullOrWhiteSpace(elig.AppDisplayName)
                    ? elig.AppDisplayName
                    : DeskSpacePinnedAppFilesV380.ResolvePinnedAppDisplayName(full));
            if (string.IsNullOrWhiteSpace(label))
                label = System.IO.Path.GetFileNameWithoutExtension(full);

            if (seenLabels.Add(label))
                list.Add((label, full));
        }

        try
        {
            var settings = WidgetSettingsV3.Load();
            foreach (string pin in settings.PinnedApps)
                TryAddApp(pin);
        }
        catch
        {
        }

        // Auto-include installed Winman GST / Winman CA ERP / TallyPrime / Tally.ERP 9 / Excel / Word shortcuts
        string appDataStart = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        string commonStart = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        string[] candidateShortcuts =
        {
            System.IO.Path.Combine(appDataStart, "Winman Software", "Winman GST.lnk"),
            System.IO.Path.Combine(commonStart, "Winman Software", "Winman GST.lnk"),
            System.IO.Path.Combine(appDataStart, "Winman Software", "Winman CA ERP.lnk"),
            System.IO.Path.Combine(commonStart, "Winman Software", "Winman CA ERP.lnk"),
            System.IO.Path.Combine(commonStart, "Winman Software", "Winman TDS.lnk"),
            System.IO.Path.Combine(commonStart, "TallyPrime.lnk"),
            System.IO.Path.Combine(commonStart, "Tally.ERP 9", "Tally.ERP 9.lnk"),
            System.IO.Path.Combine(commonStart, "Excel.lnk"),
            System.IO.Path.Combine(commonStart, "Word.lnk"),
            @"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE",
            @"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE"
        };

        foreach (string sc in candidateShortcuts)
            TryAddApp(sc);

        return list;
    }

    private static DateTime SafeGetFileWriteTimeUtc(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }

    private static DateTime SafeGetDirWriteTimeUtc(string path)
    {
        try { return Directory.GetLastWriteTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }

    // =========================================================================
    // Existing Information Collections Persistence (Synced with Donut V36)
    // =========================================================================

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
            "DeskSpace · Information & App File Fields",
            940,
            660,
            owner,
            transient: false);
        win.MinWidth = 720;
        win.MinHeight = 480;
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

        // App File Information (Header -> Value) state
        bool showAppInfoView = false;
        string selectedAppPath = "";
        string appFileFilter = "";
        bool isAppScanning = false;
        int appScanGen = 0;
        AppInfoDiscoveryResultV380? currentAppResult = null;
        HashSet<string> activeVisibleHeaders = new(StringComparer.OrdinalIgnoreCase);
        var appResultCache = new Dictionary<string, AppInfoDiscoveryResultV380>(StringComparer.OrdinalIgnoreCase);

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

        void TriggerAppDiscovery(string appPath, bool forceRescan = false)
        {
            if (string.IsNullOrWhiteSpace(appPath))
                return;

            selectedAppPath = appPath;
            showAppInfoView = true;

            if (!forceRescan && appResultCache.TryGetValue(appPath, out var cached))
            {
                currentAppResult = cached;
                var savedPref = LoadVisibleHeadersPreference(appPath);
                activeVisibleHeaders = new HashSet<string>(
                    savedPref.Count > 0 ? savedPref : cached.VisibleHeaders,
                    StringComparer.OrdinalIgnoreCase);
                isAppScanning = false;
                renderUi?.Invoke();
                return;
            }

            var elig = EvaluateAppEligibility(appPath);
            if (!elig.IsSupported)
            {
                var unsupportedRes = DiscoverAppInformation(appPath, Array.Empty<string>());
                appResultCache[appPath] = unsupportedRes;
                currentAppResult = unsupportedRes;
                activeVisibleHeaders.Clear();
                isAppScanning = false;
                renderUi?.Invoke();
                return;
            }

            isAppScanning = true;
            int myGen = ++appScanGen;
            renderUi?.Invoke();

            _ = Task.Run(() =>
            {
                var discovered = DiscoverAppInformation(appPath);
                host.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (disposed || myGen != appScanGen)
                        return;
                    appResultCache[appPath] = discovered;
                    currentAppResult = discovered;
                    activeVisibleHeaders = new HashSet<string>(discovered.VisibleHeaders, StringComparer.OrdinalIgnoreCase);
                    isAppScanning = false;
                    renderUi?.Invoke();
                }));
            });
        }

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
                showAppInfoView = false;
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
                if (showAppInfoView && !string.IsNullOrEmpty(selectedAppPath))
                {
                    TriggerAppDiscovery(selectedAppPath, forceRescan: true);
                    return;
                }
                Render();
            }

            var modeCollectionsBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                $"Saved Collections ({all.Count})",
                () =>
                {
                    showAppInfoView = false;
                    bannerMessage = "";
                    Render();
                },
                primary: !showAppInfoView,
                tooltip: "View and edit saved Information collections & Security Vault PW rows");
            modeCollectionsBtn.Margin = new Thickness(0, 0, 8, 0);
            headerActions.Children.Add(modeCollectionsBtn);

            var modeAppInfoBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                "App File Information (Header → Value)",
                () =>
                {
                    showAppInfoView = true;
                    bannerMessage = "";
                    if (string.IsNullOrEmpty(selectedAppPath))
                    {
                        var apps = GetAvailableAppsForInformation();
                        if (apps.Count > 0)
                        {
                            TriggerAppDiscovery(apps[0].Path);
                            return;
                        }
                    }
                    Render();
                },
                primary: showAppInfoView,
                tooltip: "Select an application to inspect inside-file Header → Value fields (Winman GST, Winman CA-ERP, Tally, Excel)");
            modeAppInfoBtn.Margin = new Thickness(0, 0, 8, 0);
            headerActions.Children.Add(modeAppInfoBtn);

            var newBtn = DeskSpaceFluentBackdropV380.CreatePillButton("+ New Collection", StartNewEditor, primary: false,
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
                primary: false, tooltip: "Reload information collections or re-scan application files");
            headerActions.Children.Add(refreshBtn);

            DockPanel.SetDock(headerActions, Dock.Right);
            headerDock.Children.Add(headerActions);

            var headerTitleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            headerTitleStack.Children.Add(new TextBlock
            {
                Text = showAppInfoView
                    ? "Application File Information (Header → Value)"
                    : "Information Collections",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = DeskSpaceDesignV283.Solid(p.Text)
            });
            headerTitleStack.Children.Add(new TextBlock
            {
                Text = showAppInfoView
                    ? "Checks app Header → Value eligibility, finds headers across ≥ 2 matching files, and shows only the headers you select"
                    : $"{all.Count} {(all.Count == 1 ? "collection" : "collections")} · Shared live with the floating Donut Information arc & Security Vault",
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

            if (showAppInfoView)
            {
                BuildAppFileInformationView(
                    root,
                    p,
                    getOwner,
                    selectedAppPath,
                    appFileFilter,
                    isAppScanning,
                    currentAppResult,
                    activeVisibleHeaders,
                    onSelectApp: path => TriggerAppDiscovery(path),
                    onFilterChanged: q => appFileFilter = q,
                    onToggleHeader: (hdr, isChecked) =>
                    {
                        if (isChecked)
                            activeVisibleHeaders.Add(hdr);
                        else
                            activeVisibleHeaders.Remove(hdr);
                        SaveVisibleHeadersPreference(selectedAppPath, activeVisibleHeaders);
                        Render();
                    },
                    onSetVisibleBatch: headers =>
                    {
                        activeVisibleHeaders = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
                        SaveVisibleHeadersPreference(selectedAppPath, activeVisibleHeaders);
                        Render();
                    },
                    onSaveFileToCollections: (fileRecord, visRows) =>
                    {
                        if (visRows.Count == 0)
                        {
                            bannerMessage = "Select at least one visible header before saving to Information Collections.";
                            bannerIsError = true;
                            Render();
                            return;
                        }
                        try
                        {
                            var diskData = LoadCollections();
                            string colTitle = fileRecord.DisplayName;
                            var newCol = new InfoCollectionV380
                            {
                                Id = Guid.NewGuid(),
                                Name = colTitle,
                                UpdatedUtc = DateTime.UtcNow,
                                Rows = visRows.Select(kv => new InfoRowV380
                                {
                                    Label = kv.Key,
                                    Value = kv.Value
                                }).ToList()
                            };
                            int existIdx = diskData.FindIndex(c => string.Equals(c.Name, colTitle, StringComparison.OrdinalIgnoreCase));
                            if (existIdx >= 0)
                            {
                                newCol.Id = diskData[existIdx].Id;
                                diskData[existIdx] = newCol;
                            }
                            else
                            {
                                diskData.Add(newCol);
                            }
                            SaveCollections(diskData);
                            loadedStamp = CurrentStamp();
                            all = LoadCollections();
                            selectedId = newCol.Id;
                            bannerMessage = $"Saved '{colTitle}' ({visRows.Count} selected Header → Value fields) to Donut Information Collections.";
                            bannerIsError = false;
                            Render();
                        }
                        catch (Exception ex)
                        {
                            bannerMessage = ex.Message;
                            bannerIsError = true;
                            Render();
                        }
                    },
                    onStatusToast: (msg, isErr) =>
                    {
                        bannerMessage = msg;
                        bannerIsError = isErr;
                        Render();
                    });
                return;
            }

            // --- Master-Detail Split Grid ---
            var bodyGrid = new Grid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(285) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(bodyGrid);

            // LEFT PANE: Quick App File Info Launcher + Search + Collection List
            var leftDock = new DockPanel { LastChildFill = true };

            var quickAppBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                "⊞ Inspect App Files (Winman / Tally / Excel)…",
                () =>
                {
                    showAppInfoView = true;
                    if (string.IsNullOrEmpty(selectedAppPath))
                    {
                        var apps = GetAvailableAppsForInformation();
                        if (apps.Count > 0)
                        {
                            TriggerAppDiscovery(apps[0].Path);
                            return;
                        }
                    }
                    Render();
                },
                primary: false,
                tooltip: "Select an application to inspect inside-file Header → Value fields across ≥ 2 matching files");
            quickAppBtn.Margin = new Thickness(0, 0, 0, 8);
            DockPanel.SetDock(quickAppBtn, Dock.Top);
            leftDock.Children.Add(quickAppBtn);

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
                        Text = "Click '+ New Collection' to save user IDs, GST/portal credentials, or encrypted passwords, or click 'App File Information (Header → Value)' above to inspect inside-file fields for Winman GST, Winman CA-ERP, TallyPrime, Tally.ERP 9, or Excel.",
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                        Margin = new Thickness(0, 0, 0, 12)
                    });
                    var emptyBtns = new WrapPanel();
                    var createFirstBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                        "+ Create First Collection", StartNewEditor, primary: true);
                    createFirstBtn.Margin = new Thickness(0, 0, 8, 6);
                    var openAppInfoBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                        "Inspect App File Information (Header → Value)", () =>
                        {
                            showAppInfoView = true;
                            var apps = GetAvailableAppsForInformation();
                            if (apps.Count > 0)
                                TriggerAppDiscovery(apps[0].Path);
                            else
                                Render();
                        }, primary: false);
                    openAppInfoBtn.Margin = new Thickness(0, 0, 8, 6);
                    emptyBtns.Children.Add(createFirstBtn);
                    emptyBtns.Children.Add(openAppInfoBtn);
                    rightContent.Children.Add(emptyBtns);
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

    private static void BuildAppFileInformationView(
        DockPanel root,
        DeskSpaceDesignV283.Palette p,
        Func<Window?> getOwner,
        string selectedAppPath,
        string appFileFilter,
        bool isAppScanning,
        AppInfoDiscoveryResultV380? result,
        HashSet<string> activeVisibleHeaders,
        Action<string> onSelectApp,
        Action<string> onFilterChanged,
        Action<string, bool> onToggleHeader,
        Action<IEnumerable<string>> onSetVisibleBatch,
        Action<AppInfoFileRecordV380, IReadOnlyList<KeyValuePair<string, string>>> onSaveFileToCollections,
        Action<string, bool> onStatusToast)
    {
        // 1. Application Selector Card
        var appBarStack = new StackPanel();
        appBarStack.Children.Add(new TextBlock
        {
            Text = "1. Select Application (Checks if application files support structured Header → Value data)",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            Margin = new Thickness(0, 0, 0, 6)
        });

        var appPillsWrap = new WrapPanel();
        var availableApps = GetAvailableAppsForInformation();
        if (!string.IsNullOrWhiteSpace(selectedAppPath) &&
            availableApps.All(a => !string.Equals(a.Path, selectedAppPath, StringComparison.OrdinalIgnoreCase)))
        {
            var customElig = EvaluateAppEligibility(selectedAppPath);
            availableApps.Insert(0, (customElig.AppDisplayName, selectedAppPath));
        }

        foreach (var (label, path) in availableApps)
        {
            string capturedPath = path;
            bool isActive = string.Equals(capturedPath, selectedAppPath, StringComparison.OrdinalIgnoreCase);
            var btn = DeskSpaceFluentBackdropV380.CreatePillButton(
                label,
                () => onSelectApp(capturedPath),
                primary: isActive,
                tooltip: capturedPath);
            btn.Margin = new Thickness(0, 0, 8, 6);
            appPillsWrap.Children.Add(btn);
        }

        var browseBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
            "+ Browse Any App (.exe / .lnk)…",
            () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select an application to check Header → Value support",
                    Filter = "Applications & Shortcuts|*.exe;*.lnk|All Files|*.*"
                };
                if (dlg.ShowDialog(getOwner()) == true)
                    onSelectApp(dlg.FileName);
            },
            primary: false,
            tooltip: "Pick any installed application or shortcut (e.g. Winman GST, TallyPrime, Excel, Word)");
        browseBtn.Margin = new Thickness(0, 0, 8, 6);
        appPillsWrap.Children.Add(browseBtn);
        appBarStack.Children.Add(appPillsWrap);

        var appSelectorCard = DeskSpaceFluentBackdropV380.CreateCard(appBarStack, p);
        appSelectorCard.Padding = new Thickness(14, 10, 14, 10);
        appSelectorCard.Margin = new Thickness(0, 0, 0, 10);
        DockPanel.SetDock(appSelectorCard, Dock.Top);
        root.Children.Add(appSelectorCard);

        if (isAppScanning)
        {
            var loadingCard = DeskSpaceFluentBackdropV380.CreateCard(new TextBlock
            {
                Text = "Scanning compatible files and extracting inside-file Header → Value fields…",
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = DeskSpaceDesignV283.Solid(DeskSpaceDesignV283.AccentText(p))
            }, p);
            loadingCard.Padding = new Thickness(16, 14, 16, 14);
            root.Children.Add(loadingCard);
            return;
        }

        if (result == null)
        {
            var promptCard = DeskSpaceFluentBackdropV380.CreateCard(new TextBlock
            {
                Text = "Select an application above (such as Winman GST, Winman CA ERP, TallyPrime, Tally.ERP 9, or Microsoft Excel) to inspect its inside-file Header → Value fields.",
                FontSize = 13.5,
                Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                TextWrapping = TextWrapping.Wrap
            }, p);
            promptCard.Padding = new Thickness(16, 14, 16, 14);
            root.Children.Add(promptCard);
            return;
        }

        // 2. Eligibility & >= 2 Matching Files Status Card
        if (!result.Eligibility.IsSupported || !result.MeetsMinimumTwoFilesRule)
        {
            var warnStack = new StackPanel();
            warnStack.Children.Add(new TextBlock
            {
                Text = !result.Eligibility.IsSupported
                    ? $"✕ {result.Eligibility.AppDisplayName} · Does Not Support Header → Value Information"
                    : $"⚠ {result.Eligibility.AppDisplayName} · Insufficient Matching Files (< 2)",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = DeskSpaceDesignV283.Solid(DeskSpaceDesignV283.Danger(p)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            warnStack.Children.Add(new TextBlock
            {
                Text = result.StatusMessage,
                FontSize = 13,
                Foreground = DeskSpaceDesignV283.Solid(p.Text),
                TextWrapping = TextWrapping.Wrap
            });
            var warnCard = DeskSpaceFluentBackdropV380.CreateCard(warnStack, p);
            warnCard.BorderBrush = DeskSpaceDesignV283.Solid(DeskSpaceDesignV283.Danger(p));
            warnCard.Padding = new Thickness(16, 14, 16, 14);
            root.Children.Add(warnCard);
            return;
        }

        // 3. Header Visibility Selection Card (>= 2 Matching Files)
        var headersStack = new StackPanel();
        var headersTopRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
        var headerQuickBtns = new StackPanel { Orientation = Orientation.Horizontal };

        var selectAllBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
            "Select All",
            () => onSetVisibleBatch(result.MatchedHeaders.Select(h => h.Header)),
            primary: false);
        selectAllBtn.Padding = new Thickness(9, 3, 9, 3);
        selectAllBtn.MinHeight = 25;
        selectAllBtn.FontSize = 11;
        selectAllBtn.Margin = new Thickness(0, 0, 6, 0);

        var defaultFieldsBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
            "Default Key Fields",
            () => onSetVisibleBatch(result.MatchedHeaders.Where(h => h.IsDefaultVisible).Select(h => h.Header)),
            primary: false);
        defaultFieldsBtn.Padding = new Thickness(9, 3, 9, 3);
        defaultFieldsBtn.MinHeight = 25;
        defaultFieldsBtn.FontSize = 11;

        headerQuickBtns.Children.Add(selectAllBtn);
        headerQuickBtns.Children.Add(defaultFieldsBtn);
        DockPanel.SetDock(headerQuickBtns, Dock.Right);
        headersTopRow.Children.Add(headerQuickBtns);

        headersTopRow.Children.Add(new TextBlock
        {
            Text = $"2. Choose Visible Headers for {result.Eligibility.AppDisplayName} ({result.MatchedHeaders.Count} headers verified across ≥ 2 files · {activeVisibleHeaders.Count} visible)",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center
        });
        headersStack.Children.Add(headersTopRow);

        var checkWrap = new WrapPanel();
        foreach (var hdr in result.MatchedHeaders)
        {
            string capturedHeader = hdr.Header;
            bool isChecked = activeVisibleHeaders.Contains(capturedHeader);
            var cb = new CheckBox
            {
                IsChecked = isChecked,
                Content = $"{capturedHeader} ({hdr.MatchingFileCount})",
                Foreground = DeskSpaceDesignV283.Solid(isChecked ? DeskSpaceDesignV283.AccentText(p) : p.Text),
                FontWeight = isChecked ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = $"Sample: {hdr.SampleValue} · Found in {hdr.MatchingFileCount}/{hdr.TotalFileCount} files"
            };
            cb.Checked += (_, _) => onToggleHeader(capturedHeader, true);
            cb.Unchecked += (_, _) => onToggleHeader(capturedHeader, false);

            var pillBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = DeskSpaceDesignV283.Solid(isChecked ? p.AccentSoft : p.Surface),
                BorderBrush = DeskSpaceDesignV283.Solid(isChecked ? p.Accent : p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 4, 9, 4),
                Margin = new Thickness(0, 0, 7, 6),
                Child = cb
            };
            checkWrap.Children.Add(pillBorder);
        }

        var checkScroll = new ScrollViewer
        {
            MaxHeight = 115,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = checkWrap
        };
        headersStack.Children.Add(checkScroll);

        var headersCard = DeskSpaceFluentBackdropV380.CreateCard(headersStack, p);
        headersCard.Padding = new Thickness(14, 10, 14, 8);
        headersCard.Margin = new Thickness(0, 0, 0, 10);
        DockPanel.SetDock(headersCard, Dock.Top);
        root.Children.Add(headersCard);

        // 4. Per-File Inside Information Cards (Filtered by Selected Visible Headers)
        var filesOuterDock = new DockPanel { LastChildFill = true };
        var filterBox = new TextBox
        {
            Text = appFileFilter,
            Margin = new Thickness(0, 0, 0, 10),
            ToolTip = "Search files by company/client name, GSTIN, PAN, year, or any visible field value"
        };
        DeskSpaceDesignV283.Input(filterBox, p);
        AutomationProperties.SetName(filterBox, "Filter application files by inside information");
        DockPanel.SetDock(filterBox, Dock.Top);
        filesOuterDock.Children.Add(filterBox);

        var filesScroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var filesStack = new StackPanel();
        filesScroller.Content = filesStack;
        filesOuterDock.Children.Add(filesScroller);

        var orderedVisibleList = result.MatchedHeaders
            .Select(h => h.Header)
            .Where(activeVisibleHeaders.Contains)
            .ToList();

        void PopulateFilesStack(string queryText)
        {
            filesStack.Children.Clear();
            string q = queryText.Trim();

            var filteredFiles = result.MatchedFiles.Where(f =>
            {
                if (string.IsNullOrEmpty(q))
                    return true;
                if (f.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    f.FullPath.Contains(q, StringComparison.OrdinalIgnoreCase))
                    return true;
                return f.OrderedFields.Any(kv =>
                    kv.Key.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    kv.Value.Contains(q, StringComparison.OrdinalIgnoreCase));
            }).ToList();

            if (orderedVisibleList.Count == 0)
            {
                filesStack.Children.Add(new TextBlock
                {
                    Text = "Check one or more Header checkboxes above to display inside-file Header : Value information.",
                    FontSize = 13,
                    Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                    Margin = new Thickness(4, 8, 4, 8)
                });
                return;
            }

            foreach (var fileRec in filteredFiles)
            {
                var visiblePairs = GetVisibleFieldsForFile(fileRec, orderedVisibleList);
                if (visiblePairs.Count == 0)
                    continue;

                var cardStack = new StackPanel();
                var cardHeader = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 8) };
                var cardBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var capturedFile = fileRec;
                var capturedPairs = visiblePairs;

                var copyCardBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                    "Copy Visible",
                    () =>
                    {
                        string text = string.Join(Environment.NewLine, capturedPairs.Select(kv => $"{kv.Key}: {kv.Value}"));
                        Clipboard.SetText(text);
                        onStatusToast($"Copied {capturedPairs.Count} visible fields for '{capturedFile.DisplayName}'.", false);
                    },
                    primary: false,
                    tooltip: "Copy all visible Header : Value pairs for this file");
                copyCardBtn.Padding = new Thickness(9, 3, 9, 3);
                copyCardBtn.MinHeight = 26;
                copyCardBtn.FontSize = 11;
                copyCardBtn.Margin = new Thickness(0, 0, 6, 0);

                var saveToDonutBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                    "+ Save to Donut Info",
                    () => onSaveFileToCollections(capturedFile, capturedPairs),
                    primary: true,
                    tooltip: "Save these selected Header → Value rows into Donut Information Collections");
                saveToDonutBtn.Padding = new Thickness(10, 3, 10, 3);
                saveToDonutBtn.MinHeight = 26;
                saveToDonutBtn.FontSize = 11;
                saveToDonutBtn.Margin = new Thickness(0, 0, 6, 0);

                var openFileBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                    "Open in App ↗",
                    () => DeskSpacePinnedAppFilesV380.TryOpenQuickArcItem(selectedAppPath, capturedFile.FullPath, getOwner()),
                    primary: false,
                    tooltip: capturedFile.FullPath);
                openFileBtn.Padding = new Thickness(9, 3, 9, 3);
                openFileBtn.MinHeight = 26;
                openFileBtn.FontSize = 11;

                cardBtns.Children.Add(copyCardBtn);
                cardBtns.Children.Add(saveToDonutBtn);
                cardBtns.Children.Add(openFileBtn);
                DockPanel.SetDock(cardBtns, Dock.Right);
                cardHeader.Children.Add(cardBtns);

                var titleWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                titleWrap.Children.Add(new TextBlock
                {
                    Text = fileRec.DisplayName,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Text),
                    Margin = new Thickness(0, 0, 8, 0)
                });
                titleWrap.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(5),
                    Background = DeskSpaceDesignV283.Solid(p.Navigation),
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = fileRec.KindBadge,
                        FontSize = 10.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = DeskSpaceDesignV283.Solid(p.Muted)
                    }
                });
                cardHeader.Children.Add(titleWrap);
                cardStack.Children.Add(cardHeader);

                var fieldsGrid = new WrapPanel();
                foreach (var kv in visiblePairs)
                {
                    string hKey = kv.Key;
                    string hVal = kv.Value;
                    var itemDock = new DockPanel { LastChildFill = true };
                    var copyFieldBtn = DeskSpaceFluentBackdropV380.CreatePillButton(
                        "⧉",
                        () =>
                        {
                            Clipboard.SetText(hVal);
                            onStatusToast($"Copied {hKey}: {hVal}", false);
                        },
                        primary: false,
                        tooltip: $"Copy {hKey}");
                    copyFieldBtn.Padding = new Thickness(6, 2, 6, 2);
                    copyFieldBtn.MinHeight = 22;
                    copyFieldBtn.FontSize = 10.5;
                    copyFieldBtn.Margin = new Thickness(6, 0, 0, 0);
                    DockPanel.SetDock(copyFieldBtn, Dock.Right);
                    itemDock.Children.Add(copyFieldBtn);

                    var kvStack = new StackPanel();
                    kvStack.Children.Add(new TextBlock
                    {
                        Text = hKey,
                        FontSize = 10.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                    kvStack.Children.Add(new TextBlock
                    {
                        Text = hVal,
                        FontSize = 12.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = DeskSpaceDesignV283.Solid(p.Text),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        ToolTip = hVal
                    });
                    itemDock.Children.Add(kvStack);

                    var fieldTile = new Border
                    {
                        Width = 248,
                        CornerRadius = new CornerRadius(8),
                        Background = DeskSpaceDesignV283.Solid(p.Raised),
                        BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(9, 6, 9, 6),
                        Margin = new Thickness(0, 0, 8, 6),
                        Child = itemDock
                    };
                    fieldsGrid.Children.Add(fieldTile);
                }
                cardStack.Children.Add(fieldsGrid);

                var fileCard = new Border
                {
                    CornerRadius = new CornerRadius(11),
                    Background = DeskSpaceDesignV283.Solid(p.Surface),
                    BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(13, 10, 13, 8),
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = cardStack
                };
                filesStack.Children.Add(fileCard);
            }
        }

        PopulateFilesStack(appFileFilter);
        filterBox.TextChanged += (_, _) =>
        {
            onFilterChanged(filterBox.Text);
            PopulateFilesStack(filterBox.Text);
        };

        var filesContainerCard = DeskSpaceFluentBackdropV380.CreateCard(filesOuterDock, p);
        filesContainerCard.Padding = new Thickness(14);
        root.Children.Add(filesContainerCard);
    }
}

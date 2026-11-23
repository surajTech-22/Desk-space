using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DeskSpace;

internal enum PinnedAppKindV380
{
    StandardDocument,
    TallyAccounting,
    WinmanAccounting,
    IdeWorkspace
}

internal sealed record AppFileCapabilityV380(
    string PinnedPath,
    string ExecutablePath,
    string ExeName,
    string DisplayName,
    PinnedAppKindV380 Kind,
    HashSet<string> Extensions,
    bool IncludesTallyCompanies,
    bool IncludesProjectFolders,
    List<string> ExtraDataRoots,
    List<string> TallyAutoLoadCodes);

internal sealed record PinnedAppFileItemV380(
    string DisplayName,
    string FullPath,
    string ParentFolder,
    string KindBadge,
    DateTime SortTimeUtc,
    bool IsRecent,
    string SourceBadge,
    bool IsDirectory,
    string? TallyCompanyCode = null,
    string? TallyDataRoot = null);

/// <summary>
/// Context-aware file, accounting-company, and workspace launcher for pinned DeskSpace applications.
/// Discovers per-app launch history, Windows Recent shortcuts, Office MRU entries, Windows Search,
/// DeskSpace indexes, and bounded multi-drive locations without reading private document contents.
/// </summary>
internal static class DeskSpacePinnedAppFilesV380
{
    private const uint ASSOCSTR_EXECUTABLE = 2;
    private const int MaxHistoryPerExe = 25;
    private const int MaxSystemSearchHits = 400;
    private const int MaxDriveDepth = 4;
    private const int MaxVisitedDirs = 6000;

    private static readonly object HistoryLock = new();
    private static readonly object OpenWindowsLock = new();
    private static readonly Dictionary<string, Window> OpenWindows = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> AlwaysBlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".msi", ".msp", ".scr", ".dll", ".lnk", ".reg",
        ".pif", ".cpl", ".hta", ".sys", ".drv", ".ocx", ".msix", ".appx",
        ".appref-ms", ".msc", ".gadget", ".scf", ".url", ".1800", ".900"
    };

    private static readonly HashSet<string> ScriptExtensionsBlockedOutsideIde = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".vbs", ".vbe",
        ".js", ".jse", ".wsf", ".wsh"
    };

    private static readonly HashSet<string> NoisyGlobalExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xml", ".json", ".txt", ".log", ".ini", ".cfg", ".html", ".htm", ".css", ".js"
    };

    private static readonly HashSet<string> SkipDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Program Files", "Program Files (x86)", "ProgramData", "AppData",
        "$Recycle.Bin", "System Volume Information", "node_modules", ".git", "bin", "obj", "WinSxS"
    };

    private static readonly string[] CommonProbeExtensions =
    {
        ".txt", ".log", ".md", ".rtf", ".pdf", ".doc", ".docx", ".docm", ".dotx", ".odt",
        ".xls", ".xlsx", ".xlsm", ".xlsb", ".xltx", ".xltm", ".xlam", ".csv", ".tsv", ".ods",
        ".ppt", ".pptx", ".pptm", ".ppsx", ".odp",
        ".json", ".xml", ".xaml", ".yml", ".yaml", ".ini", ".cfg", ".sql", ".html", ".htm", ".css",
        ".cs", ".csproj", ".sln", ".slnx", ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".kt",
        ".cpp", ".h", ".go", ".rs", ".gradle", ".ps1",
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".svg", ".psd",
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".mp3", ".wav", ".flac", ".m4a", ".aac",
        ".zip", ".7z", ".rar", ".tar", ".gz", ".xz",
        ".tax", ".wgst", ".24q", ".26q", ".27q", ".27eq", ".7eq", ".tbk", ".tsf", ".tcp", ".tdl"
    };

    private static readonly string[] StandardTallyDataPaths =
    {
        @"C:\Users\Public\TallyPrime\Data",
        @"C:\TallyPrime\Data",
        @"C:\Users\Public\Tally.ERP9\Data",
        @"C:\Tally.ERP9\Data",
        @"D:\TallyData",
        @"D:\TallyPrime\Data"
    };

    private static readonly HashSet<string> BackupOnlyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tax.wb", ".wgst.wb", ".wt", ".tbk"
    };

    private sealed class LaunchHistoryEntryDto
    {
        public string FullPath { get; set; } = string.Empty;
        public DateTime LastOpenedUtc { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "AssocQueryStringW")]
    private static extern int AssocQueryStringW(
        uint flags,
        uint query,
        string association,
        string? extra,
        StringBuilder? buffer,
        ref uint chars);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    private static string DefaultHistoryPath =>
        System.IO.Path.Combine(WorkspaceStore.Root, "pinned-app-launches-v380.json");

    internal static string ResolvePinnedExecutable(string pinnedPath)
    {
        if (string.IsNullOrWhiteSpace(pinnedPath))
            return string.Empty;

        string fullPinned = NormalizeFullPathSafe(pinnedPath);
        if (string.IsNullOrEmpty(fullPinned))
            return string.Empty;

        if (!System.IO.Path.GetExtension(fullPinned).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            return fullPinned;

        if (!File.Exists(fullPinned))
            return string.Empty;

        object? wsh = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return string.Empty;

            wsh = Activator.CreateInstance(shellType);
            if (wsh == null)
                return string.Empty;

            string target = ResolveShortcutWithShell(wsh, fullPinned);
            return string.IsNullOrWhiteSpace(target) ? string.Empty : NormalizeFullPathSafe(target);
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            ReleaseComObject(wsh);
        }
    }

    internal static (List<string> DataDirs, List<string> LoadCodes) ParseTallyIni(string iniText)
    {
        var dataDirs = new List<string>();
        var loadCodes = new List<string>();
        if (string.IsNullOrWhiteSpace(iniText))
            return (dataDirs, loadCodes);

        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool? defaultCompanies = null;

        string[] lines = iniText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[')
                continue;

            int eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            string key = line.Substring(0, eqIndex).Trim();
            string rawValue = line.Substring(eqIndex + 1).Trim();

            if (key.Equals("Default Companies", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("DefaultCompanies", StringComparison.OrdinalIgnoreCase))
            {
                string val = StripIniComment(rawValue).Trim();
                if (val.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                    val.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    val.Equals("1", StringComparison.OrdinalIgnoreCase))
                {
                    defaultCompanies = true;
                }
                else if (val.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                         val.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                         val.Equals("0", StringComparison.OrdinalIgnoreCase))
                {
                    defaultCompanies = false;
                }
                continue;
            }

            if (key.Equals("Data", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = rawValue.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string seg = parts[i].Trim().Trim('"');
                    if (i == 0 && seg.Length == 0)
                        break;
                    if (seg.Length == 0)
                        continue;
                    if (i > 0 && !LooksLikeDirectoryPath(seg))
                        break;

                    string cleanDir = seg.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                    if (cleanDir.Length == 2 && cleanDir[1] == ':')
                        cleanDir += System.IO.Path.DirectorySeparatorChar;
                    if (seenDirs.Add(cleanDir))
                        dataDirs.Add(cleanDir);
                }
                continue;
            }

            if (key.Equals("Load", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = rawValue.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string seg = parts[i].Trim();
                    if (i == 0 && seg.Length == 0)
                        break;
                    if (seg.Length == 0)
                        continue;
                    if (i > 0 && !LooksLikeCompanyCodeList(seg))
                        break;

                    string[] tokens = seg.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string rawToken in tokens)
                    {
                        string code = rawToken.Trim().Trim('"');
                        if (code.Length > 0 && code.All(char.IsDigit) && seenCodes.Add(code))
                            loadCodes.Add(code);
                    }
                }
            }
        }

        if (defaultCompanies == false)
            loadCodes.Clear();

        return (dataDirs, loadCodes);
    }

    internal static bool IsSafeTargetExtension(string ext, bool ideMode)
    {
        if (string.IsNullOrWhiteSpace(ext))
            return false;

        string normalized = NormalizeExtension(ext);
        if (normalized.Length < 2 || normalized.Length > 20)
            return false;

        if (AlwaysBlockedExtensions.Contains(normalized))
            return false;

        if (!ideMode && ScriptExtensionsBlockedOutsideIde.Contains(normalized))
            return false;

        return true;
    }

    internal static HashSet<string> DiscoverRegistryExtensions(string exeName)
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(exeName))
            return extensions;

        string cleanExe = System.IO.Path.GetFileName(exeName.Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(cleanExe))
            return extensions;

        if (!cleanExe.Contains('.', StringComparison.Ordinal))
            cleanExe += ".exe";

        bool ideMode = IsIdeExecutableName(cleanExe);
        var progIdExeCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void TryAddExt(string? rawExt)
        {
            if (string.IsNullOrWhiteSpace(rawExt))
                return;
            string norm = NormalizeExtension(rawExt);
            if (norm == ".*" || norm.IndexOfAny(new[] { ' ', '/', '\\', '*', '?' }) >= 0)
                return;
            if (IsSafeTargetExtension(norm, ideMode))
                extensions.Add(norm);
        }

        // 1. HKCR\Applications\<exeName>\SupportedTypes
        try
        {
            using var appSupported = Registry.ClassesRoot.OpenSubKey($@"Applications\{cleanExe}\SupportedTypes");
            if (appSupported != null)
            {
                foreach (string valueName in appSupported.GetValueNames())
                {
                    if (valueName.StartsWith('.'))
                        TryAddExt(valueName);
                }
            }
        }
        catch
        {
        }

        // 2. HKCU / HKLM SOFTWARE\RegisteredApplications -> Capabilities\FileAssociations
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var regApps = baseKey.OpenSubKey(@"SOFTWARE\RegisteredApplications");
                    if (regApps == null)
                        continue;

                    foreach (string appName in regApps.GetValueNames())
                    {
                        string? capSubPath = regApps.GetValue(appName) as string;
                        if (string.IsNullOrWhiteSpace(capSubPath))
                            continue;

                        using var capKey = baseKey.OpenSubKey(capSubPath);
                        if (capKey == null)
                            continue;

                        using var fileAssoc = capKey.OpenSubKey("FileAssociations");
                        if (fileAssoc == null)
                            continue;

                        string? appIcon = capKey.GetValue("ApplicationIcon") as string;
                        bool iconMatches = !string.IsNullOrWhiteSpace(appIcon) &&
                                           appIcon.Contains(cleanExe, StringComparison.OrdinalIgnoreCase);

                        foreach (string extKey in fileAssoc.GetValueNames())
                        {
                            if (!extKey.StartsWith('.'))
                                continue;

                            string? progId = fileAssoc.GetValue(extKey) as string;
                            if (string.IsNullOrWhiteSpace(progId))
                                continue;

                            if (iconMatches || DoesProgIdMatchExecutable(progId, cleanExe, progIdExeCache))
                                TryAddExt(extKey);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        // 3. Probe common extensions via shlwapi.dll AssocQueryStringW(ASSOCSTR_EXECUTABLE = 2)
        foreach (string probe in CommonProbeExtensions)
        {
            if (!IsSafeTargetExtension(probe, ideMode))
                continue;

            string assocExe = QueryAssociatedExecutableByExtension(probe);
            if (!string.IsNullOrWhiteSpace(assocExe) &&
                DeskSpacePinnedRecentV122.SameExecutableV122(assocExe, cleanExe))
            {
                extensions.Add(probe);
            }
        }

        return extensions;
    }

    internal static AppFileCapabilityV380 ResolveCapability(string pinnedPath)
    {
        string safePinned = NormalizeFullPathSafe(pinnedPath);
        string resolved = ResolvePinnedExecutable(pinnedPath);
        string effectiveExe = !string.IsNullOrWhiteSpace(resolved) ? resolved : safePinned;
        string exeName = System.IO.Path.GetFileName(effectiveExe);
        string exeLower = exeName.ToLowerInvariant();

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extraRoots = new List<string>();
        var loadCodes = new List<string>();
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddExtensions(params string[] exts)
        {
            foreach (string ext in exts)
                extensions.Add(NormalizeExtension(ext));
        }

        void AddRoot(string? dir, bool requireExisting)
        {
            if (string.IsNullOrWhiteSpace(dir))
                return;
            string norm = NormalizeFullPathSafe(dir);
            if (string.IsNullOrEmpty(norm))
                return;
            if (requireExisting && !SafeDirectoryExists(norm))
                return;
            if (seenRoots.Add(norm))
                extraRoots.Add(norm);
        }

        PinnedAppKindV380 kind = PinnedAppKindV380.StandardDocument;
        bool includesTallyCompanies = false;
        bool includesProjectFolders = false;
        string? profileDisplayName = null;

        switch (exeLower)
        {
            case "tally.exe":
            case "tallyprime.exe":
                kind = PinnedAppKindV380.TallyAccounting;
                includesTallyCompanies = true;
                profileDisplayName = exeLower == "tallyprime.exe" ? "TallyPrime" : "Tally";
                AddExtensions(".tbk", ".tsf", ".tcp", ".tdl", ".xml");

                string? exeDir = System.IO.Path.GetDirectoryName(effectiveExe);
                if (!string.IsNullOrWhiteSpace(exeDir))
                {
                    string iniPath = System.IO.Path.Combine(exeDir, "Tally.ini");
                    if (SafeFileExists(iniPath))
                    {
                        try
                        {
                            var (dataDirs, iniLoadCodes) = ParseTallyIni(File.ReadAllText(iniPath));
                            foreach (string dir in dataDirs)
                            {
                                string resolvedDir = System.IO.Path.IsPathRooted(dir)
                                    ? dir
                                    : System.IO.Path.Combine(exeDir, dir);
                                AddRoot(resolvedDir, requireExisting: false);
                            }
                            foreach (string code in iniLoadCodes)
                            {
                                if (!loadCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
                                    loadCodes.Add(code);
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                foreach (string stdPath in StandardTallyDataPaths)
                    AddRoot(stdPath, requireExisting: true);
                break;

            case "excel.exe":
                profileDisplayName = "Microsoft Excel";
                AddExtensions(".xlsx", ".xls", ".xlsm", ".xlsb", ".csv", ".tsv", ".ods", ".xltx", ".xltm", ".xlam", ".xml");
                break;

            case "wintax.exe":
                kind = PinnedAppKindV380.WinmanAccounting;
                profileDisplayName = "Winman CA-ERP / GST";
                AddExtensions(".tax", ".wgst", ".tax.wb", ".wgst.wb");
                AddRoot(@"C:\Winman", requireExisting: true);
                AddRoot(@"D:\Winman", requireExisting: true);
                break;

            case "wintds.exe":
                kind = PinnedAppKindV380.WinmanAccounting;
                profileDisplayName = "Winman TDS";
                AddExtensions(".24q", ".26q", ".27q", ".27eq", ".7eq", ".wt");
                AddRoot(@"C:\Winman", requireExisting: true);
                AddRoot(@"D:\Winman", requireExisting: true);
                break;

            case "winword.exe":
                profileDisplayName = "Microsoft Word";
                AddExtensions(".docx", ".doc", ".docm", ".rtf", ".odt", ".txt", ".dotx");
                break;

            case "powerpnt.exe":
                profileDisplayName = "Microsoft PowerPoint";
                AddExtensions(".pptx", ".ppt", ".pptm", ".ppsx", ".odp");
                break;

            case "acrord32.exe":
            case "acrobat.exe":
            case "foxitpdfreader.exe":
            case "sumatrapdf.exe":
                AddExtensions(".pdf");
                break;

            case "msedge.exe":
            case "chrome.exe":
            case "firefox.exe":
            case "brave.exe":
                AddExtensions(".pdf", ".html", ".htm", ".svg", ".webp");
                break;

            case "code.exe":
            case "devenv.exe":
            case "studio64.exe":
            case "rider64.exe":
            case "notepad++.exe":
            case "sublime_text.exe":
                kind = PinnedAppKindV380.IdeWorkspace;
                includesProjectFolders = exeLower is "code.exe" or "devenv.exe" or "studio64.exe" or "rider64.exe";
                profileDisplayName = exeLower switch
                {
                    "code.exe" => "Visual Studio Code",
                    "devenv.exe" => "Visual Studio",
                    "studio64.exe" => "Android Studio",
                    "rider64.exe" => "JetBrains Rider",
                    "notepad++.exe" => "Notepad++",
                    "sublime_text.exe" => "Sublime Text",
                    _ => null
                };
                AddExtensions(
                    ".cs", ".csproj", ".sln", ".slnx", ".json", ".xml", ".xaml", ".js", ".ts",
                    ".tsx", ".jsx", ".py", ".java", ".kt", ".html", ".css", ".md", ".ps1",
                    ".yml", ".yaml", ".sql", ".txt", ".gradle", ".cpp", ".h", ".go", ".rs");
                break;

            case "notepad.exe":
                profileDisplayName = "Notepad";
                AddExtensions(".txt", ".log", ".md", ".json", ".csv", ".ini", ".cfg", ".xml", ".ps1");
                break;

            case "vlc.exe":
            case "wmplayer.exe":
            case "mpc-hc64.exe":
            case "potplayermini64.exe":
                AddExtensions(".mp4", ".mkv", ".avi", ".mov", ".webm", ".mp3", ".wav", ".flac", ".m4a", ".aac");
                break;

            case "photoshop.exe":
            case "mspaint.exe":
            case "paintdotnet.exe":
                AddExtensions(".psd", ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff", ".gif", ".svg");
                break;

            case "7zfm.exe":
            case "winrar.exe":
                AddExtensions(".zip", ".7z", ".rar", ".tar", ".gz", ".xz");
                break;
        }

        // Merge registry & OS association discoveries safely.
        bool ideSafe = kind == PinnedAppKindV380.IdeWorkspace;
        foreach (string regExt in DiscoverRegistryExtensions(exeName))
        {
            if (IsSafeTargetExtension(regExt, ideSafe))
                extensions.Add(regExt);
        }

        string displayName = ResolveAppDisplayName(safePinned, effectiveExe, profileDisplayName);
        return new AppFileCapabilityV380(
            safePinned,
            effectiveExe,
            exeName,
            displayName,
            kind,
            extensions,
            includesTallyCompanies,
            includesProjectFolders,
            extraRoots,
            loadCodes);
    }

    internal static bool TryParseTallyCompanyFolder(
        string folderPath,
        out PinnedAppFileItemV380? item,
        bool isRecent = false,
        string sourceBadge = "Tally Data")
    {
        item = null;
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        try
        {
            string fullFolder = NormalizeFullPathSafe(folderPath)
                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(fullFolder) || !Directory.Exists(fullFolder))
                return false;

            var dirInfo = new DirectoryInfo(fullFolder);
            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                return false;

            string company1800 = System.IO.Path.Combine(fullFolder, "Company.1800");
            string manager1800 = System.IO.Path.Combine(fullFolder, "Manager.1800");
            string tranMgr1800 = System.IO.Path.Combine(fullFolder, "TranMgr.1800");

            string company900 = System.IO.Path.Combine(fullFolder, "Company.900");
            string manager900 = System.IO.Path.Combine(fullFolder, "Manager.900");
            string tranMgr900 = System.IO.Path.Combine(fullFolder, "TranMgr.900");

            bool has1800 = File.Exists(company1800) && (File.Exists(manager1800) || File.Exists(tranMgr1800));
            bool has900 = File.Exists(company900) && (File.Exists(manager900) || File.Exists(tranMgr900));

            if (!has1800 && !has900)
                return false;

            var masterFiles = new List<string>(3);
            string kindBadge;
            if (has1800)
            {
                kindBadge = "TallyPrime (.1800)";
                masterFiles.Add(company1800);
                if (File.Exists(manager1800)) masterFiles.Add(manager1800);
                if (File.Exists(tranMgr1800)) masterFiles.Add(tranMgr1800);
            }
            else
            {
                kindBadge = "Tally.ERP 9 (.900)";
                masterFiles.Add(company900);
                if (File.Exists(manager900)) masterFiles.Add(manager900);
                if (File.Exists(tranMgr900)) masterFiles.Add(tranMgr900);
            }

            DateTime newestUtc = DateTime.MinValue;
            foreach (string master in masterFiles)
            {
                DateTime ts = File.GetLastWriteTimeUtc(master);
                if (ts > newestUtc)
                    newestUtc = ts;
            }
            if (newestUtc == DateTime.MinValue)
                newestUtc = dirInfo.LastWriteTimeUtc;

            string folderName = System.IO.Path.GetFileName(fullFolder);
            string? code = !string.IsNullOrEmpty(folderName) && folderName.All(char.IsDigit)
                ? folderName
                : null;
            string? dataRoot = System.IO.Path.GetDirectoryName(fullFolder);
            string displayName = "Tally Company #" + (code ?? folderName);

            item = new PinnedAppFileItemV380(
                DisplayName: displayName,
                FullPath: fullFolder,
                ParentFolder: dataRoot ?? string.Empty,
                KindBadge: kindBadge,
                SortTimeUtc: newestUtc,
                IsRecent: isRecent,
                SourceBadge: sourceBadge,
                IsDirectory: true,
                TallyCompanyCode: code,
                TallyDataRoot: dataRoot);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal static void RecordLaunch(string exeName, string fullPath, string? storagePath = null)
    {
        if (string.IsNullOrWhiteSpace(exeName) || string.IsNullOrWhiteSpace(fullPath))
            return;

        string exeKey = NormalizeExeKey(exeName);
        string canonicalPath = NormalizeFullPathSafe(fullPath);
        if (string.IsNullOrEmpty(exeKey) || string.IsNullOrEmpty(canonicalPath))
            return;

        string targetFile = string.IsNullOrWhiteSpace(storagePath) ? DefaultHistoryPath : storagePath;

        lock (HistoryLock)
        {
            var dict = ReadHistoryMapLocked(targetFile);
            if (!dict.TryGetValue(exeKey, out var entries))
            {
                entries = new List<LaunchHistoryEntryDto>();
                dict[exeKey] = entries;
            }

            entries.RemoveAll(e => string.Equals(e.FullPath, canonicalPath, StringComparison.OrdinalIgnoreCase));
            entries.Insert(0, new LaunchHistoryEntryDto
            {
                FullPath = canonicalPath,
                LastOpenedUtc = DateTime.UtcNow
            });

            if (entries.Count > MaxHistoryPerExe)
                entries.RemoveRange(MaxHistoryPerExe, entries.Count - MaxHistoryPerExe);

            WriteHistoryMapLocked(targetFile, dict);
        }
    }

    internal static IReadOnlyList<(string FullPath, DateTime LastOpenedUtc)> GetRecentLaunches(
        string exeName,
        string? storagePath = null)
    {
        if (string.IsNullOrWhiteSpace(exeName))
            return Array.Empty<(string, DateTime)>();

        string exeKey = NormalizeExeKey(exeName);
        if (string.IsNullOrEmpty(exeKey))
            return Array.Empty<(string, DateTime)>();

        string targetFile = string.IsNullOrWhiteSpace(storagePath) ? DefaultHistoryPath : storagePath;

        lock (HistoryLock)
        {
            var dict = ReadHistoryMapLocked(targetFile);
            if (!dict.TryGetValue(exeKey, out var entries) || entries.Count == 0)
                return Array.Empty<(string, DateTime)>();

            return entries
                .Where(e => !string.IsNullOrWhiteSpace(e.FullPath))
                .OrderByDescending(e => e.LastOpenedUtc)
                .Take(MaxHistoryPerExe)
                .Select(e => (e.FullPath, e.LastOpenedUtc))
                .ToArray();
        }
    }

    internal static List<PinnedAppFileItemV380> DiscoverRecentItems(
        AppFileCapabilityV380 cap,
        string? historyStoragePath = null)
    {
        var collected = new List<PinnedAppFileItemV380>();
        if (cap == null)
            return collected;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(PinnedAppFileItemV380? candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.FullPath))
                return;
            string key = NormalizeFullPathSafe(candidate.FullPath)
                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            if (key.Length > 0 && seen.Add(key))
                collected.Add(candidate);
        }

        // Tier 1: Per-app DeskSpace launch history
        foreach (var (fullPath, lastOpenedUtc) in GetRecentLaunches(cap.ExeName, historyStoragePath))
        {
            if (cap.IncludesTallyCompanies && SafeDirectoryExists(fullPath))
            {
                if (TryParseTallyCompanyFolder(fullPath, out var tallyItem, isRecent: true, sourceBadge: "★ Opened in DeskSpace") &&
                    tallyItem != null)
                {
                    DateTime bestTime = tallyItem.SortTimeUtc > lastOpenedUtc ? tallyItem.SortTimeUtc : lastOpenedUtc;
                    TryAdd(tallyItem with { SortTimeUtc = bestTime });
                }
                continue;
            }

            if (cap.IncludesProjectFolders && SafeDirectoryExists(fullPath))
            {
                string trimmed = fullPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                TryAdd(new PinnedAppFileItemV380(
                    DisplayName: System.IO.Path.GetFileName(trimmed),
                    FullPath: trimmed,
                    ParentFolder: System.IO.Path.GetDirectoryName(trimmed) ?? string.Empty,
                    KindBadge: "Project Folder",
                    SortTimeUtc: lastOpenedUtc,
                    IsRecent: true,
                    SourceBadge: "★ Opened in DeskSpace",
                    IsDirectory: true));
                continue;
            }

            if (SafeFileExists(fullPath) && IsAllowedForCapability(cap, fullPath, checkExtensionMatch: false))
            {
                DateTime fileTime = SafeGetFileWriteTimeUtc(fullPath);
                DateTime bestTime = fileTime > lastOpenedUtc ? fileTime : lastOpenedUtc;
                TryAdd(CreateFileItem(fullPath, bestTime, isRecent: true, sourceBadge: "★ Opened in DeskSpace"));
            }
        }

        // Tier 2: Windows Recent folder .lnk targets
        try
        {
            string recentDir = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (!string.IsNullOrWhiteSpace(recentDir) && Directory.Exists(recentDir))
            {
                string[] recentShortcuts = Directory.EnumerateFiles(recentDir, "*.lnk")
                    .OrderByDescending(SafeGetFileWriteTimeUtc)
                    .Take(120)
                    .ToArray();

                if (recentShortcuts.Length > 0)
                {
                    object? wsh = null;
                    try
                    {
                        var shellType = Type.GetTypeFromProgID("WScript.Shell");
                        if (shellType != null)
                            wsh = Activator.CreateInstance(shellType);

                        if (wsh != null)
                        {
                            foreach (string lnk in recentShortcuts)
                            {
                                string target = ResolveShortcutWithShell(wsh, lnk);
                                if (string.IsNullOrWhiteSpace(target))
                                    continue;

                                string fullTarget = NormalizeFullPathSafe(target);
                                if (string.IsNullOrEmpty(fullTarget) ||
                                    string.Equals(fullTarget, cap.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                DateTime lnkTime = SafeGetFileWriteTimeUtc(lnk);

                                if (SafeDirectoryExists(fullTarget))
                                {
                                    if (cap.IncludesTallyCompanies &&
                                        TryParseTallyCompanyFolder(fullTarget, out var tallyItem, isRecent: true, sourceBadge: "★ Windows Recent") &&
                                        tallyItem != null)
                                    {
                                        DateTime bestTime = tallyItem.SortTimeUtc > lnkTime ? tallyItem.SortTimeUtc : lnkTime;
                                        TryAdd(tallyItem with { SortTimeUtc = bestTime });
                                    }
                                    else if (cap.IncludesProjectFolders && IsLikelyProjectFolder(fullTarget, out string projBadge))
                                    {
                                        string trimmed = fullTarget.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                                        TryAdd(new PinnedAppFileItemV380(
                                            DisplayName: System.IO.Path.GetFileName(trimmed),
                                            FullPath: trimmed,
                                            ParentFolder: System.IO.Path.GetDirectoryName(trimmed) ?? string.Empty,
                                            KindBadge: projBadge,
                                            SortTimeUtc: lnkTime,
                                            IsRecent: true,
                                            SourceBadge: "★ Windows Recent",
                                            IsDirectory: true));
                                    }
                                    continue;
                                }

                                if (!SafeFileExists(fullTarget) || IsReparsePoint(fullTarget))
                                    continue;

                                if (MatchesCapabilityOrExecutable(cap, fullTarget))
                                    TryAdd(CreateFileItem(fullTarget, lnkTime, isRecent: true, sourceBadge: "★ Windows Recent"));
                            }
                        }
                    }
                    finally
                    {
                        ReleaseComObject(wsh);
                    }
                }
            }
        }
        catch
        {
        }

        // Tier 3: Office MRU Registry (Excel, Word, PowerPoint)
        string? officeAppSubKey = cap.ExeName.ToLowerInvariant() switch
        {
            "excel.exe" => "Excel",
            "winword.exe" => "Word",
            "powerpnt.exe" => "PowerPoint",
            _ => null
        };

        if (officeAppSubKey != null)
        {
            foreach (var (mruPath, mruTimeUtc) in DiscoverOfficeMruPaths(officeAppSubKey))
            {
                if (SafeFileExists(mruPath) && !IsReparsePoint(mruPath) &&
                    IsAllowedForCapability(cap, mruPath, checkExtensionMatch: false))
                {
                    DateTime fileTime = SafeGetFileWriteTimeUtc(mruPath);
                    DateTime bestTime = mruTimeUtc > fileTime ? mruTimeUtc : fileTime;
                    TryAdd(CreateFileItem(mruPath, bestTime, isRecent: true, sourceBadge: "★ Office Recent"));
                }
            }
        }

        // Tier 4: Tally auto-load / recently active companies
        if (cap.IncludesTallyCompanies)
        {
            var autoCodes = new HashSet<string>(cap.TallyAutoLoadCodes, StringComparer.OrdinalIgnoreCase);
            var recentTallyCandidates = new List<PinnedAppFileItemV380>();
            DateTime activeCutoffUtc = DateTime.UtcNow.AddDays(-30);

            foreach (string root in cap.ExtraDataRoots)
            {
                if (!SafeDirectoryExists(root))
                    continue;

                try
                {
                    foreach (string subDir in Directory.EnumerateDirectories(root))
                    {
                        if (!TryParseTallyCompanyFolder(subDir, out var companyItem, isRecent: true, sourceBadge: "★ Tally Recent") ||
                            companyItem == null)
                        {
                            continue;
                        }

                        bool isAutoLoad = !string.IsNullOrEmpty(companyItem.TallyCompanyCode) &&
                                          autoCodes.Contains(companyItem.TallyCompanyCode);
                        if (isAutoLoad)
                        {
                            TryAdd(companyItem);
                        }
                        else if (companyItem.SortTimeUtc >= activeCutoffUtc)
                        {
                            recentTallyCandidates.Add(companyItem);
                        }
                    }
                }
                catch
                {
                }
            }

            foreach (var activeCompany in recentTallyCandidates
                         .OrderByDescending(c => c.SortTimeUtc)
                         .Take(5))
            {
                TryAdd(activeCompany);
            }
        }

        return collected;
    }

    internal static async Task<List<PinnedAppFileItemV380>> DiscoverSystemItemsAsync(
        AppFileCapabilityV380 cap,
        Action<IReadOnlyList<PinnedAppFileItemV380>>? onProgress,
        CancellationToken ct)
    {
        if (cap == null)
            return new List<PinnedAppFileItemV380>();

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var results = new List<PinnedAppFileItemV380>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lastProgressUtc = DateTime.UtcNow;

            void AddUnique(PinnedAppFileItemV380? item)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.FullPath))
                    return;
                string key = NormalizeFullPathSafe(item.FullPath)
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                if (key.Length > 0 && seen.Add(key))
                    results.Add(item);
            }

            void EmitProgress(bool force = false)
            {
                if (onProgress == null || ct.IsCancellationRequested)
                    return;
                DateTime now = DateTime.UtcNow;
                if (!force && (now - lastProgressUtc).TotalMilliseconds < 220)
                    return;
                lastProgressUtc = now;
                onProgress(results.ToArray());
            }

            // 1. Windows Search SYSTEMINDEX via ADODB.Connection
            foreach (var hit in QueryWindowsSearchSystemIndex(cap, ct))
                AddUnique(hit);
            EmitProgress(force: true);

            // 2. Reflection query into DeskSpaceDiskSearchV330 if present + DeskSpaceSearchIndexV13 if already warmed
            foreach (var hit in QueryDeskSpaceDiskSearchReflection(cap, ct))
                AddUnique(hit);

            foreach (var hit in QueryWarmSearchIndexV13(cap, ct))
                AddUnique(hit);
            EmitProgress(force: true);

            // 3. Bounded multi-drive enumeration
            RunBoundedDriveEnumeration(cap, AddUnique, () => EmitProgress(force: false), ct);

            EmitProgress(force: true);
            return results;
        }, ct).ConfigureAwait(false);
    }

    internal static List<PinnedAppFileItemV380> MergeAndRank(
        IEnumerable<PinnedAppFileItemV380> recent,
        IEnumerable<PinnedAppFileItemV380> system,
        int maxResults = 300)
    {
        if (maxResults <= 0)
            return new List<PinnedAppFileItemV380>();

        var merged = new Dictionary<string, PinnedAppFileItemV380>(StringComparer.OrdinalIgnoreCase);

        void MergeOne(PinnedAppFileItemV380? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FullPath))
                return;

            string key = NormalizeFullPathSafe(item.FullPath)
                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            if (key.Length == 0)
                return;

            if (!merged.TryGetValue(key, out var existing))
            {
                merged[key] = item;
                return;
            }

            bool combinedRecent = existing.IsRecent || item.IsRecent;
            DateTime bestTime = existing.SortTimeUtc >= item.SortTimeUtc ? existing.SortTimeUtc : item.SortTimeUtc;

            PinnedAppFileItemV380 preferred;
            if (item.IsRecent && !existing.IsRecent)
                preferred = item;
            else if (existing.IsRecent && !item.IsRecent)
                preferred = existing;
            else
                preferred = existing.SortTimeUtc >= item.SortTimeUtc ? existing : item;

            merged[key] = preferred with
            {
                IsRecent = combinedRecent,
                SortTimeUtc = bestTime,
                TallyCompanyCode = preferred.TallyCompanyCode ?? existing.TallyCompanyCode ?? item.TallyCompanyCode,
                TallyDataRoot = preferred.TallyDataRoot ?? existing.TallyDataRoot ?? item.TallyDataRoot
            };
        }

        if (recent != null)
        {
            foreach (var item in recent)
                MergeOne(item);
        }

        if (system != null)
        {
            foreach (var item in system)
                MergeOne(item);
        }

        return merged.Values
            .OrderByDescending(x => x.IsRecent)
            .ThenByDescending(x => x.SortTimeUtc)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    internal static bool BuildLaunchInfo(
        AppFileCapabilityV380 cap,
        PinnedAppFileItemV380 item,
        out ProcessStartInfo? psi,
        out string statusNote,
        IReadOnlyList<string>? iniPrimaryDataDirs = null)
    {
        psi = null;
        statusNote = string.Empty;

        if (cap == null || item == null || string.IsNullOrWhiteSpace(cap.ExecutablePath) || string.IsNullOrWhiteSpace(item.FullPath))
        {
            statusNote = "Missing application or target file path.";
            return false;
        }

        string exeDir = System.IO.Path.GetDirectoryName(cap.ExecutablePath) ?? string.Empty;

        // 1. Tally company folder launch
        if (cap.Kind == PinnedAppKindV380.TallyAccounting && item.IsDirectory)
        {
            IReadOnlyList<string> primaryDirs = iniPrimaryDataDirs ?? ReadPrimaryTallyDataDirs(cap.ExecutablePath);
            var info = new ProcessStartInfo
            {
                FileName = cap.ExecutablePath,
                UseShellExecute = false,
                WorkingDirectory = exeDir
            };

            if (!string.IsNullOrWhiteSpace(item.TallyDataRoot) &&
                !MatchesAnyNormalizedDirectory(item.TallyDataRoot, primaryDirs))
            {
                info.ArgumentList.Add(item.TallyDataRoot);
            }

            if (!string.IsNullOrWhiteSpace(item.TallyCompanyCode) &&
                item.TallyCompanyCode.All(char.IsDigit))
            {
                info.ArgumentList.Add("/NOINILOAD");
                info.ArgumentList.Add($"/LOAD:{item.TallyCompanyCode}");
                statusNote = $"Launching {cap.DisplayName} with Company #{item.TallyCompanyCode}.";
            }
            else
            {
                statusNote = $"Launching {cap.DisplayName} (select company from {item.ParentFolder}).";
            }

            psi = info;
            return true;
        }

        // Security gate for non-directory files (and block raw .1800/.900 shards explicitly)
        if (!item.IsDirectory)
        {
            string rawExt = System.IO.Path.GetExtension(item.FullPath);
            if (rawExt.Equals(".1800", StringComparison.OrdinalIgnoreCase) ||
                rawExt.Equals(".900", StringComparison.OrdinalIgnoreCase))
            {
                statusNote = "Raw Tally database shard (.1800/.900) cannot be launched directly; open the Tally company folder instead.";
                return false;
            }

            bool allowScriptAsText = cap.Kind == PinnedAppKindV380.IdeWorkspace ||
                                     string.Equals(cap.ExeName, "notepad.exe", StringComparison.OrdinalIgnoreCase);
            if (!IsSafeTargetExtension(rawExt, allowScriptAsText))
            {
                statusNote = $"Blocked unsafe target extension: {rawExt}";
                return false;
            }
        }

        // 2. Accounting backup files (.tax.wb, .wgst.wb, .wt, .tbk)
        string effectiveExt = GetEffectiveExtension(item.FullPath);
        if (BackupOnlyExtensions.Contains(effectiveExt))
        {
            psi = new ProcessStartInfo
            {
                FileName = cap.ExecutablePath,
                UseShellExecute = false,
                WorkingDirectory = exeDir
            };
            statusNote = $"Opened {cap.DisplayName}. Restore backup from inside app: {item.FullPath}";
            return true;
        }

        // 3. Standard document / IDE file or project folder
        string workingDir = !string.IsNullOrWhiteSpace(item.ParentFolder)
            ? item.ParentFolder
            : (System.IO.Path.GetDirectoryName(item.FullPath) ?? string.Empty);

        var stdInfo = new ProcessStartInfo
        {
            FileName = cap.ExecutablePath,
            UseShellExecute = false,
            WorkingDirectory = workingDir
        };
        stdInfo.ArgumentList.Add(item.FullPath);

        psi = stdInfo;
        statusNote = $"Opening {item.DisplayName} with {cap.DisplayName}.";
        return true;
    }

    internal static bool TryOpenItem(
        AppFileCapabilityV380 cap,
        PinnedAppFileItemV380 item,
        Window? owner = null,
        string? historyStoragePath = null)
    {
        if (!BuildLaunchInfo(cap, item, out var psi, out string statusNote) || psi == null)
        {
            ShowError(owner, statusNote.Length > 0 ? statusNote : "Cannot open the selected item.", cap?.DisplayName ?? "DeskSpace");
            return false;
        }

        // Sanitize WorkingDirectory right before spawning so a deleted folder doesn't fail the launch.
        if (!string.IsNullOrWhiteSpace(psi.WorkingDirectory) && !SafeDirectoryExists(psi.WorkingDirectory))
            psi.WorkingDirectory = string.Empty;

        // If shortcut target could not be resolved to an exe and pinnedPath is a .lnk, use shell launch.
        if (psi.FileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            if (psi.ArgumentList.Count == 0)
            {
                psi.UseShellExecute = true;
            }
            else if (TryShellFallbackOpen(cap, item, historyStoragePath))
            {
                return true;
            }
        }

        try
        {
            Process.Start(psi);
            RecordLaunch(cap.ExeName, item.FullPath, historyStoragePath);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException or UnauthorizedAccessException)
        {
            if (TryShellFallbackOpen(cap, item, historyStoragePath))
                return true;

            ShowError(owner, $"Unable to open \"{item.DisplayName}\":\n{ex.Message}", cap.DisplayName);
            return false;
        }
    }

    internal static void Show(Window? owner, string pinnedPath)
    {
        if (string.IsNullOrWhiteSpace(pinnedPath))
            return;

        string canonicalPin = NormalizeFullPathSafe(pinnedPath);
        lock (OpenWindowsLock)
        {
            if (OpenWindows.TryGetValue(canonicalPin, out var existing))
            {
                try
                {
                    if (existing.IsLoaded)
                    {
                        if (existing.WindowState == WindowState.Minimized)
                            existing.WindowState = WindowState.Normal;
                        existing.Show();
                        existing.Activate();
                        return;
                    }
                }
                catch
                {
                }
                OpenWindows.Remove(canonicalPin);
            }
        }

        var cap = ResolveCapability(pinnedPath);
        var p = DeskSpaceDesignV283.Current;

        var win = DeskSpaceFluentBackdropV380.CreateModernWindow(
            $"DeskSpace · {cap.DisplayName}",
            720,
            580,
            owner,
            transient: false);
        win.MinWidth = 560;
        win.MinHeight = 440;
        PositionOnOwnerScreen(win, owner);

        lock (OpenWindowsLock)
            OpenWindows[canonicalPin] = win;

        var root = new DockPanel { Margin = new Thickness(18) };

        // --- Header Card ---
        var headerInner = new DockPanel { LastChildFill = true };

        var headerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var openAppBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Open App Only", () =>
        {
            if (TryLaunchAppOnly(cap, win))
                win.Close();
        }, primary: false, tooltip: $"Launch {cap.DisplayName} without opening a specific file");
        openAppBtn.Margin = new Thickness(0, 0, 8, 0);
        headerActions.Children.Add(openAppBtn);

        Button? refreshBtn = null;
        Action? triggerRefresh = null;
        refreshBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Refresh", () => triggerRefresh?.Invoke(),
            primary: false, tooltip: "Re-scan recent and system locations (F5)");
        headerActions.Children.Add(refreshBtn);

        DockPanel.SetDock(headerActions, Dock.Right);
        headerInner.Children.Add(headerActions);

        var titleArea = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        var appIconSource = DeskSpaceApplicationIconsV65.Get(cap.ExecutablePath) ??
                            DeskSpaceApplicationIconsV65.Get(cap.PinnedPath);

        var iconFrame = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(11),
            Background = DeskSpaceDesignV283.Solid(p.Surface),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = appIconSource != null
                ? (UIElement)new Image
                {
                    Source = appIconSource,
                    Width = 26,
                    Height = 26,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
                : new TextBlock
                {
                    Text = cap.DisplayName.Length > 0 ? cap.DisplayName.Substring(0, 1).ToUpperInvariant() : "A",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = DeskSpaceDesignV283.Solid(p.Accent),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
        };
        DockPanel.SetDock(iconFrame, Dock.Left);
        titleArea.Children.Add(iconFrame);

        var titleTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleTextStack.Children.Add(new TextBlock
        {
            Text = cap.DisplayName,
            FontSize = 18.5,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        string typeSummary = FormatCapabilitySummary(cap);
        var typePill = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = DeskSpaceDesignV283.Solid(p.AccentSoft),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 2.5, 8, 2.5),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = BuildCapabilityTooltip(cap),
            Child = new TextBlock
            {
                Text = typeSummary,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = DeskSpaceDesignV283.Solid(p.Accent),
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
        titleTextStack.Children.Add(typePill);
        titleArea.Children.Add(titleTextStack);
        headerInner.Children.Add(titleArea);

        var headerCard = DeskSpaceFluentBackdropV380.CreateCard(headerInner, p);
        headerCard.Padding = new Thickness(14, 12, 14, 12);
        headerCard.Margin = new Thickness(0, 0, 0, 12);

        // --- Search & Filter Toolbar ---
        var filterStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        var searchContainer = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        var searchBox = new TextBox
        {
            FontSize = 13.5,
            Padding = new Thickness(11, 8, 34, 8),
            ToolTip = "Type to filter by name, folder, extension, or Tally company code (Ctrl+F)"
        };
        DeskSpaceDesignV283.Input(searchBox, p);
        AutomationProperties.SetName(searchBox, "Filter files and folders");

        var searchPlaceholder = new TextBlock
        {
            Text = cap.IncludesTallyCompanies
                ? "Search Tally company code, data folder, or backup…"
                : "Filter files, folders, or paths… (Up/Down to browse, Enter to open)",
            FontSize = 13,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 34, 0),
            IsHitTestVisible = false
        };

        var clearSearchBtn = new Button
        {
            Content = "✕",
            Width = 24,
            Height = 24,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(0),
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            BorderThickness = new Thickness(0),
            Visibility = Visibility.Collapsed,
            ToolTip = "Clear search"
        };
        AutomationProperties.SetName(clearSearchBtn, "Clear search filter");

        searchContainer.Children.Add(searchBox);
        searchContainer.Children.Add(searchPlaceholder);
        searchContainer.Children.Add(clearSearchBtn);
        filterStack.Children.Add(searchContainer);

        var chipAndStatusRow = new DockPanel { LastChildFill = true };
        var chipsWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var statusText = new TextBlock
        {
            FontSize = 12,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(10, 0, 0, 0),
            Text = "Loading recent items…"
        };
        DockPanel.SetDock(statusText, Dock.Right);
        chipAndStatusRow.Children.Add(statusText);
        chipAndStatusRow.Children.Add(chipsWrap);
        filterStack.Children.Add(chipAndStatusRow);

        var topPanel = new StackPanel();
        topPanel.Children.Add(headerCard);
        topPanel.Children.Add(filterStack);
        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        // --- Bottom Action Bar ---
        var actionBarGrid = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 10, 0, 0) };
        var leftActionButtons = new StackPanel { Orientation = Orientation.Horizontal };

        var openBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Open", () => { }, primary: true, "Open selected item with " + cap.DisplayName + " (Enter)");
        openBtn.Margin = new Thickness(0, 0, 8, 0);

        var folderBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Folder", () => { }, primary: false, "Reveal in File Explorer");
        folderBtn.Margin = new Thickness(0, 0, 8, 0);

        var copyPathBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Copy Path", () => { }, primary: false, "Copy full path to clipboard (Ctrl+C)");
        copyPathBtn.Margin = new Thickness(0, 0, 8, 0);

        leftActionButtons.Children.Add(openBtn);
        leftActionButtons.Children.Add(folderBtn);
        leftActionButtons.Children.Add(copyPathBtn);
        DockPanel.SetDock(leftActionButtons, Dock.Left);
        actionBarGrid.Children.Add(leftActionButtons);

        var feedbackText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(feedbackText, Dock.Right);
        actionBarGrid.Children.Add(feedbackText);

        var bottomCard = DeskSpaceFluentBackdropV380.CreateCard(actionBarGrid, p);
        bottomCard.Padding = new Thickness(12, 10, 12, 10);
        bottomCard.Margin = new Thickness(0, 10, 0, 0);
        DockPanel.SetDock(bottomCard, Dock.Bottom);
        root.Children.Add(bottomCard);

        // --- Scrollable Keyboard-Navigable List ---
        var itemsList = new ListBox
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            ItemContainerStyle = CreateListItemStyle(p)
        };
        DeskSpaceDesignV283.Selection(itemsList, p);
        ScrollViewer.SetHorizontalScrollBarVisibility(itemsList, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetIsVirtualizing(itemsList, true);
        VirtualizingPanel.SetVirtualizationMode(itemsList, VirtualizationMode.Recycling);
        AutomationProperties.SetName(itemsList, $"Files for {cap.DisplayName}");
        root.Children.Add(itemsList);

        win.Content = root;
        DeskSpaceFluentBackdropV380.ApplyFadeEntrance(root);

        // --- State & Filtering Logic ---
        string activeChip = "★ Recent First";
        var chipButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        string[] chipLabels = { "All", "★ Recent First", "Recent Only" };

        List<PinnedAppFileItemV380> recentItems = new();
        List<PinnedAppFileItemV380> systemItems = new();
        bool isScanning = false;
        bool isClosed = false;
        int scanGeneration = 0;
        CancellationTokenSource? activeCts = null;

        PinnedAppFileItemV380? GetSelectedItem()
        {
            if (itemsList.SelectedItem is ListBoxItem lbi && lbi.Tag is PinnedAppFileItemV380 tagged)
                return tagged;
            return null;
        }

        void UpdateActionButtons()
        {
            var sel = GetSelectedItem();
            bool hasSelection = sel != null;
            openBtn.IsEnabled = hasSelection;
            folderBtn.IsEnabled = hasSelection;
            copyPathBtn.IsEnabled = hasSelection;
        }

        void UpdateChipStyles()
        {
            foreach (var pair in chipButtons)
            {
                bool active = pair.Key == activeChip;
                pair.Value.Background = DeskSpaceDesignV283.Solid(active ? p.AccentSoft : p.Raised);
                pair.Value.BorderBrush = DeskSpaceDesignV283.Solid(active ? p.Accent : p.Border);
                pair.Value.Foreground = DeskSpaceDesignV283.Solid(active ? p.Accent : p.Text);
                pair.Value.FontWeight = active ? FontWeights.SemiBold : FontWeights.Medium;
            }
        }

        void RefreshListView()
        {
            if (isClosed)
                return;

            string? prevPath = GetSelectedItem()?.FullPath;
            var ranked = MergeAndRank(recentItems, systemItems, 300);

            int totalRecent = ranked.Count(x => x.IsRecent);
            int totalSystem = systemItems.Count;

            IEnumerable<PinnedAppFileItemV380> view = activeChip switch
            {
                "Recent Only" => ranked.Where(x => x.IsRecent),
                "All" => ranked.OrderByDescending(x => x.SortTimeUtc)
                               .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase),
                _ => ranked // "★ Recent First"
            };

            string q = searchBox.Text.Trim();
            if (q.Length > 0)
            {
                view = view.Where(x =>
                    x.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.FullPath.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.KindBadge.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.SourceBadge.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(x.TallyCompanyCode) &&
                     x.TallyCompanyCode.Contains(q, StringComparison.OrdinalIgnoreCase)));
            }

            var filtered = view.ToList();
            itemsList.Items.Clear();

            ListBoxItem? toSelect = null;
            foreach (var item in filtered)
            {
                var rowVisual = BuildItemRowVisual(item, p);
                var lbi = new ListBoxItem
                {
                    Tag = item,
                    Content = rowVisual,
                    ToolTip = item.FullPath
                };
                AutomationProperties.SetName(lbi, $"{item.DisplayName}, {item.KindBadge}, {item.ParentFolder}");
                itemsList.Items.Add(lbi);

                if (prevPath != null && string.Equals(item.FullPath, prevPath, StringComparison.OrdinalIgnoreCase))
                    toSelect = lbi;
            }

            if (toSelect != null)
                itemsList.SelectedItem = toSelect;
            else if (itemsList.Items.Count > 0)
                itemsList.SelectedIndex = 0;

            string scanSuffix = isScanning ? " · scanning…" : string.Empty;
            string filterSuffix = q.Length > 0 ? $" · showing {filtered.Count}" : string.Empty;
            statusText.Text = $"{totalRecent} recent · {totalSystem} system files found{filterSuffix}{scanSuffix}";
            UpdateActionButtons();
        }

        foreach (string label in chipLabels)
        {
            string captured = label;
            var chip = DeskSpaceFluentBackdropV380.CreatePillButton(captured, () =>
            {
                activeChip = captured;
                UpdateChipStyles();
                RefreshListView();
            });
            chip.Padding = new Thickness(11, 4, 11, 4);
            chip.MinHeight = 27;
            chip.FontSize = 12;
            chip.Margin = new Thickness(0, 0, 6, 0);
            chipButtons[captured] = chip;
            chipsWrap.Children.Add(chip);
        }
        UpdateChipStyles();

        void OpenSelectedItem()
        {
            var sel = GetSelectedItem();
            if (sel == null)
                return;

            if (TryOpenItem(cap, sel, win))
            {
                recentItems = DiscoverRecentItems(cap);
                RefreshListView();
                win.Close();
            }
        }

        void RevealSelectedInExplorer()
        {
            var sel = GetSelectedItem();
            if (sel == null)
                return;

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{sel.FullPath}\"")
                {
                    UseShellExecute = true
                });
                feedbackText.Text = $"Revealed in Explorer: {sel.DisplayName}";
            }
            catch (Exception ex)
            {
                ShowError(win, $"Unable to open Explorer:\n{ex.Message}", cap.DisplayName);
            }
        }

        void CopySelectedPath()
        {
            var sel = GetSelectedItem();
            if (sel == null)
                return;

            try
            {
                System.Windows.Clipboard.SetText(sel.FullPath);
                feedbackText.Text = $"Copied path: {sel.DisplayName}";
            }
            catch (Exception ex)
            {
                feedbackText.Text = "Copy failed: " + ex.Message;
            }
        }

        openBtn.Click += (_, _) => OpenSelectedItem();
        folderBtn.Click += (_, _) => RevealSelectedInExplorer();
        copyPathBtn.Click += (_, _) => CopySelectedPath();

        void StartDiscovery()
        {
            activeCts?.Cancel();
            activeCts?.Dispose();
            activeCts = new CancellationTokenSource();
            var ct = activeCts.Token;
            int myGen = ++scanGeneration;

            isScanning = true;
            if (refreshBtn != null)
                refreshBtn.IsEnabled = false;
            feedbackText.Text = string.Empty;

            _ = Task.Run(async () =>
            {
                try
                {
                    var fastRecent = DiscoverRecentItems(cap);
                    if (!ct.IsCancellationRequested && !isClosed)
                    {
                        await win.Dispatcher.InvokeAsync(() =>
                        {
                            if (isClosed || ct.IsCancellationRequested || myGen != scanGeneration)
                                return;
                            recentItems = fastRecent;
                            RefreshListView();
                        });
                    }

                    var discoveredSystem = await DiscoverSystemItemsAsync(cap, snapshot =>
                    {
                        if (isClosed || ct.IsCancellationRequested || win.Dispatcher.HasShutdownStarted)
                            return;
                        win.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (isClosed || ct.IsCancellationRequested || myGen != scanGeneration)
                                return;
                            systemItems = snapshot.ToList();
                            RefreshListView();
                        }), DispatcherPriority.Background);
                    }, ct).ConfigureAwait(false);

                    if (!isClosed && !ct.IsCancellationRequested && !win.Dispatcher.HasShutdownStarted)
                    {
                        await win.Dispatcher.InvokeAsync(() =>
                        {
                            if (isClosed || ct.IsCancellationRequested || myGen != scanGeneration)
                                return;
                            systemItems = discoveredSystem;
                            isScanning = false;
                            if (refreshBtn != null)
                                refreshBtn.IsEnabled = true;
                            RefreshListView();
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    if (!isClosed && !win.Dispatcher.HasShutdownStarted)
                    {
                        _ = win.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (isClosed || myGen != scanGeneration)
                                return;
                            isScanning = false;
                            if (refreshBtn != null)
                                refreshBtn.IsEnabled = true;
                            feedbackText.Text = $"Scan note: {ex.Message}";
                            RefreshListView();
                        }));
                    }
                }
            }, ct);
        }

        triggerRefresh = StartDiscovery;

        searchBox.TextChanged += (_, _) =>
        {
            bool empty = string.IsNullOrEmpty(searchBox.Text);
            searchPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            clearSearchBtn.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            RefreshListView();
        };

        clearSearchBtn.Click += (_, _) =>
        {
            searchBox.Clear();
            searchBox.Focus();
        };

        searchBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && itemsList.Items.Count > 0)
            {
                int next = itemsList.SelectedIndex < 0
                    ? 0
                    : Math.Min(itemsList.Items.Count - 1, itemsList.SelectedIndex + 1);
                itemsList.SelectedIndex = next;
                itemsList.ScrollIntoView(itemsList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && itemsList.Items.Count > 0)
            {
                int prev = itemsList.SelectedIndex <= 0 ? 0 : itemsList.SelectedIndex - 1;
                itemsList.SelectedIndex = prev;
                itemsList.ScrollIntoView(itemsList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenSelectedItem();
            }
            else if (e.Key == Key.Escape)
            {
                if (!string.IsNullOrEmpty(searchBox.Text))
                {
                    searchBox.Clear();
                    e.Handled = true;
                }
                else
                {
                    win.Close();
                    e.Handled = true;
                }
            }
        };

        itemsList.SelectionChanged += (_, _) => UpdateActionButtons();

        itemsList.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenSelectedItem();
            }
            else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                e.Handled = true;
                CopySelectedPath();
            }
        };

        itemsList.MouseDoubleClick += (_, e) =>
        {
            if (IsClickInsideListBoxItem(e.OriginalSource as DependencyObject))
                OpenSelectedItem();
        };

        win.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (searchBox.IsKeyboardFocusWithin && !string.IsNullOrEmpty(searchBox.Text))
                {
                    searchBox.Clear();
                    e.Handled = true;
                }
                else
                {
                    win.Close();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                searchBox.Focus();
                searchBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                StartDiscovery();
                e.Handled = true;
            }
        };

        win.Loaded += (_, _) =>
        {
            searchBox.Focus();
            StartDiscovery();
        };

        win.Closed += (_, _) =>
        {
            isClosed = true;
            scanGeneration++;
            try
            {
                activeCts?.Cancel();
                activeCts?.Dispose();
            }
            catch
            {
            }
            activeCts = null;

            lock (OpenWindowsLock)
            {
                if (OpenWindows.TryGetValue(canonicalPin, out var stored) && ReferenceEquals(stored, win))
                    OpenWindows.Remove(canonicalPin);
            }
        };

        win.Show();
        win.Activate();
    }

    // =========================================================================
    // Internal Discovery & Parsing Helpers
    // =========================================================================

    private static string ResolveShortcutWithShell(object wsh, string lnkPath)
    {
        object? linkObj = null;
        try
        {
            dynamic shell = wsh;
            linkObj = shell.CreateShortcut(lnkPath);
            if (linkObj == null)
                return string.Empty;
            dynamic link = linkObj;
            string? target = link.TargetPath as string;
            if (string.IsNullOrWhiteSpace(target))
                return string.Empty;
            return Environment.ExpandEnvironmentVariables(target.Trim());
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            ReleaseComObject(linkObj);
        }
    }

    private static void ReleaseComObject(object? obj)
    {
        if (obj != null && Marshal.IsComObject(obj))
        {
            try
            {
                Marshal.FinalReleaseComObject(obj);
            }
            catch
            {
            }
        }
    }

    private static string StripIniComment(string value)
    {
        int semi = value.IndexOf(';');
        if (semi >= 0)
            value = value.Substring(0, semi);
        int hash = value.IndexOf('#');
        if (hash >= 0)
            value = value.Substring(0, hash);
        return value;
    }

    private static bool LooksLikeDirectoryPath(string candidate)
    {
        if (candidate.Length < 2)
            return false;
        if (char.IsLetter(candidate[0]) && candidate[1] == ':')
            return true;
        if (candidate.StartsWith(@"\\", StringComparison.Ordinal))
            return true;
        return candidate.Contains('\\') || candidate.Contains('/');
    }

    private static bool LooksLikeCompanyCodeList(string segment)
    {
        string[] tokens = segment.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return false;

        foreach (string raw in tokens)
        {
            string t = raw.Trim().Trim('"');
            if (t.Length == 0 || !t.All(char.IsDigit))
                return false;
        }
        return true;
    }

    private static string NormalizeExtension(string ext)
    {
        string trimmed = ext.Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
            return string.Empty;
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    private static bool IsIdeExecutableName(string exeName)
    {
        return exeName.ToLowerInvariant() switch
        {
            "code.exe" or "devenv.exe" or "studio64.exe" or "rider64.exe" or
            "notepad++.exe" or "sublime_text.exe" => true,
            _ => false
        };
    }

    private static bool DoesProgIdMatchExecutable(
        string progId,
        string exeName,
        Dictionary<string, string> cache)
    {
        if (!cache.TryGetValue(progId, out string? cmdExe))
        {
            cmdExe = string.Empty;
            try
            {
                using var cmdKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
                string? rawCmd = cmdKey?.GetValue(null) as string;
                cmdExe = ExtractExecutableFromCommand(rawCmd);
            }
            catch
            {
            }
            cache[progId] = cmdExe;
        }

        return !string.IsNullOrEmpty(cmdExe) &&
               DeskSpacePinnedRecentV122.SameExecutableV122(cmdExe, exeName);
    }

    private static string ExtractExecutableFromCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        string s = command.Trim();
        if (s[0] == '"')
        {
            int endQuote = s.IndexOf('"', 1);
            if (endQuote > 1)
                return s.Substring(1, endQuote - 1).Trim();
        }

        int exeIdx = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
        {
            int afterExe = exeIdx + 4;
            if (afterExe == s.Length || char.IsWhiteSpace(s[afterExe]))
                return s.Substring(0, afterExe).Trim('"');
        }

        int space = s.IndexOf(' ');
        return (space > 0 ? s.Substring(0, space) : s).Trim('"');
    }

    private static string QueryAssociatedExecutableByExtension(string extension)
    {
        try
        {
            var sb = new StringBuilder(512);
            uint len = (uint)sb.Capacity;
            int hr = AssocQueryStringW(0, ASSOCSTR_EXECUTABLE, extension, "open", sb, ref len);
            if (hr != 0)
            {
                sb.Clear();
                len = (uint)sb.Capacity;
                hr = AssocQueryStringW(0, ASSOCSTR_EXECUTABLE, extension, null, sb, ref len);
            }

            if (hr != 0)
                return string.Empty;

            return sb.ToString().Trim().Trim('"');
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveAppDisplayName(string pinnedPath, string exePath, string? profileDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(profileDisplayName))
            return profileDisplayName;

        if (!string.IsNullOrWhiteSpace(pinnedPath) &&
            System.IO.Path.GetExtension(pinnedPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            string lnkTitle = System.IO.Path.GetFileNameWithoutExtension(pinnedPath);
            if (!string.IsNullOrWhiteSpace(lnkTitle))
                return lnkTitle;
        }

        if (SafeFileExists(exePath))
        {
            try
            {
                var ver = FileVersionInfo.GetVersionInfo(exePath);
                string? candidate = !string.IsNullOrWhiteSpace(ver.FileDescription)
                    ? ver.FileDescription.Trim()
                    : ver.ProductName?.Trim();
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 48)
                    return candidate;
            }
            catch
            {
            }
        }

        string fallback = System.IO.Path.GetFileNameWithoutExtension(exePath);
        return string.IsNullOrWhiteSpace(fallback) ? "Pinned Application" : fallback;
    }

    private static string NormalizeFullPathSafe(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return string.Empty;

        try
        {
            string cleaned = rawPath.Trim().Trim('"');
            return System.IO.Path.GetFullPath(cleaned);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeExeKey(string exeName)
    {
        string file = System.IO.Path.GetFileName(exeName.Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(file))
            return string.Empty;
        if (!file.Contains('.', StringComparison.Ordinal))
            file += ".exe";
        return file.ToLowerInvariant();
    }

    private static Dictionary<string, List<LaunchHistoryEntryDto>> ReadHistoryMapLocked(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return new Dictionary<string, List<LaunchHistoryEntryDto>>(StringComparer.OrdinalIgnoreCase);

            var raw = JsonSerializer.Deserialize<Dictionary<string, List<LaunchHistoryEntryDto>>>(
                File.ReadAllText(filePath),
                JsonOptions);

            var normalized = new Dictionary<string, List<LaunchHistoryEntryDto>>(StringComparer.OrdinalIgnoreCase);
            if (raw != null)
            {
                foreach (var kv in raw)
                {
                    string key = NormalizeExeKey(kv.Key);
                    if (string.IsNullOrEmpty(key) || kv.Value == null)
                        continue;
                    normalized[key] = kv.Value;
                }
            }
            return normalized;
        }
        catch
        {
            return new Dictionary<string, List<LaunchHistoryEntryDto>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void WriteHistoryMapLocked(string filePath, Dictionary<string, List<LaunchHistoryEntryDto>> map)
    {
        string? dir = System.IO.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string tmp = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string json = JsonSerializer.Serialize(map, JsonOptions);
            File.WriteAllText(tmp, json);
            File.Move(tmp, filePath, overwrite: true);
        }
        catch
        {
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

    private static string GetEffectiveExtension(string path)
    {
        if (path.EndsWith(".tax.wb", StringComparison.OrdinalIgnoreCase))
            return ".tax.wb";
        if (path.EndsWith(".wgst.wb", StringComparison.OrdinalIgnoreCase))
            return ".wgst.wb";
        return System.IO.Path.GetExtension(path).ToLowerInvariant();
    }

    private static bool MatchesCapabilityExtension(string filePath, HashSet<string> extensions)
    {
        if (extensions.Count == 0)
            return false;

        string effective = GetEffectiveExtension(filePath);
        if (effective.Length > 0 && extensions.Contains(effective))
            return true;

        string singleExt = System.IO.Path.GetExtension(filePath);
        if (singleExt.Length > 0 && extensions.Contains(singleExt))
        {
            // Prevent raw ".wb" from matching if capability only lists ".tax.wb"/".wgst.wb"
            return true;
        }

        return false;
    }

    private static bool IsAllowedForCapability(AppFileCapabilityV380 cap, string filePath, bool checkExtensionMatch)
    {
        string singleExt = System.IO.Path.GetExtension(filePath);
        if (singleExt.Equals(".1800", StringComparison.OrdinalIgnoreCase) ||
            singleExt.Equals(".900", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool ideOrNotepad = cap.Kind == PinnedAppKindV380.IdeWorkspace ||
                            string.Equals(cap.ExeName, "notepad.exe", StringComparison.OrdinalIgnoreCase);

        if (!IsSafeTargetExtension(singleExt, ideOrNotepad))
            return false;

        if (!checkExtensionMatch)
            return true;

        return MatchesCapabilityExtension(filePath, cap.Extensions);
    }

    private static bool MatchesCapabilityOrExecutable(AppFileCapabilityV380 cap, string filePath)
    {
        if (IsAllowedForCapability(cap, filePath, checkExtensionMatch: true))
            return true;

        string ext = System.IO.Path.GetExtension(filePath);
        bool ideMode = cap.Kind == PinnedAppKindV380.IdeWorkspace;
        if (!IsSafeTargetExtension(ext, ideMode))
            return false;

        if (string.IsNullOrWhiteSpace(cap.ExecutablePath))
            return false;

        string assocExe = QueryAssociatedExecutableByExtension(ext);
        return !string.IsNullOrWhiteSpace(assocExe) &&
               DeskSpacePinnedRecentV122.SameExecutableV122(assocExe, cap.ExecutablePath);
    }

    private static PinnedAppFileItemV380 CreateFileItem(
        string fullPath,
        DateTime sortTimeUtc,
        bool isRecent,
        string sourceBadge)
    {
        string full = NormalizeFullPathSafe(fullPath);
        string fileName = System.IO.Path.GetFileName(full);
        string parent = System.IO.Path.GetDirectoryName(full) ?? string.Empty;
        string ext = GetEffectiveExtension(full);

        string kindBadge = ext switch
        {
            ".tax.wb" => "Winman Tax Backup",
            ".wgst.wb" => "Winman GST Backup",
            ".wt" => "Winman TDS Backup",
            ".tbk" => "Tally Backup (.tbk)",
            "" => "File",
            _ => ext.ToUpperInvariant()
        };

        return new PinnedAppFileItemV380(
            DisplayName: fileName,
            FullPath: full,
            ParentFolder: parent,
            KindBadge: kindBadge,
            SortTimeUtc: sortTimeUtc,
            IsRecent: isRecent,
            SourceBadge: sourceBadge,
            IsDirectory: false);
    }

    private static IEnumerable<(string Path, DateTime LastUsedUtc)> DiscoverOfficeMruPaths(string officeAppSubKey)
    {
        var results = new List<(string, DateTime)>();
        foreach (string version in new[] { "16.0", "15.0" })
        {
            string basePath = $@"Software\Microsoft\Office\{version}\{officeAppSubKey}\User MRU";
            try
            {
                using var userMru = Registry.CurrentUser.OpenSubKey(basePath);
                if (userMru == null)
                    continue;

                foreach (string profileKeyName in userMru.GetSubKeyNames())
                {
                    using var fileMru = userMru.OpenSubKey($@"{profileKeyName}\File MRU");
                    if (fileMru == null)
                        continue;

                    foreach (string valName in fileMru.GetValueNames())
                    {
                        if (!valName.StartsWith("Item ", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string? raw = fileMru.GetValue(valName) as string;
                        if (string.IsNullOrWhiteSpace(raw))
                            continue;

                        int star = raw.IndexOf('*');
                        if (star < 0 || star + 1 >= raw.Length)
                            continue;

                        string candidatePath = raw.Substring(star + 1).Trim();
                        if (!System.IO.Path.IsPathFullyQualified(candidatePath))
                            continue;

                        DateTime tsUtc = ParseOfficeMruTimestampUtc(raw.Substring(0, star));
                        results.Add((candidatePath, tsUtc));
                    }
                }
            }
            catch
            {
            }
        }
        return results;
    }

    private static DateTime ParseOfficeMruTimestampUtc(string metadataPrefix)
    {
        int tStart = metadataPrefix.IndexOf("[T", StringComparison.OrdinalIgnoreCase);
        if (tStart >= 0)
        {
            int tEnd = metadataPrefix.IndexOf(']', tStart + 2);
            if (tEnd > tStart + 2)
            {
                string hex = metadataPrefix.Substring(tStart + 2, tEnd - (tStart + 2));
                if (long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long fileTime) &&
                    fileTime > 0)
                {
                    try
                    {
                        return DateTime.FromFileTimeUtc(fileTime);
                    }
                    catch
                    {
                    }
                }
            }
        }
        return DateTime.MinValue;
    }

    private static List<PinnedAppFileItemV380> QueryWindowsSearchSystemIndex(
        AppFileCapabilityV380 cap,
        CancellationToken ct)
    {
        var hits = new List<PinnedAppFileItemV380>();
        var adodbType = Type.GetTypeFromProgID("ADODB.Connection");
        if (adodbType == null)
            return hits;

        object? conn = null;
        try
        {
            conn = Activator.CreateInstance(adodbType);
            if (conn == null)
                return hits;

            dynamic db = conn;
            db.ConnectionTimeout = 2;
            db.CommandTimeout = 4;
            db.Open("Provider=Search.CollatorDSO.1;Extended Properties='Application=Windows';");

            // Query 1: Tally Company.1800 / Company.900 folders
            if (cap.IncludesTallyCompanies)
            {
                string tallySql =
                    $"SELECT TOP {MaxSystemSearchHits} System.ItemPathDisplay " +
                    "FROM SYSTEMINDEX " +
                    "WHERE scope='file:' AND (System.FileName = 'Company.1800' OR System.FileName = 'Company.900') " +
                    "ORDER BY System.DateModified DESC";

                foreach (string filePath in ExecuteAdodbPathQuery(db, tallySql, ct))
                {
                    string? folder = System.IO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrWhiteSpace(folder) &&
                        TryParseTallyCompanyFolder(folder, out var companyItem, isRecent: false, sourceBadge: "Windows Search") &&
                        companyItem != null)
                    {
                        hits.Add(companyItem);
                        if (hits.Count >= MaxSystemSearchHits)
                            return hits;
                    }
                }
            }

            // Query 2: Primary high-signal extensions first, then noisier extensions if budget remains
            var singleDotPrimary = new List<string>();
            var singleDotNoisy = new List<string>();
            var compoundFilePatterns = new List<string>();

            foreach (string ext in cap.Extensions)
            {
                string norm = NormalizeExtension(ext);
                if (norm == ".tax.wb" || norm == ".wgst.wb")
                {
                    compoundFilePatterns.Add($"System.FileName LIKE '%{EscapeSqlLikeLiteral(norm)}'");
                    continue;
                }

                if (!IsSafeSqlExtensionToken(norm))
                    continue;

                if (NoisyGlobalExtensions.Contains(norm))
                    singleDotNoisy.Add($"'{norm}'");
                else
                    singleDotPrimary.Add($"'{norm}'");
            }

            void RunExtensionBatch(List<string> extLiterals, List<string> extraClauses)
            {
                int remaining = MaxSystemSearchHits - hits.Count;
                if (remaining <= 0)
                    return;

                var clauses = new List<string>();
                if (extLiterals.Count > 0)
                    clauses.Add("System.FileExtension IN (" + string.Join(", ", extLiterals) + ")");
                clauses.AddRange(extraClauses);
                if (clauses.Count == 0)
                    return;

                string where = string.Join(" OR ", clauses);
                string sql =
                    $"SELECT TOP {remaining} System.ItemPathDisplay " +
                    $"FROM SYSTEMINDEX WHERE scope='file:' AND ({where}) " +
                    "ORDER BY System.DateModified DESC";

                foreach (string path in ExecuteAdodbPathQuery(db, sql, ct))
                {
                    if (IsNoiseSystemPath(path) || !SafeFileExists(path) || IsReparsePoint(path))
                        continue;
                    if (!IsAllowedForCapability(cap, path, checkExtensionMatch: true))
                        continue;

                    hits.Add(CreateFileItem(path, SafeGetFileWriteTimeUtc(path), isRecent: false, sourceBadge: "Windows Search"));
                    if (hits.Count >= MaxSystemSearchHits)
                        break;
                }
            }

            RunExtensionBatch(singleDotPrimary, compoundFilePatterns);

            if (hits.Count < MaxSystemSearchHits && singleDotNoisy.Count > 0)
                RunExtensionBatch(singleDotNoisy, new List<string>());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
        finally
        {
            if (conn != null)
            {
                try { ((dynamic)conn).Close(); } catch { }
                ReleaseComObject(conn);
            }
        }

        return hits;
    }

    private static List<string> ExecuteAdodbPathQuery(dynamic db, string sql, CancellationToken ct)
    {
        var paths = new List<string>();
        object? rows = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            rows = db.Execute(sql);
            dynamic reader = rows;
            while (!(bool)reader.EOF)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    string? path = Convert.ToString(reader.Fields.Item("System.ItemPathDisplay").Value);
                    if (!string.IsNullOrWhiteSpace(path) && System.IO.Path.IsPathFullyQualified(path))
                        paths.Add(path);
                }
                catch
                {
                }
                reader.MoveNext();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
        finally
        {
            if (rows != null)
            {
                try { ((dynamic)rows).Close(); } catch { }
                ReleaseComObject(rows);
            }
        }
        return paths;
    }

    private static List<PinnedAppFileItemV380> QueryDeskSpaceDiskSearchReflection(
        AppFileCapabilityV380 cap,
        CancellationToken ct)
    {
        var results = new List<PinnedAppFileItemV380>();
        try
        {
            var diskSearchType = typeof(DeskSpacePinnedAppFilesV380).Assembly
                .GetType("DeskSpace.DeskSpaceDiskSearchV330", throwOnError: false);
            if (diskSearchType == null)
                return results;

            var candidateNames = new[] { "SearchByExtensions", "FindByExtensions", "QueryExtensions", "Search", "Query" };
            var methods = diskSearchType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            foreach (string methodName in candidateNames)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var method in methods.Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase)))
                {
                    var parameters = method.GetParameters();
                    object? invoked = null;
                    try
                    {
                        if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(HashSet<string>)))
                            invoked = method.Invoke(null, new object?[] { cap.Extensions });
                        else if (parameters.Length == 2 &&
                                 parameters[0].ParameterType.IsAssignableFrom(typeof(HashSet<string>)) &&
                                 parameters[1].ParameterType == typeof(CancellationToken))
                            invoked = method.Invoke(null, new object?[] { cap.Extensions, ct });
                        else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IEnumerable<string>))
                            invoked = method.Invoke(null, new object?[] { cap.Extensions.ToArray() });
                    }
                    catch
                    {
                        continue;
                    }

                    if (invoked is System.Collections.IEnumerable seq)
                    {
                        ExtractItemsFromReflectionEnumerable(seq, cap, "Disk Search", results, ct);
                        if (results.Count > 0)
                            return results;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }

        return results;
    }

    private static void ExtractItemsFromReflectionEnumerable(
        System.Collections.IEnumerable seq,
        AppFileCapabilityV380 cap,
        string sourceBadge,
        List<PinnedAppFileItemV380> results,
        CancellationToken ct)
    {
        foreach (object? entry in seq)
        {
            ct.ThrowIfCancellationRequested();
            if (entry == null)
                continue;

            string? path = entry as string;
            if (path == null)
            {
                var t = entry.GetType();
                path = (t.GetProperty("FullPath")?.GetValue(entry) ??
                        t.GetProperty("Path")?.GetValue(entry) ??
                        t.GetProperty("OpenTarget")?.GetValue(entry)) as string;
            }

            if (string.IsNullOrWhiteSpace(path) || IsNoiseSystemPath(path))
                continue;

            if (cap.IncludesTallyCompanies)
            {
                string fileName = System.IO.Path.GetFileName(path);
                if (fileName.Equals("Company.1800", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("Company.900", StringComparison.OrdinalIgnoreCase))
                {
                    string? dir = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(dir) &&
                        TryParseTallyCompanyFolder(dir, out var tallyItem, isRecent: false, sourceBadge: sourceBadge) &&
                        tallyItem != null)
                    {
                        results.Add(tallyItem);
                    }
                    continue;
                }
            }

            if (SafeFileExists(path) && !IsReparsePoint(path) && IsAllowedForCapability(cap, path, checkExtensionMatch: true))
                results.Add(CreateFileItem(path, SafeGetFileWriteTimeUtc(path), isRecent: false, sourceBadge: sourceBadge));

            if (results.Count >= MaxSystemSearchHits)
                break;
        }
    }

    private static List<PinnedAppFileItemV380> QueryWarmSearchIndexV13(
        AppFileCapabilityV380 cap,
        CancellationToken ct)
    {
        var results = new List<PinnedAppFileItemV380>();
        try
        {
            var indexType = typeof(DeskSpaceSearchIndexV13);
            var safeCounts = indexType.GetMethod("SafeCountsV260", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (safeCounts != null)
            {
                object? stats = safeCounts.Invoke(null, null);
                long names = stats?.GetType().GetProperty("Names")?.GetValue(stats) is long n ? n : 0;
                int finished = stats?.GetType().GetProperty("Finished")?.GetValue(stats) is int f ? f : 0;
                if (names <= 0 && finished <= 0)
                    return results;
            }
            else if (!DeskSpaceSearchIndexV13.IsIndexing && string.IsNullOrEmpty(DeskSpaceSearchIndexV13.Coverage))
            {
                return results;
            }

            var gateField = indexType.GetField("Gate", BindingFlags.NonPublic | BindingFlags.Static);
            var entriesField = indexType.GetField("Entries", BindingFlags.NonPublic | BindingFlags.Static);
            object? gate = gateField?.GetValue(null);
            object? entriesObj = entriesField?.GetValue(null);

            if (gate != null && entriesObj is System.Collections.IEnumerable entriesSeq)
            {
                var snapshot = new List<(string Name, string Path, bool Folder)>();
                lock (gate)
                {
                    foreach (object? item in entriesSeq)
                    {
                        if (item == null) continue;
                        var it = item.GetType();
                        string? name = it.GetProperty("Name")?.GetValue(item) as string;
                        string? path = it.GetProperty("Path")?.GetValue(item) as string;
                        bool folder = it.GetProperty("Folder")?.GetValue(item) is bool b && b;
                        if (!string.IsNullOrEmpty(path))
                            snapshot.Add((name ?? string.Empty, path, folder));
                    }
                }

                int scanned = 0;
                foreach (var entry in snapshot)
                {
                    if ((++scanned & 2047) == 0)
                        ct.ThrowIfCancellationRequested();

                    if (IsNoiseSystemPath(entry.Path))
                        continue;

                    if (entry.Folder)
                    {
                        if (cap.IncludesTallyCompanies &&
                            TryParseTallyCompanyFolder(entry.Path, out var tallyItem, isRecent: false, sourceBadge: "DeskSpace Index") &&
                            tallyItem != null)
                        {
                            results.Add(tallyItem);
                        }
                        continue;
                    }

                    if (cap.IncludesTallyCompanies &&
                        (entry.Name.Equals("Company.1800", StringComparison.OrdinalIgnoreCase) ||
                         entry.Name.Equals("Company.900", StringComparison.OrdinalIgnoreCase)))
                    {
                        string? parent = System.IO.Path.GetDirectoryName(entry.Path);
                        if (!string.IsNullOrWhiteSpace(parent) &&
                            TryParseTallyCompanyFolder(parent, out var tallyItem, isRecent: false, sourceBadge: "DeskSpace Index") &&
                            tallyItem != null)
                        {
                            results.Add(tallyItem);
                        }
                        continue;
                    }

                    if (MatchesCapabilityExtension(entry.Path, cap.Extensions) &&
                        SafeFileExists(entry.Path) &&
                        !IsReparsePoint(entry.Path) &&
                        IsAllowedForCapability(cap, entry.Path, checkExtensionMatch: true))
                    {
                        results.Add(CreateFileItem(entry.Path, SafeGetFileWriteTimeUtc(entry.Path), isRecent: false, sourceBadge: "DeskSpace Index"));
                        if (results.Count >= MaxSystemSearchHits)
                            break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }

        return results;
    }

    private static void RunBoundedDriveEnumeration(
        AppFileCapabilityV380 cap,
        Action<PinnedAppFileItemV380> onItem,
        Action onBatchProgress,
        CancellationToken ct)
    {
        var visitedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int totalVisitedCount = 0;

        void ScanQueue(IEnumerable<string> initialRoots, int rootBudgetLimit, bool isExplicitDataRoot)
        {
            var queue = new Queue<(string Dir, int Depth)>();
            foreach (string rawRoot in initialRoots)
            {
                string norm = NormalizeFullPathSafe(rawRoot);
                if (string.IsNullOrEmpty(norm) || !SafeDirectoryExists(norm))
                    continue;

                string key = TrimTrailingSeparatorsUnlessDriveRoot(norm);
                if (visitedDirs.Add(key))
                    queue.Enqueue((norm, 0));
            }

            int phaseVisited = 0;
            while (queue.Count > 0 && phaseVisited < rootBudgetLimit && totalVisitedCount < MaxVisitedDirs)
            {
                ct.ThrowIfCancellationRequested();
                var (dir, depth) = queue.Dequeue();
                phaseVisited++;
                totalVisitedCount++;

                if ((totalVisitedCount & 63) == 0)
                    onBatchProgress();

                // Check if this directory itself is a Tally company or IDE project folder
                if (cap.IncludesTallyCompanies &&
                    TryParseTallyCompanyFolder(dir, out var tallySelf, isRecent: false, sourceBadge: "Drive Scan") &&
                    tallySelf != null)
                {
                    onItem(tallySelf);
                    continue;
                }

                if (cap.IncludesProjectFolders && depth > 0 &&
                    IsLikelyProjectFolder(dir, out string projBadge))
                {
                    string trimmed = TrimTrailingSeparatorsUnlessDriveRoot(dir);
                    onItem(new PinnedAppFileItemV380(
                        DisplayName: System.IO.Path.GetFileName(trimmed),
                        FullPath: trimmed,
                        ParentFolder: System.IO.Path.GetDirectoryName(trimmed) ?? string.Empty,
                        KindBadge: projBadge,
                        SortTimeUtc: SafeGetDirectoryWriteTimeUtc(trimmed),
                        IsRecent: false,
                        SourceBadge: "Project Folder",
                        IsDirectory: true));
                }

                string[] entries;
                try
                {
                    entries = Directory.GetFileSystemEntries(dir);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
                {
                    continue;
                }

                foreach (string entry in entries)
                {
                    if ((totalVisitedCount & 63) == 0)
                        ct.ThrowIfCancellationRequested();

                    FileAttributes attrs;
                    try
                    {
                        attrs = File.GetAttributes(entry);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
                    {
                        continue;
                    }

                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                        continue;

                    if ((attrs & FileAttributes.Directory) != 0)
                    {
                        if (depth >= MaxDriveDepth)
                            continue;

                        string dirName = System.IO.Path.GetFileName(entry);
                        if (string.IsNullOrEmpty(dirName) || SkipDirectoryNames.Contains(dirName))
                            continue;

                        if (!isExplicitDataRoot && (attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                            continue;

                        string childKey = TrimTrailingSeparatorsUnlessDriveRoot(entry);
                        if (visitedDirs.Add(childKey))
                            queue.Enqueue((entry, depth + 1));
                        continue;
                    }

                    if ((attrs & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.Temporary)) != 0)
                        continue;

                    if (IsAllowedForCapability(cap, entry, checkExtensionMatch: true))
                        onItem(CreateFileItem(entry, SafeGetFileWriteTimeUtc(entry), isRecent: false, sourceBadge: "Drive Scan"));
                }
            }
        }

        // Phase 1: ExtraDataRoots (e.g., Tally.ini Data folders, standard Tally/Winman folders)
        if (cap.ExtraDataRoots.Count > 0)
            ScanQueue(cap.ExtraDataRoots, rootBudgetLimit: 1500, isExplicitDataRoot: true);

        // Phase 2: User profile locations (Desktop, Documents, Downloads, OneDrive)
        var userRoots = new List<string>();
        void AddUserRoot(string? p)
        {
            if (!string.IsNullOrWhiteSpace(p) && SafeDirectoryExists(p))
                userRoots.Add(p);
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        AddUserRoot(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddUserRoot(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            AddUserRoot(System.IO.Path.Combine(userProfile, "Downloads"));
            AddUserRoot(System.IO.Path.Combine(userProfile, "OneDrive"));
        }
        AddUserRoot(Environment.GetEnvironmentVariable("OneDrive"));
        AddUserRoot(Environment.GetEnvironmentVariable("OneDriveCommercial"));
        AddUserRoot(Environment.GetEnvironmentVariable("OneDriveConsumer"));

        if (userRoots.Count > 0)
            ScanQueue(userRoots, rootBudgetLimit: 2000, isExplicitDataRoot: false);

        // Phase 3: Top-level directories across all ready DriveType.Fixed volumes (interleaved BFS)
        var fixedRoots = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                        fixedRoots.Add(drive.RootDirectory.FullName);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        if (fixedRoots.Count > 0 && totalVisitedCount < MaxVisitedDirs)
            ScanQueue(fixedRoots, rootBudgetLimit: MaxVisitedDirs - totalVisitedCount, isExplicitDataRoot: false);
    }

    private static bool IsLikelyProjectFolder(string dirPath, out string kindBadge)
    {
        kindBadge = "Project Folder";
        try
        {
            if (Directory.Exists(System.IO.Path.Combine(dirPath, ".git")))
            {
                kindBadge = "Git Workspace";
                return true;
            }
            if (Directory.EnumerateFiles(dirPath, "*.sln", SearchOption.TopDirectoryOnly).Any() ||
                Directory.EnumerateFiles(dirPath, "*.slnx", SearchOption.TopDirectoryOnly).Any())
            {
                kindBadge = ".NET Solution";
                return true;
            }
            if (Directory.EnumerateFiles(dirPath, "*.csproj", SearchOption.TopDirectoryOnly).Any())
            {
                kindBadge = ".NET Project";
                return true;
            }
            if (File.Exists(System.IO.Path.Combine(dirPath, "package.json")))
            {
                kindBadge = "Node Project";
                return true;
            }
            if (File.Exists(System.IO.Path.Combine(dirPath, "build.gradle")) ||
                File.Exists(System.IO.Path.Combine(dirPath, "build.gradle.kts")) ||
                File.Exists(System.IO.Path.Combine(dirPath, "pom.xml")))
            {
                kindBadge = "Java/Kotlin Project";
                return true;
            }
            if (File.Exists(System.IO.Path.Combine(dirPath, "Cargo.toml")) ||
                File.Exists(System.IO.Path.Combine(dirPath, "go.mod")) ||
                File.Exists(System.IO.Path.Combine(dirPath, "pyproject.toml")))
            {
                kindBadge = "Code Workspace";
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool IsNoiseSystemPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        string[] segments = path.Split(
            new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        // Check parent folder segments (excluding final filename)
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (SkipDirectoryNames.Contains(segments[i]))
                return true;
        }
        return false;
    }

    private static bool IsSafeSqlExtensionToken(string ext)
    {
        if (ext.Length < 2 || ext.Length > 15 || !ext.StartsWith('.'))
            return false;
        for (int i = 1; i < ext.Length; i++)
        {
            char c = ext[i];
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                return false;
        }
        return true;
    }

    private static string EscapeSqlLikeLiteral(string value) =>
        value.Replace("'", "''").Replace("%", "[%]").Replace("_", "[_]");

    private static string TrimTrailingSeparatorsUnlessDriveRoot(string path)
    {
        string trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        if (trimmed.Length == 2 && trimmed[1] == ':')
            return trimmed + System.IO.Path.DirectorySeparatorChar;
        return trimmed;
    }

    private static IReadOnlyList<string> ReadPrimaryTallyDataDirs(string executablePath)
    {
        try
        {
            string? exeDir = System.IO.Path.GetDirectoryName(executablePath);
            if (string.IsNullOrWhiteSpace(exeDir))
                return Array.Empty<string>();

            string iniPath = System.IO.Path.Combine(exeDir, "Tally.ini");
            if (!SafeFileExists(iniPath))
                return Array.Empty<string>();

            var (dirs, _) = ParseTallyIni(File.ReadAllText(iniPath));
            var resolved = new List<string>(dirs.Count);
            foreach (string dir in dirs)
            {
                resolved.Add(System.IO.Path.IsPathRooted(dir)
                    ? dir
                    : System.IO.Path.Combine(exeDir, dir));
            }
            return resolved;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool MatchesAnyNormalizedDirectory(string candidateDir, IReadOnlyList<string> dirs)
    {
        string normCandidate = TrimTrailingSeparatorsUnlessDriveRoot(NormalizeFullPathSafe(candidateDir));
        if (string.IsNullOrEmpty(normCandidate))
            return false;

        foreach (string dir in dirs)
        {
            string normDir = TrimTrailingSeparatorsUnlessDriveRoot(NormalizeFullPathSafe(dir));
            if (normDir.Length > 0 && string.Equals(normCandidate, normDir, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool TryShellFallbackOpen(
        AppFileCapabilityV380 cap,
        PinnedAppFileItemV380 item,
        string? historyStoragePath)
    {
        if (item.IsDirectory ||
            cap.Kind == PinnedAppKindV380.TallyAccounting ||
            cap.Kind == PinnedAppKindV380.WinmanAccounting)
        {
            return false;
        }

        string ext = System.IO.Path.GetExtension(item.FullPath);
        if (!IsSafeTargetExtension(ext, ideMode: false) ||
            BackupOnlyExtensions.Contains(GetEffectiveExtension(item.FullPath)) ||
            !SafeFileExists(item.FullPath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true });
            RecordLaunch(cap.ExeName, item.FullPath, historyStoragePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLaunchAppOnly(AppFileCapabilityV380 cap, Window? owner)
    {
        try
        {
            if (SafeFileExists(cap.ExecutablePath) &&
                System.IO.Path.GetExtension(cap.ExecutablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo(cap.ExecutablePath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(cap.ExecutablePath) ?? string.Empty
                });
                return true;
            }

            if (SafeFileExists(cap.PinnedPath))
            {
                Process.Start(new ProcessStartInfo(cap.PinnedPath) { UseShellExecute = true });
                return true;
            }

            ShowError(owner, $"Application executable could not be found:\n{cap.ExecutablePath}", cap.DisplayName);
            return false;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException or UnauthorizedAccessException)
        {
            ShowError(owner, $"Unable to launch {cap.DisplayName}:\n{ex.Message}", cap.DisplayName);
            return false;
        }
    }

    private static void ShowError(Window? owner, string message, string title)
    {
        try
        {
            if (owner != null && owner.IsLoaded && owner.IsVisible)
                System.Windows.MessageBox.Show(owner, message, $"DeskSpace · {title}", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                System.Windows.MessageBox.Show(message, $"DeskSpace · {title}", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch
        {
        }
    }

    private static bool SafeFileExists(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static bool SafeDirectoryExists(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static DateTime SafeGetFileWriteTimeUtc(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static DateTime SafeGetDirectoryWriteTimeUtc(string path)
    {
        try
        {
            return Directory.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    // =========================================================================
    // Modern Fluent UI Helpers & Multi-Monitor Screen Placement
    // =========================================================================

    private static void PositionOnOwnerScreen(Window win, Window? owner)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        bool placed = false;

        void PlaceWindow(bool clampSize)
        {
            IntPtr winHwnd = new WindowInteropHelper(win).Handle;
            if (winHwnd == IntPtr.Zero)
                return;

            IntPtr ownerHwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            Forms.Screen? targetScreen = ownerHwnd != IntPtr.Zero
                ? Forms.Screen.FromHandle(ownerHwnd)
                : Forms.Screen.FromPoint(Forms.Cursor.Position);
            targetScreen ??= Forms.Screen.PrimaryScreen;
            if (targetScreen == null)
                return;

            Drawing.Rectangle work = targetScreen.WorkingArea;
            if (work.Width <= 0 || work.Height <= 0)
                return;

            if (!placed)
            {
                // Move near target screen center first so per-monitor DPI updates accurately.
                int roughX = work.Left + Math.Max(0, (work.Width - 720) / 2);
                int roughY = work.Top + Math.Max(0, (work.Height - 580) / 2);
                SetWindowPos(winHwnd, IntPtr.Zero, roughX, roughY, 0, 0, 0x0001 | 0x0004 | 0x0010);
                placed = true;
            }

            if (clampSize)
            {
                var source = HwndSource.FromHwnd(winHwnd);
                var fromDevice = source?.CompositionTarget?.TransformFromDevice;
                double scaleX = fromDevice.HasValue && fromDevice.Value.M11 > 0 ? fromDevice.Value.M11 : 1.0;
                double scaleY = fromDevice.HasValue && fromDevice.Value.M22 > 0 ? fromDevice.Value.M22 : 1.0;

                double maxW = Math.Max(320, (work.Width - 20) * scaleX);
                double maxH = Math.Max(280, (work.Height - 20) * scaleY);

                if (win.Width > maxW)
                {
                    win.MinWidth = Math.Min(win.MinWidth, maxW);
                    win.Width = maxW;
                }
                if (win.Height > maxH)
                {
                    win.MinHeight = Math.Min(win.MinHeight, maxH);
                    win.Height = maxH;
                }
            }

            if (!GetWindowRect(winHwnd, out NativeRect popupRect) ||
                popupRect.Width <= 0 || popupRect.Height <= 0)
            {
                return;
            }

            int desiredX = work.Left + (work.Width - popupRect.Width) / 2;
            int desiredY = work.Top + (work.Height - popupRect.Height) / 2;

            if (ownerHwnd != IntPtr.Zero && owner != null && owner.IsVisible &&
                owner.WindowState != WindowState.Minimized &&
                GetWindowRect(ownerHwnd, out NativeRect ownerRect) &&
                ownerRect.Width > 0 && ownerRect.Height > 0)
            {
                desiredX = ownerRect.Left + (ownerRect.Width - popupRect.Width) / 2;
                desiredY = ownerRect.Top + (ownerRect.Height - popupRect.Height) / 2;
            }

            int maxX = Math.Max(work.Left, work.Right - popupRect.Width);
            int maxY = Math.Max(work.Top, work.Bottom - popupRect.Height);
            int clampedX = Math.Clamp(desiredX, work.Left, maxX);
            int clampedY = Math.Clamp(desiredY, work.Top, maxY);

            if (popupRect.Left != clampedX || popupRect.Top != clampedY)
                SetWindowPos(winHwnd, IntPtr.Zero, clampedX, clampedY, 0, 0, 0x0001 | 0x0004 | 0x0010);
        }

        win.SourceInitialized += (_, _) => PlaceWindow(clampSize: true);
        win.ContentRendered += (_, _) => PlaceWindow(clampSize: false);
    }

    private static string FormatCapabilitySummary(AppFileCapabilityV380 cap)
    {
        var parts = new List<string>();
        if (cap.IncludesTallyCompanies)
            parts.Add("Companies (.1800/.900)");
        if (cap.IncludesProjectFolders)
            parts.Add("Project Folders");

        var exts = cap.Extensions
            .OrderBy(e => e.Length)
            .ThenBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (exts.Count > 0)
        {
            const int previewMax = 4;
            string sample = string.Join(" · ", exts.Take(previewMax));
            if (exts.Count > previewMax)
                sample += $" · +{exts.Count - previewMax} more";
            parts.Add(sample);
        }

        if (parts.Count == 0)
            return $"{cap.ExeName} · Recent launches";

        return string.Join("  |  ", parts);
    }

    private static string BuildCapabilityTooltip(AppFileCapabilityV380 cap)
    {
        var sb = new StringBuilder();
        sb.AppendLine(cap.ExecutablePath);
        if (cap.Extensions.Count > 0)
        {
            sb.Append("Supported extensions: ");
            sb.AppendLine(string.Join(", ", cap.Extensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase)));
        }
        if (cap.ExtraDataRoots.Count > 0)
        {
            sb.Append("Data roots: ");
            sb.AppendLine(string.Join("; ", cap.ExtraDataRoots));
        }
        return sb.ToString().TrimEnd();
    }

    private static Style CreateListItemStyle(DeskSpaceDesignV283.Palette p)
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 5)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Raised)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Border)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));

        var borderFactory = new FrameworkElementFactory(typeof(Border)) { Name = "RowChrome" };
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Control.Background))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        borderFactory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(Control.BorderBrush))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        borderFactory.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(Control.BorderThickness))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        borderFactory.AppendChild(cp);

        var template = new ControlTemplate(typeof(ListBoxItem)) { VisualTree = borderFactory };

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, DeskSpaceDesignV283.Solid(p.Hover), "RowChrome"));
        template.Triggers.Add(hover);

        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty, DeskSpaceDesignV283.Solid(p.AccentSoft), "RowChrome"));
        selected.Setters.Add(new Setter(Border.BorderBrushProperty, DeskSpaceDesignV283.Solid(p.Accent), "RowChrome"));
        selected.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(p.HighContrast ? 2 : 1.5), "RowChrome"));
        template.Triggers.Add(selected);

        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static FrameworkElement BuildItemRowVisual(PinnedAppFileItemV380 item, DeskSpaceDesignV283.Palette p)
    {
        var rowGrid = new Grid { Margin = new Thickness(11, 9, 11, 9) };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left glyph/extension badge
        string leftLabel = item.IsDirectory
            ? (item.TallyCompanyCode != null ? "TALLY" : "DIR")
            : ShortExtensionBadge(item.FullPath);

        var iconPill = new Border
        {
            Width = 44,
            Height = 36,
            CornerRadius = new CornerRadius(8),
            Background = DeskSpaceDesignV283.Solid(item.IsRecent ? p.AccentSoft : p.Surface),
            BorderBrush = DeskSpaceDesignV283.Solid(item.IsRecent ? p.Accent : p.Border),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 11, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = leftLabel,
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = DeskSpaceDesignV283.Solid(item.IsRecent ? p.Accent : p.Muted),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(iconPill, 0);
        rowGrid.Children.Add(iconPill);

        // Middle title + path + badges
        var centerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var topLine = new WrapPanel { Orientation = Orientation.Horizontal };
        topLine.Children.Add(new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        if (item.IsRecent)
        {
            topLine.Children.Add(CreatePillTag("★ RECENT", p.Accent, p.HighContrast ? p.Text : Color.FromRgb(8, 16, 33)));
        }

        topLine.Children.Add(CreatePillTag(item.KindBadge, p.Navigation, p.Muted));

        centerStack.Children.Add(topLine);

        centerStack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.ParentFolder) ? item.FullPath : item.ParentFolder,
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 3, 0, 0)
        });

        Grid.SetColumn(centerStack, 1);
        rowGrid.Children.Add(centerStack);

        // Right timestamp & source badge
        var rightStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10, 0, 0, 0)
        };

        string timeText = item.SortTimeUtc > DateTime.MinValue
            ? item.SortTimeUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : string.Empty;

        if (timeText.Length > 0)
        {
            rightStack.Children.Add(new TextBlock
            {
                Text = timeText,
                FontSize = 11,
                Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                HorizontalAlignment = HorizontalAlignment.Right
            });
        }

        if (!string.IsNullOrWhiteSpace(item.SourceBadge))
        {
            rightStack.Children.Add(new TextBlock
            {
                Text = item.SourceBadge,
                FontSize = 10.5,
                FontWeight = item.IsRecent ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = DeskSpaceDesignV283.Solid(item.IsRecent ? p.Accent : p.Muted),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        Grid.SetColumn(rightStack, 2);
        rowGrid.Children.Add(rightStack);

        return rowGrid;
    }

    private static Border CreatePillTag(string text, Color bg, Color fg)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = DeskSpaceDesignV283.Solid(bg),
            Padding = new Thickness(6, 1.5, 6, 1.5),
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = DeskSpaceDesignV283.Solid(fg)
            }
        };
    }

    private static string ShortExtensionBadge(string path)
    {
        string ext = GetEffectiveExtension(path).TrimStart('.').ToUpperInvariant();
        if (ext.Length == 0)
            return "FILE";
        return ext.Length > 5 ? ext.Substring(0, 5) : ext;
    }

    private static bool IsClickInsideListBoxItem(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ListBoxItem)
                return true;
            if (source is ListBox)
                return false;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}

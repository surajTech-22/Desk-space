using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskSpace;
using Drawing = System.Drawing;
using Color = System.Windows.Media.Color;
using Button = System.Windows.Controls.Button;

internal static class Program
{
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("R380 TEST FAIL: " + message);
    }

    private static object? InvokeStatic(Type type, string method, params object?[] args)
    {
        var m = type.GetMethod(method, AnyStatic)
            ?? throw new MissingMethodException(type.FullName, method);
        return m.Invoke(null, args);
    }

    private static T GetProp<T>(object instance, string propName)
    {
        var p = instance.GetType().GetProperty(propName, AnyInstance | AnyStatic)
            ?? throw new MissingMemberException(instance.GetType().FullName, propName);
        return (T)p.GetValue(instance)!;
    }

    private static void SetProp(object instance, string propName, object? val)
    {
        var p = instance.GetType().GetProperty(propName, AnyInstance | AnyStatic)
            ?? throw new MissingMemberException(instance.GetType().FullName, propName);
        p.SetValue(instance, val);
    }

    private static void TestCommandBarV380(Assembly asm)
    {
        var cmdType = asm.GetType("DeskSpace.DeskSpaceCommandBarV380", true)!;
        var evalMethod = cmdType.GetMethod("TryEvaluateExpression", AnyStatic)!;

        (bool Ok, string Res, string Detail) Eval(string query)
        {
            object?[] args = { query, null, null };
            bool ok = (bool)evalMethod.Invoke(null, args)!;
            return (ok, (string?)args[1] ?? "", (string?)args[2] ?? "");
        }

        var arith = Eval("(4500*18%)+1200");
        Check(arith.Ok && arith.Res == "2010", "Expected (4500*18%)+1200 = 2010, got: " + arith.Res);

        var fwdGst = Eval("5000 + 18% gst");
        Check(fwdGst.Ok && (fwdGst.Res.Contains("5900") || fwdGst.Res.Contains("5,900")),
            "Expected forward GST 5000 + 18% = 5900, got: " + fwdGst.Res);

        var revGst = Eval("5900 excl 18% gst");
        Check(revGst.Ok && (revGst.Res.Contains("5000") || revGst.Res.Contains("5,000")) && revGst.Detail.Contains("900"),
            "Expected reverse GST 5900 excl 18% -> 5000 + 900, got: " + revGst.Res + " / " + revGst.Detail);

        var dist = Eval("100 km to mi");
        Check(dist.Ok && double.TryParse(new string(dist.Res.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()), NumberStyles.Float, CultureInfo.InvariantCulture, out double miles) && Math.Abs(miles - 62.1371) < 0.2,
            "Expected 100 km to ~62.14 mi, got: " + dist.Res);

        var temp = Eval("100 c to f");
        Check(temp.Ok && temp.Res.StartsWith("212", StringComparison.Ordinal),
            "Expected 100 c to 212 f, got: " + temp.Res);

        var dataUnit = Eval("2 gb to mb");
        Check(dataUnit.Ok && (dataUnit.Res.Contains("2048") || dataUnit.Res.Contains("2000")),
            "Expected 2 gb to mb conversion, got: " + dataUnit.Res);

        var dateAdd = Eval("today + 45d");
        string expectedDate = DateTime.Today.AddDays(45).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Check(dateAdd.Ok && (dateAdd.Res.Contains(expectedDate) || dateAdd.Detail.Contains(expectedDate) || dateAdd.Res.Contains(DateTime.Today.AddDays(45).ToString("dd MMM yyyy", CultureInfo.InvariantCulture))),
            "Expected today + 45d date result, got: " + dateAdd.Res + " / " + dateAdd.Detail);

        var daysBetween = Eval("days between 2026-01-01 and 2026-12-31");
        Check(daysBetween.Ok && daysBetween.Res.StartsWith("364", StringComparison.Ordinal),
            "Expected 364 days between 2026-01-01 and 2026-12-31, got: " + daysBetween.Res);

        var hex = Eval("hex 255");
        Check(hex.Ok && hex.Res.Equals("0xFF", StringComparison.OrdinalIgnoreCase),
            "Expected hex 255 = 0xFF, got: " + hex.Res);

        var bin = Eval("bin 42");
        Check(bin.Ok && bin.Res.Equals("0b101010", StringComparison.OrdinalIgnoreCase),
            "Expected bin 42 = 0b101010, got: " + bin.Res);

        var fromHex = Eval("0xFF to dec");
        Check(fromHex.Ok && fromHex.Res == "255",
            "Expected 0xFF to dec = 255, got: " + fromHex.Res);

        var ws = new Workspace();
        ws.Notes.Add(new NoteItem { Title = "GST Quarterly Return", Content = "Check GSTR-3B reconciliation" });
        var searchMethod = cmdType.GetMethod("Search", AnyStatic)!;
        var hits = ((IEnumerable)searchMethod.Invoke(null, new object?[] { "#GST", ws })!).Cast<object>().ToList();
        Check(hits.Count >= 1 && GetProp<string>(hits[0], "Title").Contains("GST", StringComparison.OrdinalIgnoreCase),
            "CommandBar #GST search must rank matching note");
    }

    private static void TestPinnedAppFilesV380(Assembly asm)
    {
        var type = asm.GetType("DeskSpace.DeskSpacePinnedAppFilesV380", true)!;
        var itemType = asm.GetType("DeskSpace.PinnedAppFileItemV380", true)!;

        // 1. ResolveCapability for Excel & Tally
        var resolveCap = type.GetMethod("ResolveCapability", AnyStatic)!;
        var excelCap = resolveCap.Invoke(null, new object[] { @"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE" })!;
        var excelExts = GetProp<HashSet<string>>(excelCap, "Extensions");
        Check(excelExts.Contains(".xlsx") && excelExts.Contains(".csv") && excelExts.Contains(".xls"),
            "Excel capability must include .xlsx, .csv, and .xls");

        var tallyCap = resolveCap.Invoke(null, new object[] { @"C:\TallyPrime\Tally.exe" })!;
        Check(GetProp<bool>(tallyCap, "IncludesTallyCompanies"),
            "Tally capability must enable IncludesTallyCompanies");

        // 2. ParseTallyIni
        var parseIni = type.GetMethod("ParseTallyIni", AnyStatic)!;
        string sampleIni = """
            ;; Comment line
            Data = D:\TallyData\2026 ; company data folder
            Default Companies = Yes
            Load = 10001
            Load = 10008
            """;
        var (dataDirs, loadCodes) = (ValueTuple<List<string>, List<string>>)parseIni.Invoke(null, new object[] { sampleIni })!;
        Check(dataDirs.Any(d => d.Equals(@"D:\TallyData\2026", StringComparison.OrdinalIgnoreCase)) &&
              loadCodes.Contains("10001") && loadCodes.Contains("10008"),
            "ParseTallyIni must parse Data path (without comment) and Load codes");

        // 3. Synthetic TallyPrime (.1800) & Tally.ERP 9 (.900) company folders + BuildLaunchInfo
        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_Tally_" + Guid.NewGuid().ToString("N"));
        try
        {
            string comp1800 = Path.Combine(tempRoot, "Data", "10001");
            string comp900 = Path.Combine(tempRoot, "LegacyData", "10009");
            string xlsFile = Path.Combine(tempRoot, "SalesReport.csv");
            Directory.CreateDirectory(comp1800);
            Directory.CreateDirectory(comp900);
            File.WriteAllText(Path.Combine(comp1800, "Company.1800"), "mock");
            File.WriteAllText(Path.Combine(comp1800, "TranMgr.1800"), "mock");
            File.WriteAllText(Path.Combine(comp900, "Company.900"), "mock");
            File.WriteAllText(Path.Combine(comp900, "Manager.900"), "mock");
            File.WriteAllText(xlsFile, "Item,Qty\nA,1\n");

            var tryParseTally = type.GetMethod("TryParseTallyCompanyFolder", AnyStatic)!;
            object?[] args1800 = { comp1800, null, true, "★ Tally Recent" };
            bool ok1800 = (bool)tryParseTally.Invoke(null, args1800)!;
            Check(ok1800 && args1800[1] != null &&
                  GetProp<string>(args1800[1]!, "TallyCompanyCode") == "10001" &&
                  GetProp<bool>(args1800[1]!, "IsDirectory"),
                "TryParseTallyCompanyFolder must detect .1800 TallyPrime company #10001");

            object?[] args900 = { comp900, null, false, "Drive Scan" };
            bool ok900 = (bool)tryParseTally.Invoke(null, args900)!;
            Check(ok900 && args900[1] != null &&
                  GetProp<string>(args900[1]!, "TallyCompanyCode") == "10009",
                "TryParseTallyCompanyFolder must detect .900 Tally.ERP 9 company #10009");

            var buildLaunch = type.GetMethod("BuildLaunchInfo", AnyStatic)!;
            object?[] launchTallyArgs = { tallyCap, args1800[1]!, null, null, new List<string> { @"C:\TallyPrime\Data" } };
            bool okLaunchTally = (bool)buildLaunch.Invoke(null, launchTallyArgs)!;
            var tallyPsi = (ProcessStartInfo)launchTallyArgs[2]!;
            Check(okLaunchTally && !tallyPsi.UseShellExecute &&
                  tallyPsi.ArgumentList.Contains("/NOINILOAD") &&
                  tallyPsi.ArgumentList.Contains("/LOAD:10001") &&
                  tallyPsi.ArgumentList.Any(a => a.Equals(Path.Combine(tempRoot, "Data"), StringComparison.OrdinalIgnoreCase)),
                "Tally launch must pass data root, /NOINILOAD, and /LOAD:10001 via ArgumentList");

            // Standard Excel CSV item opens explicitly with excel.exe even when CSV is plain text
            var csvItem = Activator.CreateInstance(itemType, new object?[]
            {
                "SalesReport.csv", xlsFile, tempRoot, "CSV", DateTime.UtcNow, true, "★ Recent", false, null, null
            })!;
            object?[] launchExcelArgs = { excelCap, csvItem, null, null, null };
            bool okLaunchExcel = (bool)buildLaunch.Invoke(null, launchExcelArgs)!;
            var excelPsi = (ProcessStartInfo)launchExcelArgs[2]!;
            Check(okLaunchExcel && !excelPsi.UseShellExecute &&
                  excelPsi.FileName.EndsWith("EXCEL.EXE", StringComparison.OrdinalIgnoreCase) &&
                  excelPsi.ArgumentList.Contains(xlsFile),
                "Excel launch must pass selected CSV directly to EXCEL.EXE");

            // 4. Dangerous & raw database shard blocking
            var isSafeExt = type.GetMethod("IsSafeTargetExtension", AnyStatic)!;
            Check(!(bool)isSafeExt.Invoke(null, new object[] { ".exe", false })! &&
                  !(bool)isSafeExt.Invoke(null, new object[] { ".bat", false })! &&
                  !(bool)isSafeExt.Invoke(null, new object[] { ".ps1", false })! &&
                  !(bool)isSafeExt.Invoke(null, new object[] { ".msi", false })! &&
                  !(bool)isSafeExt.Invoke(null, new object[] { ".1800", false })! &&
                  !(bool)isSafeExt.Invoke(null, new object[] { ".900", false })! &&
                  (bool)isSafeExt.Invoke(null, new object[] { ".xlsx", false })!,
                "IsSafeTargetExtension must block dangerous executables and raw .1800/.900 shards");

            // 5. MergeAndRank strictly places IsRecent first and deduplicates paths
            DateTime now = DateTime.UtcNow;
            var oldRecent = Activator.CreateInstance(itemType, new object?[]
            {
                "OldButRecent.xlsx", Path.Combine(tempRoot, "OldButRecent.xlsx"), tempRoot, "XLSX",
                now.AddDays(-10), true, "★ Windows Recent", false, null, null
            })!;
            var newerSystem = Activator.CreateInstance(itemType, new object?[]
            {
                "NewerNonRecent.xlsx", Path.Combine(tempRoot, "NewerNonRecent.xlsx"), tempRoot, "XLSX",
                now, false, "Drive Scan", false, null, null
            })!;
            var dupSystem = Activator.CreateInstance(itemType, new object?[]
            {
                "OldButRecent.xlsx", Path.Combine(tempRoot, "OldButRecent.xlsx"), tempRoot, "XLSX",
                now.AddDays(-10), false, "Drive Scan", false, null, null
            })!;

            var typedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
            typedList.Add(oldRecent);
            var sysList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
            sysList.Add(newerSystem);
            sysList.Add(dupSystem);

            var mergeMethod = type.GetMethod("MergeAndRank", AnyStatic)!;
            var merged = ((IEnumerable)mergeMethod.Invoke(null, new object[] { typedList, sysList, 100 })!).Cast<object>().ToList();
            Check(merged.Count == 2 &&
                  GetProp<string>(merged[0], "DisplayName") == "OldButRecent.xlsx" &&
                  GetProp<bool>(merged[0], "IsRecent") &&
                  GetProp<string>(merged[1], "DisplayName") == "NewerNonRecent.xlsx",
                "MergeAndRank must deduplicate and place Recent items strictly ahead of newer non-recent files");

            // 6. Per-app launch history persistence round-trip
            string histFile = Path.Combine(tempRoot, "launches.json");
            InvokeStatic(type, "RecordLaunch", "excel.exe", xlsFile, histFile);
            var loadedHist = ((IEnumerable<ValueTuple<string, DateTime>>)InvokeStatic(type, "GetRecentLaunches", "excel.exe", histFile)!).ToList();
            Check(loadedHist.Count == 1 && loadedHist[0].Item1.Equals(Path.GetFullPath(xlsFile), StringComparison.OrdinalIgnoreCase),
                "RecordLaunch/GetRecentLaunches must persist per-app recent launch history atomically");

            // 7. TallyPrime CmpSave.1800 company detection
            string compSaveOnly = Path.Combine(tempRoot, "Data", "100001");
            Directory.CreateDirectory(compSaveOnly);
            File.WriteAllText(Path.Combine(compSaveOnly, "Company.1800"), "mock");
            File.WriteAllText(Path.Combine(compSaveOnly, "CmpSave.1800"), "mock");
            object?[] argsSave = { compSaveOnly, null, false, "Tally Data" };
            bool okSaveOnly = (bool)tryParseTally.Invoke(null, argsSave)!;
            Check(okSaveOnly && argsSave[1] != null &&
                  GetProp<string>(argsSave[1]!, "TallyCompanyCode") == "100001",
                "TryParseTallyCompanyFolder must detect .1800 TallyPrime company with Company.1800 + CmpSave.1800");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void TestPinnedAppsDirectOpenAndWinmanV380(Assembly asm)
    {
        var type = asm.GetType("DeskSpace.DeskSpacePinnedAppFilesV380", true)!;
        var itemType = asm.GetType("DeskSpace.PinnedAppFileItemV380", true)!;

        // 1. Shortcut direct-open shell preference preserves .lnk args (e.g. Winman CA ERP.lnk)
        Check((bool)InvokeStatic(type, "ShouldPreferShortcutShellLaunch", @"C:\Desktop\Winman CA ERP.lnk")! &&
              (bool)InvokeStatic(type, "ShouldPreferShortcutShellLaunch", @"C:\Links\app.url")! &&
              !(bool)InvokeStatic(type, "ShouldPreferShortcutShellLaunch", @"C:\Apps\tally.exe")!,
            "ShouldPreferShortcutShellLaunch must prefer .lnk/.url over bare .exe");

        // 2. Quoted shell-retry argument formatter
        string quoted = (string)InvokeStatic(type, "QuoteWindowsArguments",
            new object[] { new[] { @"F:\", "/NOINILOAD", "/LOAD:100001", @"C:\My Folder\Report 1.xlsx", @"C:\Trailing Space\" } })!;
        Check(quoted == @"F:\ /NOINILOAD /LOAD:100001 ""C:\My Folder\Report 1.xlsx"" ""C:\Trailing Space\\""",
            "QuoteWindowsArguments mismatch: " + quoted);

        // 3. Winman_Launcher.exe capability + routing to sibling WinTax.exe / winTds.exe
        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_Winman_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            string launcherExe = Path.Combine(tempRoot, "Winman_Launcher.exe");
            string winTaxExe = Path.Combine(tempRoot, "WinTax.exe");
            string winTdsExe = Path.Combine(tempRoot, "winTds.exe");
            string taxFile = Path.Combine(tempRoot, "ClientFY26.tax");
            string tdsFile = Path.Combine(tempRoot, "Q4Form.26q");
            File.WriteAllText(launcherExe, "mock");
            File.WriteAllText(winTaxExe, "mock");
            File.WriteAllText(winTdsExe, "mock");
            File.WriteAllText(taxFile, "mock");
            File.WriteAllText(tdsFile, "mock");

            var cap = InvokeStatic(type, "ResolveCapability", launcherExe)!;
            Check(GetProp<object>(cap, "Kind").ToString() == "WinmanAccounting",
                "Winman_Launcher.exe must resolve to WinmanAccounting capability");
            var exts = GetProp<HashSet<string>>(cap, "Extensions");
            Check(exts.Contains(".tax") && exts.Contains(".wgst") && exts.Contains(".26q") && exts.Contains(".24q"),
                "Winman_Launcher.exe capability must include .tax, .wgst, .26q, and .24q");

            var buildLaunch = type.GetMethod("BuildLaunchInfo", AnyStatic)!;
            var taxItem = Activator.CreateInstance(itemType, new object?[]
            {
                "ClientFY26.tax", taxFile, tempRoot, ".TAX", DateTime.UtcNow, true, "★ Recent", false, null, null
            })!;
            object?[] taxArgs = { cap, taxItem, null, null, null };
            Check((bool)buildLaunch.Invoke(null, taxArgs)!, "BuildLaunchInfo failed for .tax");
            var taxPsi = (ProcessStartInfo)taxArgs[2]!;
            Check(taxPsi.FileName.Equals(winTaxExe, StringComparison.OrdinalIgnoreCase) &&
                  taxPsi.ArgumentList.Contains(taxFile),
                ".tax file must route to sibling WinTax.exe");

            var tdsItem = Activator.CreateInstance(itemType, new object?[]
            {
                "Q4Form.26q", tdsFile, tempRoot, ".26Q", DateTime.UtcNow, true, "★ Recent", false, null, null
            })!;
            object?[] tdsArgs = { cap, tdsItem, null, null, null };
            Check((bool)buildLaunch.Invoke(null, tdsArgs)!, "BuildLaunchInfo failed for .26q");
            var tdsPsi = (ProcessStartInfo)tdsArgs[2]!;
            Check(tdsPsi.FileName.Equals(winTdsExe, StringComparison.OrdinalIgnoreCase) &&
                  tdsPsi.ArgumentList.Contains(tdsFile),
                ".26q file must route to sibling winTds.exe");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void TestInformationDonutGeometryAndSectorV380(Assembly asm)
    {
        var hostType = asm.GetType("DeskSpace.WidgetShellWindow", true)!;
        var segs = (string[])hostType.GetField("Segments", AnyStatic)!.GetValue(null)!;
        Check(Array.IndexOf(segs, "Information") == 6 &&
              Array.IndexOf(segs, "Applications") == 10 &&
              Array.IndexOf(segs, "Excel Functions") == 11,
            "WidgetShellWindow donut outer indices must keep Information=6, Applications=10, Excel Functions=11");

        var infoType = asm.GetType("DeskSpace.DeskSpaceInformationV36", true)!;
        var owner = new Window
        {
            Width = 408,
            Height = 408,
            Left = 1512,
            Top = 423,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };

        var info = Activator.CreateInstance(
            infoType,
            AnyInstance,
            null,
            new object[] { owner, new Action(() => { }), new Action(() => { }) },
            null)!;

        try
        {
            var allList = (IList)infoType.GetField("all", AnyInstance)!.GetValue(info)!;
            var colType = infoType.GetNestedType("Collection", AnyInstance | BindingFlags.Public)!;
            for (int i = 0; i < 6; i++)
            {
                var c = Activator.CreateInstance(colType, true)!;
                colType.GetProperty("Name")!.SetValue(c, "Info " + (i + 1));
                allList.Add(c);
            }

            infoType.GetMethod("RenderArc", AnyInstance)!.Invoke(info, null);
            var canvas = (Canvas)infoType.GetField("arcCanvas", AnyInstance)!.GetValue(info)!;
            var popup = (System.Windows.Controls.Primitives.Popup)infoType.GetField("arc", AnyInstance)!.GetValue(info)!;

            // Tight clipped bounds (much smaller than legacy 488x222 padded box)
            Check(canvas.Width > 350 && canvas.Width < 430 && canvas.Height > 110 && canvas.Height < 170,
                $"Information arc canvas must be tightly clipped around sectors, got {canvas.Width:F1}x{canvas.Height:F1}");

            var geomType = asm.GetType("DeskSpace.DeskSpaceDonutGeometryV43", true)!;
            double scale = (double)geomType.GetMethod("Scale", AnyStatic, new[] { typeof(Window) })!.Invoke(null, new object[] { owner })!;
            double renderedCenter = popup.HorizontalOffset + (canvas.Width * scale) / 2.0;
            Check(Math.Abs(renderedCenter - 204.0) < 2.0,
                $"Information arc must be concentric with donut center (expected ~204.0, got {renderedCenter:F2})");

            // At right edge Left=1512 on 1920px monitor, scaled arc right edge must fit inside 1920 without clamping
            double rightEdge = owner.Left + popup.HorizontalOffset + canvas.Width * scale;
            Check(rightEdge <= 1920.0,
                $"Information arc must fit within 1920px right monitor edge without detachment (right={rightEdge:F1})");

            // R158 hidden count invariant preserved: 4 visible saved items + 1 '+ New' = 5 wedges, 1 inline counter
            var wedges = canvas.Children.OfType<System.Windows.Shapes.Path>()
                .Where(p => p.Tag is int)
                .ToArray();
            Check(wedges.Length == 5, "Expected 5 visible Information wedges for 6 saved items, got " + wedges.Length);
            var inline = canvas.Children.OfType<System.Windows.Shapes.Path>()
                .Where(p => (p.Tag as string) == "DeskSpaceHiddenCountInline")
                .ToArray();
            Check(inline.Length == 1, "Expected 1 R158 inline divider counter for 6 saved items, got " + inline.Length);
        }
        finally
        {
            ((IDisposable)info).Dispose();
        }
    }

    private static void TestInformationTabSyncAndSchemaV380(Assembly asm)
    {
        var tabType = asm.GetType("DeskSpace.DeskSpaceInformationTabV380", true)!;
        var colType = asm.GetType("DeskSpace.InfoCollectionV380", true)!;
        var rowType = asm.GetType("DeskSpace.InfoRowV380", true)!;

        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_InfoTab_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            string colPath = Path.Combine(tempRoot, "information-collections-v26.json");
            string legPath = Path.Combine(tempRoot, "information-items-v22.json");

            var listType = typeof(List<>).MakeGenericType(colType);
            var rowListType = typeof(List<>).MakeGenericType(rowType);

            var col1 = Activator.CreateInstance(colType)!;
            Guid col1Id = Guid.NewGuid();
            Guid rowVaultId = Guid.NewGuid();
            SetProp(col1, "Id", col1Id);
            SetProp(col1, "Name", "GST Portal");
            SetProp(col1, "UpdatedUtc", DateTime.UtcNow.AddMinutes(-5));
            var rows1 = (IList)Activator.CreateInstance(rowListType)!;
            var r1 = Activator.CreateInstance(rowType)!;
            SetProp(r1, "Label", "UI");
            SetProp(r1, "Value", "CA_USER_01");
            var r2 = Activator.CreateInstance(rowType)!;
            SetProp(r2, "Label", "PW");
            SetProp(r2, "Value", "");
            SetProp(r2, "VaultId", (Guid?)rowVaultId);
            rows1.Add(r1);
            rows1.Add(r2);
            SetProp(col1, "Rows", rows1);

            var list = (IList)Activator.CreateInstance(listType)!;
            list.Add(col1);

            InvokeStatic(tabType, "SaveCollections", list, colPath);
            Check(File.Exists(colPath) && !File.Exists(colPath + ".pending"),
                "SaveCollections must write target atomically with no leftover .pending file");

            string rawJson = File.ReadAllText(colPath);
            using (var doc = System.Text.Json.JsonDocument.Parse(rawJson))
            {
                var first = doc.RootElement[0];
                Check(first.TryGetProperty("Name", out var nameEl) && nameEl.GetString() == "GST Portal" &&
                      first.TryGetProperty("Rows", out var rowsEl) && rowsEl.GetArrayLength() == 2 &&
                      rowsEl[1].TryGetProperty("VaultId", out var vidEl) && vidEl.GetGuid() == rowVaultId,
                    "Saved JSON must match DeskSpaceInformationV36 exact schema");
            }

            // Second write creates .previous atomic backup
            InvokeStatic(tabType, "SaveCollections", list, colPath);
            Check(File.Exists(colPath + ".previous"),
                "Second SaveCollections call must create .previous backup");

            var reloaded = ((IEnumerable)InvokeStatic(tabType, "LoadCollections", colPath, legPath)!).Cast<object>().ToList();
            Check(reloaded.Count == 1 &&
                  GetProp<Guid>(reloaded[0], "Id") == col1Id &&
                  GetProp<IList>(reloaded[0], "Rows").Count == 2,
                "LoadCollections must round-trip DeskSpaceInformationV36 collection format cleanly");

            // Read-only legacy V22 fallback when V26 collections file does not exist
            File.WriteAllText(legPath, "[{\"Title\":\"PAN Card\",\"Value\":\"ABCDE1234F\",\"Category\":\"Tax\"}]");
            var legacyLoaded = ((IEnumerable)InvokeStatic(tabType, "LoadCollections",
                Path.Combine(tempRoot, "missing-v26.json"), legPath)!).Cast<object>().ToList();
            Check(legacyLoaded.Count == 1 &&
                  GetProp<string>(legacyLoaded[0], "Name") == "PAN Card",
                "LoadCollections must read-only import V22 items when V26 file does not exist");

            // Verify Action Palette exposes in-app Information command
            var paletteType = asm.GetType("DeskSpace.DeskSpaceActionPaletteV197", true)!;
            var commands = ((IEnumerable)paletteType.GetField("Commands", AnyStatic)!.GetValue(null)!).Cast<object>().ToList();
            Check(commands.Any(c => GetProp<string>(c, "Id") == "v380_information"),
                "DeskSpaceActionPaletteV197 must expose v380_information command");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void TestApplicationsSubDonutLabelFitAndOrderV380(Assembly asm)
    {
        var appsType = asm.GetType("DeskSpace.DeskSpaceApplicationsV55", true)!;
        var itemType = asm.GetType("DeskSpace.PinnedAppFileItemV380", true)!;
        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_AppsSub_" + Guid.NewGuid().ToString("N"));
        var owner = new Window
        {
            Width = 408,
            Height = 408,
            Left = 1512,
            Top = 423,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };

        object? apps = null;
        try
        {
            Directory.CreateDirectory(tempRoot);
            string winmanExe = Path.Combine(tempRoot, "Winman CA ERP.exe");
            string tallyPrimeExe = Path.Combine(tempRoot, "TallyPrime.exe");
            string tallyErpExe = Path.Combine(tempRoot, "Tally.ERP 9.exe");
            File.WriteAllText(winmanExe, "mock");
            File.WriteAllText(tallyPrimeExe, "mock");
            File.WriteAllText(tallyErpExe, "mock");

            var pinnedList = new[] { tallyErpExe, tallyPrimeExe, winmanExe };
            Func<IReadOnlyList<string>> getPinned = () => pinnedList;
            Action<string> noopPin = _ => { };
            Action noopSearch = () => { };

            apps = Activator.CreateInstance(appsType, new object[] { owner, getPinned, noopPin, noopPin, noopSearch })!;
            appsType.GetField("group", AnyInstance)!.SetValue(apps, "pinned");
            appsType.GetField("activeAppPath", AnyInstance)!.SetValue(apps, winmanExe);
            appsType.GetField("activeAppCenter", AnyInstance)!.SetValue(apps, 166.0);

            var filesList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
            filesList.Add(Activator.CreateInstance(itemType, new object?[]
            {
                "Chandrashekharayya_Hiremath_25-26.tax",
                Path.Combine(tempRoot, "Chandrashekharayya_Hiremath_25-26.tax"),
                tempRoot, ".TAX", DateTime.UtcNow.AddMinutes(-1), true, "★ Recent", false, null, null
            })!);
            filesList.Add(Activator.CreateInstance(itemType, new object?[]
            {
                "Chandrashekharayya Hiremath_26-27.tax",
                Path.Combine(tempRoot, "Chandrashekharayya Hiremath_26-27.tax"),
                tempRoot, ".TAX", DateTime.UtcNow.AddMinutes(-2), true, "★ Recent", false, null, null
            })!);
            filesList.Add(Activator.CreateInstance(itemType, new object?[]
            {
                "Bangalore Ananda Sudhir_26-27.tax",
                Path.Combine(tempRoot, "Bangalore Ananda Sudhir_26-27.tax"),
                tempRoot, ".TAX", DateTime.UtcNow.AddMinutes(-3), true, "★ Recent", false, null, null
            })!);
            filesList.Add(Activator.CreateInstance(itemType, new object?[]
            {
                "Venugopal_25-26.tax",
                Path.Combine(tempRoot, "Venugopal_25-26.tax"),
                tempRoot, ".TAX", DateTime.UtcNow.AddMinutes(-4), true, "★ Recent", false, null, null
            })!);
            appsType.GetField("activeAppFiles", AnyInstance)!.SetValue(apps, filesList);

            appsType.GetMethod("Render", AnyInstance)!.Invoke(apps, null);

            var canvas = (Canvas)appsType.GetField("canvas", AnyInstance)!.GetValue(apps)!;
            var wedges = canvas.Children.OfType<System.Windows.Shapes.Path>().ToArray();
            var labels = canvas.Children.OfType<TextBlock>().ToArray();
            Check(wedges.Length > 0 && labels.Length >= 8,
                $"Expected Applications sub-donut wedges & labels, got {wedges.Length} wedges / {labels.Length} labels");

            int maxWedgeZ = wedges.Max(System.Windows.Controls.Panel.GetZIndex);
            TextBlock? topFileLabel = null;
            TextBlock? allFilesLabel = null;

            foreach (var tb in labels)
            {
                double l = Canvas.GetLeft(tb);
                double t = Canvas.GetTop(tb);
                Check(!double.IsNaN(l) && !double.IsNaN(t), $"Label '{tb.Text}' missing Canvas coordinates");
                Check(l >= 0 && l + tb.Width <= canvas.Width + 0.5,
                    $"Label '{tb.Text}' horizontally exceeds canvas ({l:F1}..{l + tb.Width:F1} vs width {canvas.Width:F1})");
                Check(t >= 0 && t + Math.Min(tb.MaxHeight, 30) <= canvas.Height + 2.0,
                    $"Label '{tb.Text}' vertically exceeds canvas ({t:F1} vs height {canvas.Height:F1})");
                Check(tb.Width <= 130, $"Label '{tb.Text}' width {tb.Width:F1} exceeds cap 130");
                Check(System.Windows.Controls.Panel.GetZIndex(tb) > maxWedgeZ,
                    $"Label '{tb.Text}' Z-index ({System.Windows.Controls.Panel.GetZIndex(tb)}) must exceed wedges ({maxWedgeZ})");

                string cleanText = tb.Text.Replace("\u200B", "");
                if (cleanText == "Winman CA ERP")
                    Check(tb.Width <= 66, "Winman CA ERP depth-1 label width must stay inside wedge, got " + tb.Width);
                if (cleanText.Contains("Chandrashekharayya_Hiremath_25", StringComparison.Ordinal))
                    topFileLabel = tb;
                if (cleanText == "All files…")
                    allFilesLabel = tb;
            }

            Check(topFileLabel != null && allFilesLabel != null,
                "Depth-2 must render first recent file and All files… label");
            Check(Canvas.GetTop(topFileLabel!) < Canvas.GetTop(allFilesLabel!),
                $"Left-half Depth-2 must place most-recent file above All files… (topFile={Canvas.GetTop(topFileLabel!):F1}, allFiles={Canvas.GetTop(allFilesLabel!):F1})");

            // Verify compact Depth-2 angular span
            var itemsRaw = ((IEnumerable)appsType.GetField("items", AnyInstance)!.GetValue(apps)!).Cast<object>().ToList();
            var d2Items = itemsRaw.Where(x => (int)x.GetType().GetField("Item6")!.GetValue(x)! == 2).ToList();
            Check(d2Items.Count == 5, "Expected 5 depth-2 wedges (4 files + All files…), got " + d2Items.Count);
            double minA = d2Items.Min(x => (double)x.GetType().GetField("Item4")!.GetValue(x)!);
            double maxB = d2Items.Max(x => (double)x.GetType().GetField("Item5")!.GetValue(x)!);
            Check(maxB - minA <= 62.0, $"Depth-2 angular sweep must stay compact (<=62°), got {(maxB - minA):F1}°");
        }
        finally
        {
            (apps as IDisposable)?.Dispose();
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void TestVaultProV380(Assembly asm)
    {
        var vaultType = asm.GetType("DeskSpace.DeskSpaceVaultProV380", true)!;
        var totpType = asm.GetType("DeskSpace.TotpEngineV380", true)!;
        var pwdType = asm.GetType("DeskSpace.PasswordGeneratorV380", true)!;
        var backupType = asm.GetType("DeskSpace.EncryptedWorkspaceBackupV380", true)!;

        // RFC 6238 SHA1 test vector: key "12345678901234567890" -> Base32 "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", time=59s => 8 digits = 94287082, 6 digits = 287082
        byte[] decoded = (byte[])InvokeStatic(totpType, "DecodeBase32", "JBSWY3DPEHPK3PXP")!;
        Check(decoded.SequenceEqual(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x21, 0xDE, 0xAD, 0xBE, 0xEF }),
            "Base32 decode failed RFC 4648 byte vector");

        DateTimeOffset t59 = DateTimeOffset.FromUnixTimeSeconds(59);
        string code8 = (string)InvokeStatic(totpType, "GenerateCode", "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", t59, 8, 30)!;
        string code6 = (string)InvokeStatic(totpType, "GenerateCode", "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", t59, 6, 30)!;
        Check(code8 == "94287082" && code6 == "287082",
            $"RFC 6238 TOTP failed at t=59: expected 94287082/287082, got {code8}/{code6}");

        // Password & Passphrase generator
        string pw = (string)InvokeStatic(pwdType, "GeneratePassword", 20, true, true, true, true)!;
        Check(pw.Length == 20 && pw.Any(char.IsUpper) && pw.Any(char.IsLower) && pw.Any(char.IsDigit) && pw.Any(c => !char.IsLetterOrDigit(c)),
            "GeneratePassword must include all enabled character classes");

        string phrase = (string)InvokeStatic(pwdType, "GeneratePassphrase", 5, "-")!;
        Check(phrase.Split('-').Length == 5,
            "GeneratePassphrase must generate 5 hyphen-separated words");

        var (bits, rating) = (ValueTuple<double, string>)InvokeStatic(pwdType, "EstimateEntropy", pw)!;
        Check(bits >= 80 && !string.IsNullOrEmpty(rating),
            "20-char random password must score >= 80 bits entropy");

        // Win32 DPAPI round-trip
        byte[] secretBytes = Encoding.UTF8.GetBytes("DeskSpace-DPAPI-Secret-Test-2026");
        byte[] protectedBlob = (byte[])InvokeStatic(vaultType, "DpapiProtectCurrentUser", secretBytes)!;
        byte[] unprotected = (byte[])InvokeStatic(vaultType, "DpapiUnprotectCurrentUser", protectedBlob)!;
        Check(Encoding.UTF8.GetString(unprotected) == "DeskSpace-DPAPI-Secret-Test-2026",
            "Win32 DPAPI round-trip must recover exact bytes");

        // Encrypted AES-256-GCM workspace backup & restore in temp dirs
        string tempDir = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_Backup_" + Guid.NewGuid().ToString("N"));
        try
        {
            string srcRoot = Path.Combine(tempDir, "src");
            string dstRoot = Path.Combine(tempDir, "dst");
            Directory.CreateDirectory(srcRoot);
            Directory.CreateDirectory(dstRoot);
            File.WriteAllText(Path.Combine(srcRoot, "workspace.json"), """{"Notes":[{"Title":"SecretBackupNote","Content":"Body123"}]}""");
            File.WriteAllText(Path.Combine(dstRoot, "workspace.json"), """{"Notes":[]}""");
            string encFile = Path.Combine(tempDir, "backup.dspace.enc");

            InvokeStatic(backupType, "ExportEncryptedBackup", encFile, "StrongPassphrase!99", srcRoot);
            Check(File.Exists(encFile) && !File.ReadAllText(encFile).Contains("SecretBackupNote", StringComparison.Ordinal),
                "ExportEncryptedBackup must encrypt workspace payload with no plaintext leakage");

            string summary = (string)InvokeStatic(backupType, "ImportEncryptedBackup", encFile, "StrongPassphrase!99", dstRoot)!;
            Check(File.ReadAllText(Path.Combine(dstRoot, "workspace.json")).Contains("SecretBackupNote", StringComparison.Ordinal) &&
                  summary.Length > 0,
                "ImportEncryptedBackup must restore encrypted workspace JSON");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void TestNotesSnippetsV380(Assembly asm)
    {
        var mdType = asm.GetType("DeskSpace.MarkdownNoteEngineV380", true)!;
        var snipType = asm.GetType("DeskSpace.DynamicSnippetEngineV380", true)!;
        var designType = asm.GetType("DeskSpace.DeskSpaceDesignV283", true)!;

        string md = "# GST Checklist\nTags: #tax #gst #FFAA00\n- [ ] File GSTR-1\n- [x] Verify ITC\n> Important note";
        var tags = ((IEnumerable<string>)InvokeStatic(mdType, "ExtractTags", md)!).ToList();
        Check(tags.Contains("tax") && tags.Contains("gst") && !tags.Any(t => t.Equals("FFAA00", StringComparison.OrdinalIgnoreCase)),
            "ExtractTags must find #tax/#gst and ignore hex color #FFAA00");

        var (done, total) = (ValueTuple<int, int>)InvokeStatic(mdType, "CountChecklist", md)!;
        Check(done == 1 && total == 2, $"Expected checklist 1/2, got {done}/{total}");

        string toggled = (string)InvokeStatic(mdType, "ToggleChecklistItem", md, 0)!;
        var (done2, _) = (ValueTuple<int, int>)InvokeStatic(mdType, "CountChecklist", toggled)!;
        Check(done2 == 2, "ToggleChecklistItem(0) must toggle item 0 to checked");

        var palette = designType.GetProperty("Current", AnyStatic)!.GetValue(null)!;
        var flow = (FlowDocument)InvokeStatic(mdType, "RenderToFlowDocument", md, null, palette)!;
        Check(flow.Blocks.Count >= 3, "RenderToFlowDocument must produce FlowDocument blocks");

        string tmpl = "Hello {{prompt:Client}}, Date={{date}}, Clip={{clipboard}}";
        var prompts = ((IEnumerable<string>)InvokeStatic(snipType, "FindPrompts", tmpl)!).ToList();
        Check(prompts.Count == 1 && prompts[0] == "Client", "FindPrompts must extract Client");

        var promptValues = new Dictionary<string, string> { ["Client"] = "Acme Corp" };
        DateTime fixedDate = new(2026, 11, 23, 14, 30, 0);
        string expanded = (string)InvokeStatic(snipType, "Expand", tmpl, "INV-42", promptValues, fixedDate)!;
        Check(expanded == "Hello Acme Corp, Date=2026-11-23, Clip=INV-42",
            "DynamicSnippet Expand produced: " + expanded);
    }

    private static string ComputeExpectedGstinCheckDigit(string first14)
    {
        const string alpha = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        int total = 0;
        for (int i = 0; i < 14; i++)
        {
            int cp = alpha.IndexOf(first14[i]);
            int factor = (i % 2 == 0) ? 1 : 2;
            int prod = cp * factor;
            total += (prod / 36) + (prod % 36);
        }
        int idx = (36 - (total % 36)) % 36;
        return alpha[idx].ToString();
    }

    private static void TestExcelProV380(Assembly asm)
    {
        var normType = asm.GetType("DeskSpace.AccountingNormalizerV380", true)!;
        var rollbackType = asm.GetType("DeskSpace.ExcelRollbackStoreV380", true)!;

        Check((string)InvokeStatic(normType, "NormalizeIndianNumber", "₹ 1,23,456.75")! == "123456.75",
            "NormalizeIndianNumber must strip rupee sign & Indian commas");
        Check((string)InvokeStatic(normType, "NormalizeIndianNumber", "(1,200.00)")! == "-1200.00",
            "NormalizeIndianNumber must convert accounting parentheses to negative");
        Check((string)InvokeStatic(normType, "NormalizeDateDdMmYyyy", "2026-11-18")! == "18/11/2026",
            "NormalizeDateDdMmYyyy must convert ISO date to dd/MM/yyyy");

        string prefix14 = "29ABCDE1234F1Z";
        string checkChar = ComputeExpectedGstinCheckDigit(prefix14);
        string validGstin = prefix14 + checkChar;
        string badGstin = prefix14 + (checkChar == "0" ? "1" : "0");

        var valGstin = normType.GetMethod("ValidateGstin", AnyStatic)!;
        object?[] goodArgs = { validGstin, null };
        Check((bool)valGstin.Invoke(null, goodArgs)!, "Valid GSTIN failed: " + goodArgs[1]);
        object?[] badArgs = { badGstin, null };
        Check(!(bool)valGstin.Invoke(null, badArgs)!, "Mutated checksum GSTIN must fail validation");

        Check((bool)InvokeStatic(normType, "ValidatePan", "ABCDE1234F")! &&
              !(bool)InvokeStatic(normType, "ValidatePan", "INVALID")!,
            "ValidatePan check failed");

        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Invoice", "Amount" },
            new[] { "INV-001", "100" },
            new[] { "INV-002", "200" },
            new[] { "INV-001", "150" }
        };
        var dups = ((IEnumerable<(string InvoiceNo, List<int> RowNumbers)>)InvokeStatic(normType, "FindDuplicateInvoices", rows, 0, true)!).ToList();
        Check(dups.Count == 1 && dups[0].InvoiceNo == "INV-001" && dups[0].RowNumbers.SequenceEqual(new[] { 2, 4 }),
            "FindDuplicateInvoices must report INV-001 on rows 2 and 4");

        string tempFile = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_Rollback_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var proposalType = asm.GetType("DeskSpace.DeskSpaceExcelProposalV16", true)!;
            var changeType = asm.GetType("DeskSpace.DeskSpaceExcelChangeV16", true)!;
            var proposal = Activator.CreateInstance(proposalType)!;
            SetProp(proposal, "WorkbookFullName", "Book1.xlsx");
            SetProp(proposal, "SheetName", "Sheet1");
            var changes = GetProp<IList>(proposal, "Changes");

            var c1 = Activator.CreateInstance(changeType)!;
            SetProp(c1, "Row", 1); SetProp(c1, "Column", 1); SetProp(c1, "CellAddress", "A1");
            SetProp(c1, "Previous", "OldA1"); SetProp(c1, "NewValue", "NewA1"); SetProp(c1, "Decision", "Ready");
            changes.Add(c1);

            var c2 = Activator.CreateInstance(changeType)!;
            SetProp(c2, "Row", 1); SetProp(c2, "Column", 2); SetProp(c2, "CellAddress", "B1");
            SetProp(c2, "Previous", "OldB1"); SetProp(c2, "NewValue", "NewB1"); SetProp(c2, "Decision", "Ready");
            changes.Add(c2);

            var entry = InvokeStatic(rollbackType, "Record", proposal, tempFile)!;
            var loaded = ((IEnumerable)InvokeStatic(rollbackType, "LoadHistory", tempFile)!).Cast<object>().ToList();
            string tsv = (string)InvokeStatic(rollbackType, "BuildRestoreTsv", entry)!;
            Check(loaded.Count == 1 && tsv.Contains("OldA1\tOldB1", StringComparison.Ordinal),
                "RollbackStore Record/LoadHistory/BuildRestoreTsv failed: " + tsv);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private static void TestFileToolkitV380(Assembly asm)
    {
        var qrType = asm.GetType("DeskSpace.QrCodeEncoderV380", true)!;
        var hashType = asm.GetType("DeskSpace.HashToolkitV380", true)!;
        var imgType = asm.GetType("DeskSpace.ImageToolkitV380", true)!;
        var zipType = asm.GetType("DeskSpace.ZipToolkitV380", true)!;

        bool[,] matrix = (bool[,])InvokeStatic(qrType, "EncodeMatrix", "https://desktouch.app/v380")!;
        Check(matrix.GetLength(0) >= 21 && matrix.GetLength(0) == matrix.GetLength(1),
            "QR matrix must be square >= 21x21");
        WriteableBitmap bmp = (WriteableBitmap)InvokeStatic(qrType, "RenderBitmap", "WIFI:T:WPA;S:Office;P:Secret123;;", 6, 4)!;
        Check(bmp.PixelWidth > 150 && bmp.PixelHeight == bmp.PixelWidth,
            "QR WriteableBitmap dimensions invalid");

        string tempDir = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_Files_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string pngPath = Path.Combine(tempDir, "sample.png");
            string outImg = Path.Combine(tempDir, "stripped.jpg");
            using (var gdi = new Drawing.Bitmap(64, 48))
            using (var g = Drawing.Graphics.FromImage(gdi))
            {
                g.Clear(Drawing.Color.SteelBlue);
                gdi.Save(pngPath, Drawing.Imaging.ImageFormat.Png);
            }

            var optRes = (ValueTuple<long, long, int, int>)InvokeStatic(imgType, "OptimizeAndStripExif", pngPath, outImg, 32, 85, ".jpg")!;
            Check(File.Exists(outImg) && optRes.Item3 == 32 && optRes.Item4 == 24,
                $"OptimizeAndStripExif expected 32x24 output, got {optRes.Item3}x{optRes.Item4}");

            string actualHex = ((Task<string>)InvokeStatic(hashType, "ComputeSha256Async", pngPath, CancellationToken.None)!).GetAwaiter().GetResult();
            Check(actualHex.Length == 64 &&
                  (bool)InvokeStatic(hashType, "VerifySha256", actualHex, actualHex.ToUpperInvariant() + "  sample.png")! &&
                  !(bool)InvokeStatic(hashType, "VerifySha256", actualHex, new string('0', 64))!,
                "HashToolkitV380 SHA-256 verification failed");

            string zipPath = Path.Combine(tempDir, "bundle.zip");
            ((Task)InvokeStatic(zipType, "CreateZipAsync", new[] { pngPath, outImg }, zipPath, CancellationToken.None)!).GetAwaiter().GetResult();
            using var archive = ZipFile.OpenRead(zipPath);
            Check(archive.Entries.Count == 2, "ZIP bundler must contain both files");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void TestFocusRingAndWindowPowerV380(Assembly asm)
    {
        var focusType = asm.GetType("DeskSpace.DeskSpaceFocusRingV380", true)!;
        var topoType = asm.GetType("DeskSpace.MonitorTopologyV380", true)!;
        var snapType = asm.GetType("DeskSpace.DeskSpaceSnapZonesV380", true)!;

        // Focus Ring geometry & Donut Canvas attachment
        InvokeStatic(focusType, "Reset");
        var idleCanvas = new Canvas { Width = 292, Height = 292 };
        InvokeStatic(focusType, "AttachToDonutCanvas", idleCanvas);
        Check(idleCanvas.Children.Count == 0, "Idle Focus Ring must add zero children to compact clock canvas");

        var g0 = (PathGeometry)InvokeStatic(focusType, "BuildProgressArcGeometry", 146.0, 146.0, 130.0, 0.0)!;
        var gQuarter = (PathGeometry)InvokeStatic(focusType, "BuildProgressArcGeometry", 146.0, 146.0, 130.0, 0.25)!;
        var gFull = (PathGeometry)InvokeStatic(focusType, "BuildProgressArcGeometry", 146.0, 146.0, 130.0, 1.0)!;
        Check(g0.Figures.Count == 0 && gQuarter.Figures.Count == 1 && gFull.Figures.Count == 1,
            "BuildProgressArcGeometry must handle 0, 0.25, and 1.0");

        try
        {
            InvokeStatic(focusType, "StartFocus", 25, "R380 Test Session");
            Check((bool)focusType.GetProperty("IsActive", AnyStatic)!.GetValue(null)!,
                "StartFocus must set IsActive = true");
            var activeCanvas = new Canvas { Width = 292, Height = 292 };
            InvokeStatic(focusType, "AttachToDonutCanvas", activeCanvas);
            Check(activeCanvas.Children.Count >= 1,
                "Active Focus Ring must render progress ring onto Donut canvas");
            InvokeStatic(focusType, "TogglePauseResume");
            Check((bool)focusType.GetProperty("IsPaused", AnyStatic)!.GetValue(null)!,
                "TogglePauseResume must pause active timer");
        }
        finally
        {
            InvokeStatic(focusType, "Reset");
        }

        // Monitor topology & Snap Zones
        var screens = new[]
        {
            ("DISPLAY2", 2560, 1440, false),
            ("DISPLAY1", 1920, 1080, true)
        };
        string sig = (string)InvokeStatic(topoType, "ComputeSignature", screens)!;
        Check(sig == "P:1920x1080|S:2560x1440", "Unexpected topology signature: " + sig);

        var work = new Drawing.Rectangle(0, 0, 1920, 1032);
        foreach (string tmpl in new[] { "Halves", "Focus70_30", "Thirds", "Quarters", "Coding", "Ultrawide" })
        {
            var rects = ((IEnumerable<Drawing.Rectangle>)InvokeStatic(snapType, "ComputeZoneRects", tmpl, work)!).ToList();
            Check(rects.Count >= 2 && rects.All(r => r.Width > 0 && r.Height > 0 && work.Contains(r)),
                "SnapZones template " + tmpl + " produced out-of-bounds rects");
        }
    }

    private static void TestClipboardEcosystemAutomationLocalAiHubV380(Assembly asm)
    {
        // Rich Clipboard
        var clipType = asm.GetType("DeskSpace.DeskSpaceRichClipboardV380", true)!;
        var tryColor = clipType.GetMethod("TryParseColorHex", AnyStatic)!;
        object?[] colArgs = { "rgb(255, 128, 0)", null };
        Check((bool)tryColor.Invoke(null, colArgs)! && (string)colArgs[1]! == "#FF8000",
            "TryParseColorHex must normalize rgb(255,128,0) to #FF8000");

        string snake = (string)InvokeStatic(clipType, "TransformQuick", "Hello World Test", "snake_case")!;
        string urls = (string)InvokeStatic(clipType, "TransformQuick", "Visit https://example.com/a and http://test.local/b now.", "Extract URLs")!;
        string prettyJson = (string)InvokeStatic(clipType, "TransformQuick", "{\"a\":1}", "Format JSON")!;
        Check(snake == "hello_world_test" && urls.Contains("https://example.com/a") && prettyJson.Contains('\n'),
            "RichClipboard transforms failed");

        InvokeStatic(clipType, "NotifySensitiveCopy", "MySuperSecretPassword123!");

        // DeskTouch Ecosystem OTP Extractor
        var ecoType = asm.GetType("DeskSpace.DeskSpaceDeskTouchEcosystemV380", true)!;
        Check((string?)InvokeStatic(ecoType, "ExtractOtpCode", "Your verification OTP is 482910. Valid for 5 mins.") == "482910" &&
              (string?)InvokeStatic(ecoType, "ExtractOtpCode", "G-938471 is your Google verification code") == "938471" &&
              (string?)InvokeStatic(ecoType, "ExtractOtpCode", "Total billed ₹5000 on 2026-11-23 call 9876543210") == null,
            "ExtractOtpCode must detect real OTPs and reject currency, years, and 10-digit phone numbers");

        // Automation Pro
        var autoType = asm.GetType("DeskSpace.DeskSpaceAutomationProV380", true)!;
        var ruleType = asm.GetType("DeskSpace.RoutineRuleV380", true)!;
        var actionType = asm.GetType("DeskSpace.RoutineActionV380", true)!;
        Check((string)InvokeStatic(autoType, "ClassifyAppExtended", "tally")! == "Accounting" &&
              (string)InvokeStatic(autoType, "ClassifyAppExtended", "Code")! == "Coding" &&
              (string)InvokeStatic(autoType, "ClassifyAppExtended", "ms-teams")! == "Meetings",
            "ClassifyAppExtended failed");

        var isActionSafe = autoType.GetMethod("IsActionSafe", AnyStatic)!;
        var badAct = Activator.CreateInstance(actionType, new object?[] { "OpenAppOrUrl", "powershell.exe", "Bad" })!;
        object?[] safeArgs = { badAct, null };
        Check(!(bool)isActionSafe.Invoke(null, safeArgs)!, "IsActionSafe must block dangerous script targets");

        var goodAct = Activator.CreateInstance(actionType, new object?[] { "StartFocusTimer", "25", "Focus" })!;
        var actList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(actionType))!;
        actList.Add(goodAct);
        var rule = Activator.CreateInstance(ruleType, new object?[] { Guid.NewGuid(), "Deep Work", true, "Manual", "", actList })!;
        string dryRun = (string)InvokeStatic(autoType, "BuildDryRunSummary", rule)!;
        Check(dryRun.Contains("Deep Work") && dryRun.Contains("Focus"), "BuildDryRunSummary missing action description");

        // Local AI (opt-in disabled by default + DPAPI API key)
        var aiType = asm.GetType("DeskSpace.DeskSpaceLocalAiV380", true)!;
        var cfg = InvokeStatic(aiType, "LoadConfig", Path.Combine(Path.GetTempPath(), "nonexistent_ai_" + Guid.NewGuid().ToString("N") + ".json"))!;
        Check(!GetProp<bool>(cfg, "Enabled"), "LocalAI must be disabled by default");
        var isEndpoint = aiType.GetMethod("IsValidEndpoint", AnyStatic)!;
        object?[] epArgs = { "http://127.0.0.1:11434", false, null };
        Check((bool)isEndpoint.Invoke(null, epArgs)! && (bool)epArgs[1]!, "Localhost Ollama endpoint validation failed");
        InvokeStatic(aiType, "SetEncryptedApiKey", cfg, "sk-local-test-999");
        Check(string.IsNullOrEmpty(GetProp<string>(cfg, "EncryptedApiKeyBase64")) == false &&
              (string)InvokeStatic(aiType, "GetDecryptedApiKey", cfg)! == "sk-local-test-999",
            "LocalAI DPAPI key protect/unprotect round-trip failed");

        // Fluent Backdrop + Hub + ActionPalette Commands
        var fluentType = asm.GetType("DeskSpace.DeskSpaceFluentBackdropV380", true)!;
        var testWin = new Window { Width = 120, Height = 90, Left = -3000, Top = -3000, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        testWin.Show();
        try { InvokeStatic(fluentType, "ApplyToWindow", testWin, false); }
        finally { testWin.Close(); }

        var hubType = asm.GetType("DeskSpace.DeskSpaceModernHubV380", true)!;
        InvokeStatic(hubType, "EnsureInitialized", new object?[] { null });
        var palType = asm.GetType("DeskSpace.DeskSpaceActionPaletteV197", true)!;
        var commands = ((IEnumerable)palType.GetField("Commands", AnyStatic)!.GetValue(null)!).Cast<object>().ToList();
        var ids = commands.Select(c => GetProp<string>(c, "Id")).ToHashSet(StringComparer.Ordinal);
        Check(ids.Contains("v380_commandbar") && ids.Contains("v380_pinnedfiles") && ids.Contains("v380_excelpro") && ids.Contains("downloads") && ids.Contains("clipboard"),
            "ActionPalette must register V380 commands while preserving built-in commands");
    }

    private static void PumpDispatcher(DispatcherPriority priority)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void TestPinnedAppSearchDisposeUnloadV380(Assembly asm)
    {
        var searchType = asm.GetType("DeskSpace.DeskSpaceSearchV11", true)!;
        var pendingField = searchType.GetField("pending", AnyInstance)!;

        // 1. Reproduce exact crash order: Content swap -> Dispose() -> resize triggers Unloaded
        InvokeStatic(searchType, "RequestApplicationPinMode");
        var search1 = Activator.CreateInstance(searchType, new object[]
        {
            new WidgetSettingsV3(),
            new Action(() => { }),
            new Action<double>(_ => { })
        })!;
        var frame1 = (FrameworkElement)searchType.GetMethod("Create", AnyInstance)!.Invoke(search1, null)!;

        var win1 = new Window
        {
            Width = 340,
            Height = 140,
            Left = -3200,
            Top = -3200,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = frame1
        };
        win1.Show();
        PumpDispatcher(DispatcherPriority.Loaded);

        var liveCts = new CancellationTokenSource();
        pendingField.SetValue(search1, liveCts);

        win1.Content = new Border();
        ((IDisposable)search1).Dispose();
        win1.Width = 116;
        win1.Height = 116;
        PumpDispatcher(DispatcherPriority.Background);
        win1.Close();
        PumpDispatcher(DispatcherPriority.Background);

        Check(pendingField.GetValue(search1) == null, "DeskSpaceSearchV11 pending CTS must be cleared on Dispose/Unload");

        // 2. Reverse order: Unloaded fires first, followed by double Dispose()
        var search2 = Activator.CreateInstance(searchType, new object[]
        {
            new WidgetSettingsV3(),
            new Action(() => { }),
            new Action<double>(_ => { })
        })!;
        var frame2 = (FrameworkElement)searchType.GetMethod("Create", AnyInstance)!.Invoke(search2, null)!;
        var win2 = new Window
        {
            Width = 340,
            Height = 140,
            Left = -3200,
            Top = -3200,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = frame2
        };
        win2.Show();
        PumpDispatcher(DispatcherPriority.Loaded);
        pendingField.SetValue(search2, new CancellationTokenSource());

        win2.Content = null;
        win2.Close();
        PumpDispatcher(DispatcherPriority.Background);
        ((IDisposable)search2).Dispose();
        ((IDisposable)search2).Dispose();
    }

    private static void TestColorPaletteContrastAndHeroV380(Assembly asm)
    {
        var designType = asm.GetType("DeskSpace.DeskSpaceDesignV283", true)!;
        var appType = asm.GetType("DeskSpace.DeskSpaceAppearanceV148", true)!;
        var fluentType = asm.GetType("DeskSpace.DeskSpaceFluentBackdropV380", true)!;

        var tryColor = appType.GetMethod("TryColor", AnyStatic)!;
        var resolve = designType.GetMethod("Resolve", AnyStatic)!;
        var contrastRatio = designType.GetMethod("ContrastRatio", AnyStatic)!;
        var onAccent = designType.GetMethod("OnAccent", AnyStatic)!;
        var accentText = designType.GetMethod("AccentText", AnyStatic)!;
        var secBg = designType.GetMethod("SecondaryButtonBg", AnyStatic)!;
        var inputBg = designType.GetMethod("InputBg", AnyStatic)!;
        var successM = designType.GetMethod("Success", AnyStatic)!;
        var dangerM = designType.GetMethod("Danger", AnyStatic)!;
        var warningM = designType.GetMethod("Warning", AnyStatic)!;
        var infoM = designType.GetMethod("Info", AnyStatic)!;

        double Ratio(Color a, Color b) => (double)contrastRatio.Invoke(null, new object[] { a, b })!;

        string[] accents = { "#7364D3", "#199978", "#DA633E", "#3589DE", "#D64D80", "#B77615", "#629924", "#5F5F63", "#8291FF" };
        foreach (bool dark in new[] { false, true })
        {
            foreach (string hex in accents)
            {
                object?[] cArgs = { hex, null };
                Check((bool)tryColor.Invoke(null, cArgs)!, "TryColor failed for " + hex);
                Color accent = (Color)cArgs[1]!;

                object p = resolve.Invoke(null, new object[] { dark, false, accent })!;
                Color text = GetProp<Color>(p, "Text");
                Color muted = GetProp<Color>(p, "Muted");
                Color raised = GetProp<Color>(p, "Raised");
                Color surface = GetProp<Color>(p, "Surface");

                Check(raised != surface, $"Raised and Surface must differ cleanly (dark={dark})");
                Color sec = (Color)secBg.Invoke(null, new[] { p })!;
                Color inp = (Color)inputBg.Invoke(null, new[] { p })!;
                Check(sec != raised && inp != raised, $"SecondaryButtonBg and InputBg must separate from Raised card (dark={dark})");

                Check(Ratio(text, raised) >= 7.0, $"Primary text contrast < 7.0 on Raised ({hex}, dark={dark})");
                Check(Ratio(muted, raised) >= 4.5, $"Muted text contrast < 4.5 on Raised ({hex}, dark={dark})");

                Color fgOnAcc = (Color)onAccent.Invoke(null, new object[] { accent, false })!;
                Check(Ratio(fgOnAcc, accent) >= 4.2, $"OnAccent contrast < 4.2 for {hex}");

                Color accTxt = (Color)accentText.Invoke(null, new[] { p })!;
                Check(Ratio(accTxt, raised) >= 4.5 && Ratio(accTxt, surface) >= 4.5,
                    $"AccentText contrast < 4.5 for {hex} (dark={dark})");

                foreach (var sem in new[] { successM, dangerM, warningM, infoM })
                {
                    Color sc = (Color)sem.Invoke(null, new[] { p })!;
                    Check(Ratio(sc, raised) >= 4.5, $"{sem.Name} semantic contrast < 4.5 (dark={dark})");
                }
            }
        }

        // Verify CreatePillButton(primary=true) uses OnAccent instead of hardcoded dark navy
        object curP = designType.GetProperty("Current", AnyStatic)!.GetValue(null)!;
        Color curAccent = GetProp<Color>(curP, "Accent");
        bool curHc = GetProp<bool>(curP, "HighContrast");
        Color expectedFg = (Color)onAccent.Invoke(null, new object[] { curAccent, curHc })!;
        var pill = (Button)InvokeStatic(fluentType, "CreatePillButton", "Pin App", new Action(() => { }), true, null)!;
        Check(pill.Foreground is SolidColorBrush fgBrush && fgBrush.Color == expectedFg,
            "Primary pill button foreground must match DeskSpaceDesignV283.OnAccent");
    }

    private static void TestUniversalInAppDisplayNamesV380(Assembly asm)
    {
        var type = asm.GetType("DeskSpace.DeskSpacePinnedAppFilesV380", true)!;
        var tryParseTally = type.GetMethod("TryParseTallyCompanyFolder", AnyStatic)!;
        var resolveInApp = type.GetMethod("ResolveInAppDisplayName", AnyStatic)!;
        var resolveAppName = type.GetMethod("ResolvePinnedAppDisplayName", AnyStatic)!;
        var resolveCap = type.GetMethod("ResolveCapability", AnyStatic)!;
        var createFile = type.GetMethod("CreateFileItem", AnyStatic)!;

        // App display names (generic pinned .exe vs .lnk)
        Check((string)resolveAppName.Invoke(null, new object[] { @"C:\Office\EXCEL.EXE" })! == "Microsoft Excel",
            "ResolvePinnedAppDisplayName must show friendly product name for EXCEL.EXE");
        Check((string)resolveAppName.Invoke(null, new object[] { @"C:\Links\Winman CA ERP.lnk" })! == "Winman CA ERP",
            "ResolvePinnedAppDisplayName must preserve friendly shortcut title for .lnk");

        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_InAppNames_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);

            static byte[] BuildSyntheticTallyMeta(string companyName, string mailingName = "ShortCo")
            {
                using var ms = new MemoryStream();
                ms.Write(new byte[512]);
                byte[] tag1B = { 0x02, 0x10, 0x1B, 0x00, 0x00, 0x0F };
                byte[] nameBytes = Encoding.Unicode.GetBytes(companyName + "\0");
                ms.Write(tag1B);
                ms.Write(BitConverter.GetBytes((ushort)nameBytes.Length));
                ms.Write(nameBytes);
                byte[] tag1C = { 0x02, 0x10, 0x1C, 0x00, 0x00, 0x0F };
                byte[] mailBytes = Encoding.Unicode.GetBytes(mailingName + "\0");
                ms.Write(tag1C);
                ms.Write(BitConverter.GetBytes((ushort)mailBytes.Length));
                ms.Write(mailBytes);
                ms.Write(new byte[128]);
                return ms.ToArray();
            }

            // 1. TallyPrime .1800 company name + non-locking concurrent read
            string comp1800 = Path.Combine(tempRoot, "TallyData", "100001");
            Directory.CreateDirectory(comp1800);
            string company1800Path = Path.Combine(comp1800, "Company.1800");
            File.WriteAllBytes(company1800Path, BuildSyntheticTallyMeta("Acme Traders Pvt Ltd"));
            File.WriteAllBytes(Path.Combine(comp1800, "CmpSave.1800"), new byte[] { 1, 2, 3, 4 });

            using (var lockStream = new FileStream(company1800Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                object?[] args1800 = { comp1800, null, true, "★ Tally Recent" };
                bool ok1800 = (bool)tryParseTally.Invoke(null, args1800)!;
                Check(ok1800 && args1800[1] != null &&
                      GetProp<string>(args1800[1]!, "DisplayName") == "Acme Traders Pvt Ltd" &&
                      GetProp<string>(args1800[1]!, "TallyCompanyCode") == "100001" &&
                      GetProp<string>(args1800[1]!, "KindBadge").Contains("#100001", StringComparison.Ordinal),
                    "TallyPrime Company.1800 must resolve in-app company name non-lockingly and keep #code in KindBadge");
            }

            // 2. Tally.ERP 9 .900 company name
            string comp900 = Path.Combine(tempRoot, "LegacyData", "10009");
            Directory.CreateDirectory(comp900);
            File.WriteAllBytes(Path.Combine(comp900, "Company.900"), BuildSyntheticTallyMeta("Bharat Logistics Corp"));
            File.WriteAllBytes(Path.Combine(comp900, "Manager.900"), new byte[] { 1, 2, 3, 4 });
            object?[] args900 = { comp900, null, false, "Tally Data" };
            Check((bool)tryParseTally.Invoke(null, args900)! &&
                  GetProp<string>(args900[1]!, "DisplayName") == "Bharat Logistics Corp",
                "Tally.ERP 9 Company.900 must resolve in-app company name");

            // 3. TallyVault masked company falls back safely to code label
            string compVault = Path.Combine(tempRoot, "TallyData", "100003");
            Directory.CreateDirectory(compVault);
            File.WriteAllBytes(Path.Combine(compVault, "Company.1800"), BuildSyntheticTallyMeta("*****", "*****"));
            File.WriteAllBytes(Path.Combine(compVault, "CmpSave.1800"), new byte[] { 1, 2, 3, 4 });
            object?[] argsVault = { compVault, null, false, "Tally Data" };
            Check((bool)tryParseTally.Invoke(null, argsVault)! &&
                  GetProp<string>(argsVault[1]!, "DisplayName") == "Tally Company #100003",
                "TallyVault masked company must fall back to Tally Company #100003");

            // 4. OOXML .docx title vs boilerplate .xlsx fallback
            string docxPath = Path.Combine(tempRoot, "doc_99812.docx");
            using (var zip = ZipFile.Open(docxPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("docProps/core.xml");
                using var sw = new StreamWriter(entry.Open(), Encoding.UTF8);
                sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>Annual Audit Summary</dc:title></cp:coreProperties>");
            }
            Check((string)resolveInApp.Invoke(null, new object[] { docxPath, false })! == "Annual Audit Summary",
                "OOXML .docx core.xml dc:title must resolve in-app document title");

            string xlsxPath = Path.Combine(tempRoot, "FY26_Cashflow_Plan.xlsx");
            using (var zip = ZipFile.Open(xlsxPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("docProps/core.xml");
                using var sw = new StreamWriter(entry.Open(), Encoding.UTF8);
                sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>Book1</dc:title></cp:coreProperties>");
            }
            Check((string)resolveInApp.Invoke(null, new object[] { xlsxPath, false })! == "FY26 Cashflow Plan",
                "Boilerplate OOXML Book1 title must fall back to clean formatted filename");

            // 5. PDF /Title extraction (direct literal + exporter prefix stripping)
            string pdfPath = Path.Combine(tempRoot, "scan_0042.pdf");
            File.WriteAllText(pdfPath, "%PDF-1.4\n1 0 obj\n<< /Title (Q4 Financial Statement) >>\nendobj\n%%EOF");
            Check((string)resolveInApp.Invoke(null, new object[] { pdfPath, false })! == "Q4 Financial Statement",
                "PDF /Title must resolve in-app document title");

            string pdfExportPath = Path.Combine(tempRoot, "export_99.pdf");
            File.WriteAllText(pdfExportPath, "%PDF-1.4\n1 0 obj\n<< /Title (Microsoft Word - Draft Budget 2026.docx) >>\nendobj\n%%EOF");
            Check((string)resolveInApp.Invoke(null, new object[] { pdfExportPath, false })! == "Draft Budget 2026",
                "PDF exporter /Title must strip printer prefix and .docx extension");

            // 6. HTML, Markdown, Project Folder, Generic Tax file, Dotfile
            string htmlPath = Path.Combine(tempRoot, "portal.html");
            File.WriteAllText(htmlPath, "<!doctype html><html><head><title>Client Tax Portal</title></head></html>");
            Check((string)resolveInApp.Invoke(null, new object[] { htmlPath, false })! == "Client Tax Portal",
                "HTML <title> must resolve in-app title");

            string mdPath = Path.Combine(tempRoot, "notes.md");
            File.WriteAllText(mdPath, "# Deployment Runbook\n\nSteps...");
            Check((string)resolveInApp.Invoke(null, new object[] { mdPath, false })! == "Deployment Runbook",
                "Markdown H1 must resolve in-app title");

            string projDir = Path.Combine(tempRoot, "repo_dir");
            Directory.CreateDirectory(projDir);
            File.WriteAllText(Path.Combine(projDir, "package.json"), "{ \"name\": \"raw-pkg\", \"displayName\": \"DeskSpace Studio\" }");
            Check((string)resolveInApp.Invoke(null, new object[] { projDir, true })! == "DeskSpace Studio",
                "Project folder must resolve package.json displayName");

            string taxPath = Path.Combine(tempRoot, "Client_Return_25-26.tax");
            File.WriteAllText(taxPath, "binary");
            var taxItem = createFile.Invoke(null, new object[] { taxPath, DateTime.UtcNow, true, "★ Recent" })!;
            Check(GetProp<string>(taxItem, "DisplayName") == "Client Return 25-26",
                "Generic file CreateFileItem must format clean extension-less friendly DisplayName");

            string dotPath = Path.Combine(tempRoot, ".gitignore");
            File.WriteAllText(dotPath, "bin/\n");
            Check((string)resolveInApp.Invoke(null, new object[] { dotPath, false })! == ".gitignore",
                "Dotfile without stem before dot must preserve full dotfile name");

            // 7. Quick-arc returns > 4 items (all discovered companies in ExtraDataRoots, not capped at 4)
            string dataRoot = Path.Combine(tempRoot, "BulkTallyRoot");
            Directory.CreateDirectory(dataRoot);
            for (int k = 1; k <= 7; k++)
            {
                string cd = Path.Combine(dataRoot, $"20000{k}");
                Directory.CreateDirectory(cd);
                File.WriteAllBytes(Path.Combine(cd, "Company.1800"), BuildSyntheticTallyMeta($"Bulk Enterprise {k}"));
                File.WriteAllBytes(Path.Combine(cd, "CmpSave.1800"), new byte[] { 1, 2 });
            }
            var tallyCap = resolveCap.Invoke(null, new object[] { @"C:\TallyPrime\tallyprime.exe" })!;
            GetProp<List<string>>(tallyCap, "ExtraDataRoots").Insert(0, dataRoot);
            // Exercise MergeAndRank directly with 7 items > 4
            var mergeMethod = type.GetMethod("MergeAndRank", AnyStatic)!;
            var itemList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(asm.GetType("DeskSpace.PinnedAppFileItemV380", true)!))!;
            foreach (string cd in Directory.EnumerateDirectories(dataRoot))
            {
                object?[] pa = { cd, null, false, "Tally Data" };
                if ((bool)tryParseTally.Invoke(null, pa)! && pa[1] != null)
                    itemList.Add(pa[1]!);
            }
            var merged7 = ((IEnumerable)mergeMethod.Invoke(null, new object[] { itemList, Array.CreateInstance(asm.GetType("DeskSpace.PinnedAppFileItemV380", true)!, 0), 250 })!).Cast<object>().ToList();
            Check(merged7.Count == 7 && GetProp<string>(merged7[0], "DisplayName").StartsWith("Bulk Enterprise ", StringComparison.Ordinal),
                "Quick-arc ranking must retain all 7+ compatible items with resolved in-app names");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void TestApplicationsDepth2ScrollAndHiddenCountersV380(Assembly asm)
    {
        var appsType = asm.GetType("DeskSpace.DeskSpaceApplicationsV55", true)!;
        var itemType = asm.GetType("DeskSpace.PinnedAppFileItemV380", true)!;
        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_Scroll_" + Guid.NewGuid().ToString("N"));
        var owner = new Window
        {
            Width = 408,
            Height = 408,
            Left = 1512,
            Top = 423,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };

        object? apps = null;
        try
        {
            Directory.CreateDirectory(tempRoot);
            string appOne = Path.Combine(tempRoot, "Winman CA ERP.exe");
            string appTwo = Path.Combine(tempRoot, "TallyPrime.exe");
            File.WriteAllText(appOne, "mock");
            File.WriteAllText(appTwo, "mock");

            var pinnedList = new[] { appOne, appTwo };
            Func<IReadOnlyList<string>> getPinned = () => pinnedList;
            Action<string> noopPin = _ => { };
            Action noopSearch = () => { };

            apps = Activator.CreateInstance(appsType, new object[] { owner, getPinned, noopPin, noopPin, noopSearch })!;
            appsType.GetField("group", AnyInstance)!.SetValue(apps, "pinned");
            appsType.GetField("activeAppPath", AnyInstance)!.SetValue(apps, appOne);
            appsType.GetField("activeAppCenter", AnyInstance)!.SetValue(apps, 166.0);

            var filesList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
            for (int i = 0; i < 12; i++)
            {
                filesList.Add(Activator.CreateInstance(itemType, new object?[]
                {
                    $"Company Workspace {i}",
                    Path.Combine(tempRoot, $"item_{i}.tax"),
                    tempRoot,
                    ".TAX",
                    DateTime.UtcNow.AddMinutes(-i),
                    i < 3,
                    i < 3 ? "★ Recent" : "Drive Scan",
                    false,
                    null,
                    null
                })!);
            }
            appsType.GetField("activeAppFiles", AnyInstance)!.SetValue(apps, filesList);

            var renderMethod = appsType.GetMethod("Render", AnyInstance)!;
            var moveFilesMethod = appsType.GetMethod("MoveFilesV380", AnyInstance)!;
            var filePageField = appsType.GetField("filePage", AnyInstance)!;
            var canvas = (Canvas)appsType.GetField("canvas", AnyInstance)!.GetValue(apps)!;

            // Page 0: items 0..3 + All files…, trailing counter = 8
            renderMethod.Invoke(apps, null);
            Check((int)filePageField.GetValue(apps)! == 0, "Initial filePage must start at 0");
            var dividerButtons = canvas.Children.OfType<Button>().Select(b => b.Content?.ToString()).OrderBy(x => x).ToArray();
            var inlinePaths = canvas.Children.OfType<System.Windows.Shapes.Path>()
                .Where(p => Equals(p.Tag, "DeskSpaceHiddenCountInline")).ToArray();
            Check(dividerButtons.SequenceEqual(new[] { "8" }) && inlinePaths.Length == 1,
                $"Page 0 with 12 items must render single hidden counter '8', got [{string.Join(",", dividerButtons)}]");

            var visibleLabels = canvas.Children.OfType<TextBlock>().Select(tb => tb.Text.Replace("\u200B", "")).ToHashSet();
            Check(visibleLabels.Contains("Company Workspace 0") &&
                  visibleLabels.Contains("Company Workspace 3") &&
                  !visibleLabels.Contains("Company Workspace 4") &&
                  visibleLabels.Contains("All files…"),
                "Page 0 must display items 0..3 and All files…");

            // Bounds and Z-order sanity (excluding divider overlay strokes)
            int maxWedgeZ = canvas.Children.OfType<System.Windows.Shapes.Path>()
                .Where(p => p.Fill != null && p.Tag is int)
                .Max(System.Windows.Controls.Panel.GetZIndex);
            foreach (var tb in canvas.Children.OfType<TextBlock>())
            {
                double l = Canvas.GetLeft(tb);
                Check(l >= 0 && l + tb.Width <= canvas.Width + 0.5 &&
                      System.Windows.Controls.Panel.GetZIndex(tb) > maxWedgeZ,
                    $"Scrolled label '{tb.Text}' out of bounds or below wedges");
            }

            // Scroll forward by +1 -> filePage = 1, items 1..4, divider buttons "1" and "7"
            moveFilesMethod.Invoke(apps, new object[] { 1 });
            Check((int)filePageField.GetValue(apps)! == 1, "MoveFilesV380(+1) must advance filePage to 1");
            dividerButtons = canvas.Children.OfType<Button>().Select(b => b.Content?.ToString()).OrderBy(x => x).ToArray();
            Check(dividerButtons.SequenceEqual(new[] { "1", "7" }),
                $"Page 1 with 12 items must render hidden counters '1' and '7', got [{string.Join(",", dividerButtons)}]");
            visibleLabels = canvas.Children.OfType<TextBlock>().Select(tb => tb.Text.Replace("\u200B", "")).ToHashSet();
            Check(!visibleLabels.Contains("Company Workspace 0") &&
                  visibleLabels.Contains("Company Workspace 1") &&
                  visibleLabels.Contains("Company Workspace 4"),
                "Page 1 must shift visible slice to items 1..4");

            // Scroll to end -> clamps to filePage = 8 (12 - 4), items 8..11, divider button "8"
            moveFilesMethod.Invoke(apps, new object[] { 100 });
            Check((int)filePageField.GetValue(apps)! == 8, "MoveFilesV380(+100) must clamp filePage to 8");
            dividerButtons = canvas.Children.OfType<Button>().Select(b => b.Content?.ToString()).ToArray();
            Check(dividerButtons.SequenceEqual(new[] { "8" }),
                $"Final page with 12 items must render single leading hidden counter '8', got [{string.Join(",", dividerButtons)}]");
            visibleLabels = canvas.Children.OfType<TextBlock>().Select(tb => tb.Text.Replace("\u200B", "")).ToHashSet();
            Check(visibleLabels.Contains("Company Workspace 8") && visibleLabels.Contains("Company Workspace 11"),
                "Final page must display items 8..11");

            // Scroll back to start -> clamps to filePage = 0
            moveFilesMethod.Invoke(apps, new object[] { -100 });
            Check((int)filePageField.GetValue(apps)! == 0, "MoveFilesV380(-100) must clamp back to 0");
        }
        finally
        {
            (apps as IDisposable)?.Dispose();
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void TestStrictPerAppIsolationAndNoMixingV380(Assembly asm)
    {
        var type = asm.GetType("DeskSpace.DeskSpacePinnedAppFilesV380", true)!;
        var itemType = asm.GetType("DeskSpace.PinnedAppFileItemV380", true)!;
        var resolveCap = type.GetMethod("ResolveCapability", AnyStatic)!;
        var isStrictlyCompat = type.GetMethod("IsItemStrictlyCompatible", AnyStatic)!;
        var getHistoryKey = type.GetMethod("GetHistoryKey", AnyStatic)!;
        var discoverRecent = type.GetMethod("DiscoverRecentItems", AnyStatic)!;
        var recordLaunch = type.GetMethod("RecordLaunch", AnyStatic)!;
        var tryParseTally = type.GetMethod("TryParseTallyCompanyFolder", AnyStatic)!;
        var tryParseLnk = type.GetMethod("TryParseShortcutTargetBinary", AnyStatic, new[] { typeof(string) })!;

        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_Isolation_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            string primeDir = Path.Combine(tempRoot, "TallyPrime");
            string erpDir = Path.Combine(tempRoot, "Tally.ERP9");
            string comp1800Dir = Path.Combine(tempRoot, "Data", "100001");
            string comp900Dir = Path.Combine(tempRoot, "Data", "10001");
            string winmanDir = Path.Combine(tempRoot, "Winman");
            Directory.CreateDirectory(primeDir);
            Directory.CreateDirectory(erpDir);
            Directory.CreateDirectory(comp1800Dir);
            Directory.CreateDirectory(comp900Dir);
            Directory.CreateDirectory(winmanDir);

            string primeExe = Path.Combine(primeDir, "tally.exe");
            string erpExe = Path.Combine(erpDir, "tally.exe");
            File.WriteAllText(primeExe, "mock");
            File.WriteAllText(erpExe, "mock");

            File.WriteAllText(Path.Combine(comp1800Dir, "Company.1800"), "mock");
            File.WriteAllText(Path.Combine(comp1800Dir, "CmpSave.1800"), "mock");
            File.WriteAllText(Path.Combine(comp900Dir, "Company.900"), "mock");
            File.WriteAllText(Path.Combine(comp900Dir, "Manager.900"), "mock");

            object?[] aPrimeComp = { comp1800Dir, null, true, "Tally Data" };
            Check((bool)tryParseTally.Invoke(null, aPrimeComp)! && aPrimeComp[1] != null,
                "Synthetic .1800 company fixture must parse");
            object primeCompItem = aPrimeComp[1]!;

            object?[] aErpComp = { comp900Dir, null, true, "Tally Data" };
            Check((bool)tryParseTally.Invoke(null, aErpComp)! && aErpComp[1] != null,
                "Synthetic .900 company fixture must parse");
            object erpCompItem = aErpComp[1]!;

            var primeCap = resolveCap.Invoke(null, new object[] { primeExe })!;
            var erpCap = resolveCap.Invoke(null, new object[] { erpExe })!;
            Check(GetProp<object>(primeCap, "TallyEdition").ToString() == "TallyPrime1800",
                "TallyPrime install path must resolve TallyPrime1800 edition");
            Check(GetProp<object>(erpCap, "TallyEdition").ToString() == "TallyErp900",
                "Tally.ERP9 install path must resolve TallyErp900 edition");

            // Strict edition isolation: Prime rejects .900 & .xml; ERP 9 rejects .1800 & .xml
            string strayXml = Path.Combine(winmanDir, "winman_config.xml");
            File.WriteAllText(strayXml, "<cfg/>");
            var xmlItem = Activator.CreateInstance(itemType, new object?[]
            {
                "winman_config", strayXml, winmanDir, ".XML", DateTime.UtcNow, false, "Drive Scan", false, null, null
            })!;

            Check((bool)isStrictlyCompat.Invoke(null, new object[] { primeCap, primeCompItem, false })!,
                "TallyPrime must accept .1800 company");
            Check(!(bool)isStrictlyCompat.Invoke(null, new object[] { primeCap, erpCompItem, false })!,
                "TallyPrime must reject .900 company");
            Check(!(bool)isStrictlyCompat.Invoke(null, new object[] { primeCap, xmlItem, false })!,
                "TallyPrime must reject stray .xml files");

            Check((bool)isStrictlyCompat.Invoke(null, new object[] { erpCap, erpCompItem, false })!,
                "Tally.ERP 9 must accept .900 company");
            Check(!(bool)isStrictlyCompat.Invoke(null, new object[] { erpCap, primeCompItem, false })!,
                "Tally.ERP 9 must reject .1800 company");
            Check(!(bool)isStrictlyCompat.Invoke(null, new object[] { erpCap, xmlItem, false })!,
                "Tally.ERP 9 must reject stray .xml files");

            // Per-installation history keys prevent same-exe-name collisions (tally.exe in two folders)
            string primeHistKey = (string)getHistoryKey.Invoke(null, new object[] { primeCap })!;
            string erpHistKey = (string)getHistoryKey.Invoke(null, new object[] { erpCap })!;
            Check(!string.Equals(primeHistKey, erpHistKey, StringComparison.OrdinalIgnoreCase),
                "Separate TallyPrime and Tally.ERP 9 installs must use distinct history keys");

            string histFile = Path.Combine(tempRoot, "launches.json");
            recordLaunch.Invoke(null, new object?[] { primeHistKey, comp1800Dir, histFile });
            var erpRecent = ((IEnumerable)discoverRecent.Invoke(null, new object?[] { erpCap, histFile })!).Cast<object>().ToList();
            Check(erpRecent.All(x => GetProp<string>(x, "FullPath") != comp1800Dir),
                "Launch history recorded for TallyPrime must never appear under Tally.ERP 9");

            // Excel must not pull .xml or Winman .tax files
            var excelCap = resolveCap.Invoke(null, new object[] { @"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE" })!;
            var excelExts = GetProp<HashSet<string>>(excelCap, "Extensions");
            Check(!excelExts.Contains(".xml"), "Excel capability must not include noisy .xml extension");
            Check(!(bool)isStrictlyCompat.Invoke(null, new object[] { excelCap, xmlItem, false })!,
                "Excel must reject stray .xml files");

            // Winman rejects backup-only .tax.wb in Quick Arc (allowBackupArchives: false) while keeping .tax
            string winmanExe = Path.Combine(winmanDir, "Winman_Launcher.exe");
            File.WriteAllText(winmanExe, "mock");
            var winmanCap = resolveCap.Invoke(null, new object[] { winmanExe })!;
            string taxFile = Path.Combine(tempRoot, "Client_25_26.tax");
            string taxBackup = Path.Combine(tempRoot, "Client_25_26.tax.wb");
            File.WriteAllText(taxFile, "tax");
            File.WriteAllText(taxBackup, "wb");
            var taxItem = Activator.CreateInstance(itemType, new object?[]
            {
                "Client 25 26", taxFile, tempRoot, ".TAX", DateTime.UtcNow, false, "Drive Scan", false, null, null
            })!;
            var backupItem = Activator.CreateInstance(itemType, new object?[]
            {
                "Client 25 26", taxBackup, tempRoot, "Winman Tax Backup", DateTime.UtcNow, false, "Drive Scan", false, null, null
            })!;
            Check((bool)isStrictlyCompat.Invoke(null, new object[] { winmanCap, taxItem, false })!,
                "Winman Quick Arc must accept direct .tax return file");
            Check(!(bool)isStrictlyCompat.Invoke(null, new object[] { winmanCap, backupItem, false })!,
                "Winman Quick Arc must filter out .tax.wb backup archives");
            Check((bool)isStrictlyCompat.Invoke(null, new object[] { winmanCap, backupItem, true })!,
                "Winman full browser (allowBackupArchives=true) retains .tax.wb backup archives");

            // Managed Shell Link binary parser (.lnk local vs network share)
            static byte[] BuildMinimalLnk(string targetPath, bool networkFlag)
            {
                using var ms = new MemoryStream();
                byte[] header = new byte[0x4C];
                BitConverter.GetBytes(0x4C).CopyTo(header, 0);
                byte[] clsid = { 0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46 };
                clsid.CopyTo(header, 4);
                BitConverter.GetBytes(0x02u).CopyTo(header, 20); // HasLinkInfo
                ms.Write(header);

                byte[] pathBytes = Encoding.ASCII.GetBytes(targetPath + "\0");
                int linkInfoHeaderSize = 0x1C;
                int localBasePathOffset = networkFlag ? 0 : linkInfoHeaderSize + 1;
                int linkInfoSize = linkInfoHeaderSize + 1 + pathBytes.Length + 1;
                byte[] info = new byte[linkInfoSize];
                BitConverter.GetBytes(linkInfoSize).CopyTo(info, 0);
                BitConverter.GetBytes(linkInfoHeaderSize).CopyTo(info, 4);
                BitConverter.GetBytes(networkFlag ? 0x02 : 0x01).CopyTo(info, 8);
                BitConverter.GetBytes(localBasePathOffset).CopyTo(info, 16);
                BitConverter.GetBytes(linkInfoSize - 1).CopyTo(info, 24);
                if (!networkFlag)
                    pathBytes.CopyTo(info, localBasePathOffset);
                ms.Write(info);
                return ms.ToArray();
            }

            string localLnk = Path.Combine(tempRoot, "local.lnk");
            string netLnk = Path.Combine(tempRoot, "net.lnk");
            File.WriteAllBytes(localLnk, BuildMinimalLnk(taxFile, networkFlag: false));
            File.WriteAllBytes(netLnk, BuildMinimalLnk(@"\\SERVER\Share\file.xlsx", networkFlag: true));
            Check(string.Equals((string?)tryParseLnk.Invoke(null, new object[] { localLnk }), Path.GetFullPath(taxFile), StringComparison.OrdinalIgnoreCase),
                "TryParseShortcutTargetBinary must decode local .lnk target path cleanly");
            Check(tryParseLnk.Invoke(null, new object[] { netLnk }) == null,
                "TryParseShortcutTargetBinary must skip network/UNC links");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void TestQuickArcNoStuckLoadingAndIsolationV380(Assembly asm)
    {
        var appsType = asm.GetType("DeskSpace.DeskSpaceApplicationsV55", true)!;
        var itemType = asm.GetType("DeskSpace.PinnedAppFileItemV380", true)!;
        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_Loading_" + Guid.NewGuid().ToString("N"));
        var owner = new Window
        {
            Width = 408,
            Height = 408,
            Left = 1512,
            Top = 423,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };

        object? apps = null;
        try
        {
            Directory.CreateDirectory(tempRoot);
            string appA = Path.Combine(tempRoot, "AppAlpha.exe");
            string appB = Path.Combine(tempRoot, "AppBeta.exe");
            File.WriteAllText(appA, "mock");
            File.WriteAllText(appB, "mock");

            var pinnedList = new[] { appA, appB };
            apps = Activator.CreateInstance(appsType, new object[]
            {
                owner,
                (Func<IReadOnlyList<string>>)(() => pinnedList),
                (Action<string>)(_ => { }),
                (Action<string>)(_ => { }),
                (Action)(() => { })
            })!;

            var canvas = (Canvas)appsType.GetField("canvas", AnyInstance)!.GetValue(apps)!;
            var renderMethod = appsType.GetMethod("Render", AnyInstance)!;

            // 1. While activeAppLoading == true and 0 files, show Loading…
            appsType.GetField("group", AnyInstance)!.SetValue(apps, "pinned");
            appsType.GetField("activeAppPath", AnyInstance)!.SetValue(apps, appA);
            appsType.GetField("activeAppCenter", AnyInstance)!.SetValue(apps, 166.0);
            appsType.GetField("activeAppLoading", AnyInstance)!.SetValue(apps, true);
            renderMethod.Invoke(apps, null);
            var loadingLabels = canvas.Children.OfType<TextBlock>().Select(tb => tb.Text.Replace("\u200B", "")).ToHashSet();
            Check(loadingLabels.Contains("Loading…") && loadingLabels.Contains("All files…"),
                "Depth-2 must display Loading… while scan is active");

            // 2. When activeAppLoading == false and still 0 files, show No files found (never stuck on Loading…)
            appsType.GetField("activeAppLoading", AnyInstance)!.SetValue(apps, false);
            renderMethod.Invoke(apps, null);
            var emptyLabels = canvas.Children.OfType<TextBlock>().Select(tb => tb.Text.Replace("\u200B", "")).ToHashSet();
            Check(!emptyLabels.Contains("Loading…") && emptyLabels.Contains("No files found") && emptyLabels.Contains("All files…"),
                "Depth-2 must transition from Loading… to No files found when scan completes with 0 items");

            // 3. Scroll offset resets when switching apps
            var filesList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
            for (int i = 0; i < 6; i++)
            {
                filesList.Add(Activator.CreateInstance(itemType, new object?[]
                {
                    $"Doc {i}", Path.Combine(tempRoot, $"d{i}.xlsx"), tempRoot, ".XLSX",
                    DateTime.UtcNow.AddMinutes(-i), true, "★ Recent", false, null, null
                })!);
            }
            appsType.GetField("activeAppFiles", AnyInstance)!.SetValue(apps, filesList);
            appsType.GetField("filePage", AnyInstance)!.SetValue(apps, 2);
            appsType.GetMethod("ClearActiveAppFilesV380", AnyInstance)!.Invoke(apps, null);
            Check((int)appsType.GetField("filePage", AnyInstance)!.GetValue(apps)! == 0 &&
                  string.IsNullOrEmpty((string)appsType.GetField("activeAppPath", AnyInstance)!.GetValue(apps)!),
                "ClearActiveAppFilesV380 must clear active app path and reset filePage");
        }
        finally
        {
            (apps as IDisposable)?.Dispose();
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void TestTallyErp9FixesAndUniversalDisambiguationV380(Assembly asm)
    {
        var type = asm.GetType("DeskSpace.DeskSpacePinnedAppFilesV380", true)!;
        var itemType = asm.GetType("DeskSpace.PinnedAppFileItemV380", true)!;
        var tryParseTally = type.GetMethod("TryParseTallyCompanyFolder", AnyStatic)!;
        var tryReadName = type.GetMethod("TryReadTallyCompanyName", AnyStatic)!;
        var resolveCap = type.GetMethod("ResolveCapability", AnyStatic)!;
        var buildLaunch = type.GetMethod("BuildLaunchInfo", AnyStatic)!;
        var mergeMethod = type.GetMethod("MergeAndRank", AnyStatic)!;
        var resolveAppName = type.GetMethod("ResolvePinnedAppDisplayName", AnyStatic)!;

        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_Erp9Fixes_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);

            // 1. Reject non-numeric backup/renamed .900 and out-of-range numeric folders; accept 4..6 digit numeric folders + CmpSave.900
            string badNonNumeric = Path.Combine(tempRoot, "Backup Copy 24-25");
            string badShortNumeric = Path.Combine(tempRoot, "123");
            string badLongNumeric = Path.Combine(tempRoot, "1234567");
            string goodErpCmpSave = Path.Combine(tempRoot, "10005");
            Directory.CreateDirectory(badNonNumeric);
            Directory.CreateDirectory(badShortNumeric);
            Directory.CreateDirectory(badLongNumeric);
            Directory.CreateDirectory(goodErpCmpSave);

            foreach (string dir in new[] { badNonNumeric, badShortNumeric, badLongNumeric })
            {
                File.WriteAllText(Path.Combine(dir, "Company.900"), "mock");
                File.WriteAllText(Path.Combine(dir, "Manager.900"), "mock");
                object?[] parseArgs = { dir, null, false, "Tally Data" };
                Check(!(bool)tryParseTally.Invoke(null, parseArgs)!,
                    $"TryParseTallyCompanyFolder must reject non-loadable folder '{Path.GetFileName(dir)}'");
            }

            File.WriteAllText(Path.Combine(goodErpCmpSave, "Company.900"), "mock");
            File.WriteAllText(Path.Combine(goodErpCmpSave, "CmpSave.900"), "mock");
            object?[] goodArgs = { goodErpCmpSave, null, false, "Tally Data" };
            Check((bool)tryParseTally.Invoke(null, goodArgs)! &&
                  goodArgs[1] != null &&
                  GetProp<string>(goodArgs[1]!, "TallyCompanyCode") == "10005" &&
                  GetProp<string>(goodArgs[1]!, "KindBadge") == "Tally.ERP 9 · #10005",
                "TryParseTallyCompanyFolder must accept 5-digit .900 folder with Company.900 + CmpSave.900");

            // 2. 512-byte paged Company.900 decoding across 0x200 / 0x400 16-byte page headers
            static byte[] BuildPagedCompany900(string primaryName, string mailingName, int payloadStartInUnpaged)
            {
                byte[] tag1B = { 0x02, 0x10, 0x1B, 0x00, 0x00, 0x0F };
                byte[] tag1C = { 0x02, 0x10, 0x1C, 0x00, 0x00, 0x0F };
                byte[] nameBytes = Encoding.Unicode.GetBytes(primaryName + "\0");
                byte[] mailBytes = Encoding.Unicode.GetBytes(mailingName + "\0");

                byte[] unpaged = new byte[4 * 496];
                int pos = payloadStartInUnpaged;
                Buffer.BlockCopy(tag1B, 0, unpaged, pos, tag1B.Length);
                pos += tag1B.Length;
                BitConverter.GetBytes((ushort)nameBytes.Length).CopyTo(unpaged, pos);
                pos += 2;
                Buffer.BlockCopy(nameBytes, 0, unpaged, pos, nameBytes.Length);
                pos += nameBytes.Length;

                Buffer.BlockCopy(tag1C, 0, unpaged, pos, tag1C.Length);
                pos += tag1C.Length;
                BitConverter.GetBytes((ushort)mailBytes.Length).CopyTo(unpaged, pos);
                pos += 2;
                Buffer.BlockCopy(mailBytes, 0, unpaged, pos, mailBytes.Length);

                // Repack into 4 x 512-byte pages with realistic 16-byte page headers containing 0x01 control bytes
                byte[] paged = new byte[4 * 512];
                for (int p = 0; p < 4; p++)
                {
                    int h = p * 512;
                    paged[h + 0] = 0x3E;
                    paged[h + 1] = 0x68;
                    paged[h + 2] = 0x69;
                    paged[h + 3] = 0xD9;
                    paged[h + 4] = 0x01; // control char byte that corrupts raw UTF-16LE reads if not unpaged
                    paged[h + 8] = 0x06;
                    paged[h + 12] = (byte)(p + 1);
                    Buffer.BlockCopy(unpaged, p * 496, paged, h + 16, 496);
                }
                return paged;
            }

            // Case A: UTF-16LE company name string straddles the 0x400 (page 1 -> page 2) boundary (unpaged offset 2*496 - 30 = 962)
            string pagedDirA = Path.Combine(tempRoot, "10014");
            Directory.CreateDirectory(pagedDirA);
            File.WriteAllBytes(Path.Combine(pagedDirA, "Company.900"),
                BuildPagedCompany900("Sri Vigneshwara Enterprises 25-26", "Fallback Mailing Only", 2 * 496 - 30));
            File.WriteAllBytes(Path.Combine(pagedDirA, "Manager.900"), new byte[] { 1, 2, 3, 4 });
            string? resolvedA = (string?)tryReadName.Invoke(null, new object[] { pagedDirA });
            Check(resolvedA == "Sri Vigneshwara Enterprises 25-26",
                $"512-byte paged Company.900 straddling 0x400 must recover primary 1B name, got '{resolvedA}'");

            // Case B: The 6-byte 02-10-1B-00-00-0F tag ITSELF is split across the page boundary (unpaged offset 2*496 - 3 = 989)
            string pagedDirB = Path.Combine(tempRoot, "10015");
            Directory.CreateDirectory(pagedDirB);
            File.WriteAllBytes(Path.Combine(pagedDirB, "Company.900"),
                BuildPagedCompany900("Kaveri Exports & Imports FY26", "Wrong Mailing Fallback", 2 * 496 - 3));
            File.WriteAllBytes(Path.Combine(pagedDirB, "Manager.900"), new byte[] { 1, 2, 3, 4 });
            string? resolvedB = (string?)tryReadName.Invoke(null, new object[] { pagedDirB });
            Check(resolvedB == "Kaveri Exports & Imports FY26",
                $"512-byte paged Company.900 with split 1B tag across page boundary must recover primary 1B name, got '{resolvedB}'");

            // 3. BuildLaunchInfo always includes /NOINILOAD when overriding TallyDataRoot and normalizes trailing slashes
            string erpDir = Path.Combine(tempRoot, "Tally.ERP9");
            Directory.CreateDirectory(erpDir);
            string erpExe = Path.Combine(erpDir, "tally.exe");
            File.WriteAllText(erpExe, "mock");
            var erpCap = resolveCap.Invoke(null, new object[] { erpExe })!;

            var itemWithCode = Activator.CreateInstance(itemType, new object?[]
            {
                "Apex Traders", pagedDirA, tempRoot + @"\", "Tally.ERP 9 · #10014",
                DateTime.UtcNow, true, "★ Tally Recent", true, "10014", tempRoot + @"\"
            })!;
            object?[] launch1 = { erpCap, itemWithCode, null, null, new List<string> { @"D:\25-26" } };
            Check((bool)buildLaunch.Invoke(null, launch1)!, "BuildLaunchInfo must succeed for Tally.ERP 9 company");
            var psi1 = (ProcessStartInfo)launch1[2]!;
            Check(psi1.ArgumentList.Count == 3 &&
                  psi1.ArgumentList[0] == tempRoot.TrimEnd('\\') &&
                  psi1.ArgumentList[1] == "/NOINILOAD" &&
                  psi1.ArgumentList[2] == "/LOAD:10014",
                $"BuildLaunchInfo must pass trimmed data root + /NOINILOAD + /LOAD:10014, got [{string.Join(", ", psi1.ArgumentList)}]");

            var itemWithoutCode = Activator.CreateInstance(itemType, new object?[]
            {
                "Apex Traders", pagedDirA, tempRoot, "Tally.ERP 9",
                DateTime.UtcNow, false, "Tally Data", true, null, tempRoot
            })!;
            object?[] launch2 = { erpCap, itemWithoutCode, null, null, new List<string> { @"D:\25-26" } };
            Check((bool)buildLaunch.Invoke(null, launch2)!, "BuildLaunchInfo fallback without code must succeed");
            var psi2 = (ProcessStartInfo)launch2[2]!;
            Check(psi2.ArgumentList.Contains("/NOINILOAD"),
                "BuildLaunchInfo with overridden TallyDataRoot must always pass /NOINILOAD even if code is absent");

            // 4. Universal duplicate display-name disambiguation across folders and same-folder company codes
            var dupList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
            DateTime t0 = DateTime.UtcNow;
            dupList.Add(Activator.CreateInstance(itemType, new object?[]
            {
                "Apex Traders", @"F:\Clients\FY25-26\10005", @"F:\Clients\FY25-26", "Tally.ERP 9 · #10005",
                t0, true, "★ Recent", true, "10005", @"F:\Clients\FY25-26"
            })!);
            dupList.Add(Activator.CreateInstance(itemType, new object?[]
            {
                "Apex Traders", @"F:\Clients\FY24-25\10005", @"F:\Clients\FY24-25", "Tally.ERP 9 · #10005",
                t0.AddMinutes(-5), false, "Drive Scan", true, "10005", @"F:\Clients\FY24-25"
            })!);
            dupList.Add(Activator.CreateInstance(itemType, new object?[]
            {
                "Zenith Corp", @"F:\SharedData\10001", @"F:\SharedData", "Tally.ERP 9 · #10001",
                t0.AddMinutes(-10), false, "Drive Scan", true, "10001", @"F:\SharedData"
            })!);
            dupList.Add(Activator.CreateInstance(itemType, new object?[]
            {
                "Zenith Corp", @"F:\SharedData\10009", @"F:\SharedData", "Tally.ERP 9 · #10009",
                t0.AddMinutes(-15), false, "Drive Scan", true, "10009", @"F:\SharedData"
            })!);
            dupList.Add(Activator.CreateInstance(itemType, new object?[]
            {
                "Unique Holdings", @"F:\SharedData\10020", @"F:\SharedData", "Tally.ERP 9 · #10020",
                t0.AddMinutes(-20), false, "Drive Scan", true, "10020", @"F:\SharedData"
            })!);

            var emptyList = Array.CreateInstance(itemType, 0);
            var rankedDups = ((IEnumerable)mergeMethod.Invoke(null, new object[] { dupList, emptyList, 100 })!).Cast<object>().ToList();
            var names = rankedDups.Select(x => GetProp<string>(x, "DisplayName")).ToList();
            Check(names.Count == 5 &&
                  names[0] == "Apex Traders · FY25-26" &&
                  names[1] == "Apex Traders · FY24-25" &&
                  names[2].Contains("#10001", StringComparison.Ordinal) &&
                  names[3].Contains("#10009", StringComparison.Ordinal) &&
                  names[4] == "Unique Holdings",
                $"MergeAndRank must disambiguate duplicate names by parent folder or company code while leaving unique names untouched, got [{string.Join(" | ", names)}]");

            // 5. Edition-accurate ResolvePinnedAppDisplayName when pinning tally.exe directly
            string primeDir = Path.Combine(tempRoot, "TallyPrime");
            Directory.CreateDirectory(primeDir);
            string primeExe = Path.Combine(primeDir, "tally.exe");
            File.WriteAllText(primeExe, "mock");
            Check((string)resolveAppName.Invoke(null, new object[] { erpExe })! == "Tally.ERP 9" &&
                  (string)resolveAppName.Invoke(null, new object[] { primeExe })! == "TallyPrime",
                "ResolvePinnedAppDisplayName must distinguish Tally.ERP 9 vs TallyPrime even for direct tally.exe paths");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void TestApplicationFileHeaderValueInformationV380(Assembly asm)
    {
        var tabType = asm.GetType("DeskSpace.DeskSpaceInformationTabV380", true)!;
        var evalElig = tabType.GetMethod("EvaluateAppEligibility", AnyStatic)!;
        var isValidPair = tabType.GetMethod("IsValidHeaderValuePair", AnyStatic)!;
        var buildWinmanRec = tabType.GetMethod("BuildSyntheticWinmanRecord", AnyStatic)!;
        var discoverInfo = tabType.GetMethod("DiscoverAppInformation", AnyStatic)!;
        var savePref = tabType.GetMethod("SaveVisibleHeadersPreference", AnyStatic)!;
        var getVisibleFields = tabType.GetMethod("GetVisibleFieldsForFile", AnyStatic)!;

        // 1. App Eligibility: Structured Header->Value apps vs Unstructured prose/media apps
        var wordElig = evalElig.Invoke(null, new object[] { @"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE" })!;
        Check(!GetProp<bool>(wordElig, "IsSupported") &&
              GetProp<string>(wordElig, "Reason").Contains("prose", StringComparison.OrdinalIgnoreCase),
            "Microsoft Word must be rejected as an unstructured prose application");

        var pptElig = evalElig.Invoke(null, new object[] { @"C:\Program Files\Microsoft Office\root\Office16\POWERPNT.EXE" })!;
        Check(!GetProp<bool>(pptElig, "IsSupported"),
            "PowerPoint must be rejected as an unstructured presentation application");

        var vlcElig = evalElig.Invoke(null, new object[] { @"C:\Program Files\VideoLAN\VLC\vlc.exe" })!;
        Check(!GetProp<bool>(vlcElig, "IsSupported"),
            "VLC must be rejected as a media player application");

        var wgstElig = evalElig.Invoke(null, new object[] { @"C:\StartMenu\Winman GST.lnk" })!;
        Check(GetProp<bool>(wgstElig, "IsSupported") &&
              GetProp<string>(wgstElig, "AppDisplayName") == "Winman GST" &&
              GetProp<IReadOnlyList<string>>(wgstElig, "SupportedExtensions").Contains(".wgst"),
            "Winman GST must be eligible as a structured Header -> Value application (.wgst)");

        var caErpElig = evalElig.Invoke(null, new object[] { @"C:\StartMenu\Winman CA ERP.lnk" })!;
        Check(GetProp<bool>(caErpElig, "IsSupported") &&
              GetProp<IReadOnlyList<string>>(caErpElig, "SupportedExtensions").Contains(".tax"),
            "Winman CA ERP must be eligible as a structured Header -> Value application (.tax)");

        var excelElig = evalElig.Invoke(null, new object[] { @"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE" })!;
        Check(GetProp<bool>(excelElig, "IsSupported") &&
              GetProp<IReadOnlyList<string>>(excelElig, "SupportedExtensions").Contains(".xlsx"),
            "Microsoft Excel must be eligible as a structured Header -> Value application");

        // 2. Strict Header -> Value pair validation (rejects random prose paragraphs, paths, and placeholders)
        Check((bool)isValidPair.Invoke(null, new object?[] { "GSTIN", "29AABCA1234F1Z5" })! &&
              !(bool)isValidPair.Invoke(null, new object?[] { "12345", "Value" })! &&
              !(bool)isValidPair.Invoke(null, new object?[] { "Notes", "This is a very long random unstructured paragraph of prose text from a document that does not represent a concise header value field at all and exceeds word limits." })! &&
              !(bool)isValidPair.Invoke(null, new object?[] { "Tax", "`k`CurCellVal`k`" })! &&
              !(bool)isValidPair.Invoke(null, new object?[] { "Status", "NA" })!,
            "IsValidHeaderValuePair must accept concise Header->Value pairs and reject random prose or internal placeholders");

        // 3. Inside-file Winman GST (.wgst) binary + embedded JSON decoding & >= 2 matching files rule
        string tempRoot = Path.Combine(Path.GetTempPath(), "DeskSpace_R380_AppInfo_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            string prefPath = Path.Combine(tempRoot, "app-info-prefs.json");
            string wgstLnk = Path.Combine(tempRoot, "Winman GST.lnk");
            File.WriteAllText(wgstLnk, "mock");

            byte[] WriteWgstBytes(
                string legalName,
                string tradeName,
                string gstin,
                string fy,
                string dealerType,
                string constitution,
                string returnForm,
                string mobile,
                string? extraOnlyOne = null)
            {
                var row0 = new string?[35];
                row0[0] = "CLIENT-" + constitution;
                row0[4] = "10042025120000000000";
                row0[5] = "2";
                row0[6] = "23112026183000000000";
                row0[8] = "Original";
                row0[9] = "23112026183000000000";
                row0[15] = legalName;
                row0[16] = "0";
                row0[17] = dealerType;
                row0[22] = gstin;
                row0[23] = fy;
                row0[26] = constitution;
                row0[27] = "Version-8.9.7";
                row0[29] = returnForm;
                row0[30] = "1526";
                row0[31] = constitution;
                row0[33] = mobile;

                var row1 = new string?[35];
                row1[0] = "head";
                row1[10] = "0-0-0-0-0-0-0-0-1-";

                var row2 = new string?[35];
                row2[0] = "MAIN";
                row2[2] = legalName;
                row2[4] = fy;
                row2[10] = "0.01.00.00.00.00";
                string qrJson = extraOnlyOne != null ? $"\"Quarterly_Return\":\"{extraOnlyOne}\"," : "\"Quarterly_Return\":\" \",";
                row2[30] = $"{{\"Legal_name\":\"{legalName}\",\"Taxpayer_type\":\"{dealerType}\",\"GSTIN\":\"{gstin}\",\"Trade_Name\":\"{tradeName}\",\"State\":\"GSTIN\",\"Mobile_number\":\"{mobile}\",\"EMail_ID\":\"info@example.in\",{qrJson}\"Fin_Year\":\"{fy}\",\"MISPriority\":\"Original\",\"Created_by_PC_User\":\"CA-ADMIN\"}}";

                var rowCmpMfg = new string?[35];
                rowCmpMfg[0] = "GSTComposition+ 3";
                rowCmpMfg[1] = "GSTR_CMP_Quarter+1";
                rowCmpMfg[2] = "Manufacturer / Supplier (other than below)";
                rowCmpMfg[3] = "1.00";
                rowCmpMfg[4] = "1";
                rowCmpMfg[5] = "1,56,300.00";
                rowCmpMfg[7] = @"\781.50\781.50\\1,563.00\";
                rowCmpMfg[10] = "5.30.08.00.00.00";
                rowCmpMfg[24] = "UNHIDE";

                var rowCmpTax = new string?[35];
                rowCmpTax[0] = "GSTComposition+ 3";
                rowCmpTax[1] = "GSTR_CMP_Quarter+1";
                rowCmpTax[2] = "Total Tax payable";
                rowCmpTax[6] = "14,600.00";
                rowCmpTax[7] = @"\18,731.50\18,731.50\1,000.00\53,063.00\";
                rowCmpTax[10] = "5.30.30.00.00.00";
                rowCmpTax[24] = "UNHIDE";

                using var ms = new MemoryStream();
                foreach (var r in new[] { row0, row1, row2, rowCmpMfg, rowCmpTax })
                {
                    byte[] enc = (byte[])buildWinmanRec.Invoke(null, new object[] { r })!;
                    ms.Write(enc, 0, enc.Length);
                }
                return ms.ToArray();
            }

            string wgst1 = Path.Combine(tempRoot, "Alpha_25_26.wgst");
            string wgst2 = Path.Combine(tempRoot, "Beta_25_26.wgst");
            File.WriteAllBytes(wgst1, WriteWgstBytes(
                "Alpha Traders Pvt Ltd", "Alpha Traders", "29 AABCA1234F 1Z5",
                "2025-2026", "Regular", "Private Limited Company", "GSTR-3B", "9876543210",
                extraOnlyOne: "Yes"));
            File.WriteAllBytes(wgst2, WriteWgstBytes(
                "Beta Exports LLP", "Beta Exports", "33 AABCB5678G 1Z2",
                "2025-2026", "Composition Supplier", "LLP", "CMP-08", "9123456780",
                extraOnlyOne: null));

            // Single file must fail the >= 2 similar files rule
            var singleRes = discoverInfo.Invoke(null, new object?[] { wgstLnk, new[] { wgst1 }, prefPath })!;
            Check(!GetProp<bool>(singleRes, "MeetsMinimumTwoFilesRule"),
                "DiscoverAppInformation must reject when only 1 file exists (< 2 similar files)");

            // Two matching .wgst files must pass and extract only headers present in >= 2 files
            var pairRes = discoverInfo.Invoke(null, new object?[] { wgstLnk, new[] { wgst1, wgst2 }, prefPath })!;
            Check(GetProp<bool>(pairRes, "MeetsMinimumTwoFilesRule") &&
                  GetProp<int>(pairRes, "QualifyingFileCount") == 2,
                "DiscoverAppInformation must succeed when >= 2 .wgst files share inside-file Header -> Value fields");

            var matchedHeaders = ((IEnumerable)GetProp<object>(pairRes, "MatchedHeaders")).Cast<object>()
                .Select(h => GetProp<string>(h, "Header"))
                .ToList();
            var defaultVisibleHeaders = GetProp<IReadOnlyList<string>>(pairRes, "VisibleHeaders");
            Check(defaultVisibleHeaders.Count == matchedHeaders.Count,
                $"All discovered >= 2 matched headers should be visible by default, got {defaultVisibleHeaders.Count}/{matchedHeaders.Count}");
            Check(matchedHeaders.Contains("Legal Name") &&
                  matchedHeaders.Contains("Trade Name") &&
                  matchedHeaders.Contains("GSTIN") &&
                  matchedHeaders.Contains("PAN") &&
                  matchedHeaders.Contains("State & State Code") &&
                  matchedHeaders.Contains("Financial Year") &&
                  matchedHeaders.Contains("Taxpayer / Dealer Type") &&
                  matchedHeaders.Contains("Constitution / Status") &&
                  matchedHeaders.Contains("Return Form") &&
                  matchedHeaders.Contains("Filing Type") &&
                  matchedHeaders.Contains("Mobile Number") &&
                  matchedHeaders.Contains("Email ID") &&
                  matchedHeaders.Contains("Manufacturer / Supplier (other than below)") &&
                  matchedHeaders.Contains("Total Tax Payable") &&
                  matchedHeaders.Contains("Created By User") &&
                  !matchedHeaders.Contains("Quarterly Return"),
                $"Winman GST >= 2 matched headers mismatch: [{string.Join(", ", matchedHeaders)}]");

            var matchedFiles = ((IEnumerable)GetProp<object>(pairRes, "MatchedFiles")).Cast<object>().ToList();
            var alphaFields = GetProp<IReadOnlyDictionary<string, string>>(matchedFiles[0], "Fields");
            Check(alphaFields["Legal Name"] == "Alpha Traders Pvt Ltd" &&
                  alphaFields["Trade Name"] == "Alpha Traders" &&
                  alphaFields["GSTIN"] == "29AABCA1234F1Z5" &&
                  alphaFields["PAN"] == "AABCA1234F" &&
                  alphaFields["State & State Code"] == "29 · Karnataka" &&
                  alphaFields["Financial Year"] == "2025-2026" &&
                  alphaFields["Return Form"] == "GSTR-3B" &&
                  alphaFields["Manufacturer / Supplier (other than below)"].Contains("1,56,300.00") &&
                  alphaFields["Manufacturer / Supplier (other than below)"].Contains("Total Tax: 1,563.00") &&
                  alphaFields["Total Tax Payable"].Contains("53,063.00"),
                "Inside-file .wgst header decoding must recover Legal Name, Trade Name, GSTIN, PAN, State, Financial Year, Return Form, and inside-file GST schedule breakdowns");

            // 4. User header visibility selection persistence and filtering
            var customVisible = new[] { "Legal Name", "GSTIN", "Financial Year" };
            savePref.Invoke(null, new object?[] { wgstLnk, customVisible, prefPath });

            var reloadedRes = discoverInfo.Invoke(null, new object?[] { wgstLnk, new[] { wgst1, wgst2 }, prefPath })!;
            var reloadedVis = GetProp<IReadOnlyList<string>>(reloadedRes, "VisibleHeaders");
            Check(reloadedVis.SequenceEqual(customVisible),
                "Saved user header visibility selection must persist and reload accurately");

            var filteredAlpha = (IReadOnlyList<KeyValuePair<string, string>>)getVisibleFields.Invoke(
                null, new object[] { matchedFiles[0], reloadedVis })!;
            Check(filteredAlpha.Count == 3 &&
                  filteredAlpha[0].Key == "Legal Name" && filteredAlpha[0].Value == "Alpha Traders Pvt Ltd" &&
                  filteredAlpha[1].Key == "GSTIN" && filteredAlpha[1].Value == "29AABCA1234F1Z5" &&
                  filteredAlpha[2].Key == "Financial Year" && filteredAlpha[2].Value == "2025-2026",
                "GetVisibleFieldsForFile must return only the user-selected visible headers in exact order");

            // 5. Inside-file Winman CA-ERP (.tax) binary + embedded JSON decoding across >= 2 files
            string taxLnk = Path.Combine(tempRoot, "Winman CA ERP.lnk");
            File.WriteAllText(taxLnk, "mock");
            byte[] WriteTaxBytes(string name, string father, string pan, string status, string ay, string dob, string mobile, string city, string pin, string gti, string netTax)
            {
                var r0 = new string?[35];
                r0[0] = "CLIENT-" + status;
                r0[3] = pin;
                r0[4] = "15072025120000000000";
                r0[6] = "23112026183000000000";
                r0[8] = "Original";
                r0[10] = city;
                r0[12] = "01-Resident";
                r0[13] = " " + pan + " ";
                r0[14] = "Karnataka";
                r0[15] = name;
                r0[17] = dob;
                r0[20] = "31-Oct-2025";
                r0[21] = "MG Road";
                r0[23] = ay;
                r0[26] = status;
                r0[27] = "Version-9.6.4";
                r0[33] = mobile;

                var r1 = new string?[35];
                r1[0] = "head";
                var r2 = new string?[35];
                r2[0] = "MAIN";
                r2[10] = "0.01.00.00.00.00";
                r2[30] = $"{{\"Name\":\"{name}\",\"Father_name\":\"{father}\",\"PAN\":\"{pan}\",\"Status\":\"{status}\",\"Asstyear\":\"{ay}\",\"DOB__DOI__DOF\":\"{dob}\",\"Mobile_Number\":\"{mobile}\",\"City\":\"{city}\",\"Pincode\":\"{pin}\",\"Gross_Total_Income\":\"{gti}\",\"Net_Tax\":\"{netTax}\",\"MISPriority\":\"Original\"}}";

                var rEmp = new string?[35];
                rEmp[0] = "MAIN";
                rEmp[1] = "-";
                rEmp[2] = "Employer-1: Orbit Tech";
                rEmp[3] = "Entered";
                rEmp[10] = "1.01.01.00.00.00";
                rEmp[24] = "-";

                var rStd = new string?[35];
                rStd[0] = "MAIN";
                rStd[2] = "Standard deduction u/s 16(ia)";
                rStd[4] = "50,000";
                rStd[10] = "1.03.01.00.00.00";
                rStd[24] = "-";

                using var ms = new MemoryStream();
                foreach (var r in new[] { r0, r1, r2, rEmp, rStd })
                {
                    byte[] enc = (byte[])buildWinmanRec.Invoke(null, new object[] { r })!;
                    ms.Write(enc, 0, enc.Length);
                }
                return ms.ToArray();
            }

            string tax1 = Path.Combine(tempRoot, "Client1_25_26.tax");
            string tax2 = Path.Combine(tempRoot, "Client2_25_26.tax");
            File.WriteAllBytes(tax1, WriteTaxBytes("Ramesh Kumar", "Suresh Kumar", "ABCDE1234F", "Individual", "2025-2026", "15-Aug-1985", "9876543210", "Mangaluru", "575 001", "1450000", "185000"));
            File.WriteAllBytes(tax2, WriteTaxBytes("Kiran Rao", "Mohan Rao", "FGHIJ5678K", "Individual", "2025-2026", "20-Jan-1990", "9123456789", "Bengaluru", "560 001", "2100000", "312000"));

            var taxRes = discoverInfo.Invoke(null, new object?[] { taxLnk, new[] { tax1, tax2 }, prefPath })!;
            var taxHeaders = ((IEnumerable)GetProp<object>(taxRes, "MatchedHeaders")).Cast<object>()
                .Select(h => GetProp<string>(h, "Header"))
                .ToList();
            Check(GetProp<bool>(taxRes, "MeetsMinimumTwoFilesRule") &&
                  taxHeaders.Contains("Assessee Name") &&
                  taxHeaders.Contains("Father Name") &&
                  taxHeaders.Contains("PAN") &&
                  taxHeaders.Contains("Status / Category") &&
                  taxHeaders.Contains("Assessment Year") &&
                  taxHeaders.Contains("Date of Birth / Incorporation") &&
                  taxHeaders.Contains("Mobile Number") &&
                  taxHeaders.Contains("City / Town") &&
                  taxHeaders.Contains("State") &&
                  taxHeaders.Contains("PIN Code") &&
                  taxHeaders.Contains("Gross Total Income") &&
                  taxHeaders.Contains("Net Tax") &&
                  taxHeaders.Contains("Employer-1") &&
                  taxHeaders.Contains("Standard deduction u/s 16(ia)"),
                $"Winman .tax >= 2 matched headers mismatch: [{string.Join(", ", taxHeaders)}]");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    [STAThread]
    private static int Main()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            var asm = typeof(Workspace).Assembly;
            TestCommandBarV380(asm);
            TestPinnedAppFilesV380(asm);
            TestPinnedAppsDirectOpenAndWinmanV380(asm);
            TestApplicationsSubDonutLabelFitAndOrderV380(asm);
            TestUniversalInAppDisplayNamesV380(asm);
            TestApplicationsDepth2ScrollAndHiddenCountersV380(asm);
            TestStrictPerAppIsolationAndNoMixingV380(asm);
            TestQuickArcNoStuckLoadingAndIsolationV380(asm);
            TestTallyErp9FixesAndUniversalDisambiguationV380(asm);
            TestApplicationFileHeaderValueInformationV380(asm);
            TestInformationDonutGeometryAndSectorV380(asm);
            TestInformationTabSyncAndSchemaV380(asm);
            TestPinnedAppSearchDisposeUnloadV380(asm);
            TestColorPaletteContrastAndHeroV380(asm);
            TestVaultProV380(asm);
            TestNotesSnippetsV380(asm);
            TestExcelProV380(asm);
            TestFileToolkitV380(asm);
            TestFocusRingAndWindowPowerV380(asm);
            TestClipboardEcosystemAutomationLocalAiHubV380(asm);
            Console.WriteLine("R380 PASS: All 16 ModernV380 modules, Pinned App Compatible Files (Tally/Excel/Recent-first), and hub invariants verified.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            app.Shutdown();
        }
    }
}

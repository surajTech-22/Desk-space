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
        }
        finally
        {
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

    [STAThread]
    private static int Main()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            var asm = typeof(Workspace).Assembly;
            TestCommandBarV380(asm);
            TestPinnedAppFilesV380(asm);
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

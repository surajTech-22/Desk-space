$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function ReplaceOnce([string]$old,[string]$new,[string]$what)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw "R161 $what expected 1 anchor, got $n"}
 $script:s=$script:s.Replace($old,$new)
}
ReplaceOnce '            TestFolderDropZipOrCopyV159();' @'
            TestFolderDropZipOrCopyV159();
            TestDeskSpaceFoundationV161();
'@ 'new actual WPF regression call'
$method=@'
    private static void TestDeskSpaceFoundationV161()
    {
        var preview=Product.GetType("DeskSpace.DeskSpaceArchivePreviewV161",true)!;
        var diagnostics=Product.GetType("DeskSpace.DeskSpaceDiagnosticsV161",true)!;
        var deck=Product.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
        var summary=preview.GetMethod("Summary",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var exported=diagnostics.GetMethod("ExportToDownloads",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var errors=diagnostics.GetMethod("ErrorKinds",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var testZip=Path.Combine(outdir,"R161_"+Guid.NewGuid()+".zip");
        string? bundle=null;
        var source=Path.Combine(outdir,"r161-source-"+Guid.NewGuid()+".txt");
        File.WriteAllText(source,"disposable R161 test");
        try
        {
            using(var z=System.IO.Compression.ZipFile.Open(testZip,
                System.IO.Compression.ZipArchiveMode.Create))
            {
                z.CreateEntry("folder/");
                var entry=z.CreateEntry("folder/note.txt");
                using var writer=new StreamWriter(entry.Open());
                writer.Write("sample");
            }
            var zipText=(string)summary.Invoke(null,
                new object[]{testZip,18})!;
            Assert(zipText.Contains("folder/note.txt") &&
                zipText.Contains("2 entries"),
                "R161 ZIP preview must list actual file names without extraction");
            Assert((string)summary.Invoke(null,
                new object[]{source,18})!=="Not a ZIP archive.",
                "R161 non-ZIP files must not be parsed as archives");
            Assert(!Directory.Exists(Path.Combine(outdir,"folder")),
                "R161 read-only preview must never extract into current directory");

            var kind=(string)errors.Invoke(null,new object?[]{
                "2026-10-28 System.IO.IOException in C:\\Private\\Client "+
                "password=secret mail=person@example.com"})!;
            Assert(kind.Contains("IOException") &&
                !kind.Contains("password")&&!kind.Contains("secret")&&
                !kind.Contains("Client")&&!kind.Contains("person@"),
                "R161 diagnostics must export error categories, not raw secrets");
            bundle=(string)exported.Invoke(null,null)!;
            Assert(File.Exists(bundle)&&
                Path.GetDirectoryName(bundle)==Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),"Downloads"),
                "R161 explicit support ZIP must be saved to Downloads");
            using(var z=System.IO.Compression.ZipFile.OpenRead(bundle))
            {
                var names=z.Entries.Select(x=>x.FullName).ToHashSet();
                Assert(names.SetEquals(new[]{"system-summary.txt",
                    "crash-categories.txt","update-error-categories.txt",
                    "privacy.txt"}),
                    "R161 support ZIP must have exactly four allowlisted files");
                Assert(names.All(n=>!n.Contains("notes")&&!n.Contains("Excel")),
                    "R161 diagnostic export must not include personal files");
            }
            var duplicate=deck.GetMethod("AlreadyHeldSource",
                BindingFlags.Static|BindingFlags.NonPublic)!;
            var origins=(System.Collections.Generic.Dictionary<string,string>)
                deck.GetField("originalByStaged",
                    BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            var staged=Path.Combine(outdir,"r161-held-"+Guid.NewGuid());
            var canonical=Path.GetFullPath(source);
            Assert(!(bool)duplicate.Invoke(null,new object[]{source})!,
                "R161 first stage must be permitted");
            origins[staged]=canonical;
            Assert((bool)duplicate.Invoke(null,new object[]{source})!,
                "R161 two monitor Deck views must reject identical source");
            origins.Remove(staged);
            Assert(!(bool)duplicate.Invoke(null,new object[]{source})!,
                "R161 clearing a held source must permit staging again");
            Console.WriteLine("R161 PASS: ZIP preview read-only, opt-in "+
                "privacy-safe Downloads support ZIP, and shared-source dedup.");
        }
        finally
        {
            if(bundle!=null&&File.Exists(bundle))File.Delete(bundle);
            if(File.Exists(testZip))File.Delete(testZip);
            if(File.Exists(source))File.Delete(source);
        }
    }

'@
ReplaceOnce '    private static void TestIndependentMonitorsV154()' ($method.Replace($crlf,$lf)+
 '    private static void TestIndependentMonitorsV154()') 'foundation WPF test method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R161 WPF acceptance: read-only ZIP preview, support ZIP privacy and Deck source duplicate handling.'

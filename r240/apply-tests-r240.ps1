$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R240 "+$why+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($before,$after)
}
Swap '                TestMonitorDiagnosticsV239();' @'
                TestMonitorDiagnosticsV239();
                TestTextDeckPreviewV240();
'@ 'register native Deck file preview acceptance'
$method=@'
    private static void TestTextDeckPreviewV240()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceTextPreviewV240",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var supported=type.GetMethod("Supports",flags)!;
        bool Supports(string extension)=>(bool)supported.Invoke(null,
            new object?[]{extension})!;
        Assert(Supports(".txt") && Supports(".CSV") && Supports(".json") &&
               Supports(".md") && Supports(".ps1") && Supports(".cs") &&
               !Supports(".exe") && !Supports(".dll") && !Supports(".env") &&
               !Supports(".pdf") && !Supports(".zip"),
            "R240 file preview must use a fixed allowlist, not run scripts or parse archives");
        var read=type.GetMethod("ReadSnippet",flags)!;
        (bool Good,string Text,string Status) Inspect(string path)
        {
            var result=read.Invoke(null,new object[]{path})!;
            var t=result.GetType();
            return ((bool)t.GetProperty("Available")!.GetValue(result)!,
                (string)t.GetProperty("Text")!.GetValue(result)!,
                (string)t.GetProperty("Status")!.GetValue(result)!);
        }
        var folder=Path.Combine(outdir,"r240-text-preview");
        Directory.CreateDirectory(folder);
        var normal=Path.Combine(folder,"safe.txt");
        var large=Path.Combine(folder,"large.log");
        var unsafeFile=Path.Combine(folder,"not-text.ps1");
        var oversized=Path.Combine(folder,"oversized.csv");
        try
        {
            File.WriteAllText(normal,"A heading\nRow 1\tRow 2\n");
            var simple=Inspect(normal);
            Assert(simple.Good && simple.Text.Contains("Row 1") &&
                simple.Status.Contains("Read-only"),
                "R240 real text content read without change");
            File.WriteAllText(large,new string('X',4200));
            var bounded=Inspect(large);
            Assert(bounded.Good && bounded.Text.Length==3600 &&
                bounded.Status.Contains("beginning"),
                "R240 read only first 3600 characters, never whole file");
            File.WriteAllBytes(unsafeFile,new byte[]{65,0,66,67});
            var binary=Inspect(unsafeFile);
            Assert(!binary.Good && binary.Text.Length==0 &&
                binary.Status.Contains("binary"),
                "R240 control/binary bytes blocked and not exposed");
            using(var stream=File.Create(oversized))
                stream.SetLength(2L*1024*1024+1);
            Assert(!Inspect(oversized).Good && !Inspect(normal+".gone").Good,
                "R240 oversized or missing files are refused");
            Assert(type.GetMethod("Create",flags)!.Invoke(null,new object[]{normal})
                is System.Windows.Controls.StackPanel,
                "R240 preview must remain a native WPF Deck view, not a process launch");
            Console.WriteLine(
                "R240 PASS: native WPF Deck preview; 3600-char bounded excerpt, "+
                "strict allowlist, missing/oversized/binary rejection and zero source mutations.");
        }
        finally{Directory.Delete(folder,true);}
    }

'@
Swap '    private static void TestMonitorDiagnosticsV239()' ($method+
 '    private static void TestMonitorDiagnosticsV239()') 'insert native file test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R240: native real temp-file tests registered for bounded text/file/code Deck preview.'

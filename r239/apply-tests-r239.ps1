$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R239 "+$why+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($before,$after)
}
Swap '                TestStrictDeckOffsetRecoveryV238();' @'
                TestStrictDeckOffsetRecoveryV238();
                TestMonitorDiagnosticsV239();
'@ 'register native monitor diagnostic string tests'
$method=@'
    private static void TestMonitorDiagnosticsV239()
    {
        var type=Product.GetType(
            "DeskSpace.DeskSpaceMonitorDiagnosticsV239",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var summarize=type.GetMethod("Summarize",flags)!;
        var display=type.GetNestedType("Display",BindingFlags.NonPublic)!;
        string Format(string device,bool primary,
            System.Drawing.Rectangle bounds,System.Drawing.Rectangle area,
            int? dpi,int x,int y)
        {
            var args=new object?[]{device,primary,bounds,area,dpi,x,y};
            var instance=Activator.CreateInstance(display,args)!;
            return (string)summarize.Invoke(null,new[]{instance})!;
        }
        var left=Format(@"\\.\DISPLAY2",false,
            new System.Drawing.Rectangle(-1920,-100,1920,1080),
            new System.Drawing.Rectangle(-1920,-100,1920,1040),
            null,-125,330);
        var right=Format(@"\\.\DISPLAY1",true,
            new System.Drawing.Rectangle(0,0,2560,1440),
            new System.Drawing.Rectangle(0,0,2560,1400),
            null,245,-60);
        Assert(left.Contains("Secondary") && left.Contains("DISPLAY2") &&
               left.Contains("-1920, -100") &&
               left.Contains("Saved Deck offset: X -125, Y 330"),
            "R239 diagnostic must preserve negative virtual coordinates and secondary Deck offset");
        Assert(right.Contains("Primary") && right.Contains("DISPLAY1") &&
               right.Contains("2560 × 1440") &&
               right.Contains("Saved Deck offset: X 245, Y -60"),
            "R239 each monitor must report its OWN separate Deck coordinates");
        Assert(!left.Contains("C:\\") && !right.Contains("C:\\") &&
               type.GetMethod("Snapshot",flags)!=null &&
               type.GetMethod("Show",flags)!=null,
            "R239 local diagnostics must not include private file paths");
        Console.WriteLine("R239 PASS: synthetic primary/secondary native monitor diagnostics, "+
            "negative coordinates and independent Deck offsets.");
    }

'@
Swap '    private static void TestStrictDeckOffsetRecoveryV238()' ($method+
 '    private static void TestStrictDeckOffsetRecoveryV238()') 'insert native output verification'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R239: native WPF monitor diagnostic output test registered.'

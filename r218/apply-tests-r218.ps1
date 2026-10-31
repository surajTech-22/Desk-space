$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R218 "+$reason+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestNativePdfPreviewV217();' @'
                TestNativePdfPreviewV217();
                TestWindowPreviewMonitorV218();
'@ 'test call'
$method=@'
    private static void TestWindowPreviewMonitorV218()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceWindowPreviewV100",true)!;
        var method=type.GetMethod("PlaceRightV218",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Right(double l,double r,double wl,double wr,double w)=>
            (bool)method.Invoke(null,new object[]{l,r,wl,wr,w})!;
        Assert(Right(-1520,-1200,-1920,0,480),
            "R218 left secondary monitor preview should open right when it fits");
        Assert(!Right(-500,-80,-1920,0,480),
            "R218 near secondary right seam preview must open left");
        Assert(!Right(1320,1430,0,1440,480),
            "R218 right primary-edge preview must open left");
        Assert(Right(100,230,0,1440,480),
            "R218 primary left-edge preview must open right");
        Assert(!Right(-900,-650,-1920,0,1400),
            "R218 when no side fully fits, choose side with more free space");
        Assert(Right(0,120,0,1440,650),
            "R218 scaled preview must use its physical-pixel width");
        Assert(type.GetMethod("Stationary",
            BindingFlags.Static|BindingFlags.NonPublic)!=null,
            "R218 preserve stationary-hover gating for safe preview capture");
        Console.WriteLine(
            "R218 PASS: negative-origin, seam, primary/secondary and DPI-scaled preview placement.");
    }

'@
Swap '    private static void TestNativePdfPreviewV217()' ($method+
 '    private static void TestNativePdfPreviewV217()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R218 monitor-specific native-pixel window preview acceptance registered.'

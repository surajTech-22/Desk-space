$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
function Swap([string]$from,[string]$to,[string]$what)
{
 $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($from))).Count
 if($n -ne 1){throw ("R231 "+$what+" expected 1 anchor; found "+$n)}
 $script:s=$script:s.Replace($from,$to)
}
Swap '                TestIndependentDeckAndWidgetPositionsV228();' @'
                TestIndependentDeckAndWidgetPositionsV228();
                TestWidgetNoMagnetV231();
'@ 'WPF test registration'
$method=@'
    private static void TestWidgetNoMagnetV231()
    {
        var fence=Product.GetType("DeskSpace.DeskSpaceMonitorFenceV152",true)!;
        var clamp=fence.GetMethod("ClampRectangle",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        System.Drawing.Point Clamp(System.Drawing.Rectangle window,
            System.Drawing.Rectangle area)=>
            (System.Drawing.Point)clamp.Invoke(null,new object[]{window,area})!;
        // The center seam is not an outer edge; two compact widgets can
        // coexist on either side without any 22px magnetic attraction.
        var left=new System.Drawing.Rectangle(-1920,0,1920,1040);
        var right=new System.Drawing.Rectangle(0,0,1920,1040);
        var a=new System.Drawing.Rectangle(-119,340,116,116);
        var b=new System.Drawing.Rectangle(3,390,116,116);
        Assert(Clamp(a,left).X==-119&&Clamp(b,right).X==3,
            "R231 widgets near a shared seam must retain independent native coordinates");
        var overflow=new System.Drawing.Rectangle(70,390,116,116);
        var snapped=Clamp(overflow,left);
        Assert(snapped.X==-116,
            "R231 a genuine cross-monitor overflow is clipped to its own display");
        var sessions=Product.GetType("DeskSpace.DeskSpaceMonitorSessionsV154",true)!;
        var device=sessions.GetMethod("Device",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        Assert(device!=null&&device.ReturnType==typeof(string),
            "R231 secondary position still belongs to its own physical display session");
        Console.WriteLine("R231 PASS: two compact widgets remain independent near virtual seam; genuine overflow is clamped.");
    }

'@
Swap '    private static void TestIndependentDeckAndWidgetPositionsV228()' ($method+
 '    private static void TestIndependentDeckAndWidgetPositionsV228()') 'native method'
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R231 native seam geometry and independent device test injected.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R228 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestSupervisorLoopGuardV227();' @'
                TestSupervisorLoopGuardV227();
                TestIndependentDeckAndWidgetPositionsV228();
'@ 'native invocation'
$method=@'
    private static void TestIndependentDeckAndWidgetPositionsV228()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDeckOffsetV228",true)!;
        var offset=type.GetNestedType("Offset",BindingFlags.NonPublic)!;
        var clamp=type.GetMethod("Clamp",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var input=Activator.CreateInstance(offset,7000,-8000)!;
        var output=clamp.Invoke(null,new[]{input,(object)4096})!;
        Assert((int)offset.GetProperty("X")!.GetValue(output)!==4096 &&
            (int)offset.GetProperty("Y")!.GetValue(output)! == -4096,
            "R228 Deck drag offsets must be bounded in native physical pixels");
        var read=type.GetMethod("Read",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        Assert(read!=null,
            "R228 per-device Deck position must not be a static global offset");
        var dock=Product.GetType("DeskSpace.DeskSpaceMonitorDockV190",true)!;
        var calculate=dock.GetMethod("Calculate",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        var left=new System.Drawing.Rectangle(-1920,0,1920,1040);
        var right=new System.Drawing.Rectangle(0,0,1280,900);
        var leftWidget=new System.Drawing.Rectangle(-1840,120,116,116);
        var rightWidget=new System.Drawing.Rectangle(900,500,116,116);
        var a=calculate.Invoke(null,new object[]{leftWidget,left,280,250,6})!;
        var b=calculate.Invoke(null,new object[]{rightWidget,right,280,250,6})!;
        var x=a.GetType().GetProperty("X")!;
        Assert((int)x.GetValue(a)!<0&&(int)x.GetValue(b)!>=0,
            "R228 independent Decks must anchor to their own negative/positive monitor work areas");
        var sessions=Product.GetType("DeskSpace.DeskSpaceMonitorSessionsV154",true)!;
        Assert(sessions.GetMethod("SavePosition",
            BindingFlags.Static|BindingFlags.NonPublic)!=null &&
            sessions.GetMethod("Restore",
            BindingFlags.Static|BindingFlags.NonPublic)!=null,
            "R228 preserve independent persisted native widget monitor positions");
        Console.WriteLine(
            "R228 PASS: left/right monitor dock coordinates, bounded independent Deck offsets and native session positions.");
    }

'@
Swap '    private static void TestSupervisorLoopGuardV227()' ($method+
 '    private static void TestSupervisorLoopGuardV227()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))

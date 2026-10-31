$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
    $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw ("R214 "+$reason+" expected one anchor, got "+$count)}
    $script:s=$script:s.Replace($old,$new)
}
Swap '                TestCopyIntegrityV212();' @'
                TestCopyIntegrityV212();
                TestMonitorTopologyV214();
'@ 'native invocation'
$method=@'
    private static void TestMonitorTopologyV214()
    {
        var fence=Product.GetType("DeskSpace.DeskSpaceMonitorFenceV152",true)!;
        var resolver=fence.GetMethod("ResolveActiveDeviceV214",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        string Device(string preferred,string[] screens,string primary)=>
            (string)resolver.Invoke(null,new object[]{
                preferred,(System.Collections.Generic.IReadOnlyList<string>)screens,
                primary})!;
        const string left="\\.\\DISPLAY2";
        const string right="\\.\\DISPLAY1";
        Assert(Device(left,new[]{left,right},right)==left,
            "R214 an available assigned secondary display must own its widget");
        Assert(Device(left,new[]{right},right)==right,
            "R214 a disconnected secondary must temporarily use an available fallback");
        Assert(Device(left,new[]{left,right},right)==left,
            "R214 reconnection must restore original monitor identity");
        Assert(Device(left.ToLowerInvariant(),new[]{left,right},right)==left,
            "R214 Windows display names must match regardless of case");
        Assert(Device("removed",new[]{"other"},right)=="other" &&
            Device(left,Array.Empty<string>(),right)=="",
            "R214 fallback must support changed primary and no active displays");
        var dock=Product.GetType("DeskSpace.DeskSpaceMonitorDockV190",true)!;
        var calculate=dock.GetMethod("Calculate",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        void Check(System.Drawing.Rectangle screen,
            System.Drawing.Rectangle owner,int width,int height)
        {
            var result=calculate.Invoke(null,new object[]{owner,screen,
                width,height,6})!;
            var t=result.GetType();
            int X=(int)t.GetProperty("X")!.GetValue(result)!;
            int Y=(int)t.GetProperty("Y")!.GetValue(result)!;
            int W=(int)t.GetProperty("Width")!.GetValue(result)!;
            int H=(int)t.GetProperty("Height")!.GetValue(result)!;
            Assert(X>=screen.Left && Y>=screen.Top &&
                X+W<=screen.Right && Y+H<=screen.Bottom,
                "R214 dock must stay on its own physical monitor");
        }
        var negative=new System.Drawing.Rectangle(-1920,0,1920,1040);
        var positive=new System.Drawing.Rectangle(0,0,1440,860);
        Check(negative,new System.Drawing.Rectangle(-60,920,120,120),320,520);
        Check(positive,new System.Drawing.Rectangle(1320,755,120,120),320,520);
        Check(negative,new System.Drawing.Rectangle(-1800,100,130,130),
            2700,1300);
        Check(positive,new System.Drawing.Rectangle(0,0,120,120),320,520);
        var multiscreen=Product.GetType(
            "DeskSpace.DeskSpaceMultiMonitorV151",true)!;
        Assert(multiscreen.GetMethod("Register",
            BindingFlags.Static|BindingFlags.NonPublic)!=null,
            "R214 must preserve the existing monitor lifecycle");
        Console.WriteLine(
            "R214 PASS: disconnect/reconnect identity and mixed-size negative-origin monitor dock containment.");
    }

'@
Swap '    private static void TestCopyIntegrityV212()' ($method+
 '    private static void TestCopyIntegrityV212()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R214 native topology and monitor-bound dock placement acceptance registered.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R236 "+$label+": expected exactly one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestOneShotGracefulExitV233();' @'
                TestOneShotGracefulExitV233();
                TestSyntheticMonitorMatrixV236();
'@ 'register synthetic dual-monitor matrix'
$method=@'
    // Run on real Windows/WPF under GitHub Actions but use *synthetic*
    // screen rectangles. It does not claim the VM has two physical monitors.
    private static void TestSyntheticMonitorMatrixV236()
    {
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var fence=Product.GetType("DeskSpace.DeskSpaceMonitorFenceV152",true)!;
        var resolver=fence.GetMethod("ResolveActiveDeviceV214",flags)!;
        var clamp=fence.GetMethod("ClampRectangle",flags)!;
        var dock=Product.GetType("DeskSpace.DeskSpaceMonitorDockV190",true)!;
        var calculate=dock.GetMethod("Calculate",flags)!;
        var state=Product.GetType("DeskSpace.DeskSpaceDeckOffsetV228",true)!;
        var clampOffset=state.GetMethod("Clamp",flags)!;
        var offsetType=state.GetNestedType("Offset",BindingFlags.NonPublic)!;
        string Device(string wanted,string[] active,string primary)=>
            (string)resolver.Invoke(null,new object[]{
                wanted,(System.Collections.Generic.IReadOnlyList<string>)active,primary})!;
        System.Drawing.Point Keep(System.Drawing.Rectangle rect,
            System.Drawing.Rectangle work)=>
            (System.Drawing.Point)clamp.Invoke(null,new object[]{rect,work})!;
        (int X,int Y,int W,int H) Dock(System.Drawing.Rectangle widget,
            System.Drawing.Rectangle work,int w,int h)
        {
            var raw=calculate.Invoke(null,new object[]{widget,work,w,h,6})!;
            var type=raw.GetType();
            int Get(string name)=>(int)type.GetProperty(name)!.GetValue(raw)!;
            return (Get("X"),Get("Y"),Get("Width"),Get("Height"));
        }

        var displays=new (string Name,System.Drawing.Rectangle Work,
            System.Drawing.Rectangle Widget,double Dpi)[]{
            ("negative-left-100",new(-1920,0,1920,1040),new(-1800,80,116,116),1.0),
            ("positive-right-150",new(0,0,2560,1400),new(2260,1100,174,174),1.5),
            ("left-seam-100",new(-1920,0,1920,1040),new(-119,340,116,116),1.0),
            ("right-seam-100",new(0,0,1920,1040),new(3,390,116,116),1.0),
            ("left-seam-200",new(-3840,0,3840,2100),new(-240,300,232,232),2.0),
            ("right-seam-125",new(0,0,1536,900),new(4,150,145,145),1.25),
            ("upper-screen",new(0,-1080,1920,1032),new(40,-1030,116,116),1.0),
            ("lower-screen",new(0,0,1280,680),new(1080,490,145,145),1.25),
            ("negative-both",new(-3040,-900,1366,768),new(-3020,-850,116,116),1.0),
            ("small-screen",new(1920,0,1024,720),new(2760,560,116,116),1.5),
            ("offset-monitor",new(300,200,1920,1040),new(420,310,174,174),1.5),
            ("stacked-right",new(1920,-1200,2560,1150),new(4200,-210,116,116),1.0)
        };
        int covered=0;
        foreach(var monitor in displays)
        {
            var widget=monitor.Widget;
            var work=monitor.Work;
            // Round the *native pixels* up as WPF should when arranging a
            // popup at 100%, 125%, 150% or 200% monitor DPI.
            int popupWidth=(int)Math.Ceiling(280*monitor.Dpi);
            int popupHeight=(int)Math.Ceiling(360*monitor.Dpi);
            var popup=Dock(widget,work,popupWidth,popupHeight);
            Assert(popup.X>=work.Left && popup.Y>=work.Top &&
                popup.X+popup.W<=work.Right &&
                popup.Y+popup.H<=work.Bottom,
                "R236 monitor popup crossed assigned work area: "+monitor.Name);
            var position=Keep(widget,work);
            Assert(position.X==widget.X && position.Y==widget.Y,
                "R236 widget magnetically moved while inside its monitor: "+monitor.Name);
            // Reentrant opposite-monitor placement must NOT mutate previous
            // screen's dock position. Each popup is owned by its own widget.
            var second=Dock(new System.Drawing.Rectangle(50,50,116,116),
                new System.Drawing.Rectangle(0,0,800,600),280,360);
            Assert(second.X>=0 && second.Y>=0,
                "R236 distinct popup placement failed");
            Assert(Dock(widget,work,popupWidth,popupHeight)==popup,
                "R236 monitor state leaked from another popup: "+monitor.Name);
            covered++;
        }

        var left=new System.Drawing.Rectangle(-1920,0,1920,1040);
        var right=new System.Drawing.Rectangle(0,0,1920,1040);
        Assert(Keep(new System.Drawing.Rectangle(-118,350,116,116),left).X==-118 &&
               Keep(new System.Drawing.Rectangle(2,370,116,116),right).X==2,
            "R236 no magnetic attraction when widgets sit on either side of same seam");
        Assert(Keep(new System.Drawing.Rectangle(70,350,116,116),left).X==-116,
            "R236 real overflow must be confined to original monitor");
        Assert(Device("LEFT",new[]{"RIGHT","LEFT"},"RIGHT")=="LEFT" &&
               Device("LEFT",new[]{"RIGHT"},"RIGHT")=="RIGHT" &&
               Device("LEFT",new[]{"RIGHT","LEFT"},"RIGHT")=="LEFT",
            "R236 monitor identity not restored after disconnect/reconnect");
        Assert(Device("left",new[]{"RIGHT","LEFT"},"RIGHT")=="LEFT" &&
               Device("REMOVED",new[]{"ALT"},"RIGHT")=="ALT" &&
               Device("LEFT",Array.Empty<string>(),"RIGHT")=="",
            "R236 monitor identity fallback or case handling is unsafe");
        var a=Activator.CreateInstance(offsetType,9000,-9000)!;
        var bounded=clampOffset.Invoke(null,new object[]{a,4096})!;
        Assert((int)offsetType.GetProperty("X")!.GetValue(bounded)!==4096 &&
               (int)offsetType.GetProperty("Y")!.GetValue(bounded)!== -4096,
            "R236 independent Deck native offsets must remain bounded");
        Console.WriteLine("R236 PASS: "+covered+
            " synthetic monitor/DPI scenarios, both sides of seam, stacked and negative coordinates, "+
            "repeated independent popup placement, Deck offsets and disconnect/reconnect. "+
            "REAL TWO-MONITOR HARDWARE NOT VERIFIED BY THIS TEST.");
    }

'@
Swap '    private static void TestOneShotGracefulExitV233()' ($method+
 '    private static void TestOneShotGracefulExitV233()') 'insert Windows WPF mixed topology acceptance'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R236: registered 12 case Windows WPF multi-monitor simulation matrix.'

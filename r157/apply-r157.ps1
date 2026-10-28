$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$lf=[string][char]10
$crlf=[string][char]13+$lf
$smoke='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($smoke).Replace($crlf,$lf)
$needle='            TestSharedHeldFilesV155();'
if(([regex]::Matches($s,[regex]::Escape($needle))).Count -ne 1){
    throw 'R157 expected exact R155 smoke insertion point'
}
$s=$s.Replace($needle,$needle+$lf+'            TestMonitorAwareSubmenusV157();')
$anchor='    private static void TestIndependentMonitorsV154()'
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){
    throw 'R157 expected one native monitor-test method'
}
$test=@'
    private static void TestMonitorAwareSubmenusV157()
    {
        var arc=Product.GetType("DeskSpace.DeskSpaceSubArcGeometryV48",true)!;
        var area=arc.GetMethod("MonitorWorkArea",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var angle=arc.GetMethod("FitAngleAt",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var monitor=System.Windows.Forms.Screen.PrimaryScreen!;
        var host=new Window{Width=240,Height=240,
            Left=monitor.WorkingArea.Left+90,
            Top=monitor.WorkingArea.Top+90,
            WindowStyle=WindowStyle.None,ShowInTaskbar=false};
        try
        {
            host.Show();
            var physical=(Rect)area.Invoke(null,new object[]{host})!;
            var real=System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(host).Handle);
            Assert(Math.Abs(physical.Left-real.WorkingArea.Left)<1 &&
                   Math.Abs(physical.Top-real.WorkingArea.Top)<1 &&
                   Math.Abs(physical.Width-real.WorkingArea.Width)<1 &&
                   Math.Abs(physical.Height-real.WorkingArea.Height)<1,
                "R157 live submenu fit must use the owner HWND's actual monitor");
            var a=(double)arc.GetMethod("FitAngle",
                BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(
                    null,new object[]{host,1,60.0,17.0})!;
            Assert(double.IsFinite(a),
                "R157 live WPF window submenu fit must yield finite angle");
        }
        finally {host.Close();}
        // A monitor is at x=1920 and is not the primary WorkArea (0,0).
        var second=new Rect(1920,0,1500,900);
        var chosen=(double)angle.Invoke(null,new object[]{
            2,0.0,20.0,new Point(3055,450),second,1.0})!;
        Assert(Math.Abs(chosen)>=20 && Math.Abs(chosen)<=40,
            "R157 secondary-screen submenu should turn away from its seam");
        // The monitor may also be to the LEFT of the primary and above it.
        var negative=new Rect(-1600,-200,1600,900);
        var left=(double)angle.Invoke(null,new object[]{
            1,180.0,16.0,new Point(-1510,200),negative,1.0})!;
        Assert(double.IsFinite(left),
            "R157 negative-origin secondary screen must support menu fitting");
        Console.WriteLine("R157 PASS: live HWND monitor area and simulated positive "+
            "and negative secondary-screen submenu layouts.");
    }

'@
$s=$s.Replace($anchor,$test.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($smoke,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R157 installed native HWND, DPI, monitor-specific submenu WPF smoke regression.'

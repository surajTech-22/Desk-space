$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$widget="$root/WidgetHost.cs"
$manager="$root/DeskSpaceMultiMonitorV151.cs"
Copy-Item -LiteralPath 'r152/DeskSpaceMonitorFenceV152.cs' -Destination "$root/DeskSpaceMonitorFenceV152.cs" -Force
$lf=[string][char]10
$crlf=[string][char]13+$lf

function Swap([string]$file,[string]$before,[string]$after,[string]$label)
{
    $source=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
    $old=$before.Replace($crlf,$lf)
    $new=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($source,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R152 $label expected one anchor, got $count"}
    [IO.File]::WriteAllText($file,$source.Replace($old,$new),
        [Text.UTF8Encoding]::new($false))
}

Swap $manager @'
        primary=widget;
        ReadPreference();
'@ @'
        primary=widget;
        DeskSpaceMonitorFenceV152.Bind(widget,
            Forms.Screen.FromHandle(new WindowInteropHelper(widget).Handle).DeviceName);
        ReadPreference();
'@ 'assign primary to its actual monitor'

Swap $manager @'
            var original=Forms.Screen.FromHandle(new WindowInteropHelper(primary).Handle);
'@ @'
            // Do not change the primary assignment when dragging near the seam.
            var original=DeskSpaceMonitorFenceV152.BoundScreen(primary) ??
                Forms.Screen.FromHandle(new WindowInteropHelper(primary).Handle);
'@ 'preserve primary monitor'

Swap $manager @'
                        replica.Show();
                        replica.Closed+=(_,_)=>{
'@ @'
                        replica.Show();
                        // Reposition in native pixels AFTER WPF creates the HWND.
                        // This is safe even with different per-monitor DPI scaling.
                        DeskSpaceMonitorFenceV152.PlaceNewSatellite(replica,screen);
                        replica.Closed+=(_,_)=>{
'@ 'satellite actual-monitor native pixel placement'

Swap $manager @'
                else PlaceIfOffscreen(replica,screen);
'@ @'
                else DeskSpaceMonitorFenceV152.ClampNow(replica);
'@ 'keep already-positioned secondary widgets on their own screen'

# R151 injects a satellite escape at the top of this method. Replace the
# virtual-desktop-only clamp for bound primary AND secondary widgets.
$source=[IO.File]::ReadAllText($widget).Replace($crlf,$lf)
$match=[regex]::Match($source,
    '(?m)^\s*private\s+void\s+KeepOnVisibleDesktop\s*\([^)]*\)\s*\{')
if(!$match.Success){throw 'R152 widget clamp method not found'}
$insertion=$lf+'        if(DeskSpaceMonitorFenceV152.ClampIfBound(this))return;'
$source=$source.Insert($match.Index+$match.Length,$insertion)
[IO.File]::WriteAllText($widget,$source,[Text.UTF8Encoding]::new($false))

# Real app's existing R36 WPF UI smoke harness checks synthetic screen bounds.
$smoke='r36/UiSmoke/Program.cs'
$sm=[IO.File]::ReadAllText($smoke).Replace($crlf,$lf)
$call='            TestSettingsPagesV151();'
if(([regex]::Matches($sm,[regex]::Escape($call))).Count -ne 1){
    throw 'R152 WPF test-call anchor changed'
}
$sm=$sm.Replace($call,$call+$lf+'            TestMonitorFenceV152();')
$anchor='    private static void TestSettingsPagesV151()'
if(([regex]::Matches($sm,[regex]::Escape($anchor))).Count -ne 1){
    throw 'R152 WPF test-method anchor changed'
}
$test=@'
    private static void TestMonitorFenceV152()
    {
        var fence=Product.GetType("DeskSpace.DeskSpaceMonitorFenceV152",true)!;
        var method=fence.GetMethod("ClampRectangle",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        void Check(System.Drawing.Rectangle widget,System.Drawing.Rectangle monitor,
            int x,int y,string scenario)
        {
            var position=(System.Drawing.Point)method.Invoke(null,
                new object[]{widget,monitor})!;
            Assert(position.X==x&&position.Y==y,
                "R152 screen fence failed: "+scenario);
        }
        // An expanded widget at the seam must remain fully inside monitor 1.
        Check(new System.Drawing.Rectangle(1870,760,408,408),
            new System.Drawing.Rectangle(0,0,1920,1040),
            1512,632,"right and bottom seam");
        // The second monitor has native pixels starting at x=1920.
        Check(new System.Drawing.Rectangle(3450,750,420,420),
            new System.Drawing.Rectangle(1920,0,1600,900),
            3100,480,"second-screen edge");
        // Left/above the primary monitor: negative virtual desktop positions.
        Check(new System.Drawing.Rectangle(-1850,-350,400,400),
            new System.Drawing.Rectangle(-1600,-200,1600,900),
            -1600,-200,"negative screen offsets");
        Check(new System.Drawing.Rectangle(90,60,116,116),
            new System.Drawing.Rectangle(0,0,1920,1040),
            90,60,"normal widget unchanged");
        Console.WriteLine("R152 PASS: native pixel monitor bounds, expansion, "+
            "screen seams and negative coordinates.");
    }

'@
$sm=$sm.Replace($anchor,$test.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($smoke,$sm,[Text.UTF8Encoding]::new($false))
Write-Host 'R152: original widget, satellites, and expanded donut contained by assigned native monitor work area.'

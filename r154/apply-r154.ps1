$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$lf=[string][char]10
$crlf=[string][char]13+$lf
Copy-Item -LiteralPath 'r154/DeskSpaceMonitorSessionsV154.cs' -Destination "$r/DeskSpaceMonitorSessionsV154.cs" -Force
Copy-Item -LiteralPath 'r154/DeskSpaceNativeClipV154.cs' -Destination "$r/DeskSpaceNativeClipV154.cs" -Force
function Swap([string]$path,[string]$before,[string]$after,[string]$why){
 $source=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$before.Replace($crlf,$lf)
 $new=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($source,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R154 $why required one anchor, found $count"}
 [IO.File]::WriteAllText($path,$source.Replace($old,$new),
   [Text.UTF8Encoding]::new($false))
}

# R151 already builds distinct WindowShellWindow objects, but the secondary
# copy's ordinary SavePosition would overwrite the main widget position. Give
# each object a separate monitor-owned session; only data is shared.
Swap "$r/DeskSpaceMultiMonitorV151.cs" @'
        DeskSpaceMonitorFenceV152.Bind(widget,
            Forms.Screen.FromHandle(new WindowInteropHelper(widget).Handle).DeviceName);
        ReadPreference();
'@ @'
        DeskSpaceMonitorFenceV152.Bind(widget,
            Forms.Screen.FromHandle(new WindowInteropHelper(widget).Handle).DeviceName);
        DeskSpaceMonitorSessionsV154.Attach(widget,
            Forms.Screen.FromHandle(new WindowInteropHelper(widget).Handle).DeviceName,
            true);
        ReadPreference();
'@ 'primary widget has its own session'

Swap "$r/DeskSpaceMultiMonitorV151.cs" @'
                        DeskSpaceMonitorFenceV152.PlaceNewSatellite(replica,screen);
                        replica.Closed+=(_,_)=>{
'@ @'
                        DeskSpaceMonitorFenceV152.PlaceNewSatellite(replica,screen);
                        DeskSpaceMonitorSessionsV154.Attach(replica,screen.DeviceName,false);
                        DeskSpaceMonitorSessionsV154.Restore(replica);
                        replica.Closed+=(_,_)=>{
'@ 'independent secondary session and native saved position'

Swap "$r/DeskSpaceMultiMonitorV151.cs" @'
            var original=DeskSpaceMonitorFenceV152.BoundScreen(primary) ??
                Forms.Screen.FromHandle(new WindowInteropHelper(primary).Handle);
'@ @'
            var original=DeskSpaceMonitorFenceV152.BoundScreen(primary) ??
                Forms.Screen.FromHandle(new WindowInteropHelper(primary).Handle);
            DeskSpaceMonitorSessionsV154.Attach(primary,original.DeviceName,true);
'@ 'monitor reconnect updates primary session identity'

# Extend, do not replace, the existing SavePosition method. The secondary has
# its own on-disk pixel position and never modifies the primary's legacy prefs.
$widget="$r/WidgetHost.cs"
$source=[IO.File]::ReadAllText($widget).Replace($crlf,$lf)
$save=[regex]::Match($source,
    '(?m)^\s*private\s+void\s+SavePosition\s*\(\s*\)\s*\{')
if(!$save.Success){throw 'R154 host SavePosition method missing'}
$source=$source.Insert($save.Index+$save.Length,
    ($lf+'        if(DeskSpaceMonitorSessionsV154.SavePosition(this,ringExpanded))return;'))
$expand=[regex]::Match($source,
    '(?m)^\s*private\s+void\s+BuildDonut\s*\(\s*\)\s*\{')
if(!$expand.Success){throw 'R154 host BuildDonut method missing'}
$source=$source.Insert($expand.Index+$expand.Length,
    ($lf+'        DeskSpaceMonitorSessionsV154.PrepareExpansion(this,OpenDiameter);'))
[IO.File]::WriteAllText($widget,$source,[Text.UTF8Encoding]::new($false))

# WPF can reposition popups inside the virtual-desktop bounds while the ring
# is resizing. Keep R153's pre-paint synchronous clamp AND constrain native
# region painting when a compositor frame attempts to cross the seam.
Swap "$r/DeskSpaceMonitorFenceV153.cs" @'
    private const int WindowPosChanging=0x0046,MovingMessage=0x0216,
        DpiChanged=0x02e0;
'@ @'
    private const int WindowPosChanging=0x0046,WindowPosChanged=0x0047,
        MovingMessage=0x0216, SizingMessage=0x0214,DpiChanged=0x02e0;
'@ 'track native final position and user resize'

Swap "$r/DeskSpaceMonitorFenceV153.cs" @'
                else if(message==MovingMessage || message==DpiChanged)
'@ @'
                else if(message==WindowPosChanged)
                {
                    DeskSpaceNativeClipV154.Apply(hwnd,work);
                }
                else if(message==MovingMessage || message==SizingMessage ||
                    message==DpiChanged)
'@ 'last-resort paint clipping after Windows/WPF changes its mind'

Swap "$r/DeskSpaceMonitorFenceV153.cs" @'
            if(!Source.IsDisposed)Source.RemoveHook(Hook);
'@ @'
            if(!Source.IsDisposed)
            {
                Source.RemoveHook(Hook);
                DeskSpaceNativeClipV154.Release(Source.Handle);
            }
'@ 'cleanup native region on window teardown'

Swap "$r/DeskSpaceMonitorFenceV153.cs" @'
        if(fit.X!=native.Left||fit.Y!=native.Top||
           fit.Width!=native.Width||fit.Height!=native.Height)
            SetWindowPos(source.Handle,IntPtr.Zero,fit.X,fit.Y,fit.Width,fit.Height,
                NoZOrder|NoActivate);
'@ @'
        if(fit.X!=native.Left||fit.Y!=native.Top||
           fit.Width!=native.Width||fit.Height!=native.Height)
            SetWindowPos(source.Handle,IntPtr.Zero,fit.X,fit.Y,fit.Width,fit.Height,
                NoZOrder|NoActivate);
        DeskSpaceNativeClipV154.Apply(source.Handle,screen.Value);
'@ 'native clipping even at first show'

# Upgrade WPF smoke with a real WPF expanded Window and real Popup in a
# physically available display split into two simulated monitor rectangles.
$smoke='r36/UiSmoke/Program.cs'
Swap $smoke @'
            TestMonitorPopupsV153();
'@ @'
            TestMonitorPopupsV153();
            TestIndependentMonitorsV154();
'@ 'live expansion smoke gate'

$test=@'
    private static void TestIndependentMonitorsV154()
    {
        var fence=Product.GetType("DeskSpace.DeskSpaceMonitorFenceV153",true)!;
        var testHook=fence.GetMethod("TrackWindowInAreaForTest",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var clip=Product.GetType("DeskSpace.DeskSpaceNativeClipV154",true)!;
        var visible=clip.GetMethod("IntersectInWindow",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var screen=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var leftHalf=new System.Drawing.Rectangle(screen.Left,screen.Top,
            Math.Max(550,screen.Width/2),screen.Height);
        var rightHalf=new System.Drawing.Rectangle(leftHalf.Right,screen.Top,
            screen.Right-leftHalf.Right,screen.Height);
        Assert(leftHalf.Right<=rightHalf.Left,
            "R154 test monitor virtual seam");
        var overlap=(System.Drawing.Rectangle)visible.Invoke(null,new object[]{
            new System.Drawing.Rectangle(leftHalf.Right-80,screen.Top+60,200,160),
            leftHalf})!;
        Assert(overlap.Left==0&&overlap.Width==80,
            "R154 screen clip must prevent neighbouring-monitor painting");
        var primary=new Window{Left=screen.Left+30,Top=screen.Top+30,
            Width=130,Height=130,ShowInTaskbar=false};
        var secondary=new Window{Left=rightHalf.Left+35,
            Top=rightHalf.Top+45,Width=120,Height=120,ShowInTaskbar=false};
        try
        {
            primary.Show();secondary.Show();
            testHook.Invoke(null,new object[]{primary,leftHalf});
            testHook.Invoke(null,new object[]{secondary,rightHalf});
            // This is the actual live WPF Width/Height expansion path,
            // not a fabricated Rectangle-only calculator.
            primary.Left=leftHalf.Right-125;
            primary.Width=400;
            primary.Height=400;
            secondary.Left=rightHalf.Left+10;
            secondary.Width=390;
            secondary.Height=390;
            primary.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
            var get=fence.GetMethod("WindowBounds",
                BindingFlags.Static|BindingFlags.NonPublic)!;
            var a=(System.Drawing.Rectangle)get.Invoke(null,new object[]{primary})!;
            var b=(System.Drawing.Rectangle)get.Invoke(null,new object[]{secondary})!;
            Assert(a.Left>=leftHalf.Left&&a.Right<=leftHalf.Right &&
                a.Top>=leftHalf.Top&&a.Bottom<=leftHalf.Bottom,
                "R154 primary expanded WPF HWND crossed monitor seam");
            Assert(b.Left>=rightHalf.Left&&b.Right<=rightHalf.Right &&
                b.Top>=rightHalf.Top&&b.Bottom<=rightHalf.Bottom,
                "R154 independent secondary expanded HWND crossed monitor seam");
            // Moving/closing the first must not influence the second.
            primary.Width=130;primary.Height=130;
            primary.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
            b=(System.Drawing.Rectangle)get.Invoke(null,new object[]{secondary})!;
            Assert(b.Left>=rightHalf.Left&&b.Right<=rightHalf.Right,
                "R154 collapsing primary must not relocate secondary instance");
        }
        finally{primary.Close();secondary.Close();}
        Console.WriteLine("R154 PASS: two real WPF windows independently expand and "+
            "collapse against simulated distinct screen seams.");
    }

'@
Swap $smoke '    private static void TestMonitorPopupsV153()' (
    $test.Replace($crlf,$lf)+'    private static void TestMonitorPopupsV153()'
) 'two real native WPF windows expand independently'

Write-Host 'R154: independent widget positions, pre-expand physical monitor positioning, and native per-screen paint clip.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$lf=[string][char]10
$crlf=[string][char]13+$lf
Copy-Item 'r153/DeskSpaceMonitorFenceV153.cs' "$root/DeskSpaceMonitorFenceV153.cs" -Force
function Swap([string]$path,[string]$before,[string]$after,[string]$why)
{
  $content=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
  $old=$before.Replace($crlf,$lf)
  $new=$after.Replace($crlf,$lf)
  $count=([regex]::Matches($content,[regex]::Escape($old))).Count
  if($count -ne 1){throw "R153 $why: expected one anchor; found $count"}
  [IO.File]::WriteAllText($path,$content.Replace($old,$new),
      [Text.UTF8Encoding]::new($false))
}

# Install a synchronous WM_WINDOWPOSCHANGING hook after R152 has assigned
# the monitor. The asynchronous R152 callback alone is not enough.
Swap "$root/DeskSpaceMonitorFenceV152.cs" @'
            Assigned[window]=screenName;
            if(Hooked.Add(window))
'@ @'
            Assigned[window]=screenName;
            DeskSpaceMonitorFenceV153.TrackWidget(window);
            if(Hooked.Add(window))
'@ 'track main and satellite HWNDs'

# Information arc and detail shelf are independent Popup HWNDs.
Swap "$root/DeskSpaceInformationV36.cs" @'
        owner.LocationChanged+=OwnerLocationChanged;
'@ @'
        DeskSpaceMonitorFenceV153.TrackPopup(owner,arc);
        DeskSpaceMonitorFenceV153.TrackPopup(owner,shelf);
        owner.LocationChanged+=OwnerLocationChanged;
'@ 'track information arc and shelf'

# Each Excel child-ring has its own Popup; parent arc and shelf also need
# registered containment, including when Excel redraws the ring.
Swap "$root/DeskSpaceExcelWheelV42.cs" @'
                StaysOpen=true,Child=Surface};
'@ @'
                StaysOpen=true,Child=Surface};
            DeskSpaceMonitorFenceV153.TrackPopup(owner,Popup);
'@ 'track nested Excel child rings'
Swap "$root/DeskSpaceExcelWheelV42.cs" @'
        excelTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
'@ @'
        DeskSpaceMonitorFenceV153.TrackPopup(owner,arc);
        DeskSpaceMonitorFenceV153.TrackPopup(owner,shelf);
        excelTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
'@ 'track Excel arc and preview'

# Running Applications has popups INSIDE other popups; inherit the anchor
# popup's already-selected monitor, rather than WPF's virtual desktop.
Swap "$root/DeskSpaceStableHoverV93.cs" @'
        iconCard.Tag=windowPopup;
'@ @'
        iconCard.Tag=windowPopup;
        DeskSpaceMonitorFenceV153.TrackPopup(heading,iconPopup);
        DeskSpaceMonitorFenceV153.TrackPopup(iconCard,windowPopup);
'@ 'track nested Running Apps flyouts'

# System media player is another independent popup.
Swap "$root/DeskSpaceMediaV18.cs" @'
            host.LocationChanged+=Reposition;
'@ @'
            DeskSpaceMonitorFenceV153.TrackPopup(host,popup);
            host.LocationChanged+=Reposition;
'@ 'track media popups'

# WPF smoke test uses real HwndSource and SetWindowPos, not a pure
# arithmetic check like R152 did.
$smoke='r36/UiSmoke/Program.cs'
Swap $smoke @'
            TestMonitorFenceV152();
'@ @'
            TestMonitorFenceV152();
            TestMonitorPopupsV153();
'@ 'call real WPF popup regression'
$test=@'
    private static void TestMonitorPopupsV153()
    {
        var t=Product.GetType("DeskSpace.DeskSpaceMonitorFenceV153",true)!;
        var clamp=t.GetMethod("Constrain",BindingFlags.Static|BindingFlags.NonPublic)!;
        System.Drawing.Rectangle Fit(System.Drawing.Rectangle r,
            System.Drawing.Rectangle bounds)=>
            (System.Drawing.Rectangle)clamp.Invoke(null,new object[]{r,bounds})!;
        var leftMonitor=new System.Drawing.Rectangle(-1600,0,1600,870);
        var rightMonitor=new System.Drawing.Rectangle(0,0,1920,1040);
        var edge=Fit(new System.Drawing.Rectangle(-100,700,350,350),leftMonitor);
        Assert(edge.Left==-350 && edge.Top==520 && edge.Right==0,
            "R153 widget right edge must not extend into adjacent screen");
        var negative=Fit(new System.Drawing.Rectangle(-1800,30,280,250),leftMonitor);
        Assert(negative.Left==-1600 && negative.Right==-1320,
            "R153 negative virtual-screen origin must stay on monitor");
        var full=Fit(new System.Drawing.Rectangle(1810,950,550,400),rightMonitor);
        Assert(full.X==1370 && full.Y==640 && full.Right==1920,
            "R153 expanded popup must fit within work area");

        var track=t.GetMethod("TrackPopup",BindingFlags.Static|BindingFlags.NonPublic)!;
        var move=t.GetMethod("TryMovePopupForTest",BindingFlags.Static|BindingFlags.NonPublic)!;
        var bounds=t.GetMethod("PopupBounds",BindingFlags.Static|BindingFlags.NonPublic)!;
        var screen=System.Windows.Forms.Screen.PrimaryScreen!;
        var work=screen.WorkingArea;
        var host=new Window{Width=250,Height=170,Left=90,Top=85,
            ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        var child=new Border{Width=205,Height=135,Background=Brushes.DarkSlateBlue};
        var popup=new System.Windows.Controls.Primitives.Popup{
            PlacementTarget=host,
            Placement=System.Windows.Controls.Primitives.PlacementMode.Relative,
            HorizontalOffset=20,VerticalOffset=20,
            AllowsTransparency=true,StaysOpen=true,Child=child};
        try
        {
            host.Show();
            track.Invoke(null,new object[]{host,popup});
            popup.IsOpen=true;
            host.Dispatcher.Invoke(() => {},DispatcherPriority.ContextIdle);
            Assert((bool)move.Invoke(null,new object[]{
                popup,work.Right+140,work.Bottom+80})!,
                "R153 real popup native position test could not move HWND");
            var value=bounds.Invoke(null,new object[]{popup});
            Assert(value is System.Drawing.Rectangle,
                "R153 WPF popup did not create an HWND");
            var actual=(System.Drawing.Rectangle)value!;
            Assert(actual.Left>=work.Left && actual.Top>=work.Top &&
                actual.Right<=work.Right && actual.Bottom<=work.Bottom,
                "R153 real WPF Popup HWND crossed monitor after native SetWindowPos");
            popup.HorizontalOffset=work.Right+2000;
            host.Dispatcher.Invoke(() => {},DispatcherPriority.ContextIdle);
            actual=(System.Drawing.Rectangle)bounds.Invoke(null,new object[]{popup})!;
            Assert(actual.Left>=work.Left && actual.Right<=work.Right,
                "R153 WPF reposition should not leak popup into adjacent monitor");
        }
        finally
        {
            popup.IsOpen=false;
            host.Close();
        }
        Console.WriteLine("R153 PASS: actual WPF Popup HWND stays within the monitor "+
            "through native moves, WPF reposition, and negative-screen cases.");
    }

'@
Swap $smoke '    private static void TestMonitorFenceV152()' (
  $test.Replace($crlf,$lf)+'    private static void TestMonitorFenceV152()'
) 'inject native Win32 popup tests'
Write-Host 'R153: linked pre-paint HWND confinement to widget, Information, Excel, Running Apps and media popups.'

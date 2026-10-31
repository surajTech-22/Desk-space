$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$widget="$root/WidgetHost.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($widget).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R231 "+$label+": expected one exact anchor; found "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
# The original drag code docked automatically at ANY screen edge, including
# the physical seam shared by monitor 1 and monitor 2. This is the user's
# precise magnet symptom. Docking is now an explicit user action only.
Swap @'
            KeepOnVisibleDesktop();
            if(IsNearScreenEdge())DockToEdge(manual:true);
            else SavePosition();
'@ @'
            // Do not interpret monitor seams as magnetic edges. A completed
            // drag always leaves the widget where the user placed it,
            // constrained only by its OWN monitor's native fence.
            KeepOnVisibleDesktop();
            SavePosition();
'@ 'remove automatic docking after drag'
Swap @'
    // The tiny edge handle is independent of the circle.
    // Dock when any display boundary is reached, after mouse release.
    private bool IsNearScreenEdge()
    {
        var centre = new System.Drawing.Point((int)(Left + Width / 2), (int)(Top + Height / 2));
        var bounds = Forms.Screen.FromPoint(centre).WorkingArea;
        const double snap = 22;
        return Left <= bounds.Left + snap ||
               Top <= bounds.Top + snap ||
               Left + Width >= bounds.Right - snap ||
               Top + Height >= bounds.Bottom - snap;
    }

'@ @'
    // R231: no implicit 22-pixel edge attraction while dragging. The
    // explicit tray menu "Hide at screen edge" remains supported.

'@ 'remove accidental seam docking predicate'
Swap @'
        if(hiddenAtEdge) {hiddenAtEdge=false;dockedManually=false;edgeHandle.Hide();Left=prefs.Left??Left;Top=prefs.Top??Top;}
'@ @'
        if(hiddenAtEdge)
        {
            // prefs.Left/Top are the PRIMARY widget's legacy settings.
            // A secondary revealing from its edge handle must never
            // teleport to that shared location. Hide() never moved the
            // widget HWND; simply reveal it at its own saved location.
            hiddenAtEdge=false;
            dockedManually=false;
            edgeHandle.Hide();
        }
'@ 'stop restoring global position onto secondary'
Swap @'
        var p = new System.Drawing.Point((int)(Left+Width/2), (int)(Top+Height/2));
        var monitor=Forms.Screen.FromPoint(p).WorkingArea;
'@ @'
        // WPF Left/Top are DIPs, not Windows screen pixels. Using FromPoint
        // with DIPs assigned a mixed-DPI widget to the wrong monitor.
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var monitor=(DeskSpaceMonitorFenceV152.BoundScreen(this) ??
            Forms.Screen.FromHandle(hwnd)).WorkingArea;
'@ 'explicit edge handle uses assigned display'
Swap @'
            var work=SystemParameters.WorkArea;
            Left=Math.Clamp(Left,work.Left+42,
                Math.Max(work.Left+42,work.Right-OpenDiameter-44));
            Top=Math.Clamp(Top,work.Top+103,
                Math.Max(work.Top+103,work.Bottom-OpenDiameter));
            KeepOnVisibleDesktop();
'@ @'
            // SystemParameters.WorkArea describes the PRIMARY display.
            // Never pull a secondary widget toward its neighbour when the
            // Information arc opens. The R152/R153 native HWND fence already
            // constrains this widget and its popups to the assigned monitor.
            DeskSpaceMonitorFenceV152.ClampNow(this);
'@ 'information arc no primary work area attraction'
[IO.File]::WriteAllText($widget,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R231: removed 22px magnetic drag docking; secondary Reveal no shared prefs; explicit dock and Information arc use owning monitor.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowPreviewV100.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R218 "+$reason+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
    internal static bool Stationary(Point before,Point after,double tolerance=2)=>
'@ @'
    // Inputs are native physical pixels. The preview placement must use
    // the hovered widget's work area, never SystemParameters.WorkArea
    // (which always refers to the primary display and is in WPF DIPs).
    internal static bool PlaceRightV218(double panelLeft,double panelRight,
        double workLeft,double workRight,double requiredWidth)
    {
        double right=Math.Max(0,workRight-panelRight);
        double left=Math.Max(0,panelLeft-workLeft);
        return right>=requiredWidth || right>=left;
    }

    internal static bool Stationary(Point before,Point after,double tolerance=2)=>
'@ 'native pixel geometry model'
Swap @'
            var work=SystemParameters.WorkArea;
            try
            {
                var left=panel.PointToScreen(new Point(0,0));
                var right=panel.PointToScreen(new Point(panel.ActualWidth,0));
                bool showRight=work.Right-right.X>=318 ||
                    work.Right-right.X >= left.X-work.Left;
'@ @'
            try
            {
                // PointToScreen reports PHYSICAL pixels. Anchor on this
                // panel's actual display (negative origins are valid).
                var left=panel.PointToScreen(new Point(0,0));
                var right=panel.PointToScreen(new Point(panel.ActualWidth,0));
                var centre=new System.Drawing.Point(
                    (int)Math.Clamp((left.X+right.X)/2,
                        int.MinValue,int.MaxValue),
                    (int)Math.Clamp(left.Y,int.MinValue,int.MaxValue));
                var work=System.Windows.Forms.Screen.FromPoint(centre).WorkingArea;
                var source=PresentationSource.FromVisual(panel);
                double dpi=source?.CompositionTarget?.TransformToDevice.M11??1;
                double needed=320*Math.Max(.5,dpi);
                bool showRight=PlaceRightV218(left.X,right.X,
                    work.Left,work.Right,needed);
'@ 'select true owner monitor and scale from its WPF transform'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R218: running-window preview side calculated in native pixels against its own monitor and DPI.'

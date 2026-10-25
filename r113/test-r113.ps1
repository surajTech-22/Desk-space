$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$code=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceStableHoverV93.cs')
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
$required=@(
  'WindowSideResult ChooseWindowSide(',
  'right?targetWidth+gap:-popupWidth-gap',
  'WindowFlyoutX(outward,target.Width,size.Width,6)',
  'var monitor=screen.WorkingArea',
  'VisualTreeHelper.GetDpi(iconCard).DpiScaleX',
  'windowCard.Width=sideLayout.WidthDip;',
  'if(!sideLayout.CanOpen)',
  'windowPopup.Placement=PlacementMode.Custom',
  'windowPopup.CustomPopupPlacementCallback=',
  'var headingPoint=heading.PointToScreen(',
  'double width=Math.Min(276,right?r:l)'
)
foreach($needle in $required){
 if(!$code.Contains($needle)){throw "R113 missing side-window rule: $needle"}
}
if($code.Contains('windowPopup.Placement= pendingRight ?')){
 throw 'R113 regression: WPF auto flipping left/right toward deck centre remains'
}
foreach($needle in @('TestRunningAppsSideOnlyV113();',
    'var rightEdge=Test(610,18,1,false)',
    'var leftEdge=Test(16,590,1,true)',
    'var narrow=Test(105,98,1.0,false)',
    'var dpiMonitor=Test(180,700,2.0,true)')){
 if(!$smoke.Contains($needle)){throw "R113 WPF smoke missing $needle"}
}
Write-Host 'R113 PASS: placement callback, monitor-edge, centre-avoidance, and WPF smoke coverage present.'

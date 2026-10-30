$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowPreviewV100.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'PlaceRightV218(double panelLeft,double panelRight,',
 'panel.PointToScreen(new Point(0,0))',
 'System.Windows.Forms.Screen.FromPoint(centre).WorkingArea',
 'TransformToDevice.M11',
 'double needed=320*Math.Max(.5,dpi);',
 'PlaceRightV218(left.X,right.X,',
 'popup.Placement=showRight?PlacementMode.Right:PlacementMode.Left;',
 'if(disposed||item==null||app==null||panel==null||'
)){
 if(!$s.Contains($needle)){throw ('R218 monitor-specific preview missing '+$needle)}
}
if($s.Contains('var work=SystemParameters.WorkArea;')){
 throw 'R218 preview must not derive secondary screen placement from primary WPF work area'
}
if(!$t.Contains('TestWindowPreviewMonitorV218();')){
 throw 'R218 native multi-monitor preview tests not registered'
}
Write-Host 'R218 STATIC PASS: stationary preview uses real hover monitor and physical pixel DPI geometry.'

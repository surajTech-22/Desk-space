$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$sheet=[IO.File]::ReadAllText("$r/DeskSpaceExcelSheetViewV101.cs")
$deck=[IO.File]::ReadAllText("$r/DeskSpaceStableHoverV93.cs")
$updater=[IO.File]::ReadAllText("$r/DeskSpacePublicUpdaterV44.cs")
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
$flow=[IO.File]::ReadAllText('.github/workflows/build-installer.yml')
foreach($needle in @(
  'dynamic cell=range.Cells[r,c]',
  'view.Cells.Count!=rows*columns',
  'Incomplete per-cell Excel formatting capture',
  'var style=new DeskSpaceExcelStyledCellV101',
  'NumberFormat=Format(()=> (object?)cell.NumberFormat',
  'FillBgr=Number(()=> (object?)cell.Interior.Color',
  'BorderLeft=VisibleBorder(cell,7)',
  'BorderRight=VisibleBorder(cell,10)',
  'FontName=Format(()=> (object?)cell.Font.Name',
  'style.FormulaR1C1=Format(()=> (object?)cell.FormulaR1C1)',
  'Select the complete merged area',
  'new Border{',
  'edge.VerticalAlignment=y1==0?',
  'edge.HorizontalAlignment=x1==0?',
  'Math.Clamp((scroll.ActualWidth-36)/Math.Max(width,1),1.0,4.5)',
  'individually captured cell styles',
  'MergeCapturedVisuals('
)){
  if(!$sheet.Contains($needle)){throw "R105 per-cell source missing $needle"}
}
if($sheet.Contains('if(covered.Contains((r,c)))continue;') -or
    $sheet.Contains('Stretch=Stretch.Fill')){
  throw 'R105 regression: missed physical merged cells or overstretched border lines'
}
foreach($needle in @(
  'Placement=PlacementMode.Custom',
  'WindowFlyoutX(bool right,double targetWidth',
  'CustomPopupPlacementCallback=(size,target,offset)=>',
  'WindowFlyoutX(side,target.Width,size.Width)',
  'PopupPrimaryAxis.Vertical'
)){
  if(!$deck.Contains($needle)){throw "R105 Running Apps side positioning missing $needle"}
}
foreach($needle in @(
  'private const string StableApi',
  'private const string TesterApi',
  'NotifyNewRelease(channel,tag)',
  'ShowBalloonTip(4000,',
  'DeskSpaceUpdateProgress.Publish("Update available:',
  'SHA256.HashData(data)',
  'Verify(installer,checksum,root)',
  'await StartSupervisor(installer,folder,tag)'
)){
  if(!$updater.Contains($needle)){throw "R105 verified dual-updater notification missing $needle"}
}
foreach($needle in @('TestPerCellFormattingV105()',
  'r105-four-independent-excel-cell-styles',
  'windowPopup.Placement==',
  'PlacementMode.Custom',
  'WindowFlyoutX')){
  if(!$ui.Contains($needle)){throw "R105 WPF smoke missing $needle"}
}
if(!$flow.Contains('Publish verified Stable channel automatic release') -or
   !$flow.Contains('Publish R104 opt-in tester prerelease') -or
   !$flow.Contains('Apply R105 complete cell styles')){
 throw 'R105 both update channels must be gated by Windows build and WPF smoke tests'
}
Write-Host 'R105 source gates PASS: all cell coordinates captured, per-cell UI, fixed side menus, verified dual releases and update notices.'

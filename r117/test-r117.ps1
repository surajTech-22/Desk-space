$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$bridge=[IO.File]::ReadAllText("$root/DeskSpaceExcelBridgeV16.cs")
$hover=[IO.File]::ReadAllText("$root/DeskSpaceStableHoverV93.cs")
$editor=[IO.File]::ReadAllText("$root/DeskSpaceExcelEditorV42.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($part in @('entry.SheetVisualV101=DeskSpaceExcelVisualCaptureV101.Capture(range)',
                   'entry.SheetVisualV101.Cells.Count!=rows*cols',
                   'return entry;')){
 if(!$bridge.Contains($part)){throw "R117 missing capture persistence: $part"}
}
if($bridge.Contains('The selected range is completely empty and unformatted'))
 {throw 'R117 old empty-template rejection still active'}
if(!$editor.Contains('edited.Cells.Count==0 && edited.SheetVisualV101==null'))
 {throw 'R117 format-only editor Save must be enabled even with zero value cells'}
if(!$hover.Contains('var iconRow=new Grid') -or
   !$hover.Contains('Grid.SetRow(tile,slot.Row)') -or
   !$hover.Contains('Grid.SetColumn(tile,slot.Column)') -or
   !$hover.Contains('PlanAppGridV117') -or
   $hover.Contains('var iconRow=new WrapPanel'))
 {throw 'R117 real grid UI not wired'}
if(!$smoke.Contains('TestRunningAppsGridV117();') -or
   !$smoke.Contains('SECOND-ROW CENTER'))
 {throw 'R117 WPF geometry tests not registered'}
Write-Host 'R117 static verification PASS: empty formatting-only template capture/save and real three-column running deck.'

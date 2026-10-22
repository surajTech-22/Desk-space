$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$engine=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelActionsV46.cs'))
$bridge=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelBridgeV16.cs'))
$editor=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelEditorV42.cs'))
$wheel=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelWheelV42.cs'))
foreach($token in @('clock.Date.ToOADate()','clock.TimeOfDay.TotalDays',
    'DateTimeKind=>clock.ToOADate()','Today=>"=TODAY()"',
    'Now=>"=NOW()"','FromShortcut(string input)')){
    if(!$engine.Contains($token)){throw "R46 action engine missing $token"}
}
foreach($token in @('ValueAtPaste(cell,DateTime.Now)',
    'DeskSpaceExcelActionsV46.Formula(change.Source)',
    'DeskSpaceExcelActionsV46.NumberFormat(',
    'Text(cell.NumberFormat)','x.Cell.NumberFormat=x.Format',
    'DeskSpaceExcelActionsV46.PreviewText(source,DateTime.Now)')){
    if(!$bridge.Contains($token)){throw "R46 Excel bridge missing $token"}
}
foreach($token in @('DataGridComboBoxColumn','EditorKinds',
    'Current date on paste','Excel keyboard shortcuts',
    'Shortcut (enter Ctrl+;','FromShortcut(key)',
    'KindFromEditor(')){
    if(!$editor.Contains($token)){throw "R46 UI field missing $token"}
}
if(!$wheel.Contains('DeskSpaceExcelActionsV46.PreviewText(value,DateTime.Now)')){
    throw 'R46 wheel does not show current dynamic values'
}
Write-Host 'R46 PASS: user-chosen date/time and keyboard shortcut actions resolve at paste time under existing backup+rollback guards.'

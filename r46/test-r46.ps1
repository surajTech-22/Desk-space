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
foreach($token in @(
    'DeskSpaceExcelActionsV46.Formula(change.Source)',
    'DeskSpaceExcelActionsV46.NativeSendKeys(',
    'app.SendKeys(',
    'GetForegroundWindow()!=excelHwnd',
    'cell.Formula=',
    'Text(cell.NumberFormat)','x.Cell.NumberFormat=x.Format',
    'DeskSpaceExcelActionsV46.PreviewText(source,DateTime.Now)')){
    if(!$bridge.Contains($token)){throw "R46 Excel bridge missing $token"}
}
foreach($token in @('DataGridComboBoxColumn','EditorKinds',
    'Excel keyboard shortcuts','FromShortcut(key)',
    'KindFromEditor(')){
    if(!$editor.Contains($token)){throw "R46 UI field missing $token"}
}
if(!$engine.Contains('Current date on paste (Ctrl+;)')){
    throw 'R46 current-date picker option missing'
}
if(!$wheel.Contains('DeskSpaceExcelActionsV46.PreviewText(value,DateTime.Now)')){
    throw 'R46 wheel does not show current dynamic values'
}
if($bridge.Contains('ValueAtPaste(cell,DateTime.Now)')){
    throw 'R51 native Excel date shortcut must not be precomputed by DeskSpace'
}
if(!$engine.Contains('NativeSendKeys(string kind)')){
    throw 'R51 missing Excel-native keyboard dispatch'
}
Write-Host 'R51 PASS: Excel receives actual formula or shortcut, no precomputed replacement values, backup and rollback retained.'

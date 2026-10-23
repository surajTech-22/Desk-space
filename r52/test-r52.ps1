$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$editor=Get-Content (Join-Path $root 'DeskSpaceExcelEditorV42.cs') -Raw
$bridge=Get-Content (Join-Path $root 'DeskSpaceExcelBridgeV16.cs') -Raw
foreach($word in @('Format note (not applied)',
  'ExcelFormat=cell.NumberFormat',
  'cell.NumberFormat=row.ExcelFormat;',
  'cell.RawValue=row.Value;','cell.RawValue;')){
  if(!$editor.Contains($word)){throw "R52 missing optional format note/raw mapping: $word"}
}
foreach($wrong in @('DateFormattedV50(cell.NumberFormat)',
  'DateTime.TryParse(row.Value',
  'NumberFormat(change.Source.ValueKind)')){
  if($editor.Contains($wrong)){throw "R52 source format depends on value: $wrong"}
}
if($bridge.Contains('cell.NumberFormat=change.Source.NumberFormat') -or
   $bridge.Contains('cell.NumberFormat=DeskSpaceExcelActionsV46.NumberFormat(')){
  throw 'R52 destination number format must remain owned by Excel'
}
foreach($word in @('app.SendKeys(DeskSpaceExcelActionsV46.NativeSendKeys(',
  'cell.Formula=DeskSpaceExcelActionsV46.Formula(change.Source)',
  'else cell.Value2=TypedValue(change.Source)',
  'SaveCopyAs(backup)')){
  if(!$bridge.Contains($word)){throw "R52 lost Excel native execution/backups: $word"}
}
Write-Host 'R52 PASS: optional stored format row, format-independent values, destination Excel number formats unchanged, native formula and shortcut actions.'

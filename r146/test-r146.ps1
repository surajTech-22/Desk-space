$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$format=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$wheel=[IO.File]::ReadAllText("$r/DeskSpaceExcelWheelV42.cs")
$store=[IO.File]::ReadAllText("$r/DeskSpaceExcelStoreV16.cs")
$cleanup=[IO.File]::ReadAllText("$r/DeskSpaceExcelTempCleanupV146.cs")
function Require([string]$source,[string]$token,[string]$label){
 if(!$source.Contains($token)){throw "R146 missing $label : $token"}
}
foreach($item in @(
    'R146: source Table1 already existing elsewhere is not a conflict.',
    'targetTableCount!=features.Tables.Count',
    'An unrelated or differently sized Excel Table overlaps the destination.',
    'BuildMissingStructure(live,originallyEmpty);',
    'book.SaveCopyAs(backup);',
    'KeepBlankDestinationValuesV136(live);'
)){Require $format $item 'repeated format table policy'}
if($format.Contains('Excel Table name already exists in workbook.'))
 {throw 'R146 still rejects source Table1 collision'}
if($format.Contains('created.Name=table.Name;'))
 {throw 'R146 tries to steal/duplicate a workbook-wide Table name'}
foreach($item in @(
  'PasteFormulasV146Async(entry)',
  'PasteSpecialFormulasAsync(entry)',
  'ƒx  Paste saved formulas only',
  'Donut click applies FORMATS',
  'DeskSpaceExcelTempCleanupV146.CleanIfDue();',
  'PasteFormatV130Async('
)){Require $wheel $item 'donut formula action'}
foreach($item in @(
  'DeskSpaceExcelTempCleanupV146.CleanIfDue();',
  'formats.dpapi',
  'templates.dpapi'
)){Require ($store+$cleanup) $item 'mapping temporary cleanup'}
foreach($item in @('templates.dpapi.tmp','formats.dpapi.tmp',
  'TimeSpan.FromHours(24)','FileShare.None')){
 Require $cleanup $item 'safe cleanup whitelist'
}
dotnet run --project r146/tests/ExcelTempCleanupTests.csproj --configuration Release
if($LASTEXITCODE -ne 0){throw 'R146 safe temp-cleanup regression failed'}
Write-Host 'R146 PASS: duplicate Table1 no longer rejected; Excel auto-naming stays on; donut formats and formula paste separated; stale mapping .tmp cleanup retains original Excel and backup files.'

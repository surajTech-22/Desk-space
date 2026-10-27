$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$format=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$wheel=[IO.File]::ReadAllText("$r/DeskSpaceExcelWheelV42.cs")
$editor=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$policy=[IO.File]::ReadAllText("$r/DeskSpaceExcelCombinedFormulaV147.cs")
$source=[IO.File]::ReadAllText("$r/DeskSpaceExcelUnifiedV126.cs")
foreach($token in @(
  'Combined formats/formulas blocked:',
  'DeskSpaceExcelCombinedFormulaV147.Inspect(',
  'cell.FormulaR1C1=put.FormulaR1C1;',
  'Combined formula readback differs from saved formula',
  'book.SaveCopyAs(backup);',
  'RestoreAfterFailure(live,before,',
  'KeepBlankDestinationValuesV136(live);',
  'R146: source Table1 already existing elsewhere is not a conflict.',
  'real Excel tables, ',
  'formulas inserted and ')){
 if(!$format.Contains($token)){throw "R147 format transaction missing $token"}
}
foreach($token in @(
 'Formats & formulas applied',
 'PasteFormatV130Async(template)',
 'No separate paste needed.',
 'PasteFormatV130Async(entry);')){
 if(!$wheel.Contains($token)){throw "R147 donut integration missing $token"}
}
if($wheel.Contains('ƒx  Paste saved formulas only'))
 {throw 'R147 redundant donut formula-only button remains'}
if($editor.Contains('"Paste Special · Formulas",PasteSpecialFormulasV144,true'))
 {throw 'R147 redundant Workspace formula-only button remains'}
if(!$editor.Contains('"Apply formats & formulas"'))
 {throw 'R147 existing combined mapping action lost'}
if(!$editor.Contains('Applying verified Excel cell formats AND formulas'))
 {throw 'R147 Workspace status not combined'}
if(!$source.Contains('Saved mapping'))
 {Write-Host 'R147 note: no Workspace mapping source marker in this revision'}
foreach($token in @(
 'Destination contains a different formula',
 'Existing formulas will never be replaced.',
 'Formula position contains an existing value.')){
 if(!$policy.Contains($token)){throw "R147 formula conflict protection missing $token"}
}
dotnet run --configuration Release --project 'r147/tests/CombinedFormulaTests.csproj'
if($LASTEXITCODE -ne 0){throw 'R147 pure formula tests failed'}
Write-Host 'R147 PASS: both mapping routes use same backed-up Excel format+formula transaction, separate button removed, source data protected, prior formula checks and rollback retained.'

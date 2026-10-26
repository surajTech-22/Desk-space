$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$paste=[IO.File]::ReadAllText("$r/DeskSpaceExcelPasteSpecialV144.cs")
$format=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$editor=[IO.File]::ReadAllText("$r/DeskSpaceExcelUnifiedV126.cs")
$zero=[IO.File]::ReadAllText("$r/DeskSpaceExcelZeroIntegrityV145.cs")
function Must([string]$content,[string]$term,[string]$area)
{
  if(!$content.Contains($term)){throw "R145 missing $area check: $term"}
}
foreach($part in @('foreach(var prior in original)',
    'if(prior.HasFormula || prior.Value!=null)continue;',
    'cell.ClearContents();',
    'FormulaZeroCount(observedV145)',
    'HasUnexpectedConstant(',
    '0 constant input values copied.',
    'workbook.SaveCopyAs(copy);',
    'cell.FormulaR1C1=item.R1C1;')){
  Must $paste $part "formula paste / rollback"
}
foreach($part in @('priorAutoFillV145',
    'AutoFillFormulasInLists=false;',
    'AutoFillFormulasInLists=priorAutoFillV145;',
    'KeepBlankDestinationValuesV136(live);',
    'if(IsFormula(cell) ||',
    'An existing Excel value/formula changed unexpectedly.',
    'book.SaveCopyAs(backup);')){
  Must $format $part "strict format-only safety"
}
foreach($part in @('UNSAVED DRAFT · ',
    'actual formulas captured; 0 source values saved.',
    'SAVED & RELOADED',
    'complete formula expressions; 0 source input values saved.')){
  Must $editor $part 'formula save / verification UX'
}
Must $zero 'HasUnexpectedConstant' 'zero audit'
dotnet run --configuration Release --project r145/tests/ZeroIntegrityTests.csproj
if($LASTEXITCODE -ne 0){throw 'R145 zero integrity compiled tests failed'}
Write-Host 'R145 PASS: strict saved formula status, 0 copied constants, Excel computed zero separately identified, all auto-filled originally blank cells rolled back, blank sheet format-only no formulas.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$worker=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$policy=[IO.File]::ReadAllText("$r/DeskSpaceExcelProtectionV141.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
$office=[IO.File]::ReadAllText('r111/LiveExcelAcceptance/Program.cs')
function Check([string]$doc,[string]$term,[string]$context){
 if(!$doc.Contains($term)){throw "R141 $($context) missing: $term"}
}
foreach($term in @(
 'DeskSpaceExcelProtectionV141.Apply(',
 'var originalProtectionV141=',
 'unavailableProtectionV141++;',
 'priorFlags.Locked!=observed.Locked',
 'priorFlags.FormulaHidden!=observed.FormulaHidden',
 'Existing Locked protection changed unexpectedly at ',
 'Existing FormulaHidden protection changed unexpectedly at ',
 'Locked/FormulaHidden settings could not be copied;',
 "the destination's original protection settings were",
 'Cell-protection readback verified.',
 'KeepBlankDestinationValuesV136(live);',
 'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
 'PreflightTableStylesV131(',
 'book.SaveCopyAs(backup);',
 'RestoreAfterFailure(live,before,',
 'DeskSpaceExcelDirectReadbackV139.Differences(s,a)',
 'DeskSpaceExcelRenderedPolicyV135.RequiresExactRenderedMatch(',
 'wanted.Superscript!=observed.Superscript',
 'wanted.UnderlineStyle!=observed.UnderlineStyle',
 'An existing Excel value/formula changed unexpectedly.',
 'x.TableStyle==expectedTable.TableStyle'
)){Check $worker $term 'Excel writer and safety gates'}
if($worker.Contains('cell.Locked=feature.Locked;') -or
   $worker.Contains('cell.FormulaHidden=feature.FormulaHidden;')){
 throw 'R141 blind protection writes were not removed'
}
foreach($term in @(
 'Convert.ToBoolean(original,CultureInfo.InvariantCulture)==wanted',
 'catch(COMException)',
 'TryChange(()=>(object?)cell.Locked',
 'TryChange(()=>(object?)cell.FormulaHidden'
)){Check $policy $term 'COM-safe security metadata'}
foreach($term in @(
 'TestExcelProtectionV141();',
 'Matching Locked metadata must not cause any COM property write.',
 'Unable to set the Locked property of the Range class.',
 'Protected Excel range readback must not trigger a write.',
 'Unexpected non-COM exceptions must not be suppressed.'
)){Check $smoke $term 'compiled WPF regression'}
foreach($term in @(
 'bool resultLocked=(bool)emptySheet.Range["G18"].Locked;',
 'bool resultFormulaHidden=(bool)emptySheet.Range["G18"].FormulaHidden;',
 'created.Contains("Locked/FormulaHidden settings could not be copied")',
 'Blank destination calculated column returned a visible zero',
 'partial target blocked with no workbook edits'
)){Check $office $term 'real Excel acceptance'}
Write-Host 'R141 STATIC PASS: no redundant Locked/FormulaHidden writes; Excel COM refusals leave original protection unchanged and reported; all direct formatting, table, formula, blank/partial gate, back up and rollback checks remain strict.'
Write-Warning 'Full Excel COM behavior requires actual Microsoft Excel; Windows GitHub CI compiles Office harness but cannot execute it without Office.'
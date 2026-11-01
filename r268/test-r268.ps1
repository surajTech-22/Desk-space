$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$canonical=[IO.File]::ReadAllText('src/DeskSpace/DeskSpaceExcelFormatApplyV109.cs')
$patch=[IO.File]::ReadAllText('r266/apply-r266.ps1')
$live=[IO.File]::ReadAllText('r111/LiveExcelAcceptance/Program.cs')
foreach($term in @(
 'private static void RestoreCapturedFormulasV266(Checked live)',
 'if(IsFormula(cell))',
 'DeskSpaceExcelFormulaPlanV266.Matches(item,true,',
 'Excel auto-filled a different formula than the captured template.',
 'cell.FormulaR1C1=item.FormulaR1C1;',
 'RestoreCapturedFormulasV266(live);',
 'DeskSpaceExcelBackupReceiptV252.Verify(backup,backupReceiptV252)',
 'RestoreAfterFailure(live,before,'
)){
 if(!$canonical.Contains($term) -or (!$patch.Contains($term) -and
    $term -eq 'Excel auto-filled a different formula than the captured template.')){
  throw 'R268 canonical Excel writer or reconstruction lacks protection: '+$term
 }
}
foreach($term in @(
 'eight exact R1C1 formulas restored',
 'restoredFormulas==8',
 '=SUM(RC[-4]:RC[-1])',
 '=SUM(R[-3]C:R[-1]C)',
 'Non-formula input cell was populated',
 'Fully populated matching destination must pass',
 'A partially populated destination was accepted',
 'R268 LIVE MICROSOFT EXCEL FORMAT+FORMULA ACCEPTANCE PASSED'
)){
 if(!$live.Contains($term)){
  throw 'R268 real Office acceptance does not verify: '+$term
 }
}
if($live.Contains('all body cells blank with zero formula injection.') -or
   $live.Contains('Every source formula column must remain blank')){
 throw 'R268 live Office assertions still expect obsolete formula-suppression behavior'
}
Write-Host 'R268 STATIC PASS: exact Table auto-fill guard and real Office eight-formula, blank/full/partial acceptance updated.'

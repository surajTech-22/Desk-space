$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceExcelFormatApplyV109.cs")
$p=[IO.File]::ReadAllText("$root/DeskSpaceExcelFormulaPlanV266.cs")
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($x in @(
 'RestoreCapturedFormulasV266(live);',
 'KeepBlankDestinationValuesV136(live);',
 'cell.FormulaR1C1=item.FormulaR1C1;',
 'DeskSpaceExcelFormulaPlanV266.Matches(recordedV266,',
 'Source positions are incomplete',
 'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
 'originallyEmpty',
 'book.SaveCopyAs(backup);',
 'DeskSpaceExcelBackupReceiptV252.Verify(backup,backupReceiptV252)',
 'RestoreAfterFailure(live,before,',
 'TestExcelFormulaAndFormatPasteV266();',
 'cell.TableHeader',
 'cell.MergedChild',
 'DeskSpaceFormulaMetadataV251.Valid('
)){
 if(!$s.Contains($x) -and !$p.Contains($x) -and !$t.Contains($x)){
  throw 'R266 missing formula and safety integration: '+$x
 }
}
if($s.Contains('PopulateBlankFormulas(live);')){
 throw 'R266 obsolete unvalidated formula injection route must remain removed'
}
$blank=$s.IndexOf('KeepBlankDestinationValuesV136(live);')
$formula=$s.IndexOf('RestoreCapturedFormulasV266(live);')
if($blank -lt 0 -or $formula -lt $blank){
 throw 'R266 must clear Excel-generated contents BEFORE inserting source formulas'
}
Write-Host 'R266 STATIC PASS: captured formulas applied only on blank target after cleanup, formula readback and backup/rollback retained.'

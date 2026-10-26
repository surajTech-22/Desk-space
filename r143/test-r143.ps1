$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$view=[IO.File]::ReadAllText("$root/DeskSpaceExcelSheetViewV101.cs")
$writer=[IO.File]::ReadAllText("$root/DeskSpaceExcelFormatApplyV109.cs")
$policy=[IO.File]::ReadAllText("$root/DeskSpaceExcelFormulaPasteV143.cs")
foreach($token in @(
 'Text=isFormula?', 'item!.FormulaA1.Length>0?item.FormulaA1:item.FormulaR1C1',
 'Cell inspection below already shows its exact A1 expression.')){
 if(!$view.Contains($token)){throw "R143 in-cell formula missing: $token"}
}
foreach($ban in @('formulaBarV142','formulaTextV142','formulaAddressV142',
                    'Text=isFormula?"ƒx"')){
 if($view.Contains($ban)){throw "R143 obsolete formula-only widget remains: $ban"}
}
foreach($token in @(
 'DeskSpaceExcelFormulaPasteV143.Ready(order,to)',
 'DeskSpaceExcelFormulaPasteV143.Explanation',
 'DeskSpaceExcelFormulaPasteV143.WriteExactFormulas(',
 'Inserted formula readback did not exactly match',
 'An existing Excel value/formula changed unexpectedly.',
 'KeepBlankDestinationValuesV136(live);',
 'RestoreAfterFailure(live,before,',
 'book.SaveCopyAs(backup);')){
 if(!$writer.Contains($token)){throw "R143 guarded formula writer missing: $token"}
}
if($writer.Contains('PopulateBlankFormulas(live);')){throw 'R136 no-zero guard regressed'}
foreach($token in @('return hasInput && hasMissingFormula;',
 'if(dst.HasData ||','if(!dst.HasData ||',
 'cell.FormulaR1C1=src.FormulaR1C1;')){
 if(!$policy.Contains($token)){throw "R143 formula policy missing: $token"}
}
dotnet run --configuration Release --project r143/tests/FormulaPasteTests.csproj
if($LASTEXITCODE -ne 0){throw 'R143 formula-gate compiled regression failed'}
Write-Host 'R143 PASS: inline complete formula source, no redundant separate panel, strict fully-matching input gate, blank target no zeros, exact formula placement and rollback.'

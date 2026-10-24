$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$normalize=[IO.File]::ReadAllText("$r/DeskSpaceExcelNormalizeV102.cs")
$bridge=[IO.File]::ReadAllText("$r/DeskSpaceExcelBridgeV16.cs")
$worker=[IO.File]::ReadAllText("$r/DeskSpaceExcelWorkerV41.cs")
$editor=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
foreach($needle in @(
 'DeskSpaceExcelBridgeV16.ExcelForNormalizationV102()',
 'plan.MappingId!=confirmedMappingId',
 'OriginalText=Text(actual)',
 'DeskSpaceExcelInputTypeV101.Normalize(',
 'if(Formula(cell))',
 'if(expectedCell.ExpectedType=="Formula")',
 'plan.Warnings.Add(',
 'workbook.SaveCopyAs(backup)',
 'if(!File.Exists(backup)',
 'item.Cell.Value2=item.OldValue',
 'item.Cell.NumberFormat=item.OldFormat',
 'cell.Value=date',
 'cell.NumberFormat=change.Format')){
 if(!$normalize.Contains($needle)){throw "R102 safe Excel normalization missing: $needle"}
}
foreach($needle in @(
 'ExcelForNormalizationV102()=>Excel()',
 'ReviewTypesAsync(',
 'RepairTypesAsync(')){
 if(!$bridge.Contains($needle) -and !$worker.Contains($needle)){
  throw "R102 Excel worker bridge missing: $needle"}
}
foreach($needle in @(
 'Check new input types',
 'Apply reviewed corrections',
 'CheckInputTypesV102()',
 'ApplyInputTypesV102()',
 'pendingTypeRepairV102=null;',
 'Month first · MM/DD/YYYY',
 'Ask if ambiguous',
 'MessageBoxButton.YesNo',
 'ReviewTypesAsync(',
 'RepairTypesAsync(')){
 if(!$editor.Contains($needle)){throw "R102 preview-confirm UI missing: $needle"}
}
if($normalize.Contains('workbook.Save()') -or
   $normalize.Contains('Process.Start(') -or
   $normalize.Contains('workbook.Close(')){
 throw 'R102 cannot launch Excel, save the original workbook or close it'
}
Write-Host 'R102 PASS: new input types preview with explicit review, revalidation, backups, safe date parsing and native formatting.'

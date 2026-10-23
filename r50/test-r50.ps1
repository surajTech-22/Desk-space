$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$editor=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelEditorV42.cs'))
$bridge=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelBridgeV16.cs'))
$actions=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelActionsV46.cs'))
foreach($requirement in @(
    'editingIdV42=entry.Id;editingDraftV42=false',
    'if(editingTemplateV42!=null && editingIdV42!=Guid.Empty',
    'existing.SourceWorkbook,candidate.SourceWorkbook',
    'existing.SourceSheet,candidate.SourceSheet',
    'editingTemplateV42=existing;',
    'edited.Id=editingIdV42;',
    'disk[index]=edited;',
    'if(index<0)throw new InvalidOperationException(',
    'disk.Add(edited);',
    'OriginalShown=shown',
    'ExcelFormat=cell.NumberFormat',
    'Format note (not applied)',
    'cell.RawValue=row.Value;',
    'cell.NumberFormat=row.ExcelFormat;'
)){
    if(!$editor.Contains($requirement)){throw "R50 mapping update/date format missing: $requirement"}
}
if(!$editor.Contains('if(!editingDraftV42)') -or
   !$editor.Contains('else'+[Environment]::NewLine+'            {' )){
    throw 'R50 distinguish update-by-ID from Create-by-ID'
}
foreach($bad in @('if(existing>=0)disk[existing]=edited;',
                 'row.Value!=row.OriginalRaw)')){
    if($editor.Contains($bad)){throw "R50 unsafe old save path retained: $bad"}
}
foreach($token in @('cell.Formula=DeskSpaceExcelActionsV46.Formula(change.Source)',
     'app.SendKeys(DeskSpaceExcelActionsV46.NativeSendKeys(',
     'GetForegroundWindow()!=excelHwnd',
     'app.ActiveWindow')){
    if(!$bridge.Contains($token)){throw "R51 Excel action not native: $token"}
}
if($bridge.Contains('ValueAtPaste(cell,DateTime.Now)')){
    throw 'R51 precomputed date still written instead of native Excel shortcut'
}
foreach($required in @('Ctrl+; → Excel inserts','Excel inserts the current',
     'NativeSendKeys(string kind)')){
    if(!$actions.Contains($required)){throw "R51 symbolic shortcut display missing: $required"}
}
if($bridge.Contains('cell.NumberFormat=change.Source.NumberFormat') -or
   $bridge.Contains('cell.NumberFormat=DeskSpaceExcelActionsV46.NumberFormat(')){
    throw 'R52 must not overwrite the destination Excel format'
}
if($editor.Contains('DateFormattedV50(cell.NumberFormat)') -or
   $editor.Contains('DateTime.TryParse(row.Value')){
    throw 'R52 mapped values must not depend on a formatting note'
}
Write-Host 'R52 PASS: immutable mapping updates, raw mapped values, optional inert Excel format note and native actions.'

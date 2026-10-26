$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$editor=[IO.File]::ReadAllText("$root/DeskSpaceExcelEditorV42.cs")
$wheel=[IO.File]::ReadAllText("$root/DeskSpaceExcelWheelV42.cs")
$store=[IO.File]::ReadAllText("$root/DeskSpaceExcelStoreV16.cs")
$format=[IO.File]::ReadAllText("$root/DeskSpaceExcelFormatApplyV109.cs")
$bridge=[IO.File]::ReadAllText("$root/DeskSpaceExcelBridgeV16.cs")
foreach($needle in @(
    'Data mappings · copy / paste',
    'Formats & formulas · copy / paste',
    'routesV124.Items.Add(new TabItem',
    'Header="Data mappings · values"',
    'Header="Formats & formulas · separate library"',
    'formatPageV124.Children.Add(formatGroup);',
    'var title=formatNameV124?.Text.Trim();',
    '"Preview at selected Excel cell"',
    '"Paste reviewed mapping values"',
    'manual.IsChecked=true;',
    'Run(Preview);',
    'DeskSpaceExcelStoreV16.LoadFormats()',
    'DeskSpaceExcelStoreV16.SaveFormats(current)',
    'DeskSpaceExcelWorkerV41.ApplyFormatAsync(entry,approved)'
)) {
    if(!$editor.Contains($needle)){throw "R124 editor missing: $needle"}
}
if($editor.Contains('footer.Children.Add(formatGroup);')){
    throw 'Formats are still inside mapping footer'
}
if($editor.Contains('var title=mappingNameV42?.Text.Trim();')){
    throw 'Formatting still uses the mapping data name'
}
foreach($needle in @(
    'bool selectedCellPasteV124=false;',
    'selection:false,overwrite:true',
    'selection:true,overwrite:false',
    'if(!preview.Verified)',
    'MessageBoxButton.YesNo',
    'preview.StartRow',
    'overwrite:!selectedCellPasteV124',
    'if(accept!=MessageBoxResult.Yes)'
)) {
    if(!$wheel.Contains($needle)){throw "R124 widget selected-cell path missing: $needle"}
}
if(!$store.Contains('formats.dpapi') -or !$store.Contains('templates.dpapi')){
    throw 'R124 changed existing independent encrypted stores'
}
if(!$bridge.Contains('HeaderValueDecisionV120(') -or
   !$bridge.Contains('UsedReferenceSignatureV120')){
    throw 'R124 value safety/signature guards missing'
}
if(!$format.Contains('DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)') -or
   !$format.Contains('book.SaveCopyAs(backup)')){
    throw 'R124 formatting full-range gate/backup missing'
}
Write-Host 'R124 static PASS: distinct copy UIs/storage, selected-cell confirmation, retained live paste and formatting protections.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$editor=[IO.File]::ReadAllText("$root/DeskSpaceExcelEditorV42.cs")
$wheel=[IO.File]::ReadAllText("$root/DeskSpaceExcelWheelV42.cs")
$bridge=[IO.File]::ReadAllText("$root/DeskSpaceExcelBridgeV16.cs")
$format=[IO.File]::ReadAllText("$root/DeskSpaceExcelFormatApplyV109.cs")
$store=[IO.File]::ReadAllText("$root/DeskSpaceExcelStoreV16.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
    '"Information / Data · matched headers"',
    '"Format / Formula mappings · copy / paste"',
    'Header="Information / Data · header match"',
    'Header="Format / Formula mappings · separate library"',
    '"Preview verified data headers"',
    '"Paste matching information"',
    'manual.IsChecked=false;',
    'Information/data paste ONLY: header signature match',
    'formatPageV124.Children.Add(formatGroup);',
    'DeskSpaceExcelStoreV16.LoadFormats()',
    'DeskSpaceExcelWorkerV41.ApplyFormatAsync(entry,approved)'
)) {
    if(!$editor.Contains($needle)){throw "R125 Workspace route not correct: $needle"}
}
foreach($forbidden in @(
    '"Preview at selected Excel cell"',
    'manual.IsChecked=true;',
    'options.Children.Add(manual);',
    'Direct paste: select one start cell in Excel'
)){
    if($editor.Contains($forbidden)){throw "R125 information still allows manual fallback: $forbidden"}
}
foreach($needle in @(
    'selection:false,overwrite:true',
    'if(!preview.Verified)',
    'Data paste blocked; nothing changed.',
    'preview,entry,overwrite:true)'
)){
    if(!$wheel.Contains($needle)){throw "R125 widget strict route missing: $needle"}
}
foreach($bad in @(
    'selectedCellPasteV124',
    'selection:true,overwrite:false',
    'selected-cell value mapping',
    'Would you like to preview the mapping at the cell'
)){
    if($wheel.Contains($bad)){throw "R125 widget still prompts mismatch fallback: $bad"}
}
if(!$bridge.Contains('if(useSelection)') -or
   !$bridge.Contains('Information paste requires verified matching headers.')){
    throw 'R125 data engine still accepts unverified selected-cell paste'
}
if(!$bridge.Contains('HeaderValueDecisionV120(') -or
   !$bridge.Contains('UsedReferenceSignatureV120') -or
   !$bridge.Contains('FindAnchorMatch((object)sheet,entry)')){
    throw 'R125 unique header value signature or per-cell safety was lost'
}
if(!$format.Contains('HeadersIndependentV123(') -or
   !$format.Contains('DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)') -or
   !$format.Contains('dynamic app=Excel(), sheet=app.ActiveSheet, selected=app.Selection;') -or
   !$format.Contains('book.SaveCopyAs(backup)')){
    throw 'R125 format-only mapping requires wrong headers or lost independent selection/backup'
}
if(!$store.Contains('formats.dpapi') -or !$store.Contains('templates.dpapi'))
{throw 'R125 encrypted format/data libraries were changed'}
if(!$smoke.Contains('TestStrictInformationPasteV125();') -or
   !$smoke.Contains('TestFormatHeadersAndDeckV123();'))
{throw 'R125 compiled smoke guards missing'}
Write-Host 'R125 PASS: information paste blocks unmatched headers, no manual data fallback, independent selected-cell format mapping with full-range rule.'

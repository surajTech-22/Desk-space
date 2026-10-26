$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$ed=[IO.File]::ReadAllText("$root/DeskSpaceExcelEditorV42.cs")
$ctl=[IO.File]::ReadAllText("$root/DeskSpaceExcelUnifiedV126.cs")
$store=[IO.File]::ReadAllText("$root/DeskSpaceExcelStoreV16.cs")
$format=[IO.File]::ReadAllText("$root/DeskSpaceExcelFormatApplyV109.cs")
$wheel=[IO.File]::ReadAllText("$root/DeskSpaceExcelWheelV42.cs")
$bridge=[IO.File]::ReadAllText("$root/DeskSpaceExcelBridgeV16.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
    '"Information / Data · matched headers"',
    '"Format / Formula mappings · copy / paste"',
    'ActivateLibraryV126(false);',
    'Run(Capture);',
    'ActivateLibraryV126(true);',
    'Run(CaptureFormatDraftV126);',
    'ButtonV42("New Mapping"',
    'ButtonV42("Save Mapping"',
    'Header="Captured Sheet"',
    'Header="Mappings"',
    'unifiedTabsV126=tabs;',
    'root.Children.Add(content);',
    'templates.ItemTemplate=new DataTemplate{VisualTree=savedRow};',
    'DeleteUnifiedEntryV126(item.Id,formatModeV126)',
    'if(formatModeV126)',
    'if(formatModeV126)OnUnifiedLibrarySelectedV126();'
)){
    if(!$ed.Contains($needle) -and $needle -ne 'if(formatModeV126)OnUnifiedLibrarySelectedV126();'){
        throw "R126 missing unified UI: $needle"
    }
}
foreach($removed in @(
    'root.Children.Add(routesV124);',
    'routesV124=new TabControl',
    'Header="Information / Data · header match"',
    'Header="Format / Formula mappings · separate library"',
    'editorHead.Children.Add(editButtons);'
)){
    if($ed.Contains($removed)){throw "R126 duplicate module/action retained: $removed"}
}
foreach($needle in @(
    'DeskSpaceExcelStoreV16.LoadFormats()',
    'DeskSpaceExcelStoreV16.SaveFormats(records)',
    'DeskSpaceExcelStoreV16.Load()',
    'DeskSpaceExcelStoreV16.Save(records)',
    'if(!formatModeV126){SaveEditorV42();return;}',
    'DeskSpaceExcelSheetViewV101.Create(item)',
    'ShowFormatV126(formatDraftV126??selectedFormatV121)',
    'formatDraftV126=snapshot;',
    'snapshot.Cells.Clear();',
    'snapshot.Anchors.Clear();',
    'formatNameV124',
    'SelectedFormat',
    'pendingFormatReviewV109=null'
)){
    if($needle -eq 'SelectedFormat'){continue}
    if(!$ctl.Contains($needle)){throw "R126 library routing missing: $needle"}
}
if(!$store.Contains('formats.dpapi') -or !$store.Contains('templates.dpapi'))
{throw 'R126 saved encrypted libraries missing'}
if(!$bridge.Contains('Information paste requires verified matching headers.') -or
   !$wheel.Contains('Data paste blocked; nothing changed.') -or
   $wheel.Contains('selectedCellPasteV124')){
    throw 'R126 data/information paste no longer blocks mismatched headers'
}
if(!$format.Contains('HeadersIndependentV123(') -or
   !$format.Contains('DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)') -or
   !$format.Contains('book.SaveCopyAs(backup)')){
    throw 'R126 format mapping lost independent header/footprint backup route'
}
if($tests.Contains('"Mappings · edit & paste"') -or
   $tests.Contains('"Captured sheet · formats & formulas"')){
    throw 'R126 WPF acceptance is still asserting old nested tab names'
}
Write-Host 'R126 PASS: 2 top COPY actions, 1 shared Saved Mappings with per-row Delete, only 2 sheet/mapping tabs, library-aware Save, strict data paste, structural format paste.'

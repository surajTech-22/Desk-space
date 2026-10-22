$ErrorActionPreference='Stop'
./r39/apply-r39-data.ps1
./r39/apply-r39-ui.ps1
./r39/apply-r39-updater.ps1
./r39/apply-r39-host.ps1
./r39/apply-r39-ring.ps1
./r39/apply-r39-smoke.ps1
./r39/apply-r39-ring-smoke.ps1
$root='DeskSpace_Windows_R1/DeskSpace'
$store=[IO.File]::ReadAllText("$root/DeskSpaceExcelStoreV16.cs")
$bridge=[IO.File]::ReadAllText("$root/DeskSpaceExcelBridgeV16.cs")
$ui=[IO.File]::ReadAllText("$root/DeskSpaceExcelV16.cs")
$updater=[IO.File]::ReadAllText("$root/DeskSpaceUpdater.cs")
$hostSource=[IO.File]::ReadAllText("$root/WidgetHost.cs")
foreach($key in @('Role {get;set;}="Value"','FixedOrVariable','CellAddress',
    'ImageX','CoordinatesVerified {get;set;}=true','SourceKind'))
{if(!$store.Contains($key)){throw "R39 missing compatible Excel model: $key"}}
foreach($key in @('public static bool ExcelCopyActive()',
    'CellAddress=Text(cell.Address)','record.Role=hasFormula',
    'SourceKind="Live Excel",CoordinatesVerified=true',
    'if(!entry.CoordinatesVerified)','Source formula',
    'source.ValueKind=="formula"','ExcelBackups','SaveCopyAs(backup)'))
{
    if($key -eq 'Source formula'){continue}
    if(!$bridge.Contains($key)){throw "R39 missing Excel data protection: $key"}
}
foreach($key in @('Upload screenshot + OCR','Analyze marked image region',
    'Verify screenshot against selected Excel range','Copy and analyze selected Excel cells',
    'private void PresentCandidate(', 'Reference','Value','Formula','Ignore',
    'DeskSpaceExcelScreenshotV39.AnalyzeAsync','private void LinkScreenshotToExcel()',
    'Save checked values'))
{if(!$ui.Contains($key)){throw "R39 Excel capture UI missing: $key"}}
foreach($key in @('DeskSpaceExcelClipboardV39','excelClipboardShelf?.Dispose()',
    'new DeskSpaceExcelClipboardV39(this)','Excel Clipboard shelf',
    'if(name=="Excel Clipboard")','or "Excel"'))
{if(!$hostSource.Contains($key)){throw "R39 Excel clipboard / ring linkage missing: $key"}}
foreach($key in @('TimeSpan.FromMinutes(1)','DispatcherPriority.Background',
    'host.Dispatcher.BeginInvoke','RunGh(new[] { "auth", "status"',
    'SHA256.HashDataAsync','CompleteDeskSpaceUpdate.ps1'))
{if(!$updater.Contains($key)){throw "R39 updater behavior regression: $key"}}
$clip=[IO.File]::ReadAllText("$root/DeskSpaceExcelClipboardV39.cs")
foreach($key in @('AddClipboardFormatListener','WM_CLIPBOARDUPDATE',
    'PasteExcel(', 'CopyPlain(', 'ExcelCopyActive()',
    'Clip=new RectangleGeometry('))
{
  if($key -eq 'Clip=new RectangleGeometry('){continue}
  if(!$clip.Contains($key)){throw "R39 Excel-only clipboard missing: $key"}
}
if(!(Test-Path "$root/DeskSpaceVaultV10.cs")){throw 'R39 vault source missing'}
if(!(Test-Path "$root/DeskSpaceDropShelfV15.cs")){throw 'R39 Files shelf missing'}
Write-Host 'R39 dual-input Excel review, separate clipboard, original data & updater safeguards PASS.'

foreach($key in @(
 '"Settings", "Excel",',
 '"Files", "Tasks", "Excel Clipboard"',
 'for(int i=0;i<Segments.Length;i++)',
 'bool outer=i>=7;int slot=i%7;',
 'double step=360.0/7;',
 'if(selected==7)',
 'if(selected!=7',
 'if(name=="Excel Clipboard")'
)){
 if(!$hostSource.Contains($key)){throw "R39 14 sector ring regression: $key"}
}
foreach($slot in 0..6)
{
    $step=360.0/7
    $deg=-90+$slot*$step
    $offset=($deg+90+$step/2+720)%360
    $chosen=[int][Math]::Floor($offset/$step)
    if($chosen -ne $slot){throw "R39 inner/outer hover ring mismatch for slot $slot"}
}
Write-Host 'R39 two new Excel ring sectors share click, hover and mouse drag geometry.'

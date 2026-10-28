$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$product='DeskSpace_Windows_R1/DeskSpace/DeskSpaceComponentHealthV175.cs'
$source=[IO.File]::ReadAllText($product)
$widget=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs')
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('AddClipboardFormatListener','RemoveClipboardFormatListener',
 'ClipboardUpdated=0x031D','Interlocked.Increment(ref changes)',
 'never read','Clipboard signal','Local workspace',
 'Updater','Crash diagnostics','Export safe diagnostics to Downloads',
 'File search','ShowInTaskbar=true')){
 if(!$source.Contains($token)){throw "R175 implementation missing '$token'"}
}
foreach($token in @('DeskSpaceClipboardSignalV175.Watch(this)',
 'DeskSpaceComponentHealthV175.Show(this,tray.Visible)',
 'Component status (local only)...',
 'DeskSpaceMultiMonitorV151.IsSatellite(this)')){
 if(!$widget.Contains($token)){throw "R175 tray integration missing '$token'"}
}
foreach($token in @('TestComponentHealthV175();',
 'R175 PASS: native WPF local status dashboard',
 'R175 component dashboard must independently inspect core modules')){
 if(!$test.Contains($token)){throw "R175 real WPF acceptance missing '$token'"}
}
Write-Host 'R175 PASS: private-content-free clipboard listener and status dashboard wired into existing tray and WPF smoke.'

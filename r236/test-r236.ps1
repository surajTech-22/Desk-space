$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
$widget=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs')
$dock=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorDockV190.cs')
$fence=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceMonitorFenceV152.cs')
foreach($must in @(
 'TestSyntheticMonitorMatrixV236();',
 'private static void TestSyntheticMonitorMatrixV236()',
 'negative-left-100','positive-right-150','left-seam-100',
 'right-seam-125','upper-screen','lower-screen','negative-both',
 'small-screen','offset-monitor','stacked-right',
 'REAL TWO-MONITOR HARDWARE NOT VERIFIED BY THIS TEST',
 'Device("LEFT",new[]{"RIGHT","LEFT"},"RIGHT")',
 'Keep(new System.Drawing.Rectangle(-118,350,116,116),left).X==-118'
)){
 if(!$s.Contains($must)){throw ('R236 missing mixed-DPI test assertion: '+$must)}
}
if(!$dock.Contains('DeskSpaceDeckOffsetV228.Save(Device(),shift)')){
 throw 'R236 monitor-local Deck offset persistence missing'
}
if(!$dock.Contains('Deck position on this monitor')){
 throw 'R236 monitor-local Deck reset missing'
}
if(!$fence.Contains('ResolveActiveDeviceV214')){
 throw 'R236 reconnect-safe monitor identity missing'
}
if($widget.Contains('if(IsNearScreenEdge())DockToEdge(manual:true)')){
 throw 'R236 regression: magnetic seam docking reintroduced'
}
Write-Host 'R236 STATIC PASS: mixed DPI 100/125/150/200, negative/stacked/small monitor, independent Deck and seam regression covered.'

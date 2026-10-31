$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$a=[IO.File]::ReadAllText("$root/DeskSpacePostInstallReceiptV185.cs")
$b=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($item in @(
 'StableIntervalV224=TimeSpan.FromSeconds(12)',
 'ReadyAfterStabilityV224(WidgetShellWindow owner)',
 '!File.Exists(PendingPath)',
 'grace.Tick+=(_,_)=>{',
 'owner.Closed+=closed;',
 'owner.Closed-=closed;',
 'if(owner.IsLoaded && !DeskSpaceMultiMonitorV151.IsSatellite(owner))',
 'Ready(owner);',
 'Version.TryParse(version,out var actual) && actual==requested;'
)){
 if(!$a.Contains($item)){throw ("R224 safe postinstall flow missing "+$item)}
}
if(!$b.Contains('DeskSpacePostInstallReceiptV185.ReadyAfterStabilityV224(this);')){
 throw 'R224 WidgetHost must start delayed receipt'
}
if(!$smoke.Contains('TestPostInstallStabilityV224();')){
 throw 'R224 native WPF acceptance not registered'
}
Write-Host 'R224 STATIC PASS: no receipt before sustained primary widget Loaded, retains nonce/version guard.'

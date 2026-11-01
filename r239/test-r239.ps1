$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceMonitorDiagnosticsV239.cs")
$hub=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static IReadOnlyList<Display> Snapshot()',
 'Forms.Screen.AllScreens',
 'GetDpiForMonitor(',
 'DeskSpaceDeckOffsetV228.Read(screen.DeviceName)',
 'internal static string Summarize(Display display)',
 'Nothing is changed, ',
 'Refresh display information',
 'Refresh();'
)){
 if(!$s.Contains($needle)){throw ("R239 missing on-demand monitor diagnostics: "+$needle)}
}
foreach($forbidden in @('File.WriteAll','File.Delete','Clipboard.Get','Clipboard.Set',
 'HttpClient','Process.Start','Task.Run','DispatcherTimer','SetWinEventHook')){
 if($s.Contains($forbidden)){throw ("R239 no background/private mutation allowed: "+$forbidden)}
}
if(!$hub.Contains('DeskSpaceMonitorDiagnosticsV239.Show(this)') -or
   !$test.Contains('TestMonitorDiagnosticsV239();')){
 throw 'R239 health panel or native monitor test not wired'
}
Write-Host 'R239 STATIC PASS: read-only on-demand device/DPI/Deck status and no background observer.'

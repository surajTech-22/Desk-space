$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$project=[IO.File]::ReadAllText("$root/DeskSpace.csproj")
$manifest=[IO.File]::ReadAllText("$root/app.manifest")
$api=[IO.File]::ReadAllText("$root/DeskSpaceDpiAuditV285.cs")
$entry=[IO.File]::ReadAllText("$root/App.xaml.cs")
$studio=[IO.File]::ReadAllText("$root/DeskSpaceSettingsStudioV148.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 '<ApplicationManifest>app.manifest</ApplicationManifest>',
 'PerMonitorV2,PerMonitor',
 'true/pm',
 'level="asInvoker" uiAccess="false"',
 'GetThreadDpiAwarenessContext()',
 'AreDpiAwarenessContextsEqual(',
 'new IntPtr(-4)',
 'GetDpiForMonitor(',
 'Environment.SpecialFolder.UserProfile',
 'Downloads',
 'if(e.Args.Contains("--dpi-audit"',
 'ShutdownMode=ShutdownMode.OnExplicitShutdown;',
 'DeskSpaceDpiAuditV285.SaveToDownloads();',
 'Save actual monitor / DPI diagnostic to Downloads',
 'TestPerMonitorDpiManifestPolicyV285();',
 'R285 PMv2 awareness must differ from system-scaled mode'
)){
 if(!$project.Contains($needle) -and !$manifest.Contains($needle) -and
    !$api.Contains($needle) -and !$entry.Contains($needle) -and
    !$studio.Contains($needle) -and !$tests.Contains($needle)){
  throw ('R285 missing PMv2 proof: '+$needle)
 }
}
if($entry.IndexOf('if(e.Args.Contains("--dpi-audit"') -gt
   $entry.IndexOf('if(e.Args.Contains("--widget-host"')){
 throw 'R285 actual process DPI diagnostic must not construct a widget'
}
if($api.Contains('Process.Start(') -or $api.Contains('Clipboard.') -or
   $api.Contains('WindowText(') -or $api.Contains('File.ReadAllText(')){
 throw 'R285 DPI audit must be read-only except its explicit report'
}
Write-Host 'R285 STATIC PASS: PMv2 manifest, executable audit and Downloads diagnostics.'

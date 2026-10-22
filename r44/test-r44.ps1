$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$updater=[IO.File]::ReadAllText((Join-Path $root 'DeskSpacePublicUpdaterV44.cs'))
$old=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceUpdater.cs'))
$main=[IO.File]::ReadAllText((Join-Path $root 'MainWindow.xaml.cs'))
$ui=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceUpdateCheckV42.cs'))
$settings=[IO.File]::ReadAllText((Join-Path $root 'MainWindow.xaml'))
foreach($required in @(
    'api.github.com/repos/','/releases/latest','StartAutomaticChecks(Window owner)',
    'Interval=TimeSpan.FromMinutes(5)',
    'GetManifestResourceStream("DeskSpace.DeskSpaceUpdateSupervisor.ps1")',
    'SHA256.HashData(data)','DeskSpace_Setup.sha256.txt','DeskSpace_Setup.exe',
    'Verify(installer,checksum,root)','Missing verified release asset',
    'Application.Current.Shutdown()'
)){
    if(!$updater.Contains($required)){throw "R44 missing updater safety: $required"}
}
if($old.Contains('if (automatic && !Load().AutomaticUpdates) return;')){
    throw 'R44 manual preference still disables updates'
}
if(!$old.Contains('DeskSpacePublicUpdaterV44.StartAutomaticChecks(host)')){
    throw 'R44 widget startup not auto linked'
}
if(!$main.Contains('DeskSpacePublicUpdaterV44.StartAutomaticChecks(this)')){
    throw 'R44 Workspace startup not auto linked'
}
if(!$ui.Contains('DeskSpacePublicUpdaterV44.CheckAsync(owner)')){
    throw 'R44 Check for Updates not linked to public releases'
}
if(!$settings.Contains('Automatic updates enabled: public stable GitHub Releases')){
    throw 'R44 Workspace shows wrong update mode'
}
if(!$old.Contains('DeskSpace_Setup.sha256.txt')){
    throw 'Original updater checksum safety removed'
}
Write-Host 'R44 PASS: startup checks in widget and Workspace, public stable API, checksum gate, embedded supervised installer.'

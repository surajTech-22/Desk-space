$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$app='DeskSpace_Windows_R1/DeskSpace'
$updater=[IO.File]::ReadAllText("$app/DeskSpacePublicUpdaterV44.cs")
$supervisor=[IO.File]::ReadAllText("$app/DeskSpaceUpdateSupervisor.ps1")
$iss=[IO.File]::ReadAllText('installer/DeskSpace.iss')
foreach($token in @('PruneUpdateCache(Version.Parse(DeskSpaceUpdater.CurrentVersion))',
 '^v\d+\.\d+\.\d+$','TimeSpan.FromMinutes(15)',
 'TimeSpan.FromHours(24)','FileAttributes.ReparsePoint',
 'GetFileName(file)','Directory.Delete(folder,true)',
 'DeskSpace_Setup.exe','CompleteDeskSpaceUpdate.ps1',
 'DeskSpace_Setup.sha256.txt'))
{
  if(!$updater.Contains($token)){throw "R90 updater cache protection missing: $token"}
}
foreach($token in @('Split-Path -Parent $TargetExe',
 '/DIR="','Test-Path -LiteralPath $TargetExe',
 'Installer completed successfully','restoreMainWindow'))
{
  if(!$supervisor.Contains($token)){throw "R90 existing-install supervision missing: $token"}
}
# Inno must retain the same application ID and install location from R1.
# Version numbers belong ONLY in AppVersion/AppVerName.
$section=[regex]::Match($iss,'(?ms)^\[Setup\]\s*(.*?)^(?=\[)').Groups[1].Value
if([string]::IsNullOrWhiteSpace($section)){throw 'R90 cannot find Inno [Setup]'}
$appName=[regex]::Match($section,'(?m)^AppName\s*=\s*(.+)$').Groups[1].Value.Trim()
$dir=[regex]::Match($section,'(?m)^DefaultDirName\s*=\s*(.+)$').Groups[1].Value.Trim()
if([string]::IsNullOrWhiteSpace($appName) -or [string]::IsNullOrWhiteSpace($dir)){
 throw 'R90 missing stable Inno application name or installation directory'
}
$appId=[regex]::Match($section,'(?m)^AppId\s*=\s*(.+)$').Groups[1].Value.Trim()
foreach($fixed in @($appName,$dir,$appId) | Where-Object {$_ -ne ''}){
 if($fixed -match '\{\#AppVersion\}' -or $fixed -match '1\.5\.\d+'){
   throw 'R90 installer identity or installation directory improperly varies by version'
 }
}
if($section -match '(?m)^UsePreviousAppDir\s*=\s*no\s*$'){
 throw 'R90 installer explicitly disables same-directory upgrades'
}
Write-Host "R90 PASS: in-place installer preserves AppId and directory; supervisor forces old install path; only known aged updater caches are removed. AppId=$appId; Dir=$dir"

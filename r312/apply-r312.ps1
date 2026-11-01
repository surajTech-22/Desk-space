$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceUpdateSupervisor.ps1'
if(!(Test-Path -LiteralPath $p)){throw 'R312 shipping Supervisor.ps1 missing'}
$old=[IO.File]::ReadAllText((Resolve-Path $p))
if(!$old.Contains('Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru -ErrorAction Stop')){
 throw 'R312 verified old supervisor baseline unexpectedly changed'
}
if(!$old.Contains('Get-R230InstalledPeers') -or !$old.Contains('Save-R185Snapshot')){
 throw 'R312 refused: native shutdown/rollback safeguards missing'
}
Copy-Item -LiteralPath 'r312/DeskSpaceUpdateSupervisorV312.ps1' -Destination $p -Force
$s=[IO.File]::ReadAllText((Resolve-Path $p))
foreach($token in @('$setup.WaitForExit(240000)','$installerStillRunning=$true',
  'if($hadSnapshot -and $installerStarted -and !$installerStillRunning)',
  'if($ownsMutex -and !$exitBlocked -and !$installerStillRunning)',
  'Get-R230InstalledPeers','Save-R185Snapshot')){
 if(!$s.Contains($token)){throw ('R312 missing '+$token)}
}
if($s.Contains('Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru')){
 throw 'R312 native supervisor still waits on long-lived descendant'
}
Write-Host 'R312 shipping supervisor now awaits only direct setup process and preserves rollback.'

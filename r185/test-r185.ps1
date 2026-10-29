$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$supervisor=[IO.File]::ReadAllText("$root/DeskSpaceUpdateSupervisor.ps1")
$receipt=[IO.File]::ReadAllText("$root/DeskSpacePostInstallReceiptV185.cs")
$hostCode=[IO.File]::ReadAllText("$root/WidgetHost.cs")
foreach($token in @(
  'Save-R185Snapshot $install $snapshot',
  'Restore-R185Snapshot $install $snapshot $quarantine',
  'Start-Process -FilePath $Installer',
  'receipt.Transaction -eq $txn',
  'receipt.Version -eq $version',
  "did not acknowledge primary WPF Loaded",
  'no reparse-point',
  'verified-manifest.json',
  'New-Object System.Threading.Mutex',
  'Local\DeskSpace-UpdateSupervisor-V185'
)){
 if(!$supervisor.Contains($token)){throw "R185 supervisor missing $token"}
}
foreach($token in @(
  'Guid.TryParseExact(pending.Transaction,"N",out _)',
  'Version.TryParse(pending.Version,out var requested)',
  'File.Move(temp,ReceiptPath,true)',
  'owner.IsLoaded',
  'DeskSpaceMultiMonitorV151.IsSatellite(owner)'
)){
 if(!$receipt.Contains($token)){throw "R185 post-install receipt missing $token"}
}
if(!$hostCode.Contains('DeskSpacePostInstallReceiptV185.Ready(this);')){
 throw 'R185 primary WPF Loaded acknowledgement missing'
}
foreach($forbidden in @('Stop-Process -Force','Directory.Delete($install)',
 'Remove-Item -LiteralPath $install -Recurse','WorkspaceStore.Root',
 'Remove-Item -Recurse -Force $install')){
 if($supervisor.Contains($forbidden)){throw "R185 risks deleting or killing: $forbidden"}
}
$folder=Join-Path (Join-Path $env:USERPROFILE 'Downloads') ('DeskSpace_R185_Smoke_'+[Guid]::NewGuid().ToString('N'))
$install=Join-Path $folder 'DeskSpace'
$snapshot=Join-Path $folder 'rollback-snapshot'
$quarantine=Join-Path $folder 'quarantined-new-install'
$userdata=Join-Path $folder 'user-data-never-touch.txt'
try {
  New-Item -ItemType Directory -Force -Path $install | Out-Null
  Set-Content -LiteralPath (Join-Path $install 'DeskSpace.exe') -Value 'prior-full-install-v1'
  New-Item -ItemType Directory -Force -Path (Join-Path $install 'Assets')|Out-Null
  Set-Content -LiteralPath (Join-Path $install 'Assets\old.dll') -Value 'old-dependency-expected'
  Set-Content -LiteralPath $userdata -Value 'personal data must survive'
  . './r185/DeskSpaceRollbackOpsV185.ps1'
  $saved=Save-R185Snapshot $install $snapshot
  if($saved -ne 2){throw 'R185 expected entire multi-file snapshot'}
  Set-Content -LiteralPath (Join-Path $install 'DeskSpace.exe') -Value 'bad-new-version'
  Set-Content -LiteralPath (Join-Path $install 'Assets\old.dll') -Value 'wrong-dependency'
  Set-Content -LiteralPath (Join-Path $install 'new.dll') -Value 'new-install-only'
  $restored=Restore-R185Snapshot $install $snapshot $quarantine
  if($restored -ne 2){throw 'R185 expected every original installation file restored'}
  if((Get-Content -LiteralPath (Join-Path $install 'DeskSpace.exe') -Raw).Trim() -ne 'prior-full-install-v1' -or
     (Get-Content -LiteralPath (Join-Path $install 'Assets\old.dll') -Raw).Trim() -ne 'old-dependency-expected' -or
     (Test-Path -LiteralPath (Join-Path $install 'new.dll')) -or
     !(Test-Path -LiteralPath (Join-Path $quarantine 'new.dll')) -or
     (Get-Content -LiteralPath $userdata -Raw).Trim() -ne 'personal data must survive'){
      throw 'R185 whole directory restore/quarantine changed user data or left mixed-version files'
  }
  # Validate tamper detection BEFORE moving any current installation.
  $second=Join-Path $folder 'snapshot-tampered'
  $unused=Join-Path $folder 'never-touch'
  Save-R185Snapshot $install $second | Out-Null
  Set-Content -LiteralPath (Join-Path $second 'DeskSpace.exe') -Value 'tampered-backup'
  $denied=$false
  try {Restore-R185Snapshot $install $second $unused|Out-Null}
  catch {$denied=$true}
  if(!$denied -or !(Test-Path -LiteralPath (Join-Path $install 'DeskSpace.exe')) -or
     (Test-Path -LiteralPath $unused)){
    throw 'R185 refused no or late for corrupted snapshot'
  }
  $invalid=$false
  try {[void](Assert-R185InstallScope $folder)}catch{$invalid=$true}
  if(!$invalid){throw 'R185 failed to reject unsafe installation root'}
  Write-Host 'R185 PASS: 2-file full snapshot, mismatched new version quarantined, prior files restored, user data untouched, backup tampering blocked.'
}
finally {
  if(Test-Path -LiteralPath $folder){
    Remove-Item -LiteralPath $folder -Recurse -Force
  }
}

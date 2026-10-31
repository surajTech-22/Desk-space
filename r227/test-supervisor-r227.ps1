$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Exercise the actual rebuilt R185 supervisor with deliberately invalid
# synthetic paths while another Windows process holds its named mutex.
# No installer or DeskSpace application can run in this contention path.
$super='DeskSpace_Windows_R1/DeskSpace/DeskSpaceUpdateSupervisor.ps1'
if(!(Test-Path -LiteralPath $super -PathType Leaf)){
 throw 'R227 requires copied production supervisor'
}
$root=Join-Path ([IO.Path]::GetTempPath()) ('deskspace-r227-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force|Out-Null
$log=Join-Path $root 'update-status.log'
$target=Join-Path $root 'DeskSpace/DeskSpace.exe'
$installer=Join-Path $root 'tester-v1.5.1098/DeskSpace_Setup.exe'
$mutex=New-Object System.Threading.Mutex($true,'Local\DeskSpace-UpdateSupervisor-V185')
try{
  $arguments=@('-NoProfile','-NonInteractive','-File',
    ([IO.Path]::GetFullPath($super)),'-Installer',$installer,
    '-TargetExe',$target,'-StatusLog',$log,'-OldPid','2147483000')
  $result=Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Wait -PassThru -NoNewWindow -ErrorAction Stop
  if($result.ExitCode -ne 0){
    throw ("R227 duplicate supervisor must exit benignly, code="+$result.ExitCode)
  }
  if(!(Test-Path -LiteralPath $log) -or
     [IO.File]::ReadAllText($log) -notlike '*duplicate exits without relaunch*'){
     throw 'R227 supervisor lost mutex but entered failure fallback'
  }
  if((Test-Path -LiteralPath $target) -or
     (Test-Path -LiteralPath (Join-Path $root 'Updates'))){
     throw 'R227 duplicate supervisor touched installed application or staging'
  }
  Write-Host 'R227 REAL WINDOWS PASS: losing supervisor returned without fallback relaunch, install or file changes.'
}
finally{
  $mutex.ReleaseMutex()
  $mutex.Dispose()
  if(Test-Path -LiteralPath $root){
    Remove-Item -LiteralPath $root -Recurse -Force
  }
}

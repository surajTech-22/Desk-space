$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$source='src/DeskSpace/DeskSpaceUpdateSupervisor.ps1'
if(!(Test-Path $source)){throw 'R312 native supervisor missing'}
$text=Get-Content $source -Raw
foreach($key in @('WaitForExit(240000)', '$installerStillRunning=$true',
 'if($hadSnapshot -and $installerStarted -and !$installerStillRunning)',
 'if($ownsMutex -and !$exitBlocked -and !$installerStillRunning)')){
 if(!$text.Contains($key)){throw ('R312 direct process/rollback safety missing '+$key)}
}
if($text.Contains('Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru')){
 throw 'R312 still waits on installer descendant process tree'
}
$tokens=$null;$parseErrors=$null
[void][Management.Automation.Language.Parser]::ParseFile(
 (Resolve-Path $source).Path,[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count -gt 0){throw ('R312 PowerShell syntax invalid: '+$parseErrors[0].Message)}
$testDir=Join-Path $env:TEMP ('DeskSpace-R312-Wait-Test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir -Force|Out-Null
$childPid=$null
try {
 $childScript=Join-Path $testDir 'child.ps1'
 $parentScript=Join-Path $testDir 'parent.ps1'
 $pidPath=Join-Path $testDir 'childpid.txt'
 [IO.File]::WriteAllText($childScript,'Start-Sleep -Seconds 25')
 $parentText=@'
param([string]$ChildScript,[string]$ChildPidPath)
$child=Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList @('-NoProfile','-NonInteractive','-File',('"'+$ChildScript+'"')) -PassThru -ErrorAction Stop
[IO.File]::WriteAllText($ChildPidPath,[string]$child.Id)
'@
 [IO.File]::WriteAllText($parentScript,$parentText)
 $exe=Join-Path $PSHOME 'powershell.exe'
 $args='-NoProfile -NonInteractive -File "'+$parentScript+'" "'+$childScript+'" "'+$pidPath+'"'
 $parent=Start-Process -FilePath $exe -ArgumentList $args -PassThru -ErrorAction Stop
 $elapsed=[Diagnostics.Stopwatch]::StartNew()
 if(!$parent.WaitForExit(8000)){throw 'R312 direct parent process unexpectedly waited for child'}
 if($parent.ExitCode -ne 0){throw ('R312 fixture parent exited '+$parent.ExitCode)}
 if(!(Test-Path $pidPath)){throw 'R312 child PID fixture missing'}
 $childPid=[int](Get-Content $pidPath -Raw)
 $child=Get-Process -Id $childPid -ErrorAction Stop
 if(!$child -or $child.HasExited){throw 'R312 fixture descendant did not remain alive'}
 if($elapsed.Elapsed.TotalSeconds -ge 8){throw 'R312 direct process wait too slow'}
 Write-Host 'R312 PASS: parent returned promptly while child remains alive. PowerShell Start-Process -Wait process-tree hang prevented.'
}
finally{
 if($childPid){Stop-Process -Id $childPid -Force -ErrorAction SilentlyContinue}
 if(Test-Path $testDir){Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue}
}

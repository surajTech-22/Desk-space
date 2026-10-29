param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$TargetExe,
    [Parameter(Mandatory=$true)][string]$StatusLog,
    [Parameter(Mandatory=$true)][int]$OldPid
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# BEGIN_R185_OPERATIONS
function Report([string]$message) {
    try {
        $parent=Split-Path -Parent $StatusLog
        if(!(Test-Path -LiteralPath $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null}
        Add-Content -LiteralPath $StatusLog -Value ([DateTime]::UtcNow.ToString('O')+' '+$message)
    } catch {}
}
function Launch-R185Host {
    $running=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object {$_.CommandLine -like '*--widget-host*'})
    if($running.Count -eq 0 -and (Test-Path -LiteralPath $TargetExe -PathType Leaf)){
        Start-Process -FilePath $TargetExe -WorkingDirectory (Split-Path -Parent $TargetExe) -ArgumentList '--widget-host' -ErrorAction Stop | Out-Null
        Report 'R185 primary widget host relaunch requested.'
    }
}
$original=[IO.Path]::GetFullPath($TargetExe)
$install=Split-Path -Parent $original
$root=Join-Path $env:LOCALAPPDATA 'DeskSpace\Updates'
$txn=[guid]::NewGuid().ToString('N')
$rollback=Join-Path $root ('Rollback\'+$txn)
$snapshot=Join-Path $rollback 'snapshot'
$quarantine=Join-Path $rollback 'failed-install'
$post=Join-Path $root 'PostInstall'
$pending=Join-Path $post 'pending.json'
$ready=Join-Path $post 'ready.json'
$hadSnapshot=$false
$installerStarted=$false
$complete=$false
$ownsMutex=$false
$mutex=$null
$restoreMainWindow=$false
try {
    $mutex=New-Object System.Threading.Mutex($false,'Local\DeskSpace-UpdateSupervisor-V185')
    $ownsMutex=$mutex.WaitOne(0)
    if(!$ownsMutex){throw 'R185 another supervised install is active'}
    $tag=Split-Path -Leaf (Split-Path -Parent $Installer)
    if($tag -notmatch '^(?:tester-)?v(\d+\.\d+\.\d+)$'){
        throw 'R185 unrecognized verified release staging tag'
    }
    $version=$Matches[1]
    if(!(Test-Path -LiteralPath $Installer -PathType Leaf)){
        throw 'R185 verified installer missing before update'
    }
    if(!(Test-Path -LiteralPath $TargetExe -PathType Leaf)){
        throw 'R185 current DeskSpace executable missing'
    }
    [void](Assert-R185InstallScope $install)
    Report ('R185 update transaction '+$txn+' version '+$version+' started.')
    $others=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object {$_.ProcessId -ne $OldPid -and $_.CommandLine -notlike '*--widget-host*'})
    $restoreMainWindow=$others.Count -gt 0
    $previous=Get-Process -Id $OldPid -ErrorAction SilentlyContinue
    if($null -ne $previous){Wait-Process -Id $OldPid -Timeout 90 -ErrorAction Stop}
    # Keep other DeskSpace instances and any unsaved work untouched.
    $remaining=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object {$_.ExecutablePath -and $_.ExecutablePath.Equals($original,[StringComparison]::OrdinalIgnoreCase)})
    if($remaining.Count -gt 0){throw 'R185 another old DeskSpace process is still running'}
    $count=Save-R185Snapshot $install $snapshot
    $hadSnapshot=$true
    Report ('R185 verified full-install snapshot, files='+$count)
    New-Item -ItemType Directory -Force -Path $post|Out-Null
    if(Test-Path -LiteralPath $ready){Remove-Item -LiteralPath $ready -Force}
    $json=@{Transaction=$txn;Version=$version}|ConvertTo-Json -Compress
    [IO.File]::WriteAllText($pending,$json,[Text.UTF8Encoding]::new($false))
    $setupLog=Join-Path (Split-Path -Parent $Installer) 'inno-setup.log'
    $arguments='/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /DIR="'+$install+'" /LOG="'+$setupLog+'"'
    Report 'R185 launching verified installer over original directory.'
    $installerStarted=$true
    $setup=Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru -ErrorAction Stop
    if($setup.ExitCode -ne 0){throw ('R185 installer exited '+$setup.ExitCode)}
    if(!(Test-Path -LiteralPath $TargetExe -PathType Leaf)){
        throw 'R185 installer succeeded but new executable is absent'
    }
    Start-Sleep -Seconds 2
    Launch-R185Host
    $deadline=[DateTime]::UtcNow.AddSeconds(50)
    $ack=$false
    while([DateTime]::UtcNow -lt $deadline) {
        if(Test-Path -LiteralPath $ready -PathType Leaf){
            try{
                if((Get-Item -LiteralPath $ready).Length -le 4096){
                    $receipt=Get-Content -LiteralPath $ready -Raw|ConvertFrom-Json
                    if($receipt.Transaction -eq $txn -and
                       $receipt.Version -eq $version -and [int]$receipt.Pid -gt 0){
                        $process=Get-Process -Id ([int]$receipt.Pid) -ErrorAction SilentlyContinue
                        if($process -and !$process.HasExited -and $process.Path -and
                           $process.Path.Equals($original,[StringComparison]::OrdinalIgnoreCase)){
                            $ack=$true;break
                        }
                    }
                }
            }catch{}
        }
        Start-Sleep -Seconds 1
    }
    if(!$ack){throw 'R185 installed widget did not acknowledge primary WPF Loaded'}
    Report ('R185 verified post-install ready receipt for '+$version)
    if($restoreMainWindow){
        $openMain=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
            Where-Object {$_.CommandLine -notlike '*--widget-host*'})
        if($openMain.Count -eq 0){
            Start-Process -FilePath $TargetExe -WorkingDirectory $install -ErrorAction Stop|Out-Null
            Report 'R185 previous full Workspace window reopened.'
        }
    }
    $complete=$true
    if(Test-Path -LiteralPath $pending){Remove-Item -LiteralPath $pending -Force -ErrorAction SilentlyContinue}
    Report 'R185 SUCCESS: complete prior-install snapshot retained for recovery.'
}
catch {
    Report ('R185 FAILED: '+$_.Exception.GetType().Name+': '+$_.Exception.Message)
    if($hadSnapshot -and $installerStarted){
        try{
            $live=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
                Where-Object {$_.ExecutablePath -and $_.ExecutablePath.Equals($original,[StringComparison]::OrdinalIgnoreCase)})
            if($live.Count -gt 0){
                throw 'New app still running: do not force-terminate unsaved work; snapshot retained'
            }
            $restored=Restore-R185Snapshot $install $snapshot $quarantine
            Report ('R185 restored complete prior installation, files='+$restored)
        }catch {
            Report ('R185 manual recovery required, snapshot retained: '+$_.Exception.GetType().Name)
        }
    }
    try{Launch-R185Host}catch{Report ('R185 fallback host relaunch unavailable: '+$_.Exception.GetType().Name)}
    exit 1
}
finally {
    if(!$complete -and (Test-Path -LiteralPath $pending)){
        try{Remove-Item -LiteralPath $pending -Force}catch{}
    }
    if($ownsMutex -and $mutex){$mutex.ReleaseMutex()}
    if($mutex){$mutex.Dispose()}
}

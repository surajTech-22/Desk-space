# DeskSpace one-restart recovery: for the user's exact 1.5.1124 -> 1.5.1127 case.
# Nothing is force-terminated; reboots are never initiated by this script.
# At next sign-in, a SHA256-verified installer runs once before DeskSpace autostart.
param([switch]$Resume,[switch]$AuditOnly)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Join-Path $env:LOCALAPPDATA 'DeskSpace'
$dir = Join-Path $env:LOCALAPPDATA 'Programs\DeskSpace'
$exe = Join-Path $dir 'DeskSpace.exe'
$installer = Join-Path $root 'Updates\tester-v1.5.1127\DeskSpace_Setup.exe'
$backup = Join-Path $env:USERPROFILE 'Downloads\DeskSpace_ProgramBackup_Pre1127_20261103-132246'
$expected = '78b06e7188eda893f03ad780ad20f80364ab6ae0fba122a3b12c2bbd3ce9e309'
$maintenance = Join-Path $root 'Maintenance'
$staged = Join-Path $maintenance 'DeskSpace_1127_Resume.ps1'
$stateFile = Join-Path $maintenance 'DeskSpace_1127_ResumeState.json'
$report = Join-Path $env:USERPROFILE 'Downloads\DeskSpace_RebootRecovery1127_Status.txt'
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runOnce = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce'
$startupName = 'DeskSpaceWidget'
$onceName = 'DeskSpace_Recovery1127'
$mutex = $null
$mutexOwned = $false
$state = $null
$startupChanged = $false
function Note([string]$message) {
    $line = (Get-Date).ToString('O') + ' ' + $message
    Write-Host $line
    Add-Content -LiteralPath $script:report -Value $line -Encoding UTF8
}
function Ver {
    if (!(Test-Path -LiteralPath $script:exe -PathType Leaf)) { throw 'Installed DeskSpace.exe missing.' }
    return [version](Get-Item -LiteralPath $script:exe).VersionInfo.FileVersion
}
function Verify-Binaries {
    if (!(Test-Path -LiteralPath $script:installer -PathType Leaf)) {
        throw ('Verified installer missing: ' + $script:installer)
    }
    if (!((Get-FileHash -LiteralPath $script:installer -Algorithm SHA256).Hash.Equals(
        $script:expected,[StringComparison]::OrdinalIgnoreCase))) {
        throw 'Installer SHA256 mismatch. No installation will run.'
    }
    if (!(Test-Path -LiteralPath $script:backup -PathType Container)) {
        throw ('Previously verified backup missing: ' + $script:backup)
    }
    $files = @(Get-ChildItem -LiteralPath $script:dir -Recurse -Force -File -ErrorAction Stop)
    $extras = @(Get-ChildItem -LiteralPath $script:dir -Force -Recurse -ErrorAction Stop |
        Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0})
    if ($extras.Count) { throw 'Unexpected symlink in program folder; refusing recovery.' }
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($script:dir.TrimEnd('\').Length).TrimStart('\')
        $copy = Join-Path $script:backup $rel
        if (!(Test-Path -LiteralPath $copy -PathType Leaf) -or
            (Get-Item -LiteralPath $copy).Length -ne $f.Length -or
            (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne
              (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash) {
            throw ('Existing backup not valid for '+$rel+'. No changes scheduled.')
        }
    }
    Note ('Installer SHA256 and '+$files.Count+' backup files verified.')
}
function Peers {return @(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction Stop)}
function Restore([object]$saved) {
    if ($saved.HadRun) {
        if (!(Test-Path -LiteralPath $script:run)){New-Item -Path $script:run -Force|Out-Null}
        Set-ItemProperty -LiteralPath $script:run -Name $script:startupName -Value ([string]$saved.RunValue) -ErrorAction Stop
        Note 'Restored exactly the original DeskSpaceWidget startup value.'
    } else {
        Note 'Original setting did not autostart DeskSpace; keeping it disabled.'
    }
}
try {
    Note 'ONE-RESTART RECOVERY; only the DeskSpaceWidget startup entry may be temporarily changed.'
    $v = Ver
    Note ('Current version: '+$v)
    if ($AuditOnly){
        Note ('Processes: '+@(Peers).Count)
        return
    }
    if ($Resume) {
        if (!(Test-Path -LiteralPath $stateFile -PathType Leaf)) {
            throw 'No staged state. No unattended installation permitted.'
        }
        $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
        if ($state.Schema -ne 1 -or $state.Release -ne '1.5.1127' -or
            $state.Installer -ne $installer -or $state.Backup -ne $backup) {
            throw 'Staged state identity mismatch. No install permitted.'
        }
        $startupChanged = $true
        Note 'Windows sign-in resumed the approved one-time recovery.'
        if ($v -ge [version]'1.5.1127.0') {
            Note 'Already updated during reboot.'
        } else {
            $mutex=[Threading.Mutex]::new($false,'Local\DeskSpace-UpdateSupervisor-V185')
            try{$mutexOwned=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$mutexOwned=$true}
            if(!$mutexOwned){throw 'Update supervisor still active. No installation run.'}
            if(Test-Path -LiteralPath (Join-Path $root 'Updates\PostInstall\pending.json')){
                throw 'Another verified update is pending. Installer not launched.'
            }
            $peers = @(Peers)
            if($peers.Count){
                foreach($p in $peers){Note ('Unexpected after-restart DeskSpace PID '+$p.ProcessId+' '+$p.CommandLine)}
                throw 'DeskSpace started unexpectedly before the recovery installer; no files replaced.'
            }
            Verify-Binaries
            Note 'No DeskSpace process exists after reboot. Starting verified installer.'
            $inno = Join-Path $env:USERPROFILE ('Downloads\DeskSpace_RebootRecovery1127_Inno_'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.log')
            $args = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS /DIR="'+$dir+'" /LOG="'+$inno+'"'
            $setup = Start-Process -FilePath $installer -ArgumentList $args -PassThru -ErrorAction Stop
            if(!$setup.WaitForExit(240000)){throw ('Installer still running, PID '+$setup.Id+'. Check '+$inno)}
            if($setup.ExitCode -ne 0){throw ('Installer exit code '+$setup.ExitCode+'. Check '+$inno)}
            $v = Ver
            if($v -lt [version]'1.5.1127.0'){throw ('Installer ended but version still '+$v)}
        }
        Restore $state
        $startupChanged = $false
        Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
        Note ('INSTALLATION VERIFIED: '+$v+'. Existing settings, mappings and backup preserved.')
        if($state.RestartWidget -and !(Test-Path -LiteralPath (Join-Path $root 'widget-user-exited-v286.flag'))){
            Start-Process -FilePath $exe -ArgumentList '--widget-host' -WorkingDirectory $dir | Out-Null
            Note 'Updated widget restarted with original startup preference.'
        }
        return
    }
    if($v -ge [version]'1.5.1127.0'){Note 'Already updated; no recovery necessary.';return}
    if(Test-Path -LiteralPath $stateFile){
        Note 'Recovery is already staged. Do not rerun it; use Windows Restart once.'
        return
    }
    $peers=@(Peers)
    Note ('Observed '+$peers.Count+' DeskSpace process(es). This recovery will not force-close them.')
    foreach($p in $peers){Note ('PID '+$p.ProcessId+' created='+$p.CreationDate+' '+$p.CommandLine)}
    $existing = $null
    if(Test-Path -LiteralPath $run){
        $item = Get-ItemProperty -LiteralPath $run -ErrorAction Stop
        $prop = $item.PSObject.Properties[$startupName]
        if($null -ne $prop){$existing=[string]$prop.Value}
    }
    if($null -ne $existing -and
       (!$existing.Contains($exe) -or $existing -notmatch '--widget-host')){
        throw 'DeskSpaceWidget startup entry has unexpected contents; no registry change.'
    }
    if((Test-Path -LiteralPath $runOnce) -and
       (Get-ItemProperty -LiteralPath $runOnce -Name $onceName -ErrorAction SilentlyContinue)){
        throw 'A previous recovery is already scheduled; no duplicate.'
    }
    if(Test-Path -LiteralPath (Join-Path $root 'Updates\PostInstall\pending.json')){
        throw 'Another verified update is pending. No recovery scheduled.'
    }
    $mutex=[Threading.Mutex]::new($false,'Local\DeskSpace-UpdateSupervisor-V185')
    try{$mutexOwned=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$mutexOwned=$true}
    if(!$mutexOwned){throw 'Update supervisor is active. No recovery scheduled.'}
    Note 'PLAN: stage verified installer for next Windows sign-in and temporarily disable only DeskSpaceWidget autostart.'
    $approval = Read-Host 'Type ONE-RESTART to approve scheduling (Windows will NOT restart until you choose Restart)'
    if($approval -cne 'ONE-RESTART'){Note 'Declined. No registry changes.';return}
    Verify-Binaries
    if(!(Test-Path -LiteralPath $maintenance)){New-Item -Path $maintenance -ItemType Directory -Force|Out-Null}
    if((Get-Item -LiteralPath $maintenance -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){
        throw 'Maintenance path is redirected; no schedule.'
    }
    Copy-Item -LiteralPath $PSCommandPath -Destination $staged -Force -ErrorAction Stop
    $cmd='"'+(Join-Path $PSHOME 'powershell.exe')+'" -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$staged+'" -Resume'
    if($cmd.Length -gt 260){throw 'Windows RunOnce command too long; nothing scheduled.'}
    $state=[pscustomobject]@{Schema=1;Release='1.5.1127';Installer=$installer;
        Backup=$backup;HadRun=($null -ne $existing);RunValue=$existing;
        RestartWidget=($peers.Count -gt 0)}
    $state|ConvertTo-Json|Set-Content -LiteralPath $stateFile -Encoding UTF8 -ErrorAction Stop
    $startupChanged=$true
    if(!(Test-Path -LiteralPath $runOnce)){New-Item -Path $runOnce -Force|Out-Null}
    New-ItemProperty -LiteralPath $runOnce -Name $onceName -PropertyType String -Value $cmd -ErrorAction Stop|Out-Null
    if($null -ne $existing){Remove-ItemProperty -LiteralPath $run -Name $startupName -ErrorAction Stop}
    $startupChanged=$false
    Note 'READY: Installation safely scheduled for next sign-in. No processes terminated. No reboot forced.'
    Note 'Save open work. Use Windows Start > Power > RESTART once (not Shut down). Sign into the same account. DeskSpace will update before its original startup setting is restored.'
}
catch {
    Note ('STOPPED SAFELY: '+$_.Exception.GetType().Name+': '+$_.Exception.Message)
    if($startupChanged -and $null -ne $state){
        try{Restore $state}catch{Note ('WARNING startup restore failed: '+$_.Exception.Message)}
        try{Remove-ItemProperty -LiteralPath $runOnce -Name $onceName -ErrorAction SilentlyContinue}catch{}
        try{Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue}catch{}
        Note 'Recovery cancelled, original DeskSpace startup restored if possible.'
    }
}
finally {
    if($mutexOwned -and $null -ne $mutex){try{$mutex.ReleaseMutex()}catch{}}
    if($null -ne $mutex){$mutex.Dispose()}
    Note ('See result: '+$report)
}

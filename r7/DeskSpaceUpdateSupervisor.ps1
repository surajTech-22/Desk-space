param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$TargetExe,
    [Parameter(Mandatory=$true)][string]$StatusLog,
    [Parameter(Mandatory=$true)][int]$OldPid
)
$ErrorActionPreference = 'Stop'
function Report([string]$stage) {
    try {
        $dir = Split-Path -Parent $StatusLog
        if (!(Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Add-Content -LiteralPath $StatusLog -Value ("{0} {1}" -f [DateTime]::UtcNow.ToString('O'), $stage)
    } catch { }
}
try {
    # Remember whether the full DeskSpace window was open; reopen it after installation.
    $otherProcesses = @(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessId -ne $OldPid -and $_.CommandLine -notlike '*--widget-host*' })
    $restoreMainWindow = $otherProcesses.Count -gt 0
    Report ('Supervisor received verified update; main window previously open: ' + $restoreMainWindow)
    Report 'Supervisor waiting for previous widget process to exit.'
    $previous = Get-Process -Id $OldPid -ErrorAction SilentlyContinue
    if ($null -ne $previous) {
        Wait-Process -Id $OldPid -Timeout 90 -ErrorAction Stop
    }
    if (!(Test-Path -LiteralPath $Installer -PathType Leaf)) { throw 'Verified installer is missing.' }
    if (!(Test-Path -LiteralPath $TargetExe -PathType Leaf)) {
        throw 'Existing DeskSpace executable not found; refusing to create a second installation.'
    }
    $existingInstallDir = Split-Path -Parent $TargetExe
    Report ('Updating installed DeskSpace in existing directory: ' + $existingInstallDir)
    $setupLog = Join-Path (Split-Path -Parent $Installer) 'inno-setup.log'
    Report 'Supervisor launching silent installer.'
    # Force the ORIGINAL installation folder; Inno Setup retains its AppId
    # and upgrades installed files rather than choosing another directory.
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /DIR="' + $existingInstallDir + '" /LOG="' + $setupLog + '"'
    $setup = Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru -ErrorAction Stop
    if ($setup.ExitCode -ne 0) {
        throw ('Installer exited with code ' + $setup.ExitCode + '. Check ' + $setupLog)
    }
    if (!(Test-Path -LiteralPath $TargetExe -PathType Leaf)) {
        throw 'The installer reported success but did not restore the original DeskSpace executable.'
    }
    Report 'Installer completed successfully in the original DeskSpace directory.'
    Start-Sleep -Seconds 3
    # Inno Setup's silent [Run] step normally relaunches --widget-host.
    # Only start a host here if the setup process did not already launch one.
    $running = @(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like '*--widget-host*' })
    if ($running.Count -eq 0) {
        if (!(Test-Path -LiteralPath $TargetExe -PathType Leaf)) { throw 'Installed DeskSpace executable is missing.' }
        Start-Process -FilePath $TargetExe -ArgumentList '--widget-host' -ErrorAction Stop | Out-Null
        Report 'Widget host relaunched by independent update supervisor.'
    } else {
        Report 'Widget host already relaunched by installer.'
    }
    if ($restoreMainWindow) {
        $openMain = @(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -notlike '*--widget-host*' })
        if ($openMain.Count -eq 0) {
            Start-Process -FilePath $TargetExe -WorkingDirectory (Split-Path -Parent $TargetExe) -ErrorAction Stop | Out-Null
            Report 'Previously open DeskSpace main window automatically relaunched.'
        }
    }
} catch {
    Report ('Supervisor FAILED: ' + $_.Exception.GetType().Name + ': ' + $_.Exception.Message)
    exit 1
}

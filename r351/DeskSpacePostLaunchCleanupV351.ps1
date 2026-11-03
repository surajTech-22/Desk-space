param([Parameter(Mandatory=$true)][string]$TargetExe,[switch]$Preview)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R351_GUARDED_HELPERS
function Write-R351Report([string]$message){
    $line=[DateTime]::UtcNow.ToString('O')+' R351 '+$message
    $script:records.Add($line)
    try{Add-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'DeskSpace\update-status.log') -Value $line}catch{}
}
$records=New-Object 'System.Collections.Generic.List[string]'
$root=Join-Path $env:LOCALAPPDATA 'DeskSpace'
$updates=Join-Path $root 'Updates'
$search=Join-Path $root 'Search'
$maintenance=Join-Path $root 'Maintenance'
$ownsMutex=$false
$maintenanceMutex=$null
$updateMutex=$null
try{
    $installed=Get-R348FullPath $TargetExe
    if([IO.Path]::GetFileName($installed) -ne 'DeskSpace.exe' -or
       !(Test-R348PlainFile $installed)){throw 'running DeskSpace executable is unverified'}
    $v=[Diagnostics.FileVersionInfo]::GetVersionInfo($installed).FileVersion
    if(!$v -or [version]$v -lt [version]'1.5.1125'){
        Write-R351Report 'cleanup requires installed 1.5.1125 or newer'
        return
    }
    if(!(Test-Path -LiteralPath $maintenance)){
        New-Item -ItemType Directory -Force -Path $maintenance|Out-Null
    }
    if((Get-Item -LiteralPath $maintenance -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){
        throw 'maintenance location is a junction/symlink'
    }
    $maintenanceMutex=New-Object System.Threading.Mutex($false,'Local\DeskSpace-StorageCleanup-V351')
    $ownsMutex=$maintenanceMutex.WaitOne(0)
    if(!$ownsMutex){return}
    $updateMutex=New-Object System.Threading.Mutex($false,'Local\DeskSpace-UpdateSupervisor-V185')
    $free=$false
    for($i=0;$i -lt 25;$i++){
        if(!(Test-Path -LiteralPath (Join-Path $updates 'PostInstall\pending.json'))){
            $free=$updateMutex.WaitOne(0)
            if($free){$updateMutex.ReleaseMutex();break}
        }
        Start-Sleep -Seconds 2
    }
    if(!$free){
        Write-R351Report 'updater still active or pending; cleanup safely deferred'
        return
    }
    $stamp=Join-Path $maintenance 'last-attempt-v351.txt'
    if(!$Preview -and (Test-Path -LiteralPath $stamp) -and
       (Get-Item -LiteralPath $stamp).LastWriteTimeUtc -gt [DateTime]::UtcNow.AddMinutes(-30)){
        return
    }
    if(!$Preview){[IO.File]::WriteAllText($stamp,[DateTime]::UtcNow.ToString('O'))}
    Write-R351Report ('starting storage audit; app='+$v)
    $rb=Join-Path $updates 'Rollback'
    $backups=@()
    if(Test-Path -LiteralPath $rb -PathType Container){
        $backups=@(Get-ChildItem -LiteralPath $rb -Force -Directory -ErrorAction Stop|
            Where-Object {$_.Name -match '^[0-9a-f]{32}$'}|
            Sort-Object CreationTimeUtc -Descending)
    }
    $protect=Join-Path $rb '00000000000000000000000000000000'
    if($backups.Count -gt 0){$protect=$backups[0].FullName}
    $oldDb=Join-Path $search 'filenames-v330.sqlite'
    $newDb=Join-Path $search 'filenames-v336.sqlite'
    Write-R351Report ('before: old-index='+[bool](Test-Path -LiteralPath $oldDb)+
        '; current-index='+[bool](Test-Path -LiteralPath $newDb)+
        '; rollback-folders='+$backups.Count)
    $r348=Invoke-R348StorageCleanup -UpdatesRoot $updates -CurrentRollback $protect -SearchRoot $search -TargetExe $installed -Preview:$Preview
    foreach($entry in $r348.Messages){Write-R351Report ([string]$entry)}
    $stage=Invoke-R351StagedInstallerRetention -UpdatesRoot $updates -CurrentExe $installed -Preview:$Preview
    foreach($entry in $stage.Messages){Write-R351Report ([string]$entry)}
    $after=0
    if(Test-Path -LiteralPath $rb -PathType Container){
        $after=@(Get-ChildItem -LiteralPath $rb -Directory -Force -ErrorAction Stop|
            Where-Object {$_.Name -match '^[0-9a-f]{32}$'}).Count
    }
    [long]$reclaimed=[long]$r348.ReclaimedBytes+[long]$stage.Bytes
    Write-R351Report ('after: old-index='+[bool](Test-Path -LiteralPath $oldDb)+
        '; current-index='+[bool](Test-Path -LiteralPath $newDb)+
        '; rollback-folders='+$after)
    Write-R351Report ('reclaimed-bytes='+$reclaimed+
        '; rollbacks-removed='+$r348.SnapshotsDeleted+
        '; staged-installers-removed='+$stage.Removed+
        '; staged-skipped='+$stage.Skipped)
    Write-R351Report 'ManualBuilds, unrelated TEMP, user files, and other apps are excluded.'
}catch{
    Write-R351Report ('safe cleanup deferred: '+$_.Exception.GetType().Name+' '+$_.Exception.Message)
}finally{
    try{
        $downloads=Join-Path $env:USERPROFILE 'Downloads'
        if($records.Count -gt 0 -and
           (Test-Path -LiteralPath $downloads -PathType Container)){
            $output=Join-Path $downloads 'DeskSpace_StorageCleanup_R351_Report.txt'
            # R358 append, do not replace previous cleanup evidence. The
            # former single-run report showed 0 MiB after earlier cleanup
            # already removed the old 6-GiB Search index.
            Add-Content -LiteralPath $output -Encoding UTF8 -Value (
                '--- DeskSpace storage maintenance completed '+
                [DateTime]::UtcNow.ToString('O')+' ---')
            Add-Content -LiteralPath $output -Encoding UTF8 -Value $records.ToArray()
        }
    }catch{}
    if($ownsMutex -and $maintenanceMutex){try{$maintenanceMutex.ReleaseMutex()}catch{}}
    if($maintenanceMutex){$maintenanceMutex.Dispose()}
    if($updateMutex){$updateMutex.Dispose()}
}

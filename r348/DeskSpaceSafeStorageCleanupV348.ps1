# R348 — verified, post-install-only DeskSpace storage retention.
# These functions are embedded in the independent update supervisor by apply-r348.ps1.
# Never run from app startup and never touch user files or unrelated application caches.
function Get-R348FullPath([string]$path) {
    return [IO.Path]::GetFullPath($path).TrimEnd('\','/')
}
function Test-R348PlainFile([string]$path) {
    try {
        $item=Get-Item -LiteralPath $path -Force -ErrorAction Stop
        return (!$item.PSIsContainer -and
            ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0)
    } catch {return $false}
}
function Test-R348SqliteHeader([string]$path) {
    if(!(Test-R348PlainFile $path)){return $false}
    $inputFile=$null
    try {
        if((Get-Item -LiteralPath $path -Force).Length -lt 4096){return $false}
        $inputFile=[IO.File]::Open($path,[IO.FileMode]::Open,
            [IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
        $bytes=New-Object byte[] 16
        if($inputFile.Read($bytes,0,16) -ne 16){return $false}
        return [Text.Encoding]::ASCII.GetString($bytes,0,15) -eq 'SQLite format 3' -and
            $bytes[15] -eq 0
    } catch {return $false}
    finally {if($inputFile){$inputFile.Dispose()}}
}
function Invoke-R348StorageCleanup {
    param(
        [Parameter(Mandatory=$true)][string]$UpdatesRoot,
        [Parameter(Mandatory=$true)][string]$CurrentRollback,
        [Parameter(Mandatory=$true)][string]$SearchRoot,
        [Parameter(Mandatory=$true)][string]$TargetExe,
        [switch]$Preview
    )
    $expected=Get-R348FullPath (Join-Path $env:LOCALAPPDATA 'DeskSpace\Updates')
    $root=Get-R348FullPath $UpdatesRoot
    $rb=Get-R348FullPath (Join-Path $root 'Rollback')
    $current=Get-R348FullPath $CurrentRollback
    $search=Get-R348FullPath $SearchRoot
    $expectedSearch=Get-R348FullPath (Join-Path $env:LOCALAPPDATA 'DeskSpace\Search')
    $exe=Get-R348FullPath $TargetExe
    if(!$root.Equals($expected,[StringComparison]::OrdinalIgnoreCase) -or
       !$search.Equals($expectedSearch,[StringComparison]::OrdinalIgnoreCase) -or
       !$current.StartsWith($rb+[IO.Path]::DirectorySeparatorChar,
           [StringComparison]::OrdinalIgnoreCase) -or
       [IO.Path]::GetFileName($exe) -ne 'DeskSpace.exe') {
        throw 'R348 refused out-of-scope cleanup path'
    }
    $messages=New-Object 'System.Collections.Generic.List[string]'
    [long]$recovered=0
    [int]$snapshots=0
    [long]$legacyBytes=0
    # A second staged update or interrupted transaction must never lose its
    # potential recovery snapshot. This is a strict global stop.
    if(Test-Path -LiteralPath (Join-Path $root 'PostInstall\pending.json')) {
        $messages.Add('pending installation exists; all cleanup deferred')
        return [pscustomobject]@{
            ReclaimedBytes=[long]0;SnapshotsDeleted=0;LegacyIndexBytes=[long]0;
            Messages=@($messages.ToArray())
        }
    }
    if(Test-Path -LiteralPath $rb -PathType Container) {
        $rootItem=Get-Item -LiteralPath $rb -Force
        if(($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) {
            $verified=New-Object System.Collections.ArrayList
            if($Preview){$messages.Add('DEBUG found dirs '+@(Get-ChildItem -LiteralPath $rb -Force -Directory).Count)}
            foreach($dir in @(Get-ChildItem -LiteralPath $rb -Force -Directory -ErrorAction Stop)){
                if($dir.Name -notmatch '^[0-9a-fA-F]{32}$' -or
                   ($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                   (Get-R348FullPath $dir.FullName).Equals($current,
                       [StringComparison]::OrdinalIgnoreCase)) {continue}
                $manifest=Join-Path $dir.FullName 'verified-manifest.json'
                $oldExe=Join-Path $dir.FullName 'snapshot\DeskSpace.exe'
                $failed=Join-Path $dir.FullName 'failed-install'
                if($Preview){$messages.Add('DEBUG candidate '+$dir.Name+' manifest='+(Test-R348PlainFile $manifest)+' exe='+(Test-R348PlainFile $oldExe)+' failed='+(Test-Path -LiteralPath $failed))}
                if((Test-Path -LiteralPath $failed) -or
                   !(Test-R348PlainFile $manifest) -or
                   !(Test-R348PlainFile $oldExe)) {continue}
                try {
                    # Never descend through a junction, symbolic link or
                    # unrecognized item inside any prior installation snapshot.
                    $entries=@(Get-ChildItem -LiteralPath $dir.FullName -Force -Recurse -ErrorAction Stop)
                    if(@($entries|Where-Object {
                        ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
                    }).Count -ne 0){continue}
                    $hashes=@(Get-Content -LiteralPath $manifest -Raw -ErrorAction Stop |
                        ConvertFrom-Json -ErrorAction Stop)
                    if($hashes.Count -lt 1 -or
                       !$hashes[0].PSObject.Properties['Path'] -or
                       !$hashes[0].PSObject.Properties['Sha']){continue}
                    [long]$bytes=0
                    foreach($entry in $entries){if(!$entry.PSIsContainer){$bytes+=[long]$entry.Length}}
                    if($Preview){$messages.Add('DEBUG validated '+$dir.Name+' hashes='+$hashes.Count)}
                    [void]$verified.Add([pscustomobject]@{
                        Dir=$dir;Bytes=$bytes;TimeUtc=$dir.CreationTimeUtc
                    })
                } catch {
                    $messages.Add('skipped unverifiable rollback '+$dir.Name)
                }
            }
            # Always preserve THREE prior verified snapshots, plus the current
            # transaction (excluded above). Never prune any snapshot younger
            # than 48 hours. Failed-install quarantines are never candidates.
            $older=@($verified|Sort-Object -Property TimeUtc -Descending)
            $count=$older.Count
            if($Preview){$messages.Add('DEBUG verified='+$count)}
            for($i=3;$i -lt $count;$i++){
                $candidate=$older[$i]
                if($candidate.TimeUtc -gt [DateTime]::UtcNow.AddHours(-48)){continue}
                if($Preview){$messages.Add('PREVIEW rollback '+$candidate.Dir.Name);continue}
                try {
                    Remove-Item -LiteralPath $candidate.Dir.FullName -Recurse -Force -ErrorAction Stop
                    $snapshots++
                    $recovered+=$candidate.Bytes
                } catch {
                    $messages.Add('retained locked rollback '+$candidate.Dir.Name)
                }
            }
        } else {$messages.Add('rollback directory is a reparse point; skipped')}
    }
    # Legacy index is a rebuildable cache, never a document. R330/R335/R336
    # switched the shipping engine to filenames-v336.sqlite. Require a usable
    # v336 SQLite header AND the newly installed process before removing v330.
    $currentIndex=Join-Path $search 'filenames-v336.sqlite'
    $olderNames=@('filenames-v330.sqlite','filenames-v330.sqlite-wal',
        'filenames-v330.sqlite-shm','filenames-v330.sqlite-journal')
    if(!(Test-R348SqliteHeader $currentIndex)){
        $messages.Add('current v336 SQLite index not verified; v330 retained')
    } else {
        $sameInstallOnly=$false
        try {
            $instances=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction Stop)
            $unknown=@($instances|Where-Object {
                !$_.ExecutablePath -or
                !(Get-R348FullPath ([string]$_.ExecutablePath)).Equals(
                    $exe,[StringComparison]::OrdinalIgnoreCase)
            })
            $sameInstallOnly=$instances.Count -gt 0 -and $unknown.Count -eq 0
        } catch {$messages.Add('unable to verify DeskSpace process owners')}
        if(!$sameInstallOnly) {
            $messages.Add('other/unknown DeskSpace process; v330 retained')
        } elseif(Test-Path -LiteralPath $search -PathType Container) {
            $legacy=@()
            $blocked=$false
            foreach($name in $olderNames){
                $file=Join-Path $search $name
                if(!(Test-Path -LiteralPath $file)){continue}
                if(!(Test-R348PlainFile $file)){$blocked=$true;break}
                $legacy+=Get-Item -LiteralPath $file -Force
            }
            # Check that the entire SQLite file family is unlocked. Do not
            # delete a live WAL/SHM pair or rename any user-owned files.
            if(!$blocked){
                foreach($item in $legacy) {
                    $handle=$null
                    try {$handle=[IO.File]::Open($item.FullName,[IO.FileMode]::Open,
                        [IO.FileAccess]::Read,[IO.FileShare]::None)}
                    catch {$blocked=$true;break}
                    finally {if($handle){$handle.Dispose()}}
                }
            }
            if($blocked) {
                $messages.Add('legacy index locked/unverifiable; v330 retained')
            } elseif($Preview) {
                $messages.Add('PREVIEW legacy v330 SQLite family')
            } else {
                foreach($item in $legacy) {
                    try {
                        # Windows FileInfo may report Length=0 AFTER the
                        # file is removed. Capture exact bytes before delete.
                        [long]$length=$item.Length
                        Remove-Item -LiteralPath $item.FullName -Force -ErrorAction Stop
                        $legacyBytes+=$length
                        $recovered+=$length
                    } catch {$messages.Add('legacy index delete deferred: '+$item.Name)}
                }
            }
        }
    }
    $messages.Add(('reclaimed {0} MiB; old rollbacks {1}; legacy v330 {2} MiB' -f
        [Math]::Round($recovered/1MB,1),$snapshots,
        [Math]::Round($legacyBytes/1MB,1)))
    return [pscustomobject]@{
        ReclaimedBytes=$recovered;SnapshotsDeleted=$snapshots;
        LegacyIndexBytes=$legacyBytes;Messages=@($messages.ToArray())
    }
}

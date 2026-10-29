# R185 full-install snapshot helpers. The backup always lives outside the
# installed directory and never touches per-user DeskSpace data.
function Assert-R185InstallScope([string]$folder) {
    $full=[IO.Path]::GetFullPath($folder).TrimEnd('\','/')
    if([IO.Path]::GetFileName($full) -ne 'DeskSpace'){
        throw 'R185 refuses a folder not named DeskSpace'
    }
    $local=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DeskSpace')).TrimEnd('\','/')
    if($full.Equals($local,[StringComparison]::OrdinalIgnoreCase) -or
       $full.StartsWith($local+[IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)){
        throw 'R185 refuses the local DeskSpace data directory'
    }
    if(!(Test-Path -LiteralPath $full -PathType Container)){
        throw 'R185 original installation is missing'
    }
    $rootItem=Get-Item -LiteralPath $full -Force
    if(($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
        throw 'R185 refuses reparse-point installation roots'
    }
    return $full
}
function Get-R185Inventory([string]$folder) {
    $full=Assert-R185InstallScope $folder
    $root=$full+[IO.Path]::DirectorySeparatorChar
    $all=@(Get-ChildItem -LiteralPath $full -Force -Recurse -ErrorAction Stop)
    if($all.Count -gt 25000){throw 'R185 install file count exceeds safety limit'}
    $files=@()
    [long]$total=0
    foreach($entry in $all){
        if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
            throw 'R185 refuses symlink/junction entries'
        }
        if($entry.PSIsContainer){continue}
        $relative=$entry.FullName.Substring($root.Length)
        if($relative.StartsWith('..') -or
           [IO.Path]::IsPathRooted($relative)){
            throw 'R185 inventory escaped installation root'
        }
        $total+=$entry.Length
        if($total -gt 2GB){throw 'R185 backup exceeds 2 GiB limit'}
        $files+=New-Object PSObject -Property @{
            Path=$relative;Size=[long]$entry.Length;
            Sha=(Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash
        }
    }
    if(-not (Test-Path -LiteralPath (Join-Path $full 'DeskSpace.exe') -PathType Leaf)){
        throw 'R185 missing original DeskSpace executable'
    }
    return $files
}
function Save-R185Snapshot([string]$install,[string]$snapshot) {
    $full=Assert-R185InstallScope $install
    if(Test-Path -LiteralPath $snapshot){throw 'R185 refuses existing backup destination'}
    $inventory=@(Get-R185Inventory $full)
    if($inventory.Count -lt 1){throw 'R185 cannot snapshot an empty installation'}
    $parent=Split-Path -Parent $snapshot
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    Copy-Item -LiteralPath $full -Destination $snapshot -Recurse -Force -ErrorAction Stop
    foreach($entry in $inventory){
        $target=Join-Path $snapshot $entry.Path
        if(!(Test-Path -LiteralPath $target -PathType Leaf) -or
            (Get-Item -LiteralPath $target).Length -ne $entry.Size -or
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.Sha){
            throw ('R185 snapshot mismatch at '+$entry.Path)
        }
    }
    $count=@(Get-ChildItem -LiteralPath $snapshot -Force -Recurse -File).Count
    if($count -ne $inventory.Count){throw 'R185 snapshot file count differs'}
    $manifest=Join-Path (Split-Path -Parent $snapshot) 'verified-manifest.json'
    $record=@($inventory|Select-Object Path,Size,Sha)|ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($manifest,$record,[Text.UTF8Encoding]::new($false))

    return $inventory.Count
}
function Restore-R185Snapshot([string]$install,[string]$snapshot,[string]$quarantine) {
    # The current new install is QUARANTINED, never deleted. The prior entire
    # folder is restored, not just DeskSpace.exe or a single DLL.
    if(!(Test-Path -LiteralPath (Join-Path $snapshot 'DeskSpace.exe') -PathType Leaf)){
        throw 'R185 validated prior installation snapshot is missing'
    }
    $manifest=Join-Path (Split-Path -Parent $snapshot) 'verified-manifest.json'
    if(!(Test-Path -LiteralPath $manifest -PathType Leaf)){
        throw 'R185 missing previous snapshot SHA-256 manifest'
    }
    $prior=@(Get-ChildItem -LiteralPath $snapshot -Recurse -Force -File)
    $snapshotRoot=$snapshot.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
    if($prior.Count -eq 0){throw 'R185 empty rollback snapshot'}
    $records=@(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)
    if($prior.Count -ne $records.Count){throw 'R185 snapshot file count differs from verified manifest'}
    $oldHash=@{}
    foreach($item in $prior){
        if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
            throw 'R185 rollback contains a reparse point'
        }
        $relative=$item.FullName.Substring($snapshotRoot.Length)
        $oldHash[$relative]=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
    }
    foreach($record in $records){
        $relative=[string]$record.Path
        if([string]::IsNullOrWhiteSpace($relative) -or
           [IO.Path]::IsPathRooted($relative) -or
           $relative.Split([char]'\',[char]'/') -contains '..' -or
           !$oldHash.ContainsKey($relative) -or
           $oldHash[$relative] -ne [string]$record.Sha){
            throw 'R185 backup was changed after verification; refusing rollback'
        }
    }
    if(Test-Path -LiteralPath $install){
        [void](Assert-R185InstallScope $install)
        if(Test-Path -LiteralPath $quarantine){
            throw 'R185 failed-install quarantine already exists'
        }
        Move-Item -LiteralPath $install -Destination $quarantine -ErrorAction Stop
    }
    Copy-Item -LiteralPath $snapshot -Destination $install -Recurse -Force -ErrorAction Stop
    foreach($relative in $oldHash.Keys){
        $restored=Join-Path $install $relative
        if(!(Test-Path -LiteralPath $restored -PathType Leaf) -or
            (Get-FileHash -LiteralPath $restored -Algorithm SHA256).Hash -ne $oldHash[$relative]){
            throw ('R185 restored installation hash mismatch at '+$relative)
        }
    }
    return $oldHash.Count
}

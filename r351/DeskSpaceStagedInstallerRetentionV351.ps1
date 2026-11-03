# R351: trim only old, verified DeskSpace staged installers.
# Never recurse into user folders, ManualBuilds, Temp, or current release assets.
function Invoke-R351StagedInstallerRetention {
    param(
        [Parameter(Mandatory=$true)][string]$UpdatesRoot,
        [Parameter(Mandatory=$true)][string]$CurrentExe,
        [switch]$Preview
    )
    $expected=Get-R348FullPath (Join-Path $env:LOCALAPPDATA 'DeskSpace\Updates')
    $root=Get-R348FullPath $UpdatesRoot
    if(!$root.Equals($expected,[StringComparison]::OrdinalIgnoreCase)){
        throw 'R351 refused staged updates root outside DeskSpace'
    }
    $result=New-Object 'System.Collections.Generic.List[string]'
    [long]$bytes=0
    [int]$removed=0
    [int]$skipped=0
    if(!(Test-Path -LiteralPath $root -PathType Container)){
        return [pscustomobject]@{Bytes=0L;Removed=0;Skipped=0;Messages=@('no staged installers')}
    }
    if((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){
        throw 'R351 updates root is a junction/symlink'
    }
    if(Test-Path -LiteralPath (Join-Path $root 'PostInstall\pending.json')){
        return [pscustomobject]@{Bytes=0L;Removed=0;Skipped=0;Messages=@('pending update; staged cleanup deferred')}
    }
    $exe=Get-R348FullPath $CurrentExe
    if([IO.Path]::GetFileName($exe) -ne 'DeskSpace.exe' -or
       !(Test-R348PlainFile $exe)){throw 'R351 running application executable invalid'}
    $unknown=$true
    try {
        $processes=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction Stop)
        $other=@($processes|Where-Object {
            !$_.ExecutablePath -or
            !(Get-R348FullPath ([string]$_.ExecutablePath)).Equals($exe,
                [StringComparison]::OrdinalIgnoreCase)
        })
        $unknown=$processes.Count -eq 0 -or $other.Count -gt 0
    } catch {
        $result.Add('unable to verify DeskSpace process paths; retained old installers')
    }
    if($unknown){
        $result.Add('unrecognized or missing DeskSpace process; old installers retained')
        return [pscustomobject]@{Bytes=0L;Removed=0;Skipped=1;Messages=@($result.ToArray())}
    }
    $folders=@(Get-ChildItem -LiteralPath $root -Force -Directory -ErrorAction Stop |
        Where-Object {$_.Name -match '^(tester-)?v1\.5\.(\d+)$'})
    # Keep at least THREE newest downloaded packages for recovery as well as
    # all packages touched in the last 48 hours (stable/tester counted separately).
    $ordered=@($folders|Sort-Object -Property @{Expression={
        if($_.Name -match 'v1\.5\.(\d+)$'){[int]$Matches[1]}else{0}
    };Descending=$true},@{Expression={$_.LastWriteTimeUtc};Descending=$true})
    $protected=@{}
    foreach($dir in @($ordered|Select-Object -First 3)){
        $protected[$dir.FullName.ToUpperInvariant()]=$true
    }
    foreach($dir in $ordered){
        if($protected.ContainsKey($dir.FullName.ToUpperInvariant()) -or
           $dir.LastWriteTimeUtc -gt [DateTime]::UtcNow.AddHours(-48)){
            $skipped++;continue
        }
        if(($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
            $skipped++;continue
        }
        try {
            $items=@(Get-ChildItem -LiteralPath $dir.FullName -Force -ErrorAction Stop)
            $valid=$true
            [long]$dirBytes=0
            foreach($item in $items){
                if($item.PSIsContainer -or
                   ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                   $item.Name -notmatch '^(?:DeskSpace_Setup\.exe(?:\.[0-9a-fA-F]{32}\.partial)?|Update\.exe(?:\.[0-9a-fA-F]{32}\.partial)?|DeskSpace_Setup\.sha256\.txt|Update\.sha256\.txt|inno-setup\.log)$'){
                    $valid=$false;break
                }
                $dirBytes+=[long]$item.Length
            }
            if(!$valid){$skipped++;$result.Add('unrecognized staged files: '+$dir.Name);continue}
            $locked=$false
            foreach($item in $items){
                $stream=$null
                try {
                    $stream=[IO.File]::Open($item.FullName,[IO.FileMode]::Open,
                        [IO.FileAccess]::Read,[IO.FileShare]::None)
                } catch {$locked=$true;break}
                finally {if($stream){$stream.Dispose()}}
            }
            if($locked){$skipped++;$result.Add('locked staged installer: '+$dir.Name);continue}
            if($Preview){
                $result.Add('PREVIEW staged installer '+$dir.Name+
                    ' '+[Math]::Round($dirBytes/1MB,1)+' MiB')
                continue
            }
            Remove-Item -LiteralPath $dir.FullName -Recurse -Force -ErrorAction Stop
            $bytes+=$dirBytes
            $removed++
        } catch {
            $skipped++
            $result.Add('could not prune staged installer '+$dir.Name+
                ': '+$_.Exception.GetType().Name)
        }
    }
    $result.Add(('stage summary: {0} removed, {1} retained, {2} MiB recovered' -f
        $removed,$skipped,[Math]::Round($bytes/1MB,1)))
    return [pscustomobject]@{
        Bytes=[long]$bytes;Removed=[int]$removed;
        Skipped=[int]$skipped;Messages=@($result.ToArray())
    }
}

# R358: verified installed-version cleanup of ONLY known DeskSpace staging assets.
# Called by R351 after the V185 mutex/pending-transaction gate. No app data,
# ManualBuilds, recovery installers, user documents, Temp or active indices.
function Invoke-R351StagedInstallerRetention {
    param(
        [Parameter(Mandatory=$true)][string]$UpdatesRoot,
        [Parameter(Mandatory=$true)][string]$CurrentExe,
        [version]$InstalledVersion,
        [switch]$Preview
    )
    $expected=Get-R348FullPath (Join-Path $env:LOCALAPPDATA 'DeskSpace\Updates')
    $root=Get-R348FullPath $UpdatesRoot
    if(!$root.Equals($expected,[StringComparison]::OrdinalIgnoreCase)){
        throw 'R358 refused staging outside DeskSpace updates directory'
    }
    $result=New-Object 'System.Collections.Generic.List[string]'
    [long]$bytes=0
    [int]$removed=0
    [int]$skipped=0
    if(!(Test-Path -LiteralPath $root -PathType Container)){
        return [pscustomobject]@{Bytes=0L;Removed=0;Skipped=0;Messages=@('no staged installers')}
    }
    if((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){
        throw 'R358 staging root is a junction/symlink'
    }
    if(Test-Path -LiteralPath (Join-Path $root 'PostInstall\pending.json')){
        return [pscustomobject]@{Bytes=0L;Removed=0;Skipped=0;Messages=@('pending update; staged cleanup deferred')}
    }
    $exe=Get-R348FullPath $CurrentExe
    if([IO.Path]::GetFileName($exe) -ne 'DeskSpace.exe' -or
       !(Test-R348PlainFile $exe)){throw 'R358 running executable invalid'}
    if($null -eq $InstalledVersion){
        $versionText=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
        if(!$versionText -or ![version]::TryParse($versionText,[ref]$InstalledVersion)){
            throw 'R358 cannot verify installed app version; no staging cleanup'
        }
    }
    $unknown=$true
    try{
        $processes=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction Stop)
        $other=@($processes|Where-Object {
            !$_.ExecutablePath -or
            !(Get-R348FullPath ([string]$_.ExecutablePath)).Equals(
                $exe,[StringComparison]::OrdinalIgnoreCase)
        })
        $unknown=($processes.Count -eq 0 -or $other.Count -gt 0)
    } catch {$result.Add('cannot verify DeskSpace processes; staged installers retained')}
    if($unknown){
        $result.Add('unknown/missing DeskSpace process; staged installers retained')
        return [pscustomobject]@{Bytes=0L;Removed=0;Skipped=1;Messages=@($result.ToArray())}
    }
    $folders=@(Get-ChildItem -LiteralPath $root -Force -Directory -ErrorAction Stop |
        Where-Object {$_.Name -match '^(tester-)?v1\.5\.(\d+)$'})
    $ordered=@($folders | Sort-Object -Property @{
        Expression={ if($_.Name -match 'v1\.5\.(\d+)$'){[int]$Matches[1]}else{0} };
        Descending=$true
    }, @{Expression={$_.LastWriteTimeUtc};Descending=$true})
    $protected=@{}
    foreach($item in @($ordered|Select-Object -First 3)){
        $protected[$item.FullName.ToUpperInvariant()]=$true
    }
    $result.Add(('R358 stage policy: installed={0}; keep latest 3 release folders; '+
        'keep folders under 2h old; exclude future and unknown content' -f $InstalledVersion))
    foreach($dir in $ordered){
        if($protected.ContainsKey($dir.FullName.ToUpperInvariant())){
            $skipped++;$result.Add('retained newest installer: '+$dir.Name);continue
        }
        if($dir.LastWriteTimeUtc -gt [DateTime]::UtcNow.AddHours(-2)){
            $skipped++;$result.Add('retained recent installer (<2h): '+$dir.Name);continue
        }
        if(($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
            $skipped++;$result.Add('retained reparse-point installer: '+$dir.Name);continue
        }
        $m=[regex]::Match($dir.Name,'v1\.5\.(\d+)$',[Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if(!$m.Success -or [version]::new(1,5,[int]$m.Groups[1].Value) -gt $InstalledVersion){
            $skipped++;$result.Add('retained installer newer than installed: '+$dir.Name);continue
        }
        try{
            $items=@(Get-ChildItem -LiteralPath $dir.FullName -Force -ErrorAction Stop)
            $unrecognized=@($items|Where-Object {
                $_.PSIsContainer -or
                ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                $_.Name -notmatch '^(?:DeskSpace_Setup\.exe(?:\.[0-9a-f]{32}\.partial)?|Update\.exe(?:\.[0-9a-f]{32}\.partial)?|DeskSpace_Setup\.sha256\.txt|Update\.sha256\.txt|inno-setup\.log|CompleteDeskSpaceUpdate(?:\.[0-9a-f]{32})?\.ps1)$'
            })
            if($unrecognized.Count){
                $names=@($unrecognized|Select-Object -ExpandProperty Name)
                $skipped++
                $result.Add(('retained unrecognized installer {0}: {1}' -f $dir.Name,($names -join ', ')))
                continue
            }
            [long]$dirBytes=0
            $locked=$false
            foreach($item in $items){
                $dirBytes+=[long]$item.Length
                $handle=$null
                try{
                    $handle=[IO.File]::Open($item.FullName,[IO.FileMode]::Open,
                        [IO.FileAccess]::Read,[IO.FileShare]::None)
                }catch{$locked=$true;break}
                finally{if($handle){$handle.Dispose()}}
            }
            if($locked){
                $skipped++;$result.Add('retained locked installer: '+$dir.Name);continue
            }
            if($Preview){
                $result.Add(('PREVIEW delete known installer {0}: {1} MiB' -f
                    $dir.Name,[Math]::Round($dirBytes/1MB,1)))
                continue
            }
            Remove-Item -LiteralPath $dir.FullName -Recurse -Force -ErrorAction Stop
            $bytes+=$dirBytes
            $removed++
            $result.Add(('deleted known installer {0}: {1} MiB' -f
                $dir.Name,[Math]::Round($dirBytes/1MB,1)))
        } catch {
            $skipped++
            $result.Add(('retained installer {0}: {1}' -f
                $dir.Name,$_.Exception.GetType().Name))
        }
    }
    $result.Add(('stage summary: {0} removed, {1} retained, {2} MiB recovered' -f
        $removed,$skipped,[Math]::Round($bytes/1MB,1)))
    return [pscustomobject]@{
        Bytes=[long]$bytes;Removed=[int]$removed;
        Skipped=[int]$skipped;Messages=@($result.ToArray())
    }
}

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceSearchIndexV13.cs'
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function One([string]$before,[string]$after){
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R58 search anchor $n"}
 $script:s=$script:s.Replace($before,$after)
}
One 'if(File.Exists(full))LivePaths.TryAdd(full,0);' 'if(DeskSpaceNewFileWatchV53.IsOrdinaryUserFile(full))LivePaths.TryAdd(full,0);'
One 'if(!File.Exists(path))' 'if(!DeskSpaceNewFileWatchV53.IsOrdinaryUserFile(path))'
One 'if(!item.Folder && !File.Exists(item.Path))continue;' 'if(!item.Folder && !DeskSpaceNewFileWatchV53.IsOrdinaryUserFile(item.Path))continue;'
One @'
        foreach(var hit in indexed)
            if(!scoped || hit.OpenTarget.StartsWith(driveFilter!,StringComparison.OrdinalIgnoreCase))
                Add(hit);
'@ @'
        foreach(var hit in indexed)
            if((!scoped || hit.OpenTarget.StartsWith(driveFilter!,StringComparison.OrdinalIgnoreCase)) &&
               (hit.Category!="Files" ||
                DeskSpaceNewFileWatchV53.IsOrdinaryUserFile(hit.OpenTarget)))
                Add(hit);
'@
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R58 Search shares normal-user-file policy; hidden, cache and temp are excluded.'

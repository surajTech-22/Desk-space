$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r233/DeskSpaceFileQuickSearchV233.cs' "$root/DeskSpaceFileQuickSearchV233.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$a,[string]$b,[string]$reason)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $count=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($count -ne 1){throw "Search-file fix $reason expected one anchor, found $count"}
 [IO.File]::WriteAllText($path,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
$idx="$root/DeskSpaceSearchIndexV13.cs"
Swap $idx '        var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);' @'
        var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);
        // Immediate real files when Windows indexing is disabled or cold.
        var quickFiles=DeskSpaceFileQuickSearchV233.Find(fileQuery,cancellation,driveFilter);
'@ 'direct file fallback'
Swap $idx '        foreach(var hit in indexed)' '        foreach(var hit in quickFiles.Concat(indexed))' 'merge actual files'
Swap $idx '            var perVolume=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);' @'
            var perVolumeFiles=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            var perVolumeFolders=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
'@ 'separate files and folders quota'
Swap $idx '                if(!Matches(item.Name,fileQuery) && !Matches(item.Path,fileQuery))continue;' @'
                if(!DeskSpaceFileQuickSearchV233.MatchesIndexedEntry(
                    item.Name,item.Path,fileQuery))continue;
'@ 'prevent parent-folder-only matches'
Swap $idx @'
                perVolume.TryGetValue(volume,out var already);
                if(already>=48)continue;
                if(Add(new SearchHit{Category="Files",
                    Name=item.Name,OpenTarget=item.Path,Location=item.Path}))
                    perVolume[volume]=already+1;
'@ @'
                var quota=item.Folder?perVolumeFolders:perVolumeFiles;
                quota.TryGetValue(volume,out var already);
                if(already>=(item.Folder?18:64))continue;
                if(Add(new SearchHit{Category="Files",
                    Name=item.Name,OpenTarget=item.Path,Location=item.Path}))
                    quota[volume]=already+1;
'@ 'file result quota'
$win="$root/DeskSpaceWindowsSearchV18.cs"
Swap $win @'
                : "(System.FileName LIKE '%"+literal+"%' OR "+
                  "System.ItemPathDisplay LIKE '%"+literal+"%')";
'@ @'
                : "(System.FileName LIKE '%"+literal+"%')";
'@ 'Windows Search name-only SQL'
$ranking="$root/DeskSpaceSearchRankingV192.cs"
Swap $ranking @'
            if(quality!=0)return quality;
            int category=CategoryOrder(first).CompareTo(CategoryOrder(second));
'@ @'
            if(quality!=0)return quality;
            if(first.Category=="Files" && second.Category=="Files")
            {
                bool a=Path.HasExtension(first.Name),b=Path.HasExtension(second.Name);
                if(a!=b)return a?-1:1;
            }
            int category=CategoryOrder(first).CompareTo(CategoryOrder(second));
'@ 'prioritize equally relevant files over folders'
Write-Host 'File Search: filename-only indexed query, bounded direct discovery, non-starving file quota.'

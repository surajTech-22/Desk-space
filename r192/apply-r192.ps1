$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r192/DeskSpaceSearchRankingV192.cs' "$root/DeskSpaceSearchRankingV192.cs" -Force
$p="$root/DeskSpaceSearchIndexV13.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Once([string]$before,[string]$after,[string]$label)
{
    $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
    $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
    if($n -ne 1){throw ("R192 "+$label+": expected one anchor, found "+$n)}
    $script:s=$script:s.Replace($before,$after)
}
Once @'
        var results=new List<SearchHit>();
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(SearchHit hit)
        {
            if(results.Count<400 && seen.Add(hit.OpenTarget ?? ""))results.Add(hit);
        }
'@ @'
        // Names from ALL sources compete by relevance, not discovery order.
        // Keep each category bounded so a directory with thousands of
        // weak matches cannot bury an exact file or application.
        var ranked=new DeskSpaceSearchRankingV192.Collector(fileQuery);
        bool Add(SearchHit hit)=>ranked.Add(hit);
'@ 'replace early global cap'
Once @'
            return results;
        }
        bool scoped=driveFilter!=null;
'@ @'
            return ranked.Finish();
        }
        bool scoped=driveFilter!=null;
'@ 'drive list output'
Once @'
                var before=results.Count;
                Add(new SearchHit{Category="Files",
                    Name=item.Name,OpenTarget=item.Path,Location=item.Path});
                if(results.Count>before)perVolume[volume]=already+1;
'@ @'
                if(Add(new SearchHit{Category="Files",
                    Name=item.Name,OpenTarget=item.Path,Location=item.Path}))
                    perVolume[volume]=already+1;
'@ 'cross-volume fair quota'
Once @'
        return results.OrderBy(h=>h.Category=="Applications"?0:
             h.Category=="Settings"?1:
             h.Category=="Notes"?2:h.Category=="Reminders"?3:
             h.Category=="Calendar"?4:h.Category=="Information"?5:6)
            .ThenBy(h=>h.Name.Equals(fileQuery,StringComparison.OrdinalIgnoreCase)?0:
                h.Name.StartsWith(fileQuery,StringComparison.OrdinalIgnoreCase)?1:2)
            .ThenBy(h=>h.Name,StringComparer.OrdinalIgnoreCase)
            .Take(160).ToList();
'@ @'
        return ranked.Finish();
'@ 'final bounded relevance ranking'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R192: retained Windows Search + all-drive coverage, prioritised exact names across categories and suppressed duplicate files and app shortcuts.'

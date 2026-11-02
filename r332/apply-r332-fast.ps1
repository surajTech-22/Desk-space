$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceSearchIndexV13.cs'
if(!(Test-Path -LiteralPath $p)){throw 'R334 search source missing'}
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
# Move the bounded local disk query ahead of slow COM Windows Search and
# speculative directory walks. Before this, a found XLSX took ~17 seconds
# to be returned by the public Search() method on Windows CI.
$begin=$s.IndexOf('        // R332: disk search is an OPTIONAL source')
$end=$s.IndexOf('        return ranked.Finish();',$begin)
$indexed=$s.IndexOf('        var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);')
if($begin -lt 0 -or $end -le $begin -or $indexed -lt 0 -or $indexed -ge $begin){
 throw 'R334 could not locate disk block or original Windows Search ordering'
}
$block=$s.Substring($begin,$end-$begin)
$before='        // R332: disk search is an OPTIONAL source'
$after='        int fileMatchesV334=0;'+$lf+$before
if(!$block.Contains($before)){throw 'R334 bad optional disk block anchor'}
$block=$block.Replace($before,$after)
$before='''            if(Add(new SearchHit{Category="Files",Name=item.Name,'
# Preserve the existing conditional exactly, adding a counter only when a
# real indexed file passed existence checks and the ranking collector.
$old=@'
                        if(Add(new SearchHit{Category="Files",Name=item.Name,
                            OpenTarget=item.Path,Location=item.Path}))
                            accepted++;
'@
$new=@'
                        if(Add(new SearchHit{Category="Files",Name=item.Name,
                            OpenTarget=item.Path,Location=item.Path}))
                        {
                            accepted++;
                            fileMatchesV334++;
                        }
'@
if(!$block.Contains($old.Replace($crlf,$lf))){
 throw 'R334 could not locate indexed acceptance counter'
}
$block=$block.Replace($old.Replace($crlf,$lf),$new.Replace($crlf,$lf))
# Cut old late disk query, insert before other providers, to return fast.
$s=$s.Remove($begin,$end-$begin)
$indexed=$s.IndexOf('        var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);')
if($indexed -lt 0){throw 'R334 Windows provider anchor disappeared'}
$provider=@'
        // Filename and path searches use the local disk catalog first.
        // Synchronous ADODB/Windows Search can block for >15 seconds on a
        // busy or unindexed PC. Retain it only for explicitly requested
        // document content searches; the name index and quick fallback
        // are the ordinary file-search providers.
        var indexed=fileQuery.StartsWith("content:",
            StringComparison.OrdinalIgnoreCase)
            ?DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation)
            :new List<SearchHit>();
'@
$s=$s.Substring(0,$indexed)+$block+$provider.Replace($crlf,$lf)+
    $s.Substring($indexed+
    '        var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);'.Length)
# The fast directory crawl is valuable when the local index has no results.
# But never make a user wait for it after the exact indexed filename matches.
$quick=@'
        if(fileQuery.Length>=2 &&
           !fileQuery.StartsWith("content:",StringComparison.OrdinalIgnoreCase))
'@
$fastQuick=@'
        if(fileMatchesV334==0 && fileQuery.Length>=2 &&
           !fileQuery.StartsWith("content:",StringComparison.OrdinalIgnoreCase))
'@
if(([regex]::Matches($s,[regex]::Escape($quick.Replace($crlf,$lf)))).Count -ne 1){
 throw 'R334 quick-file fallback conditional changed'
}
$s=$s.Replace($quick.Replace($crlf,$lf),$fastQuick.Replace($crlf,$lf))
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
if($s.Contains('var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);') -or
   !$s.Contains('fileMatchesV334==0') -or
   !$s.Contains('int fileMatchesV334=0;')){
 throw 'R334 file-search fast-first invariants are not satisfied'
}
Write-Host 'R334 indexed filename results now precede slow COM and quick crawl, avoiding observed 17-second delay; content search unchanged.'

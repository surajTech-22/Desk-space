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
$s=$s.Remove($begin,$end-$begin)
# The accepted R221 shipping source uses quickFiles.Concat(indexed).
# The quickFiles declaration is an expensive fallback before Windows Search.
$match=[regex]::Match($s,'(?m)^[ \t]*var[ \t]+quickFiles[ \t]*=')
$quickStart=if($match.Success){$match.Index}else{-1}
$indexed=$s.IndexOf('        var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);')
if($indexed -lt 0){throw 'R334 Windows provider anchor disappeared'}
$insert=if($quickStart -ge 0 -and $quickStart -lt $indexed){
 $quickStart
}else{$indexed}
$s=$s.Substring(0,$insert)+$block+$s.Substring($insert)
if($match.Success)
{
 $match=[regex]::Match($s,'(?m)^[ \t]*var[ \t]+quickFiles[ \t]*=')
 $startExpr=$match.Index+$match.Length
 $endExpr=$s.IndexOf(';',$startExpr)
 if($endExpr -lt $startExpr){throw 'R334 quickFiles expression is not a normal statement'}
 $expression=$s.Substring($startExpr,$endExpr-$startExpr).Trim()
 if($expression.Length -eq 0){throw 'R334 empty quickFiles expression'}
 $s=$s.Substring(0,$startExpr)+
    'fileMatchesV334>0 ? new List<SearchHit>() : ('+$expression+')'+
    $s.Substring($endExpr)
}
else
{
 $quickRoot=$s.IndexOf('foreach(var root in QuickRootsV288())')
 if($quickRoot -ge 0)
 {
  $quickIf=$s.LastIndexOf('if(fileQuery.Length>=2',$quickRoot)
  if($quickIf -ge 0)
   { $s=$s.Substring(0,$quickIf)+'if(fileMatchesV334==0 && fileQuery.Length>=2'+
       $s.Substring($quickIf+'if(fileQuery.Length>=2'.Length) }
 }
}
$original='        var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);'
if(([regex]::Matches($s,[regex]::Escape($original))).Count -ne 1){
 throw 'R334 Windows Search provider must appear exactly once'
}
$provider=@'
        var indexed=fileQuery.StartsWith("content:",
            StringComparison.OrdinalIgnoreCase)
            ?DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation)
            :new List<SearchHit>();
'@
$s=$s.Replace($original,$provider.Replace($crlf,$lf))
# The R221 quickFiles fallback historically crawled up to 2400
# directories synchronously. Cap ONLY this best-effort interactive crawl;
# deep files remain indexed by the uncapped R330 disk catalog.
$quickPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceFileQuickSearchV233.cs'
$q=[IO.File]::ReadAllText($quickPath).Replace($crlf,$lf)
$quickScanOld='        int scanned = 0;'
$quickScanNew=@'
        // R334: keep a responsive search UI even on inaccessible/network
        // directories; the disk-backed index remains the complete provider.
        var interactiveClockV334=System.Diagnostics.Stopwatch.StartNew();
        int scanned = 0;
'@
if(([regex]::Matches($q,[regex]::Escape($quickScanOld))).Count -ne 1){
 throw 'R334 cannot locate bounded quick scan count'
}
$q=$q.Replace($quickScanOld,$quickScanNew.Replace($crlf,$lf).TrimEnd($lf.ToCharArray()))
$old='while (queue.Count > 0 && scanned < MaximumDirectories &&'
if(([regex]::Matches($q,[regex]::Escape($old))).Count -ne 1){
 throw 'R334 cannot locate quick crawler guard'
}
$q=$q.Replace($old,'while (queue.Count > 0 && interactiveClockV334.ElapsedMilliseconds<1200 && scanned < MaximumDirectories &&')
[IO.File]::WriteAllText($quickPath,$q,[Text.UTF8Encoding]::new($false))

# Prevent the all-volume scanner from indexing the SQLite DB it is
# actively writing. The watcher side is excluded in DiskSearch.Enqueue.
# R368 uses a single metadata-aware enumerator that already excludes
# its own SQLite cache directory before descending. Keep legacy R335 support.
$iterStart=$s.IndexOf('EnumerateCatalogEntriesV368(string root)')
$iterEnd=if($iterStart -ge 0){$s.IndexOf('    private static void ScanVolume(string root)', $iterStart)}else{-1}
if($iterStart -ge 0 -and $iterEnd -gt $iterStart){
    $iteratorBody=$s.Substring($iterStart,$iterEnd-$iterStart)
    if(!$iteratorBody.Contains('DeskSpaceDiskSearchV330.IsOwnIndexPathV335(path)')){
        throw 'R335/R368 full-drive scanner own-cache exclusion missing'
    }
    Write-Host 'R335 own-cache exclusion preserved in R368 enumerator.'
}
else {
    $scan='            foreach(var path in EnumerateTree(root))'+$lf+
          '            {'
    if(([regex]::Matches($s,[regex]::Escape($scan))).Count -ne 1){
        throw 'R335 must find one complete foreach opening brace'
    }
    $s=$s.Replace($scan,$scan+$lf+
        '                if(DeskSpaceDiskSearchV330.IsOwnIndexPathV335(path))continue;')
}

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
if($s.Contains('var indexed=DeskSpaceWindowsSearchV18.Query(fileQuery,cancellation);') -or
   !$s.Contains('fileMatchesV334') -or
   !$s.Contains('int fileMatchesV334=0;')){
 throw 'R334 file-search fast-first invariants are not satisfied'
}
Write-Host 'R334 indexed filename results now precede slow COM and quick crawl, avoiding observed 17-second delay; content search unchanged.'

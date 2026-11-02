$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$index=Join-Path $root 'DeskSpaceSearchIndexV13.cs'
$ui=Join-Path $root 'DeskSpaceSearchV11.cs'
$disk=Join-Path $root 'DeskSpaceDiskSearchV330.cs'
foreach($p in @($index,$ui,$disk)){if(!(Test-Path -LiteralPath $p)){throw "R332 missing shipping search component $p"}}
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($index).Replace($crlf,$lf)
$begin=$s.IndexOf('        // The old search enumerated the entire growing Entries list on')
$finish=$s.IndexOf('        return ranked.Finish();',$begin)
if($begin -lt 0 -or $finish -le $begin){throw 'R332 cannot locate previous R330 disk search block'}
$fixed=@'
        // R332: disk search is an OPTIONAL source of EXTRA file matches.
        // A slow, busy or partially indexed SQLite DB must NEVER suppress
        // Windows Search, applications, notes, or the quick filename crawl.
        // The old code threw out every result and left a blank panel.
        if(!fileQuery.StartsWith("content:",StringComparison.OrdinalIgnoreCase))
        {
            var diskBudgetV332=System.Diagnostics.Stopwatch.StartNew();
            bool diskFailedV332=false;
            foreach(var root in driveRoots)
            {
                if(scoped&&!root.Equals(driveFilter,
                    StringComparison.OrdinalIgnoreCase))continue;
                if(diskFailedV332||diskBudgetV332.ElapsedMilliseconds>900)break;
                for(int pass=0;pass<2;pass++)
                {
                    if(diskBudgetV332.ElapsedMilliseconds>900)break;
                    int quota=pass==0?48:12;
                    int accepted=0;
                    IReadOnlyList<DeskSpaceDiskSearchV330.Item> diskHits;
                    try
                    {
                        diskHits=DeskSpaceDiskSearchV330.Query(
                            fileQuery,root,cancellation,pass==0?240:72,pass==1);
                    }
                    catch(OperationCanceledException){throw;}
                    catch(Exception ex) when(
                        ex is Microsoft.Data.Sqlite.SqliteException or IOException or
                        UnauthorizedAccessException or InvalidOperationException or
                        System.Security.SecurityException)
                    {
                        // Record a numeric health counter, no query/path data.
                        // Another search and the ongoing full-drive index may
                        // recover automatically without a restart.
                        DeskSpaceDiskSearchV330.NoteQueryFailure();
                        diskFailedV332=true;
                        break;
                    }
                    foreach(var item in diskHits)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if(item.Name.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)||
                            item.Name.EndsWith(".url",StringComparison.OrdinalIgnoreCase))
                            continue;
                        if(item.Folder&&!Directory.Exists(item.Path))continue;
                        if(!item.Folder&&!IsSearchableFileV288(item.Path))continue;
                        if(Add(new SearchHit{Category="Files",Name=item.Name,
                            OpenTarget=item.Path,Location=item.Path}))
                            accepted++;
                        if(accepted>=quota)break;
                    }
                }
            }
        }
'@
$s=$s.Substring(0,$begin)+$fixed.Replace($crlf,$lf)+$s.Substring($finish)
# Search boxes must show recoverable status rather than an empty blank area.
$w=[IO.File]::ReadAllText($ui).Replace($crlf,$lf)
$old=@'
            catch(Exception ex){if(!token.IsCancellationRequested)status.Text=ex.Message;}
'@
$new=@'
            catch(Exception ex){
                if(!token.IsCancellationRequested){
                    status.Text="Search issue — retrying indexed search. "+ex.GetType().Name;
                    list.Children.Clear();
                    list.Children.Add(new TextBlock{
                        Text="Unable to complete this search. Try the filename again; "+
                             "Windows Search and quick file fallback remain available.",
                        Margin=new Thickness(15),TextWrapping=TextWrapping.Wrap});
                    below.Visibility=Visibility.Visible;SetSize();
                }
            }
'@
if(([regex]::Matches($w,[regex]::Escape($old))).Count -ne 1){
 throw 'R332 Search UI error visibility anchor changed'
}
$w=$w.Replace($old,$new.Replace($crlf,$lf))
[IO.File]::WriteAllText($index,$s,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($ui,$w,[Text.UTF8Encoding]::new($false))
if(!$s.Contains('diskFailedV332=true') -or
   !$s.Contains('diskBudgetV332.ElapsedMilliseconds>900') -or
   !$s.Contains('return ranked.Finish();')){
 throw 'R332 failed to preserve fail-open result collector'
}
Write-Host 'R332 fixed shipping Search: filename results return from quick/Windows sources despite SQLite lock/error, 900ms optional DB budget, status instead of blank results.'

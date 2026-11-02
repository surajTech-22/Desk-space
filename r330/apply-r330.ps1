$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$idx=Join-Path $root 'DeskSpaceSearchIndexV13.cs'
$project=Join-Path $root 'DeskSpace.csproj'
if(!(Test-Path $idx) -or !(Test-Path $project)){
 throw 'R330 shipping DeskSpace search/index/project files missing'
}
Copy-Item 'r330/DeskSpaceDiskSearchV330.cs' (Join-Path $root 'DeskSpaceDiskSearchV330.cs') -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($idx).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw ("R330 "+$label+" expected exactly one anchor, found "+$hits)}
 $script:s=$script:s.Replace($old,$new)
}
One @'
        _=Task.Run(BuildIndex);
        _=Task.Run(()=>DeskSpaceAllDriveLiveIndexV324.Start(
            RegisterFileSystemChangeV324,root=>ScanVolume(root)));
'@ @'
        // R330 scans all ready volumes without buffering their inventories
        // in managed memory. The local disk index accepts small transactions.
        DeskSpaceDiskSearchV330.EnableUpdates(
            root=>{_=Task.Run(()=>ScanVolume(root));});
        _=Task.Run(BuildIndex);
        _=Task.Run(()=>DeskSpaceAllDriveLiveIndexV324.Start(
            RegisterFileSystemChangeV324,root=>ScanVolume(root)));
'@ 'bounded disk watcher initialization'
$old=@'
    internal static void RegisterFileSystemChangeV324(string path,bool exists)
    {
        try
        {
            string full=Path.GetFullPath(path);
            if(!exists || (!File.Exists(full) && !Directory.Exists(full)))
            {
                LivePaths.TryRemove(full,out _);
                LiveFoldersV324.TryRemove(full,out _);
            }
            else if(Directory.Exists(full))
            {
                LiveFoldersV324[full]=0;
                LivePaths.TryRemove(full,out _);
            }
            else
            {
                LivePaths[full]=0;
                LiveFoldersV324.TryRemove(full,out _);
            }
            Interlocked.Increment(ref revisionV324);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException
            or System.Security.SecurityException){}
    }
'@
$new=@'
    internal static void RegisterFileSystemChangeV324(string path,bool exists)
    {
        // Never retain every file-change path in ConcurrentDictionary.
        // An 8,000-event burst is bounded and overflow requests a volume
        // rescan. Local index remains complete and survives app restarts.
        DeskSpaceDiskSearchV330.Enqueue(path,exists);
        Interlocked.Increment(ref revisionV324);
    }
'@
One $old $new 'remove unlimited live changed filename dictionaries'
$start=$s.IndexOf('    private static void ScanVolume(string root)')
$end=$s.IndexOf('    private static void BuildIndex()', $start)
if($start -lt 0 -or $end -le $start){
 throw 'R330 full-volume scan method boundaries changed'
}
$scan=@'
    private static void ScanVolume(string root)
    {
        if(!ScanningRootsV324.TryAdd(root,0))return;
        var batch=new List<DeskSpaceDiskSearchV330.Item>(
            DeskSpaceDiskSearchV330.BatchSize);
        long epoch=0;
        bool succeeded=false;
        try
        {
            VolumeComplete[root]=false;
            epoch=DeskSpaceDiskSearchV330.BeginScan();
            long found=0;
            foreach(var path in EnumerateTree(root))
            {
                FileAttributes flags;
                try{flags=File.GetAttributes(path);}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                    or ArgumentException or System.Security.SecurityException
                    or NotSupportedException){continue;}
                batch.Add(new DeskSpaceDiskSearchV330.Item(
                    Path.GetFileName(path),path,
                    (flags&FileAttributes.Directory)!=0,root));
                if(batch.Count<DeskSpaceDiskSearchV330.BatchSize)continue;
                DeskSpaceDiskSearchV330.WriteBatch(root,epoch,batch);
                found+=batch.Count;
                VolumeCounts[root]=found;
                batch.Clear();
                Interlocked.Increment(ref revisionV324);
            }
            if(batch.Count!=0)
            {
                DeskSpaceDiskSearchV330.WriteBatch(root,epoch,batch);
                found+=batch.Count;
                VolumeCounts[root]=found;
                batch.Clear();
            }
            DeskSpaceDiskSearchV330.CompleteScan(root,epoch);
            succeeded=true;
        }
        catch(Exception)
        {
            // Leave the last complete disk index searchable if a volume
            // disappears or its scan fails midway. No large heap retained.
        }
        finally
        {
            batch.Clear();
            VolumeComplete[root]=succeeded;
            ScanningRootsV324.TryRemove(root,out _);
            Interlocked.Increment(ref revisionV324);
        }
    }
'@
$s=$s.Substring(0,$start)+$scan.Replace($crlf,$lf)+$s.Substring($end)
$marker=$s.LastIndexOf('foreach(var item in Entries)')
if($marker -lt 0){
 $candidates=($s -split $lf | Where-Object {$_ -match 'Entries|foreach|SearchHit'}| Select-Object -Last 35) -join $lf
 throw ("R330 cannot find in-memory Entries search; candidate lines: "+$candidates)
}
$searchStart=$s.LastIndexOf('        lock(Gate)',$marker)
if($searchStart -lt 0){$searchStart=$s.LastIndexOf('        lock (Gate)',$marker)}
if($searchStart -lt 0){
 $a=[Math]::Max(0,$marker-450)
 throw ("R330 old search loop has no lock anchor: "+$s.Substring($a,[Math]::Min(1300,$s.Length-$a)))
}
$searchEnd=$s.IndexOf('        return ranked.Finish();',$marker)
if($searchEnd -lt $marker){
 throw ("R330 old indexed search has no finishing result marker: "+$s.Substring($marker,[Math]::Min(600,$s.Length-$marker)))
}
$replacement=@'
        // The old search enumerated the entire growing Entries list on
        // every keystroke, allocating up to gigabytes in a large computer.
        // Query bounded disk rows, per drive and per category; the ranking
        // collector still enforces normal global result limits.
        if(!fileQuery.StartsWith("content:",StringComparison.OrdinalIgnoreCase))
        {
            foreach(var root in driveRoots)
            {
                if(scoped&&!root.Equals(driveFilter,
                    StringComparison.OrdinalIgnoreCase))continue;
                for(int pass=0;pass<2;pass++)
                {
                    int quota=pass==0?48:12;
                    int accepted=0;
                    foreach(var item in DeskSpaceDiskSearchV330.Query(
                        fileQuery,root,cancellation,
                        DeskSpaceDiskSearchV330.MaximumReturned,pass==1))
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
$s=$s.Substring(0,$searchStart)+$replacement.Replace($crlf,$lf)+$s.Substring($searchEnd)
# Existing dynamic quick-file registration must not become another massive
# catalog. Manually registered recent paths are limited to 256; old
# full-drive watcher never writes to this dictionary after R330.
One @'
            if(IsSearchableFileV288(full))LivePaths.TryAdd(full,0);
'@ @'
            if(IsSearchableFileV288(full))
            {
                DeskSpaceDiskSearchV330.Enqueue(full,true);
                if(LivePaths.Count<256)LivePaths.TryAdd(full,0);
            }
'@ 'cap explicit live entries'
One @'
        try{LivePaths.TryRemove(Path.GetFullPath(path),out _);}
'@ @'
        try{
            var full=Path.GetFullPath(path);
            LivePaths.TryRemove(full,out _);
            DeskSpaceDiskSearchV330.Enqueue(full,false);
        }
'@ 'remove deleted manual live entry'
$cs=[IO.File]::ReadAllText($project)
if($cs.Contains('Microsoft.Data.Sqlite')){throw 'R330 project already references SQLite, avoid duplicate dependency'}
$needle='  <ItemGroup>'+$crlf
if(!$cs.Contains($needle)){
 $needle='  <ItemGroup>'+$lf
}
if(!$cs.Contains($needle)){throw 'R330 cannot locate project item-group insertion anchor'}
$pkg=@'
  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.11" />
  </ItemGroup>
'@
$cs=$cs.Replace($needle,$pkg.Replace($crlf,$lf)+$needle)
[IO.File]::WriteAllText($project,$cs,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($idx,$s,[Text.UTF8Encoding]::new($false))
if($s.Contains('foreach(var item in Entries)') -or $s.Contains('Entries.AddRange(batch)') -or
   !$s.Contains('DeskSpaceDiskSearchV330.Query(')){
 throw 'R330 in-memory all-files list still used for search or ingestion'
}
Write-Host 'R330: full drive catalog is disk-backed, with bounded batches/results/live event queue. No full-drive file list held in managed RAM.'

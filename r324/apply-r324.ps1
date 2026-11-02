$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$target=Join-Path $root 'DeskSpaceSearchIndexV13.cs'
$ui=Join-Path $root 'DeskSpaceSearchV11.cs'
if(!(Test-Path $target) -or !(Test-Path $ui)){throw 'R324 source search missing'}
Copy-Item 'r324/DeskSpaceAllDriveLiveIndexV324.cs' (Join-Path $root 'DeskSpaceAllDriveLiveIndexV324.cs') -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($target).Replace($crlf,$lf)
$w=[IO.File]::ReadAllText($ui).Replace($crlf,$lf)
function One([string]$variable,[string]$before,[string]$after,[string]$label){
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $value=Get-Variable -Name $variable -ValueOnly
 $hits=([regex]::Matches($value,[regex]::Escape($before))).Count
 if($hits -ne 1){throw ("R324 "+$label+" anchor count "+$hits)}
 Set-Variable -Name $variable -Value ($value.Replace($before,$after)) -Scope Script
}
One s @'
    private static readonly ConcurrentDictionary<string,byte> LivePaths=
        new(StringComparer.OrdinalIgnoreCase);
'@ @'
    private static readonly ConcurrentDictionary<string,byte> LivePaths=
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string,byte> LiveFoldersV324=
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string,byte> ScanningRootsV324=
        new(StringComparer.OrdinalIgnoreCase);
    private static long revisionV324;
    internal static long RevisionV324=>Interlocked.Read(ref revisionV324);
    internal static bool IsSearchingDrivesV324=>!ScanningRootsV324.IsEmpty;
    // Event callback never opens document contents and never blocks the WPF UI.
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
'@ 'incremental changed path records'
One s '    public static bool IsIndexing => started && !completed;' @'
    public static bool IsIndexing => started &&
        (!completed || IsSearchingDrivesV324);
'@ 'dynamic crawl status'
One s @'
                if((flags&FileAttributes.ReparsePoint)!=0)continue;
                if((flags&FileAttributes.Directory)!=0)stack.Push(path);
                yield return path;
'@ @'
                // Symlinked/Junction directories can loop forever. The
                // link itself is still a searchable path, but never recurse.
                if((flags&FileAttributes.Directory)!=0 &&
                   (flags&FileAttributes.ReparsePoint)==0)stack.Push(path);
                yield return path;
'@ 'do not recurse into junctions'
# Some accepted R221/R232 shipping snapshots precede R288. Supply a
# stable eligibility method if absent, or replace it by method signature
# without assuming new repository source had already been copied to the EXE.
$methodStart=$s.IndexOf('    internal static bool IsSearchableFileV288(string path)')
$method=@'
    internal static bool IsSearchableFileV288(string path)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            var name=Path.GetFileName(path);
            if(name.Length==0)return false;
            // All accessible names, including hidden, system, temporary,
            // dotfiles and file links; never open document contents.
            var flags=File.GetAttributes(path);
            return (flags&FileAttributes.Directory)==0;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException
            or ArgumentException or System.Security.SecurityException
            or NotSupportedException){return false;}
    }

'@
$method=$method.Replace($crlf,$lf)
if($methodStart -lt 0)
{
 $insert=$s.IndexOf('    private static void ScanVolume(string root)')
 if($insert -lt 0){throw 'R324 shipping source contains no ScanVolume insertion point'}
 $s=$s.Insert($insert,$method)
}
else
{
 $methodEnd=$s.IndexOf('    // Before a full multi-drive crawl', $methodStart)
 if($methodEnd -lt 0){
    $methodEnd=$s.IndexOf('    internal static ', $methodStart+20)
 }
 if($methodEnd -lt 0){
    $methodEnd=$s.IndexOf('    private static void ScanVolume(string root)', $methodStart+20)
 }
 if($methodEnd -le $methodStart){throw 'R324 cannot find end of old file predicate'}
 $s=$s.Substring(0,$methodStart)+$method+$s.Substring($methodEnd)
}
# The R221 catalog reused the recent-file watcher filter. Search is not
# the recently-modified files deck: all accessible files must be eligible.
$s=$s.Replace('DeskSpaceNewFileWatchV53.IsOrdinaryUserFile(item.Path)',
              'IsSearchableFileV288(item.Path)')
$s=$s.Replace('DeskSpaceNewFileWatchV53.IsOrdinaryUserFile(full)',
              'IsSearchableFileV288(full)')
One s @'
        _=Task.Run(BuildIndex);
'@ @'
        _=Task.Run(BuildIndex);
        _=Task.Run(()=>DeskSpaceAllDriveLiveIndexV324.Start(
            RegisterFileSystemChangeV324,root=>ScanVolume(root)));
'@ 'attach live all-drive name changes'
One s @'
    private static void ScanVolume(string root)
    {
        var batch=new List<Entry>(256);
        VolumeCounts[root]=0;
        VolumeComplete[root]=false;
        try{
            foreach(var path in EnumerateTree(root))
            {
                bool folder;
                try{folder=Directory.Exists(path);}
                catch{continue;}
                batch.Add(new Entry(Path.GetFileName(path),path,folder));
                if(batch.Count<256)continue;
                lock(Gate)Entries.AddRange(batch);
                VolumeCounts.AddOrUpdate(root,batch.Count,(_,count)=>count+batch.Count);
                batch.Clear();
            }
        }catch(Exception){}
        if(batch.Count>0)
        {
            lock(Gate)Entries.AddRange(batch);
            VolumeCounts.AddOrUpdate(root,batch.Count,(_,count)=>count+batch.Count);
        }
        VolumeComplete[root]=true;
    }
'@ @'
    private static void ScanVolume(string root)
    {
        // Prevent simultaneous scans triggered by initial crawl, new-volume
        // detection and watcher-overflow recovery. A rescan replaces only
        // this volume; other monitors/drives and workspace state are untouched.
        if(!ScanningRootsV324.TryAdd(root,0))return;
        var batch=new List<Entry>(512);
        try
        {
            VolumeComplete[root]=false;
            lock(Gate)
            {
                Entries.RemoveAll(e=>string.Equals(
                    Path.GetPathRoot(e.Path),root,StringComparison.OrdinalIgnoreCase));
                VolumeCounts[root]=0;
            }
            foreach(var path in EnumerateTree(root))
            {
                FileAttributes flags;
                try{flags=File.GetAttributes(path);}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                    or ArgumentException or System.Security.SecurityException
                    or NotSupportedException){continue;}
                batch.Add(new Entry(Path.GetFileName(path),path,
                    (flags&FileAttributes.Directory)!=0));
                if(batch.Count<512)continue;
                lock(Gate)Entries.AddRange(batch);
                VolumeCounts.AddOrUpdate(root,batch.Count,(_,count)=>count+batch.Count);
                Interlocked.Increment(ref revisionV324);
                batch.Clear();
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or System.Security.SecurityException
            or NotSupportedException){}
        finally
        {
            if(batch.Count!=0)
            {
                lock(Gate)Entries.AddRange(batch);
                VolumeCounts.AddOrUpdate(root,batch.Count,(_,count)=>count+batch.Count);
            }
            VolumeComplete[root]=true;
            ScanningRootsV324.TryRemove(root,out _);
            Interlocked.Increment(ref revisionV324);
        }
    }
'@ 'replace stale one-shot all-drive scan with safe root rescan'
One s @'
        // A query should not return only matching folder names while files
'@ @'
        // Live folder rename/new-folder events become searchable without
        // requiring an application restart or a full-drive rescan.
        if(!fileQuery.StartsWith("content:",StringComparison.OrdinalIgnoreCase))
            foreach(var path in LiveFoldersV324.Keys)
            {
                cancellation.ThrowIfCancellationRequested();
                if(!Directory.Exists(path))
                {
                    LiveFoldersV324.TryRemove(path,out _);
                    continue;
                }
                if(scoped && !path.StartsWith(driveFilter!,
                    StringComparison.OrdinalIgnoreCase))continue;
                if(DeskSpaceFileQuickSearchV233.MatchesIndexedEntry(
                    Path.GetFileName(path),path,fileQuery))
                    Add(new SearchHit{Category="Files",Name=Path.GetFileName(path),
                        OpenTarget=path,Location=path});
            }
        // A query should not return only matching folder names while files
'@ 'merge new folders'
# Existing R288 quick file crawler silently skips hidden/system paths, and
# caps immediate discovery to 1,200 dirs; full index handles deep coverage.
$quick=Join-Path $root 'DeskSpaceFileQuickSearchV233.cs'
$q=[IO.File]::ReadAllText($quick).Replace($crlf,$lf)
One q @'
    private const int MaximumDirectories = 1200;
'@ @'
    // Bounded interactive fallback only; the uncapped full-volume crawl
    // indexes every accessible descendant in the background.
    private const int MaximumDirectories = 2400;
'@ 'wider fast fallback'
One q @'
                    if ((attributes & (FileAttributes.ReparsePoint |
                        FileAttributes.Hidden | FileAttributes.System)) != 0)
'@ @'
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
'@ 'search hidden folders but avoid infinite junction loops'
# Background search result refresh also runs after the initial crawl, only
# if the index revision changed. Avoid perpetual rescans at every timer tick.
One w @'
        indexRefresh.Tick+=(_,_)=>{
            if(disposed || string.IsNullOrWhiteSpace(field.Text))
            {indexRefresh.Stop();return;}
            // One final refresh is required after the index completes.
            debounce.Stop();debounce.Start();
            if(!DeskSpaceSearchIndexV13.IsIndexing)indexRefresh.Stop();
        };
'@ @'
        long observedIndexRevisionV324=-1;
        indexRefresh.Tick+=(_,_)=>{
            if(disposed || string.IsNullOrWhiteSpace(field.Text))
            {indexRefresh.Stop();return;}
            long changed=DeskSpaceSearchIndexV13.RevisionV324;
            if(DeskSpaceSearchIndexV13.IsIndexing ||
               changed!=observedIndexRevisionV324)
            {
                observedIndexRevisionV324=changed;
                debounce.Stop();debounce.Start();
            }
        };
'@ 'show newly created and indexed results while search remains open'
[IO.File]::WriteAllText($target,$s,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($quick,$q,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($ui,$w,[Text.UTF8Encoding]::new($false))
Write-Host 'R324 full-PC filename search: uncapped all-ready-volume catalog; new drives and watcher overflow rescans; live created/renamed/deleted file updates; hidden/system names; cancellable UI refresh.'

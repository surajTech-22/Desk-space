$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r58/DeskSpaceFileVisibilityV58.cs' -Destination (Join-Path $root 'DeskSpaceFileVisibilityV58.cs') -Force
function Read([string]$path){[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$path,[string]$s){[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))}
function One([string]$old,[string]$new)
{
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R58 patch anchor found $count times: $old"}
    $script:s=$script:s.Replace($old,$new)
}

$p=Join-Path $root 'DeskSpaceNewFileWatchV53.cs';$s=Read $p
$begin=$s.IndexOf('    private static bool IsUserDocumentPath(',[StringComparison]::Ordinal)
$end=$s.IndexOf('    private void Created(',[StringComparison]::Ordinal)
if($begin -lt 0 -or $end -le $begin){throw 'R58 watcher filter location missing'}
$replace=@'
    private string? ExplicitScope(string path) =>
        explicitRoots?roots.FirstOrDefault(r=>IsInside(path,r)):null;

    private bool Eligible(string path) =>
        DeskSpaceFileVisibilityV58.Eligible(path,ExplicitScope(path));

'@
$s=$s.Substring(0,$begin)+$replace.Replace([string][char]13+[char]10,[string][char]10)+$s.Substring($end)
One 'NotifyFilters.LastWrite|NotifyFilters.Size,' 'NotifyFilters.LastWrite|NotifyFilters.Size|NotifyFilters.Attributes,'
One @'
        if(Volatile.Read(ref stopped)!=0 || (!explicitRoots&&!IsUserDocumentPath(path)))return;
        if(!inFlight.TryAdd(path,0))return; // coalesce bursty Office writes
'@ @'
        if(Volatile.Read(ref stopped)!=0)return;
        string? scope=ExplicitScope(path);
        if(!DeskSpaceFileVisibilityV58.Candidate(path,scope,Directory.Exists(path)))
        {
            if(recent.ContainsKey(path))Remove(path);
            return;
        }
        if(File.Exists(path)&&!Eligible(path))
        {
            // A file can become Hidden after it was already offered.
            if(recent.ContainsKey(path))Remove(path);
            return;
        }
        if(!inFlight.TryAdd(path,0))return; // coalesce bursty Office writes
'@
One @'
                    if(!Eligible(path))
                    {
                        if(Directory.Exists(path) && i==1)
'@ @'
                    if(!Eligible(path))
                    {
                        if(File.Exists(path)&&recent.ContainsKey(path))Remove(path);
                        if(Directory.Exists(path) && i==1 &&
                            DeskSpaceFileVisibilityV58.Eligible(path,
                                ExplicitScope(path),allowDirectory:true))
'@
One @'
                            foreach(var child in Directory.EnumerateFiles(path,"*",
                                new EnumerationOptions{RecurseSubdirectories=true,
                                    IgnoreInaccessible=true}))
'@ @'
                            foreach(var child in EnumerateVisibleFiles(path,100,shutdown.Token))
'@
One @'
                    foreach(var path in Directory.EnumerateFiles(dir,"*",
                        new EnumerationOptions{RecurseSubdirectories=true,
                            IgnoreInaccessible=true}))
'@ @'
                    foreach(var path in EnumerateVisibleFiles(dir,15000,token))
'@
One '    private async Task ReconcileAsync(CancellationToken token)' @'
    // Prune hidden/cache/system directories BEFORE entering them.
    private static IEnumerable<string> EnumerateVisibleFiles(string root,int limit,
        CancellationToken cancellation)
    {
        var pending=new Stack<string>();
        pending.Push(root);
        int seen=0;
        while(pending.Count>0&&!cancellation.IsCancellationRequested && seen<limit)
        {
            string dir=pending.Pop();
            string[] children;
            try{children=Directory.GetFileSystemEntries(dir);}
            catch(IOException){continue;}
            catch(UnauthorizedAccessException){continue;}
            foreach(var child in children)
            {
                if(cancellation.IsCancellationRequested||++seen>limit)yield break;
                FileAttributes flags;
                try{flags=File.GetAttributes(child);}
                catch(IOException){continue;}
                catch(UnauthorizedAccessException){continue;}
                if(!DeskSpaceFileVisibilityV58.IncludeInCrawl(child,flags))continue;
                if((flags&FileAttributes.Directory)!=0)pending.Push(child);
                else yield return child;
            }
        }
    }
    private async Task ReconcileAsync(CancellationToken token)
'@
Save $p $s

$p=Join-Path $root 'DeskSpaceDropShelfV15.cs';$s=Read $p
One @'
        DeskSpaceSearchIndexV13.UnregisterLivePath(path);
        if(detected.RemoveAll(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase))>0)Render();
'@ @'
        DeskSpaceSearchIndexV13.UnregisterLivePrefix(path);
        var prefix=path.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(detected.RemoveAll(x=>string.Equals(x.StagedPath,path,
            StringComparison.OrdinalIgnoreCase) ||
            x.StagedPath.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))>0)
            Render();
'@
Save $p $s

$p=Join-Path $root 'DeskSpaceSearchIndexV13.cs';$s=Read $p
One @'
    public static void UnregisterLivePath(string path)
    {
        try{LivePaths.TryRemove(Path.GetFullPath(path),out _);}
        catch(ArgumentException){}
    }
'@ @'
    public static void UnregisterLivePath(string path) =>
        UnregisterLivePrefix(path);
    public static void UnregisterLivePrefix(string path)
    {
        try
        {
            var full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            var prefix=full+Path.DirectorySeparatorChar;
            foreach(var key in LivePaths.Keys)
                if(key.Equals(full,StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
                    LivePaths.TryRemove(key,out _);
        }
        catch(ArgumentException){}
    }
'@
One @'
            if(File.Exists(full))LivePaths.TryAdd(full,0);
'@ @'
            if(DeskSpaceFileVisibilityV58.Eligible(full))
                LivePaths.TryAdd(full,0);
'@
One @'
    private static void ScanVolume(string root)
'@ @'
    private static IEnumerable<string> EnumerateVisibleTree(string root)
    {
        if(string.IsNullOrWhiteSpace(root)||!Directory.Exists(root))yield break;
        var stack=new Stack<string>();stack.Push(root);
        while(stack.Count>0)
        {
            var directory=stack.Pop();
            string[] nodes;
            try{nodes=Directory.GetFileSystemEntries(directory);}
            catch(Exception e) when(e is IOException or UnauthorizedAccessException
                or System.Security.SecurityException or ArgumentException){continue;}
            foreach(var path in nodes)
            {
                FileAttributes flags;
                try{flags=File.GetAttributes(path);}
                catch(Exception e) when(e is IOException or UnauthorizedAccessException
                    or System.Security.SecurityException or ArgumentException){continue;}
                if(!DeskSpaceFileVisibilityV58.IncludeInCrawl(path,flags))continue;
                if((flags&FileAttributes.Directory)!=0)stack.Push(path);
                yield return path;
            }
        }
    }
    private static void ScanVolume(string root)
'@
One @'
            foreach(var path in EnumerateTree(root))
            {
                bool folder;
'@ @'
            foreach(var path in EnumerateVisibleTree(root))
            {
                bool folder;
'@
One @'
        void Add(SearchHit hit)
        {
            if(results.Count<400 && seen.Add(hit.OpenTarget ?? ""))results.Add(hit);
        }
'@ @'
        void Add(SearchHit hit)
        {
            // Apply the same hidden/cache policy to Windows Search,
            // live watcher records and the fallback drive index. Exempt
            // Applications and Settings: they are not user documents.
            if(hit.Category=="Files" &&
               !DeskSpaceFileVisibilityV58.Eligible(hit.OpenTarget,allowDirectory:true))
                return;
            if(results.Count<400 && seen.Add(hit.OpenTarget ?? ""))results.Add(hit);
        }
'@
Save $p $s
Write-Host 'R58: hidden attributes and parent directories excluded; caches pruned; shelf and all Search routes share one visibility policy.'

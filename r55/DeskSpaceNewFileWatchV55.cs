using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DeskSpace;

// Name/path-only monitoring. Explicit drag is required to copy bytes.
// Applications commonly Save by Changed, temp-file Rename or File.Replace,
// so watching Created alone misses ordinary Word/Excel/PDF output.
internal sealed class DeskSpaceNewFileWatchV53 : IDisposable
{
    private readonly Window owner;
    private readonly Action<string> onNewFile;
    private readonly Action<string>? onRemoved;
    private readonly List<FileSystemWatcher> watchers=new();
    private readonly ConcurrentDictionary<string,long> recent=new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string,byte> inFlight=new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<string> roots;
    private readonly bool explicitRoots;
    private readonly CancellationTokenSource shutdown=new();
    private readonly ConcurrentDictionary<string,(long Size,long Stamp)> seenVersions=
        new(StringComparer.OrdinalIgnoreCase);
    private int stopped;
    private bool started;

    // Retain the exact three-argument constructor used by older UI tests.
    public DeskSpaceNewFileWatchV53(Window owner,Action<string> onNewFile,
        IEnumerable<string>? watchRoots)
        : this(owner,onNewFile,watchRoots,null){}

    public DeskSpaceNewFileWatchV53(Window owner,Action<string> onNewFile,
        IEnumerable<string>? watchRoots=null, Action<string>? onRemoved=null)
    {
        this.owner=owner;
        this.onNewFile=onNewFile;
        this.onRemoved=onRemoved;
        explicitRoots=watchRoots!=null;
        roots=(watchRoots??DefaultRoots())
            .Where(p=>!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p=>p.Length).ToArray();
    }

    internal static IReadOnlyList<string> DefaultRoots()
    {
        var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates=new List<string>{
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Path.Combine(home,"Downloads"),Path.Combine(home,"OneDrive"),
            Path.Combine(home,"OneDrive - Personal")
        };
        // App Save As is supported under common user-work folders on other
        // volumes. Never watch an entire C:/D: drive and then try to filter
        // a torrent of app-internal events by extension.
        string[] names={"Work","Projects","Documents","Desktop","Downloads",
            "Accounts","Accounting","Audit","Audits","Clients","Invoices",
            "Reports","Exports","GST","Tax","Office","My Documents"};
        foreach(var drive in DriveInfo.GetDrives())
        {
            try
            {
                if(!drive.IsReady || drive.DriveType is not
                    (DriveType.Fixed or DriveType.Removable))continue;
                foreach(var name in names)
                    candidates.Add(Path.Combine(drive.RootDirectory.FullName,name));
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
        var extras=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","RecentFileFolders.txt");
        try{if(File.Exists(extras))candidates.AddRange(File.ReadAllLines(extras));}
        catch(IOException){}catch(UnauthorizedAccessException){}
        return candidates.Where(IsAllowedWatchFolder)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p=>p.Length).ToArray();
    }

    // Only user-approved work folders may be added. Application install,
    // cache, virtual-environment and hidden directories cannot be opted in.
    internal static bool IsAllowedWatchFolder(string folder)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(folder)||!Directory.Exists(folder))return false;
            var path=Path.GetFullPath(folder);
            if(IsSystemArea(path))return false;
            var parts=path.Split(new[]{Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries);
            if(parts.Any(p=>InternalFolders.Contains(p)||
                p.StartsWith(".",StringComparison.Ordinal)))return false;
            var dir=path;
            while(!string.IsNullOrWhiteSpace(dir))
            {
                var attributes=File.GetAttributes(dir);
                if((attributes&(FileAttributes.Hidden|FileAttributes.System|
                    FileAttributes.ReparsePoint))!=0)return false;
                var parent=Path.GetDirectoryName(dir.TrimEnd(
                    Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
                if(string.IsNullOrWhiteSpace(parent)||parent==dir)break;
                dir=parent;
            }
            return true;
        }
        catch(IOException){return false;}
        catch(UnauthorizedAccessException){return false;}
        catch(ArgumentException){return false;}
    }

    public int WatchCount=>watchers.Count;
    public void Start()
    {
        if(started||Volatile.Read(ref stopped)!=0)return;
        started=true;
        foreach(var path in roots)
        {
            // If a whole volume is already monitored, an overlapping user's
            // Documents watcher would duplicate every event.
            if(watchers.Any(w=>IsInside(path,w.Path)))continue;
            try
            {
                var watcher=new FileSystemWatcher(path){
                    Filter="*",
                    IncludeSubdirectories=true,
                    NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|
                                 NotifyFilters.LastWrite|NotifyFilters.Size,
                    InternalBufferSize=64*1024
                };
                watcher.Created+=Created;
                watcher.Changed+=Changed;
                watcher.Renamed+=Renamed;
                watcher.Deleted+=Deleted;
                watcher.Error+=WatchError;
                watcher.EnableRaisingEvents=true;
                watchers.Add(watcher);
            }
            catch(IOException){}
            catch(UnauthorizedAccessException){}
            catch(ArgumentException){}
        }
        _=Task.Run(()=>ReconcileAsync(shutdown.Token));
    }

    private static bool IsInside(string candidate,string root)
    {
        var normalized=root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        return candidate.Equals(root,StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(normalized,StringComparison.OrdinalIgnoreCase);
    }
    // Classification policy applies equally to Created, Changed, Renamed,
    // reconciliation and Search. No extension allowlist: real user files
    // of ANY type can appear, but generated/hidden content stays invisible.
    private static readonly HashSet<string> InternalFolders=new(
        StringComparer.OrdinalIgnoreCase){
        ".git",".svn",".hg",".vs",".vscode",".idea","__pycache__",
        ".pytest_cache",".mypy_cache",".ruff_cache",".gradle",".nuget",
        "node_modules","bower_components",".next",".angular",".cache",
        "cache","caches","code cache","gpucache","gpu cache",
        "shadercache","shader cache","dawncache","grshadercache",
        "temp","tmp","temporary files","temporary internet files",
        "inetcache","webcache","thumbnails","thumbcache",
        "service worker","serviceworker","crashdumps","crashpad",
        "logs","log","telemetry","bin","obj",".venv","venv",
        "packages","dist","build","runtimes","plugins","extensions",
        "windowsapps","appdata",
        "programdata","$recycle.bin","system volume information",
        "windows","program files","program files (x86)",
        "$windows.~bt","$windows.~ws","$winreagent"
    };
    private static readonly HashSet<string> GeneratedNames=new(
        StringComparer.OrdinalIgnoreCase){
        "thumbs.db","ehthumbs.db","desktop.ini",".ds_store",
        "iconcache.db","ntuser.dat","ntuser.dat.log1",
        "ntuser.dat.log2","pagefile.sys","swapfile.sys",
        "hiberfil.sys","dumpstack.log.tmp"
    };
    private static readonly HashSet<string> TemporaryExtensions=new(
        StringComparer.OrdinalIgnoreCase){
        ".tmp",".temp",".part",".partial",".crdownload",".download",
        ".swp",".swo",".lock",".lck",".dmp",".etl",
        ".cache",".pyc",".pyo",".class",".obj",".pch",".ilk",
        ".pdb",".idb",".tlog",".lastbuildstate"
    };
    private static bool IsSystemArea(string path)
    {
        foreach(var root in new[]{
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        })
            if(!string.IsNullOrWhiteSpace(root) && IsInside(path,root))
                return true;
        return false;
    }
    internal static bool IsOrdinaryUserFile(string path,bool allowTestRoot=false)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(path) || !File.Exists(path))return false;
            var full=Path.GetFullPath(path);
            if(!allowTestRoot && IsSystemArea(full))return false;
            var name=Path.GetFileName(full);
            if(name.Length==0 || name.StartsWith(".",StringComparison.Ordinal)
                || name.StartsWith("~",StringComparison.Ordinal)
                || name.StartsWith("$",StringComparison.Ordinal)
                || name.EndsWith("~",StringComparison.Ordinal)
                || name.EndsWith(".autosave",StringComparison.OrdinalIgnoreCase)
                || GeneratedNames.Contains(name)
                || TemporaryExtensions.Contains(Path.GetExtension(name)))
                return false;
            var flags=File.GetAttributes(full);
            if((flags & (FileAttributes.Hidden|FileAttributes.System|
                FileAttributes.Directory|FileAttributes.ReparsePoint|
                FileAttributes.Temporary))!=0)return false;
            // Reject hidden folders, application caches and generated-output
            // trees even when a normal-looking PDF/XLSX is stored inside.
            var parent=Path.GetDirectoryName(full);
            int depth=0;
            while(!string.IsNullOrWhiteSpace(parent) && depth++<80)
            {
                var folder=Path.GetFileName(parent.TrimEnd(
                    Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
                // Explicit test roots may live beneath Windows Temp/AppData.
                // Keep testing genuine source filenames without treating the
                // test harness' ancestor folder as the user's cache.
                if((!allowTestRoot || 
                    (!folder.Equals("temp",StringComparison.OrdinalIgnoreCase) &&
                     !folder.Equals("tmp",StringComparison.OrdinalIgnoreCase) &&
                     !folder.Equals("appdata",StringComparison.OrdinalIgnoreCase))) &&
                   (InternalFolders.Contains(folder) ||
                    folder.StartsWith(".",StringComparison.Ordinal)))
                    return false;
                if(!allowTestRoot)
                {
                    var attrs=File.GetAttributes(parent);
                    if((attrs&(FileAttributes.Hidden|FileAttributes.System|
                        FileAttributes.ReparsePoint))!=0)
                        return false;
                }
                var next=Path.GetDirectoryName(parent.TrimEnd(
                    Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
                if(string.IsNullOrWhiteSpace(next)||next==parent)break;
                parent=next;
            }
            return true;
        }
        catch(IOException){return false;}
        catch(UnauthorizedAccessException){return false;}
        catch(ArgumentException){return false;}
    }
    private static bool IsUserDocumentPath(string path)
    {
        if(string.IsNullOrWhiteSpace(path))return false;
        if(IsSystemArea(path))return false;
        // Fast path before filesystem attribute reads, saving significant
        // work for high-volume temporary writes across whole drives.
        var parts=path.Split(new[]{Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries);
        if(parts.Any(p=>InternalFolders.Contains(p)||
            p.StartsWith(".",StringComparison.Ordinal)))return false;
        var name=Path.GetFileName(path);
        return !GeneratedNames.Contains(name) &&
            !TemporaryExtensions.Contains(Path.GetExtension(name)) &&
            !name.StartsWith("~",StringComparison.Ordinal) &&
            !name.StartsWith("$",StringComparison.Ordinal) &&
            !name.StartsWith(".",StringComparison.Ordinal);
    }
    private bool Eligible(string path)
    {
        try
        {
            return IsOrdinaryUserFile(path,explicitRoots);
        }
        catch(IOException){return false;}
        catch(UnauthorizedAccessException){return false;}
        catch(ArgumentException){return false;}
    }

    private void Created(object sender,FileSystemEventArgs e)=>Queue(e.FullPath);
    private void Changed(object sender,FileSystemEventArgs e)=>Queue(e.FullPath);
    private void Renamed(object sender,RenamedEventArgs e)
    {
        Remove(e.OldFullPath);
        Queue(e.FullPath);
    }
    private void Deleted(object sender,FileSystemEventArgs e)=>Remove(e.FullPath);
    private void Remove(string path)
    {
        if(Volatile.Read(ref stopped)!=0)return;
        recent.TryRemove(path,out _);
        seenVersions.TryRemove(path,out _);
        if(onRemoved==null)return;
        try{
            if(!owner.Dispatcher.HasShutdownStarted)
                owner.Dispatcher.BeginInvoke(new Action(()=>onRemoved(path)));
        }catch(InvalidOperationException){}
    }

    private void Queue(string path)
    {
        if(Volatile.Read(ref stopped)!=0 || (!explicitRoots&&!IsUserDocumentPath(path)))return;
        if(!inFlight.TryAdd(path,0))return; // coalesce bursty Office writes
        _=Task.Run(async()=>{
            try
            {
                // Some applications write for seconds. Observe two stable
                // consecutive (size, last-write) samples before exposing it.
                long previousLength=-1,previousStamp=-1;
                int stable=0;
                for(int i=0;i<24&&!shutdown.IsCancellationRequested;i++)
                {
                    await Task.Delay(275,shutdown.Token);
                    if(!Eligible(path))
                    {
                        if(Directory.Exists(path) && i==1)
                        {
                            // Save As may create/move a full output folder.
                            int n=0;
                            foreach(var child in Directory.EnumerateFiles(path,"*",
                                new EnumerationOptions{RecurseSubdirectories=true,
                                    IgnoreInaccessible=true}))
                            {
                                if(n++>=30)break;
                                Queue(child);
                            }
                        }
                        if(i>4)break;
                        continue;
                    }
                    var info=new FileInfo(path);
                    long size=info.Length,stamp=info.LastWriteTimeUtc.Ticks;
                    if(size==previousLength && stamp==previousStamp)stable++;
                    else stable=0;
                    previousLength=size;previousStamp=stamp;
                    if(stable>=2){Report(path);break;}
                }
            }
            catch(IOException){}
            catch(UnauthorizedAccessException){}
            catch(OperationCanceledException){}
            catch(ArgumentException){}
            finally{inFlight.TryRemove(path,out _);}
        },shutdown.Token);
    }
    private void Report(string path)
    {
        if(Volatile.Read(ref stopped)!=0||!Eligible(path))return;
        var info=new FileInfo(path);
        var version=(info.Length,info.LastWriteTimeUtc.Ticks);
        if(seenVersions.TryGetValue(path,out var old) && old==version)return;
        seenVersions[path]=version;
        long now=Environment.TickCount64;
        recent[path]=now;
        if(recent.Count>4096)
            foreach(var item in recent.Where(k=>now-k.Value>120000).Take(2048))
                recent.TryRemove(item.Key,out _);
        try
        {
            if(!owner.Dispatcher.HasShutdownStarted)
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(Volatile.Read(ref stopped)==0 && File.Exists(path))
                        onNewFile(path);
                }));
        }
        catch(InvalidOperationException){}
    }

    private async Task ReconcileAsync(CancellationToken token)
    {
        // Event overflow and app-created replacement files can be missed.
        // Reconcile recent outputs in common save folders periodically;
        // never recursively crawl the entire C: volume from this service.
        // Reconcile the same approved roots being watched, including opted-in
        // custom Save As folders, without visiting whole disks or caches.
        var likely=roots.ToArray();
        bool first=true;
        while(!token.IsCancellationRequested)
        {
            try
            {
                // Catch app-saved files that completed just before DeskSpace
                // opened; never require opening Search or waiting 40 seconds.
                await Task.Delay(first?TimeSpan.FromSeconds(2):
                    TimeSpan.FromSeconds(45),token);
                var since=DateTime.UtcNow-TimeSpan.FromMinutes(first?4:3);
                first=false;
                foreach(var dir in likely)
                {
                    int count=0;
                    foreach(var path in Directory.EnumerateFiles(dir,"*",
                        new EnumerationOptions{RecurseSubdirectories=true,
                            IgnoreInaccessible=true}))
                    {
                        if(token.IsCancellationRequested || ++count>15000)break;
                        try
                        {
                            if(Eligible(path) && File.GetLastWriteTimeUtc(path)>=since)
                                Queue(path);
                        }catch(IOException){}catch(UnauthorizedAccessException){}
                    }
                }
            }
            catch(OperationCanceledException){break;}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }
    private static void WatchError(object sender,ErrorEventArgs e)
    {
        try
        {
            var folder=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),"DeskSpace");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder,"new-file-watch.log"),
                DateTime.UtcNow.ToString("O")+" "+
                e.GetException().GetType().Name+Environment.NewLine);
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }

    public void Dispose()
    {
        if(Interlocked.Exchange(ref stopped,1)!=0)return;
        shutdown.Cancel();
        foreach(var watcher in watchers)
        {
            watcher.EnableRaisingEvents=false;
            watcher.Created-=Created;
            watcher.Changed-=Changed;
            watcher.Deleted-=Deleted;
            watcher.Renamed-=Renamed;
            watcher.Error-=WatchError;
            watcher.Dispose();
        }
        watchers.Clear();
        // CTS intentionally not disposed while queued tasks can still check it.
    }
}

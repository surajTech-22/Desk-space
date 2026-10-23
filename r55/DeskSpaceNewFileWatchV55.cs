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
            Path.Combine(home,"Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Path.Combine(home,"OneDrive"),
            Path.Combine(home,"OneDrive - Personal")
        };
        // App Save As can target C:\Projects, D:\Accounts, etc., not just
        // Downloads. Fixed and removable drives are event-watched without
        // enumerating their contents; no network shares or system cache scan.
        foreach(var drive in DriveInfo.GetDrives())
        {
            try
            {
                if(drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable)
                    candidates.Add(drive.RootDirectory.FullName);
            }
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
        return candidates.Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p=>p.Length).ToArray();
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
    private static bool IsUserDocumentPath(string path)
    {
        if(string.IsNullOrWhiteSpace(path))return false;
        var p=path.Replace(Path.AltDirectorySeparatorChar,Path.DirectorySeparatorChar);
        foreach(var forbidden in new[]{
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        })
            if(!string.IsNullOrWhiteSpace(forbidden) && IsInside(p,forbidden))
                return false;
        // Don't surface application caches, Git internals, recycle bins,
        // OS metadata or Office temporary save artefacts.
        var separators=new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar};
        var parts=p.Split(separators,StringSplitOptions.RemoveEmptyEntries);
        if(parts.Any(x=>x.Equals(".git",StringComparison.OrdinalIgnoreCase)||
            x.Equals("$RECYCLE.BIN",StringComparison.OrdinalIgnoreCase)||
            x.Equals("System Volume Information",StringComparison.OrdinalIgnoreCase)||
            x.Equals("node_modules",StringComparison.OrdinalIgnoreCase)||
            x.Equals("WindowsApps",StringComparison.OrdinalIgnoreCase)))
            return false;
        var name=Path.GetFileName(p);
        return !(name.StartsWith("~$",StringComparison.Ordinal)||
            name.StartsWith("~",StringComparison.Ordinal)||
            name.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)||
            name.EndsWith(".temp",StringComparison.OrdinalIgnoreCase)||
            name.EndsWith(".crdownload",StringComparison.OrdinalIgnoreCase)||
            name.EndsWith(".part",StringComparison.OrdinalIgnoreCase)||
            name.EndsWith(".partial",StringComparison.OrdinalIgnoreCase)||
            name.EndsWith(".download",StringComparison.OrdinalIgnoreCase));
    }
    private bool Eligible(string path)
    {
        try
        {
            if((!explicitRoots&&!IsUserDocumentPath(path))||!File.Exists(path))return false;
            var flags=File.GetAttributes(path);
            return (flags&(FileAttributes.System|FileAttributes.Directory|
                FileAttributes.ReparsePoint))==0;
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
        long now=Environment.TickCount64;
        if(recent.TryGetValue(path,out long last) && now-last<9000)return;
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
        var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var likely=new[]{
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(home,"Downloads"),Path.Combine(home,"OneDrive")
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        while(!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(40),token);
                var since=DateTime.UtcNow-TimeSpan.FromMinutes(3);
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DeskSpace;

// Watch user document folders for newly created files, REGARDLESS of extension.
// This watches names only: no document content is read, indexed, uploaded, or
// copied merely because a file appears. The existing drag/drop shelf decides
// when the user explicitly wants to stage a durable copy.
internal sealed class DeskSpaceNewFileWatchV53 : IDisposable
{
    private readonly Window owner;
    private readonly Action<string> onNewFile;
    private readonly List<FileSystemWatcher> watchers=new();
    private readonly ConcurrentDictionary<string,long> recent=
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<string> roots;
    private int stopped;
    private bool started;

    public DeskSpaceNewFileWatchV53(Window owner,Action<string> onNewFile,
        IEnumerable<string>? watchRoots=null)
    {
        this.owner=owner;
        this.onNewFile=onNewFile;
        roots=(watchRoots??DefaultRoots()).Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static IReadOnlyList<string> DefaultRoots()
    {
        var home=Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        return new[]{
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(home,"Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };
    }

    public int WatchCount=>watchers.Count;

    public void Start()
    {
        if(started||Volatile.Read(ref stopped)!=0)return;
        started=true;
        foreach(var path in roots)
        {
            try
            {
                var watcher=new FileSystemWatcher(path)
                {
                    IncludeSubdirectories=true,
                    Filter="*", // All file types, including extensionless files
                    NotifyFilter=NotifyFilters.FileName|
                                 NotifyFilters.DirectoryName,
                    InternalBufferSize=16*1024
                };
                watcher.Created+=Created;
                watcher.Renamed+=Renamed;
                watcher.Error+=WatchError;
                watcher.EnableRaisingEvents=true;
                watchers.Add(watcher);
            }
            catch(IOException){}
            catch(UnauthorizedAccessException){}
            catch(ArgumentException){}
        }
    }

    private static bool IsNewUserFile(string path)
    {
        try
        {
            if(!File.Exists(path))return false;
            var attrs=File.GetAttributes(path);
            if((attrs&FileAttributes.ReparsePoint)!=0 ||
               (attrs&FileAttributes.Directory)!=0 ||
               (attrs&FileAttributes.System)!=0)return false;
            // Temporary Office/partial downloads are not ready to drag yet;
            // the final renamed filename is delivered by Renamed.
            var name=Path.GetFileName(path);
            if(name.StartsWith("~$",StringComparison.Ordinal) ||
               name.EndsWith(".crdownload",StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".part",StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }
        catch(IOException){return false;}
        catch(UnauthorizedAccessException){return false;}
    }

    private void Created(object sender,FileSystemEventArgs e)=>
        Queue(e.FullPath);
    private void Renamed(object sender,RenamedEventArgs e)=>
        Queue(e.FullPath);

    private void Queue(string path)
    {
        if(Volatile.Read(ref stopped)!=0)return;
        // Keep the watcher event handlers nonblocking. A download or Excel
        // Save As may not be fully visible on the initial Created event.
        _=Task.Run(async()=>{
            try
            {
                await Task.Delay(450);
                if(Volatile.Read(ref stopped)!=0)return;
                if(Directory.Exists(path))
                {
                    // Moving a whole folder may produce a single rename
                    // event. Bound enumeration to avoid blocking DeskSpace.
                    int count=0;
                    foreach(var file in Directory.EnumerateFiles(path,"*",
                        SearchOption.AllDirectories))
                    {
                        if(count++>=35)break;
                        Report(file);
                    }
                }
                else Report(path);
            }
            catch(IOException){}
            catch(UnauthorizedAccessException){}
            catch(OperationCanceledException){}
        });
    }

    private void Report(string path)
    {
        if(Volatile.Read(ref stopped)!=0||!IsNewUserFile(path))return;
        string full=Path.GetFullPath(path);
        long now=Environment.TickCount64;
        if(recent.TryGetValue(full,out long previous) && now-previous<7000)
            return;
        recent[full]=now;
        if(recent.Count>1024)recent.Clear();
        try
        {
            if(!owner.Dispatcher.HasShutdownStarted)
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(Volatile.Read(ref stopped)==0 && File.Exists(full))
                        onNewFile(full);
                }));
        }
        catch(InvalidOperationException){}
    }

    private static void WatchError(object sender,ErrorEventArgs e)
    {
        // A buffer overflow must not crash the widget. The next created
        // events are still best effort; diagnostics can trigger reconciliation.
        try
        {
            var folder=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),"DeskSpace");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder,"new-file-watch.log"),
                DateTime.UtcNow.ToString("O")+" "+
                e.GetException().GetType().Name+Environment.NewLine);
        }
        catch(IOException){}
        catch(UnauthorizedAccessException){}
    }

    public void Dispose()
    {
        if(Interlocked.Exchange(ref stopped,1)!=0)return;
        foreach(var watcher in watchers)
        {
            watcher.EnableRaisingEvents=false;
            watcher.Created-=Created;
            watcher.Renamed-=Renamed;
            watcher.Error-=WatchError;
            watcher.Dispose();
        }
        watchers.Clear();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeskSpace;

// OS change notifications for the existing full-drive file-name catalog.
// File names and paths remain local; no file contents are opened or uploaded.
// The old one-shot catalog missed all names created after startup.
internal static class DeskSpaceAllDriveLiveIndexV324
{
    private static readonly object Gate=new();
    private static readonly Dictionary<string,FileSystemWatcher> Watchers=
        new(StringComparer.OrdinalIgnoreCase);
    private static Action<string,bool>? changed;
    private static Action<string>? newVolume;
    private static Timer? volumeCheck;
    private static bool running;

    internal static void Start(Action<string,bool> onChange,Action<string> onNewVolume)
    {
        lock(Gate)
        {
            if(running)return;
            running=true;
            changed=onChange;
            newVolume=onNewVolume;
            // Watchers detect changes during or after the full catalog crawl.
            // The slow safety check only discovers drives connected later.
            volumeCheck=new Timer(_=>CheckVolumes(),null,
                TimeSpan.FromSeconds(1),TimeSpan.FromMinutes(2));
        }
    }

    // Native tests exercise the real watcher without scanning a developer PC.
    internal static void StartForTest(Action<string,bool> onChange)
    {
        lock(Gate){changed=onChange;newVolume=_=>{};running=true;}
    }

    internal static bool WatchRoot(string root,bool scanNewVolume)
    {
        if(string.IsNullOrWhiteSpace(root)||!Directory.Exists(root))return false;
        lock(Gate)
        {
            if(Watchers.ContainsKey(root))return true;
            try
            {
                var watcher=new FileSystemWatcher(root){
                    IncludeSubdirectories=true,
                    NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName,
                    InternalBufferSize=32*1024
                };
                watcher.Created+=(_,e)=>Record(e.FullPath,true);
                watcher.Deleted+=(_,e)=>Record(e.FullPath,false);
                watcher.Renamed+=(_,e)=>{
                    Record(e.OldFullPath,false);
                    Record(e.FullPath,true);
                };
                watcher.Error+=(_,_)=>{
                    // Buffer overflows are recoverable. Rebuild this volume
                    // rather than silently declaring the index complete.
                    QueueVolumeScan(root);
                };
                Watchers.Add(root,watcher);
                watcher.EnableRaisingEvents=true;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or ArgumentException or System.Security.SecurityException
                or System.ComponentModel.Win32Exception)
            {
                return false; // Full drive crawl and Windows Search still work.
            }
        }
        if(scanNewVolume)QueueVolumeScan(root);
        return true;
    }

    private static void CheckVolumes()
    {
        try
        {
            foreach(var drive in DriveInfo.GetDrives())
            {
                bool ready=false;
                string root="";
                try{ready=drive.IsReady;root=drive.RootDirectory.FullName;}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                    or System.Security.SecurityException){continue;}
                if(ready)WatchRoot(root,true);
            }
        }
        catch(Exception){}
    }

    private static void QueueVolumeScan(string root)
    {
        var action=newVolume;
        if(action!=null)_=Task.Run(()=>{try{action(root);}catch(Exception){}});
    }

    private static void Record(string path,bool exists)
    {
        if(string.IsNullOrWhiteSpace(path))return;
        try
        {
            changed?.Invoke(path,exists);
            if(exists && Directory.Exists(path))
                _=Task.Run(()=>ScanNewDirectory(path));
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or System.Security.SecurityException
            or NotSupportedException){}
    }

    private static void ScanNewDirectory(string root)
    {
        // A moved-in directory may bring thousands of files without a
        // Created notification for every child. Enumerate its names too.
        var pending=new Stack<string>();
        pending.Push(root);
        while(pending.Count!=0)
        {
            var directory=pending.Pop();
            string[] entries;
            try{entries=Directory.GetFileSystemEntries(directory);}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or ArgumentException or System.Security.SecurityException
                or NotSupportedException){continue;}
            foreach(var path in entries)
            {
                try
                {
                    var flags=File.GetAttributes(path);
                    if((flags&FileAttributes.Directory)!=0 &&
                       (flags&FileAttributes.ReparsePoint)==0)pending.Push(path);
                    changed?.Invoke(path,true);
                }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                    or ArgumentException or System.Security.SecurityException
                    or NotSupportedException){}
            }
        }
    }

    // Native regression injection; same path callback as production watcher.
    internal static void NotifyForTest(string path,bool exists)=>Record(path,exists);
}
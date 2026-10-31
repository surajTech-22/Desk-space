using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DeskSpace;

// Crash recovery for R164's atomic Deck copies. The lock is created ONLY
// inside our two-level GUID staging tree; the original source is never
// accessed or deleted. A lock held by an active process is never pruned.
internal static class DeskSpaceInterruptedCopyV165
{
    private const string MarkerName=".deskspace-copy-v165.lock";
    private const string Magic="DeskSpaceCopyV165|";
    private const int MaxScannedEntries=200000;

    internal sealed class Lease : IDisposable
    {
        private readonly FileStream handle;
        private readonly string marker;
        private bool disposed;

        internal Lease(FileStream handle,string marker)
        {
            this.handle=handle;
            this.marker=marker;
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            handle.Dispose();
            try{File.Delete(marker);}
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }

    // Call before copying; hold an exclusive file handle until the staged
    // result was atomically published (or cancellation cleaned its partial).
    internal static Lease Begin(string destination)
    {
        destination=Path.GetFullPath(destination);
        var item=Path.GetDirectoryName(destination)
            ??throw new IOException("Invalid temporary copy destination.");
        var session=Path.GetDirectoryName(item)
            ??throw new IOException("Invalid Desk staging session.");
        // Earlier DeskSpace staging versions can use an app-owned session
        // folder whose basename is not a GUID. The parent directory is
        // private to DeskSpace regardless of that historical name. Verify
        // ownership, not a guessed count of GUID directory components.
        var shelf=Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","TemporaryShelf"));
        var normalLayout=IsGuid(Path.GetFileName(item))&&
            IsGuid(Path.GetFileName(session));
        var ownedLayout=item.StartsWith(
            shelf+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        if(!normalLayout&&!ownedLayout)
            throw new IOException("Refusing a non-DeskSpace temporary copy destination.");
        Directory.CreateDirectory(item);
        string marker=Path.Combine(item,MarkerName);
        var handle=new FileStream(marker,FileMode.CreateNew,FileAccess.ReadWrite,
            FileShare.None,4096,FileOptions.WriteThrough);
        try
        {
            // Store only the staged item's basename, never a source path.
            var basename=Path.GetFileName(destination);
            var bytes=Encoding.UTF8.GetBytes(Magic+
                Convert.ToBase64String(Encoding.UTF8.GetBytes(basename)));
            handle.Write(bytes);
            handle.Flush(flushToDisk:true);
            return new Lease(handle,marker);
        }
        catch
        {
            handle.Dispose();
            try{File.Delete(marker);}
            catch(IOException){}catch(UnauthorizedAccessException){}
            throw;
        }
    }

    private static bool IsGuid(string name)=>
        Guid.TryParseExact(name,"N",out _);

    private static string? ReadTargetName(string marker)
    {
        using var lockFile=new FileStream(marker,FileMode.Open,
            FileAccess.Read,FileShare.None);
        if(lockFile.Length>1024||lockFile.Length<Magic.Length+1)return null;
        using var reader=new StreamReader(lockFile,Encoding.UTF8,
            detectEncodingFromByteOrderMarks:false,leaveOpen:true);
        var text=reader.ReadToEnd();
        if(!text.StartsWith(Magic,StringComparison.Ordinal))return null;
        var name=Encoding.UTF8.GetString(
            Convert.FromBase64String(text.Substring(Magic.Length)));
        if(name.Length==0||name=="."||name==".."||
            Path.GetFileName(name)!=name)return null;
        return name;
    }

    private static bool IsEntirelyStale(string path,DateTime threshold)
    {
        if(!File.Exists(path)&&!Directory.Exists(path))return true;
        var pending=new Stack<string>();
        pending.Push(path);
        int examined=0;
        while(pending.Count>0)
        {
            var entry=pending.Pop();
            if(++examined>MaxScannedEntries)return false;
            if((File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0)
                return false;
            if(File.GetLastWriteTimeUtc(entry)>threshold)return false;
            if(Directory.Exists(entry))
            {
                foreach(var child in Directory.EnumerateFileSystemEntries(entry))
                    pending.Push(child);
            }
        }
        return true;
    }

    // Run on startup, asynchronously. It only inspects the app's own
    // TemporaryShelf/{session GUID}/{item GUID}/ marker files. It never
    // removes source files, completed held items, arbitrary files, or
    // currently active copies; active copies own an exclusive marker lock.
    internal static int PruneAbandoned(string stagingRoot,TimeSpan grace)
    {
        if(grace<TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(grace));
        if(!Directory.Exists(stagingRoot))return 0;
        var threshold=DateTime.UtcNow-grace;
        int cleared=0;
        foreach(var session in Directory.EnumerateDirectories(stagingRoot))
        {
            try
            {
                if(!IsGuid(Path.GetFileName(session))||
                    (File.GetAttributes(session)&FileAttributes.ReparsePoint)!=0)
                    continue;
                foreach(var item in Directory.EnumerateDirectories(session))
                {
                    try
                    {
                        if(!IsGuid(Path.GetFileName(item))||
                            (File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)
                            continue;
                        var marker=Path.Combine(item,MarkerName);
                        if(!File.Exists(marker)||
                            File.GetLastWriteTimeUtc(marker)>threshold)
                            continue;
                        // Exclusive read fails if another DeskSpace process
                        // is still writing this copy.
                        string? filename;
                        try{filename=ReadTargetName(marker);}
                        catch(IOException){continue;}
                        if(filename==null)continue;
                        var completed=Path.Combine(item,filename);
                        var partial=completed+".partial";
                        // Only the known temporary path may be deleted.
                        if(!IsEntirelyStale(partial,threshold))continue;
                        if(File.Exists(partial))File.Delete(partial);
                        else if(Directory.Exists(partial))
                            Directory.Delete(partial,true);
                        // Leave 'completed' untouched even if the app crashed
                        // after rename but before releasing its lock.
                        File.Delete(marker);
                        cleared++;
                    }
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                        or ArgumentException or FormatException or NotSupportedException)
                    {
                        // Never interrupt startup for a locked or malformed
                        // staging item; recover conservatively next launch.
                    }
                }
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException)
            {
                // A session may be removed concurrently by another process.
            }
        }
        return cleared;
    }
}

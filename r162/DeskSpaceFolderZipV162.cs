using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace DeskSpace;

// Streaming, cancellable folder ZIP writer. The source is always read-only.
// A *.partial ZIP is never put into the Deck; it is deleted on failure/cancel.
// Junctions and symlinks are skipped rather than traversed out of the folder.
internal static class DeskSpaceFolderZipV162
{
    internal static void Create(string source,string destination,
        CancellationToken cancellation,Action<int>? onFileCompleted=null)
    {
        source=Path.GetFullPath(source);
        cancellation.ThrowIfCancellationRequested();
        if(!Directory.Exists(source))throw new DirectoryNotFoundException(
            "Folder is unavailable.");
        if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)
            throw new IOException("Refusing a linked folder.");

        var directory=Path.GetDirectoryName(destination)
            ?? throw new IOException("Invalid ZIP target directory.");
        Directory.CreateDirectory(directory);
        var partial=destination+".partial";
        if(File.Exists(destination)||File.Exists(partial))
            throw new IOException("ZIP target already exists.");
        int copied=0;
        var buffer=new byte[128*1024];
        try
        {
            using(var output=new FileStream(partial,FileMode.CreateNew,
                FileAccess.Write,FileShare.None))
            using(var zip=new ZipArchive(output,ZipArchiveMode.Create,false))
            {
                var root=DeskSpaceFolderZipV159.FolderName(source)+"/";
                var pending=new Stack<(string Path,string Prefix)>();
                pending.Push((source,root));
                while(pending.Count>0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var (folder,prefix)=pending.Pop();
                    zip.CreateEntry(prefix,CompressionLevel.NoCompression);
                    foreach(var path in Directory.EnumerateFiles(folder))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
                            continue;
                        var entry=zip.CreateEntry(
                            prefix+Path.GetFileName(path),CompressionLevel.Optimal);
                        using(var input=new FileStream(path,FileMode.Open,
                            FileAccess.Read,FileShare.Read,buffer.Length))
                        using(var stream=entry.Open())
                        {
                            int count;
                            while((count=input.Read(buffer,0,buffer.Length))>0)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                stream.Write(buffer,0,count);
                            }
                        }
                        cancellation.ThrowIfCancellationRequested();
                        checked{copied++;}
                        onFileCompleted?.Invoke(copied);
                    }
                    foreach(var child in Directory.EnumerateDirectories(folder))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)
                            continue;
                        pending.Push((child,prefix+Path.GetFileName(child)+"/"));
                    }
                }
            }
            cancellation.ThrowIfCancellationRequested();
            using(var check=ZipFile.OpenRead(partial))
            {
                if(check.Entries.Count<1 || !string.Equals(
                    check.Entries[0].FullName,
                    DeskSpaceFolderZipV159.FolderName(source)+"/",
                    StringComparison.Ordinal))
                    throw new InvalidDataException("ZIP verification failed.");
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(partial,destination);
        }
        catch
        {
            try{if(File.Exists(partial))File.Delete(partial);}
            catch(IOException){}catch(UnauthorizedAccessException){}
            throw;
        }
    }

    // Only app-created GUID temp directories with ZIP files, and only when
    // older than the specified grace period, can be removed. Never delete
    // unknown files, links, user documents or an active ZIP.
    internal static int PruneAbandoned(string appTempRoot,TimeSpan grace)
    {
        if(grace<TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(grace));
        if(!Directory.Exists(appTempRoot))return 0;
        int cleared=0;
        foreach(var folder in Directory.EnumerateDirectories(appTempRoot))
        {
            try
            {
                var name=Path.GetFileName(folder);
                if(!Guid.TryParseExact(name,"N",out _))continue;
                if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)
                    continue;
                if(DateTime.UtcNow-Directory.GetLastWriteTimeUtc(folder)<grace)
                    continue;
                if(Directory.EnumerateDirectories(folder).Any())continue;
                var paths=Directory.EnumerateFiles(folder).ToArray();
                if(paths.Length==0)continue;
                if(paths.Any(p=>
                    (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0 ||
                    !(p.EndsWith(".zip",StringComparison.OrdinalIgnoreCase) ||
                      p.EndsWith(".zip.partial",StringComparison.OrdinalIgnoreCase))))
                    continue;
                // Recheck individual files: an active operation refreshes their
                // last-write timestamps while it writes chunks.
                if(paths.Any(p=>DateTime.UtcNow-File.GetLastWriteTimeUtc(p)<grace))
                    continue;
                foreach(var path in paths)File.Delete(path);
                Directory.Delete(folder);
                cleared++;
            }
            catch(Exception error) when(error is IOException
                or UnauthorizedAccessException or ArgumentException)
            {
                // Skip locks and races; cleanup is best-effort.
            }
        }
        return cleared;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace DeskSpace;

// R206: explicit user-initiated ZIP with progress/cancel and strict resource
// budgets. No source modification, no traversal of reparse points/junctions,
// and no finished Deck item unless the ZIP central directory verifies.
internal static class DeskSpaceFolderZipV206
{
    internal sealed record Progress(long CompletedBytes,long MaxBytes,
        int Files,string CurrentName);
    internal const int MaxEntries=4096;
    internal const long MaxTotalBytes=1024L*1024*1024;
    internal const long MaxFileBytes=512L*1024*1024;
    private const int BufferSize=128*1024;

    internal static void Create(string folder,string destination,
        IProgress<Progress>? progress,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        string root=Path.GetFullPath(folder);
        string output=Path.GetFullPath(destination);
        string prefix=root.TrimEnd(Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!Directory.Exists(root))throw new DirectoryNotFoundException(
            "Source directory is not available.");
        if(!output.EndsWith(".zip",StringComparison.OrdinalIgnoreCase) ||
           output.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("ZIP must be outside the source directory.");
        if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)
            throw new InvalidDataException("Symbolic link and junction roots are not allowed.");
        if(File.Exists(output) || File.Exists(output+".partial"))
            throw new IOException("Archive destination already exists.");

        // Enumerate only local, non-link descendants with finite budgets.
        var directories=new List<(string Full,string Relative)>();
        var files=new List<(string Full,string Relative,long Size)>();
        var pending=new Stack<(string Full,string Relative,int Depth)>();
        pending.Push((root,DeskSpaceFolderZipV159.FolderName(root)+"/",0));
        long totalBytes=0;
        int scanned=0;
        while(pending.Count>0)
        {
            cancel.ThrowIfCancellationRequested();
            var current=pending.Pop();
            if(current.Depth>24)throw new InvalidDataException(
                "Folder nesting exceeds safe ZIP limit.");
            if(++scanned>MaxEntries)throw new InvalidDataException(
                "Folder has more than 4,096 entries.");
            if((File.GetAttributes(current.Full)&FileAttributes.ReparsePoint)!=0)
                continue;
            directories.Add((current.Full,current.Relative));
            foreach(var path in Directory.EnumerateFiles(current.Full))
            {
                cancel.ThrowIfCancellationRequested();
                if(++scanned>MaxEntries)throw new InvalidDataException(
                    "Folder has more than 4,096 entries.");
                var info=new FileInfo(path);
                if((info.Attributes&FileAttributes.ReparsePoint)!=0)continue;
                if(info.Length<0 || info.Length>MaxFileBytes ||
                   totalBytes>MaxTotalBytes-info.Length)
                    throw new InvalidDataException(
                        "Folder exceeds the 1 GiB ZIP input budget.");
                totalBytes+=info.Length;
                files.Add((path,current.Relative+info.Name,info.Length));
            }
            foreach(var path in Directory.EnumerateDirectories(current.Full))
            {
                cancel.ThrowIfCancellationRequested();
                var info=new DirectoryInfo(path);
                if((info.Attributes&FileAttributes.ReparsePoint)!=0)continue;
                pending.Push((path,current.Relative+info.Name+"/",current.Depth+1));
            }
        }
        string parent=Path.GetDirectoryName(output) ??
            throw new InvalidDataException("ZIP destination parent missing.");
        Directory.CreateDirectory(parent);
        string partial=output+".partial";
        long copied=0;
        int finished=0;
        try
        {
            using(var target=new FileStream(partial,FileMode.CreateNew,
                FileAccess.Write,FileShare.None,BufferSize,FileOptions.SequentialScan))
            using(var archive=new ZipArchive(target,ZipArchiveMode.Create,false))
            {
                foreach(var directory in directories)
                {
                    cancel.ThrowIfCancellationRequested();
                    archive.CreateEntry(directory.Relative,CompressionLevel.NoCompression);
                }
                byte[] buffer=new byte[BufferSize];
                foreach(var file in files)
                {
                    cancel.ThrowIfCancellationRequested();
                    // Recheck file length after enumeration. A growing or
                    // replaced source must fail rather than escape the budget.
                    var info=new FileInfo(file.Full);
                    if((info.Attributes&FileAttributes.ReparsePoint)!=0 ||
                       info.Length!=file.Size)
                        throw new IOException("Source changed while preparing ZIP.");
                    var entry=archive.CreateEntry(file.Relative,CompressionLevel.Optimal);
                    using var input=new FileStream(file.Full,FileMode.Open,
                        FileAccess.Read,FileShare.Read,BufferSize,
                        FileOptions.SequentialScan);
                    using var destinationStream=entry.Open();
                    long currentBytes=0;
                    long lastReported=0;
                    int n;
                    while((n=input.Read(buffer,0,buffer.Length))>0)
                    {
                        cancel.ThrowIfCancellationRequested();
                        currentBytes+=n;
                        if(currentBytes>file.Size || copied>MaxTotalBytes-n)
                            throw new IOException("Source changed while creating ZIP.");
                        destinationStream.Write(buffer,0,n);
                        copied+=n;
                        if(currentBytes-lastReported>=4*1024*1024)
                        {
                            progress?.Report(new Progress(copied,totalBytes,
                                finished,info.Name));
                            lastReported=currentBytes;
                        }
                    }
                    if(currentBytes!=file.Size)
                        throw new IOException("Source length changed during ZIP.");
                    finished++;
                    progress?.Report(new Progress(copied,totalBytes,finished,info.Name));
                }
            }
            cancel.ThrowIfCancellationRequested();
            using(var verify=ZipFile.OpenRead(partial))
            {
                if(verify.Entries.Count!=directories.Count+files.Count ||
                   verify.Entries.Count==0 ||
                   verify.Entries[0].FullName!=directories[0].Relative)
                    throw new InvalidDataException("ZIP central directory validation failed.");
            }
            cancel.ThrowIfCancellationRequested();
            File.Move(partial,output,false);
            progress?.Report(new Progress(copied,totalBytes,finished,"Complete"));
        }
        finally
        {
            try{if(File.Exists(partial))File.Delete(partial);}
            catch(IOException){} catch(UnauthorizedAccessException){}
        }
    }
}

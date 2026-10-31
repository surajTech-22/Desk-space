using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace DeskSpace;

// Stages files and directory trees into a GUID-owned Deck location.
// The input is read-only. Only a sibling *.partial staging path is removed
// after a failed/cancelled copy. The completed destination becomes visible
// to the shared Deck only after an atomic same-volume rename.
internal static class DeskSpaceFolderCopyV164
{
    private const int BufferSize=128*1024;
    private const int MaxFiles=200000;
    private const long ReportInterval=1024*1024;

    // Progress: bytesWritten, estimatedTotalBytes, filesWritten, estimatedTotalFiles.
    internal static void Copy(string source,string destination,
        CancellationToken cancellation,
        Action<long,long,int,int>? onProgress=null)
    {
        source=Path.GetFullPath(source);
        destination=Path.GetFullPath(destination);
        cancellation.ThrowIfCancellationRequested();
        bool folder=Directory.Exists(source);
        if(!folder&&!File.Exists(source))
            throw new FileNotFoundException("Copy source is unavailable.");
        if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)
            throw new IOException("Refusing to copy a linked source.");
        if(string.Equals(source,destination,StringComparison.OrdinalIgnoreCase)||
            (folder&&destination.StartsWith(
                Path.TrimEndingDirectorySeparator(source)+Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Copy destination must be outside the source.");

        string partial=destination+".partial";
        if(File.Exists(partial)||Directory.Exists(partial)||
            File.Exists(destination)||Directory.Exists(destination))
            throw new IOException("A staged copy already exists.");
        var parent=Path.GetDirectoryName(destination)
            ??throw new IOException("Invalid Deck destination.");
        Directory.CreateDirectory(parent);
        var (totalFiles,totalBytes)=Estimate(source,folder,cancellation);
        long written=0,lastReport=0;
        int completed=0;
        onProgress?.Invoke(0,totalBytes,0,totalFiles);
        var buffer=new byte[BufferSize];

        void TransferFile(string inputPath,string outputPath)
        {
            cancellation.ThrowIfCancellationRequested();
            var original=new FileInfo(inputPath);
            if((original.Attributes&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Copy source changed to a linked file.");
            long expectedLength=original.Length;
            DateTime expectedModified=original.LastWriteTimeUtc;
            long copiedFile=0;
            using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using(var input=new FileStream(inputPath,FileMode.Open,
                FileAccess.Read,FileShare.Read,BufferSize))
            using(var output=new FileStream(outputPath,FileMode.CreateNew,
                FileAccess.Write,FileShare.None,BufferSize))
            {
                int n;
                while((n=input.Read(buffer,0,buffer.Length))!=0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if(copiedFile>expectedLength-n)
                        throw new IOException("Source file grew while staging.");
                    output.Write(buffer,0,n);
                    copiedFile+=n;
                    digest.AppendData(buffer,0,n);
                    checked{written+=n;}
                    if(written-lastReport>=ReportInterval)
                    {
                        lastReport=written;
                        onProgress?.Invoke(written,totalBytes,completed,totalFiles);
                    }
                }
            }
            cancellation.ThrowIfCancellationRequested();
            original.Refresh();
            if(copiedFile!=expectedLength || original.Length!=expectedLength ||
                original.LastWriteTimeUtc!=expectedModified)
                throw new IOException("Source changed while staging a Deck file.");
            DeskSpaceCopyIntegrityV212.VerifyWritten(outputPath,copiedFile,
                digest.GetHashAndReset(),cancellation);
            cancellation.ThrowIfCancellationRequested();
            checked{completed++;}
            // Large folders can contain tens of thousands of tiny files.
            // Throttle dispatcher updates while preserving cancellation checks.
            if(completed<=3||completed%25==0||completed==totalFiles)
                onProgress?.Invoke(written,totalBytes,completed,totalFiles);
        }

        try
        {
            if(!folder)
                TransferFile(source,partial);
            else
            {
                Directory.CreateDirectory(partial);
                var pending=new Stack<(string Input,string Output)>();
                pending.Push((source,partial));
                while(pending.Count>0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var (input,output)=pending.Pop();
                    foreach(var file in Directory.EnumerateFiles(input))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)
                            continue;
                        TransferFile(file,Path.Combine(output,Path.GetFileName(file)));
                    }
                    foreach(var child in Directory.EnumerateDirectories(input))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)
                            continue;
                        var next=Path.Combine(output,Path.GetFileName(child));
                        Directory.CreateDirectory(next); // preserve empty folders
                        pending.Push((child,next));
                    }
                }
            }
            cancellation.ThrowIfCancellationRequested();
            if(written!=totalBytes || completed!=totalFiles)
                throw new IOException("Folder contents changed while staging.");
            onProgress?.Invoke(written,totalBytes,completed,totalFiles);
            cancellation.ThrowIfCancellationRequested();
            if(folder)Directory.Move(partial,destination);
            else File.Move(partial,destination);
        }
        catch
        {
            // Never touch source, its parent, other Deck items or user files.
            // The *.partial path is known to have been created exclusively
            // for this copy operation and has never been exposed to the Deck.
            try
            {
                if(File.Exists(partial))File.Delete(partial);
                else if(Directory.Exists(partial))Directory.Delete(partial,true);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
            throw;
        }
    }

    private static (int Files,long Bytes) Estimate(string source,bool folder,
        CancellationToken cancellation)
    {
        int files=0;
        long total=0;
        if(!folder)return (1,new FileInfo(source).Length);
        var pending=new Stack<string>();
        pending.Push(source);
        while(pending.Count>0)
        {
            cancellation.ThrowIfCancellationRequested();
            var dir=pending.Pop();
            foreach(var file in Directory.EnumerateFiles(dir))
            {
                cancellation.ThrowIfCancellationRequested();
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)
                    continue;
                checked{files++;total+=new FileInfo(file).Length;}
                if(files>MaxFiles)
                    throw new IOException("Folder exceeds the 200,000 file staging limit.");
            }
            foreach(var child in Directory.EnumerateDirectories(dir))
            {
                cancellation.ThrowIfCancellationRequested();
                if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)
                    pending.Push(child);
            }
        }
        return (files,total);
    }
}

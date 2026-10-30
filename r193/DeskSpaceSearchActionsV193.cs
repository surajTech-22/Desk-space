using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace DeskSpace;

// R193: explicit, keyboard-accessible Search actions. Never inspect or zip
// an entire directory automatically. ZIP is user-initiated, bounded and
// written atomically to the user's chosen destination.
internal static class DeskSpaceSearchActionsV193
{
    internal const long MaxSingleFileBytes=128L*1024*1024;

    internal static int NextIndex(int count,int current,int movement)
    {
        if(count<=0)return -1;
        return Math.Clamp(current+movement,0,count-1);
    }

    internal static bool CanZip(string? source)
    {
        if(string.IsNullOrWhiteSpace(source))return false;
        try
        {
            var file=new FileInfo(source);
            return file.Exists && file.Length<=MaxSingleFileBytes &&
                   file.Length>=0 &&
                   (file.Attributes&FileAttributes.ReparsePoint)==0 &&
                   !Path.GetExtension(file.Name).Equals(".zip",
                       StringComparison.OrdinalIgnoreCase);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {return false;}
    }

    internal static void ZipSingleFile(string source,string destination,
        CancellationToken cancellationToken=default)
    {
        if(!CanZip(source))throw new InvalidDataException(
            "Only ordinary files up to 128 MB can be archived from Search.");
        string input=Path.GetFullPath(source);
        string output=Path.GetFullPath(destination);
        if(!output.EndsWith(".zip",StringComparison.OrdinalIgnoreCase) ||
           string.Equals(input,output,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("ZIP destination must be a separate .zip file.");
        if(File.Exists(output))throw new IOException("Destination already exists.");
        var folder=Path.GetDirectoryName(output) ??
            throw new InvalidDataException("Invalid ZIP output folder.");
        if(!Directory.Exists(folder))throw new DirectoryNotFoundException(folder);
        cancellationToken.ThrowIfCancellationRequested();
        string temp=Path.Combine(folder,".deskspace-"+
            Guid.NewGuid().ToString("N")+".partial");
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,
                FileShare.None,81920,FileOptions.SequentialScan))
            using(var archive=new ZipArchive(stream,ZipArchiveMode.Create,true))
            {
                var entry=archive.CreateEntry(Path.GetFileName(input),
                    CompressionLevel.Optimal);
                using var target=entry.Open();
                using var original=new FileStream(input,FileMode.Open,
                    FileAccess.Read,FileShare.Read,81920,
                    FileOptions.SequentialScan);
                byte[] chunk=new byte[81920];
                int count;
                long copied=0;
                while((count=original.Read(chunk,0,chunk.Length))>0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    copied+=count;
                    if(copied>MaxSingleFileBytes)
                        throw new InvalidDataException("Source changed during ZIP.");
                    target.Write(chunk,0,count);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            // Move(false): never silently overwrite files in Downloads.
            File.Move(temp,output,false);
        }
        finally
        {
            try{if(File.Exists(temp))File.Delete(temp);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }
}

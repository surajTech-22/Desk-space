using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DeskSpace;

// R222: stream official updater installer into a same-directory temporary
// file and atomically promote it only after the transfer completes.
// Existing verified cached installer is NEVER truncated mid-download.
// Real SHA-256 and PE verification still run after this staging step.
internal static class DeskSpaceAtomicInstallerV222
{
    internal const long MaxInstallerBytes=250L*1024*1024;
    internal const long MinInstallerBytes=5_000_000;
    private const int Chunk=128*1024;

    internal static void CleanupAbandoned(string directory,DateTime nowUtc)
    {
        if(!Directory.Exists(directory))return;
        foreach(var path in Directory.EnumerateFiles(directory,
            "DeskSpace_Setup.exe.*.partial",SearchOption.TopDirectoryOnly))
        {
            try
            {
                var name=Path.GetFileName(path);
                if(!Regex.IsMatch(name,
                    @"\ADeskSpace_Setup\.exe\.[0-9a-f]{32}\.partial\z",
                    RegexOptions.CultureInvariant|RegexOptions.IgnoreCase))
                    continue;
                var info=new FileInfo(path);
                if((info.Attributes&FileAttributes.ReparsePoint)!=0 ||
                   nowUtc-info.LastWriteTimeUtc<TimeSpan.FromHours(12))
                    continue;
                File.Delete(path);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
            {
                // An active downloader may still own this file. Never
                // terminate another process or delete an unrelated item.
            }
        }
    }

    internal static async Task StageFromStreamAsync(Stream source,
        string destination,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(source==null||!source.CanRead)
            throw new InvalidDataException("Installer response stream unreadable.");
        var final=Path.GetFullPath(destination);
        if(!Path.GetFileName(final).Equals("DeskSpace_Setup.exe",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unexpected updater installer destination.");
        var directory=Path.GetDirectoryName(final)!;
        if(!Directory.Exists(directory) ||
           (new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)
            throw new InvalidDataException("Installer staging directory missing or linked.");
        if(File.Exists(final) &&
           (File.GetAttributes(final)&FileAttributes.ReparsePoint)!=0)
            throw new InvalidDataException("Installer cached target cannot be a link.");
        CleanupAbandoned(directory,DateTime.UtcNow);
        var partial=Path.Combine(directory,
            "DeskSpace_Setup.exe."+Guid.NewGuid().ToString("N")+".partial");
        try
        {
            await using(var output=new FileStream(partial,FileMode.CreateNew,
                FileAccess.Write,FileShare.None,Chunk,
                FileOptions.Asynchronous|FileOptions.SequentialScan))
            {
                var buffer=new byte[Chunk];
                long length=0;
                while(true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int read=await source.ReadAsync(buffer.AsMemory(),cancellation);
                    if(read==0)break;
                    if(length>MaxInstallerBytes-read)
                        throw new InvalidDataException("Oversized installer.");
                    length+=read;
                    await output.WriteAsync(buffer.AsMemory(0,read),cancellation);
                }
                cancellation.ThrowIfCancellationRequested();
                await output.FlushAsync(cancellation);
                if(length<MinInstallerBytes)
                    throw new InvalidDataException("Truncated installer.");
            }
            cancellation.ThrowIfCancellationRequested();
            if(File.Exists(final) &&
                (File.GetAttributes(final)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("Cached installer changed to a link.");
            File.Move(partial,final,true);
        }
        finally
        {
            try{if(File.Exists(partial))File.Delete(partial);}
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}

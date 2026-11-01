using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeskSpace;

// Recent-file dismissals survive a restart, but only for THAT version of
// the original file. A later save of the same document is shown again.
// Store hashes, not a list of potentially sensitive document paths.
internal static class DeskSpaceRecentFileStateV99
{
    private static readonly object Gate=new();
    private static readonly string FileName=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","RecentDismissals.json");
    private static Dictionary<string,long>? dismissed;
    private const int MaxEntries=1024;
    private static readonly long Lifetime=TimeSpan.FromDays(21).Ticks;

    internal static string? Fingerprint(string path)
    {
        try
        {
            var info=new FileInfo(path);
            if(!info.Exists)return null;
            var key=Path.GetFullPath(path).ToUpperInvariant()+"|"+
                info.Length+"|"+info.LastWriteTimeUtc.Ticks;
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException) {return null;}
    }

    private static Dictionary<string,long> Load()
    {
        if(dismissed!=null)return dismissed;
        try
        {
            dismissed=File.Exists(FileName)?
                JsonSerializer.Deserialize<Dictionary<string,long>>(
                    File.ReadAllText(FileName))??new():
                new();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or JsonException) {dismissed=new();}
        Prune();
        return dismissed;
    }

    private static void Prune()
    {
        if(dismissed==null)return;
        long earliest=DateTime.UtcNow.Ticks-Lifetime;
        foreach(var key in dismissed.Where(x=>x.Value<earliest).Select(x=>x.Key).ToArray())
            dismissed.Remove(key);
        foreach(var key in dismissed.OrderByDescending(x=>x.Value).
            Skip(MaxEntries).Select(x=>x.Key).ToArray())
            dismissed.Remove(key);
    }

    internal static bool IsDismissed(string path)
    {
        var id=Fingerprint(path);
        if(id==null)return false;
        lock(Gate)return Load().ContainsKey(id);
    }

    internal static void Dismiss(string path)=>DismissMany(new[]{path});

    internal static void DismissMany(IEnumerable<string> paths)
    {
        var keys=paths.Select(Fingerprint).Where(x=>x!=null).ToArray();
        if(keys.Length==0)return;
        lock(Gate)
        {
            var state=Load();
            long now=DateTime.UtcNow.Ticks;
            foreach(var key in keys)state[key!]=now;
            Prune();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
                var tmp=FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    File.WriteAllText(tmp,JsonSerializer.Serialize(state));
                    File.Move(tmp,FileName,true);
                }
                finally
                {
                    try{if(File.Exists(tmp))File.Delete(tmp);}
                    catch(IOException){}
                    catch(UnauthorizedAccessException){}
                }
            }
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }

    // Used by the pre-release Windows smoke test.
    internal static bool DifferentVersionIsNew(string path,long newSize,long newTicks)
    {
        var id=Fingerprint(path);
        if(id==null)return false;
        var other=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(path).ToUpperInvariant()+"|"+newSize+"|"+newTicks)));
        return !string.Equals(id,other,StringComparison.Ordinal);
    }
}

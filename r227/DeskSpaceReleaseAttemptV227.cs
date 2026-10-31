using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace DeskSpace;

// R227: ntfy's one-hour cached replay must not relaunch the supervisor
// every time a widget restarts. All entries contain only a release tag/time.
internal static class DeskSpaceReleaseAttemptV227
{
    private sealed record Attempt(string Tag,DateTime Utc);
    internal static readonly TimeSpan ReplayWindow=TimeSpan.FromMinutes(8);
    private static readonly string Marker=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","Updates","release-hint-last-r227.json");

    internal static bool WasRecentlyAttempted(string previousTag,
        DateTime previousUtc,string currentTag,DateTime nowUtc)=>
        string.Equals(previousTag,currentTag,StringComparison.Ordinal) &&
        nowUtc>=previousUtc && nowUtc-previousUtc<ReplayWindow;

    internal static bool ShouldDispatch(string tag)
    {
        if(!Regex.IsMatch(tag,@"^(?:tester-)?v\d+\.\d+\.\d+$",
            RegexOptions.CultureInvariant))return false;
        using var gate=new Mutex(false,@"Local\DeskSpace-ReleaseHint-R227");
        bool acquired=false;
        try
        {
            try{acquired=gate.WaitOne(0);}
            catch(AbandonedMutexException){acquired=true;}
            if(!acquired)return false;
            var now=DateTime.UtcNow;
            if(File.Exists(Marker) && new FileInfo(Marker).Length<=512)
            {
                try
                {
                    var previous=JsonSerializer.Deserialize<Attempt>(
                        File.ReadAllText(Marker,Encoding.UTF8));
                    if(previous!=null && WasRecentlyAttempted(
                        previous.Tag,previous.Utc,tag,now))return false;
                }
                catch(Exception e) when(e is IOException or UnauthorizedAccessException
                    or JsonException or ArgumentException){return false;}
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Marker)!);
            var tmp=Marker+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(tmp,JsonSerializer.Serialize(
                    new Attempt(tag,now)),new UTF8Encoding(false));
                File.Move(tmp,Marker,true);
            }
            finally
            {
                try{if(File.Exists(tmp))File.Delete(tmp);}
                catch(IOException){}catch(UnauthorizedAccessException){}
            }
            return true;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException
            or System.Security.SecurityException){return false;}
        finally{if(acquired)gate.ReleaseMutex();}
    }
}

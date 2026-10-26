using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DeskSpace;

// R133: GitHub publishing sends a best-effort event to a lightweight relay.
// Messages are HINTS only: no URL, installer, checksum, command, or version is
// trusted from the relay. The existing GitHub + SHA256 updater decides whether
// to install. This subscriber does not poll the GitHub API.
internal static class DeskSpaceReleasePushV133
{
    private const string Topic="deskspace-verified-release-r133-20261026-9a6c412f";
    private static readonly Uri Endpoint=new(
        "https://ntfy.sh/"+Topic+"/json?since=latest");
    private static readonly HttpClient StreamHttp=new()
    {
        Timeout=System.Threading.Timeout.InfiniteTimeSpan
    };
    private static bool started;
    private static string lastAttemptedTag="";
    private static DateTime lastAttemptUtc=DateTime.MinValue;

    private static void Log(string text)
    {
        try
        {
            string local=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),"DeskSpace");
            Directory.CreateDirectory(local);
            File.AppendAllText(Path.Combine(local,"update-status.log"),
                DateTime.UtcNow.ToString("O")+" PUSH "+text+Environment.NewLine);
        }
        catch(IOException){} catch(UnauthorizedAccessException){}
    }

    public static void Start(Window owner)
    {
        if(started)return;
        started=true;
        _=ListenAsync(owner);
    }

    // Parse a service event without side effects. A forged notification never
    // supplies an executable or download URL. Release validation occurs ONLY
    // in DeskSpacePublicUpdaterV44.CheckAsync against canonical GitHub.
    internal static bool TryReleaseEvent(string raw,string selectedChannel,
        out string tag)
    {
        tag="";
        try
        {
            using var doc=JsonDocument.Parse(raw);
            var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object ||
               !root.TryGetProperty("event",out var type) ||
               type.GetString()!="message" ||
               !root.TryGetProperty("topic",out var topic) ||
               topic.GetString()!=Topic ||
               !root.TryGetProperty("message",out var payload))
                return false;
            string message=payload.GetString()??"";
            string prefix=selectedChannel=="tester"?"tester|":"stable|";
            if(!message.StartsWith(prefix,StringComparison.Ordinal))return false;
            string requested=message.Substring(prefix.Length);
            string pattern=selectedChannel=="tester"
                ?@"^tester-v\d+\.\d+\.\d+$":@"^v\d+\.\d+\.\d+$";
            if(!Regex.IsMatch(requested,pattern))return false;
            string version=requested.Substring(
                selectedChannel=="tester"?8:1);
            if(!Version.TryParse(version,out var announced) ||
               !Version.TryParse(DeskSpaceUpdater.CurrentVersion,
                   out var installed) || announced<=installed)return false;
            tag=requested;
            return true;
        }
        catch(JsonException){return false;}
        catch(InvalidOperationException){return false;}
    }

    private static void Dispatch(Window owner,string tag)
    {
        var now=DateTime.UtcNow;
        if(lastAttemptedTag==tag &&
           now-lastAttemptUtc<TimeSpan.FromMinutes(1))return;
        lastAttemptedTag=tag;
        lastAttemptUtc=now;
        Log("Release hint "+tag+" received; requesting immediate verified GitHub check.");
        // UI thread owns WPF notification and the existing updater. Event
        // calls bypass ONLY the periodic poll timer, NEVER SHA256, installer
        // trust checks, or the GitHub API rate-limit backoff.
        if(owner.Dispatcher.HasShutdownStarted)return;
        owner.Dispatcher.BeginInvoke(new Action(async ()=>
        {
            await DeskSpacePublicUpdaterV44.CheckAsync(
                owner,automatic:true,releaseEvent:true);
        }),DispatcherPriority.Background);
    }

    private static async Task ListenAsync(Window owner)
    {
        int failures=0;
        while(!owner.Dispatcher.HasShutdownStarted)
        {
            try
            {
                using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint);
                request.Headers.UserAgent.ParseAdd("DeskSpace-Release-Listener/1.0");
                using var response=await StreamHttp.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                failures=0;
                Log("Release-event stream connected.");
                await using var stream=await response.Content.ReadAsStreamAsync();
                using var reader=new StreamReader(stream);
                while(!reader.EndOfStream && !owner.Dispatcher.HasShutdownStarted)
                {
                    string? line=await reader.ReadLineAsync();
                    if(line==null)break;
                    if(line.Length>4096)continue;
                    if(TryReleaseEvent(line,DeskSpacePublicUpdaterV44.Channel,
                        out var tag))
                        Dispatch(owner,tag);
                }
                Log("Release-event stream closed; reconnecting.");
            }
            catch(Exception ex) when(ex is HttpRequestException ||
                ex is IOException || ex is TaskCanceledException ||
                ex is UnauthorizedAccessException)
            {
                Log("Release-event stream interrupted: "+
                    ex.GetType().Name+" "+ex.Message);
            }
            // Network loss does not spin, or block five-minute recovery checks.
            failures=Math.Min(failures+1,6);
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(60,5*failures)));
        }
    }
}

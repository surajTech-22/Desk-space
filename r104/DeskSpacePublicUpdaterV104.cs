using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DeskSpace;

// Public stable channel; no gh.exe or login required, with the existing
// independent installation supervisor retained and SHA-256 verified.
internal static class DeskSpacePublicUpdaterV44
{
    private const string Repo="surajTech-22/Desk-space";
    private const string StableApi="https://api.github.com/repos/"+Repo+"/releases/latest";
    private const string TesterApi="https://api.github.com/repos/"+Repo+"/releases?per_page=40";
    private static readonly HttpClient Http=NewClient();
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static DispatcherTimer? timer;
    private static bool started;
    private static HttpClient NewClient()
    {
        var c=new HttpClient{Timeout=TimeSpan.FromMinutes(6)};
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DeskSpace-Windows-Updater/1.0");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }
    private static string Local=>
        Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),"DeskSpace");
    // Channel membership is device-local, never tied to a GitHub login.
    // Stable is the safe default, including malformed or missing preferences.
    public static string Channel
    {
        get
        {
            try
            {
                var selected=File.ReadAllText(Path.Combine(Local,"update-channel.txt")).Trim();
                return selected=="tester"?"tester":"stable";
            }
            catch(IOException){return "stable";}
            catch(UnauthorizedAccessException){return "stable";}
        }
    }
    public static void SetChannel(string value)
    {
        if(value!="stable" && value!="tester")
            throw new ArgumentException("Unknown DeskSpace update channel",nameof(value));
        Directory.CreateDirectory(Local);
        File.WriteAllText(Path.Combine(Local,"update-channel.txt"),value);
        Log("Selected "+value+" update channel");
    }
    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Local);
            File.AppendAllText(Path.Combine(Local,"update-status.log"),
                DateTime.UtcNow.ToString("O")+" AUTO "+message+Environment.NewLine);
        }
        catch(IOException){} catch(UnauthorizedAccessException){}
    }
    // Updates install over the existing app. Only version-named STAGING
    // folders are disposable. User data, TemporaryShelf, profiles and the
    // installed app never participate in this cleanup.
    private static void PruneUpdateCache(Version installed)
    {
        var parent=Path.Combine(Local,"Updates");
        if(!Directory.Exists(parent))return;
        try
        {
            foreach(var folder in Directory.EnumerateDirectories(parent))
            {
                try
                {
                    var name=Path.GetFileName(folder);
                    if(!Regex.IsMatch(name,@"^(?:tester-)?v\d+\.\d+\.\d+$") ||
                       !Version.TryParse(name.StartsWith("tester-v",StringComparison.Ordinal)?
                           name.Substring(8):name.Substring(1),out var version))continue;
                    var info=new DirectoryInfo(folder);
                    if((info.Attributes&FileAttributes.ReparsePoint)!=0)continue;
                    var age=DateTime.UtcNow-info.LastWriteTimeUtc;
                    // Leave the currently downloading or installing release alone.
                    if(version<=installed)
                    {
                        if(age<TimeSpan.FromMinutes(15))continue;
                    }
                    else if(age<TimeSpan.FromHours(24))continue;
                    // Never purge arbitrary user-added items or links.
                    if(Directory.EnumerateDirectories(folder).Any())continue;
                    bool recognized=true;
                    foreach(var file in Directory.EnumerateFiles(folder))
                    {
                        var leaf=Path.GetFileName(file);
                        if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0 ||
                           (leaf!="DeskSpace_Setup.exe" &&
                            leaf!="DeskSpace_Setup.sha256.txt" &&
                            leaf!="CompleteDeskSpaceUpdate.ps1" &&
                            leaf!="inno-setup.log"))
                        {
                            recognized=false;
                            break;
                        }
                    }
                    if(!recognized)continue;
                    Directory.Delete(folder,true);
                    Log("Removed obsolete installer cache "+name);
                }
                catch(IOException){}
                catch(UnauthorizedAccessException){}
                catch(System.Security.SecurityException){}
            }
        }
        catch(IOException){}
        catch(UnauthorizedAccessException){}
    }

    public static void StartAutomaticChecks(Window owner)
    {
        if(started)return;
        started=true;
        timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(5)};
        timer.Tick+=async (_,_)=>await CheckAsync(owner,true);
        timer.Start();
        if(owner.IsLoaded)Schedule();
        else owner.Loaded+=WhenLoaded;
        void WhenLoaded(object sender,RoutedEventArgs e)
        {
            owner.Loaded-=WhenLoaded;
            Schedule();
        }
        void Schedule() => owner.Dispatcher.BeginInvoke(
            new Action(async ()=>await CheckAsync(owner,true)),
            DispatcherPriority.Background);
    }
    private static Uri FindAsset(JsonElement root,string name)
    {
        foreach(var item in root.GetProperty("assets").EnumerateArray())
        {
            if(item.GetProperty("name").GetString()!=name)continue;
            var uri=new Uri(item.GetProperty("browser_download_url").GetString()??"",
                UriKind.Absolute);
            if(uri.Scheme!="https" ||
                !uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith("/"+Repo+"/releases/download/",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Release asset host/path is not trusted");
            return uri;
        }
        throw new InvalidDataException("Missing verified release asset: "+name);
    }
    private static async Task<string> Checksum(Uri uri,CancellationToken ct)
    {
        using var response=await Http.GetAsync(uri,ct);
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>4096)
            throw new InvalidDataException("Oversized checksum file");
        var content=await response.Content.ReadAsStringAsync(ct);
        if(content.Length>4096)throw new InvalidDataException("Oversized checksum");
        return content;
    }
    private static async Task Download(Uri uri,string file,CancellationToken ct)
    {
        using var response=await Http.GetAsync(uri,
            HttpCompletionOption.ResponseHeadersRead,ct);
        response.EnsureSuccessStatusCode();
        const long limit=250L*1024*1024;
        if(response.Content.Headers.ContentLength>limit)
            throw new InvalidDataException("Unexpected installer size");
        await using var input=await response.Content.ReadAsStreamAsync(ct);
        await using var output=new FileStream(file,FileMode.Create,FileAccess.Write,
            FileShare.None,128*1024,true);
        byte[] buffer=new byte[128*1024];long length=0;
        while(true)
        {
            int n=await input.ReadAsync(buffer.AsMemory(),ct);
            if(n==0)break;
            length+=n;
            if(length>limit)throw new InvalidDataException("Oversized installer");
            await output.WriteAsync(buffer.AsMemory(0,n),ct);
        }
        await output.FlushAsync(ct);
        if(length<5_000_000)throw new InvalidDataException("Truncated installer");
    }
    private static void Verify(string installer,string checksum,JsonElement root)
    {
        var match=Regex.Match(checksum,@"^\s*(?<sha>[0-9a-fA-F]{64})(?:\s|$)");
        if(!match.Success)throw new InvalidDataException("Invalid checksum format");
        using var data=File.OpenRead(installer);
        var sha=Convert.ToHexString(SHA256.HashData(data));
        if(!sha.Equals(match.Groups["sha"].Value,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Installer SHA-256 mismatch");
        foreach(var asset in root.GetProperty("assets").EnumerateArray())
        {
            if(asset.GetProperty("name").GetString()!="DeskSpace_Setup.exe")continue;
            if(asset.TryGetProperty("digest",out var digest) &&
                digest.ValueKind==JsonValueKind.String)
            {
                var published=digest.GetString()??"";
                if(published.Length>0 &&
                   !published.Equals("sha256:"+sha,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Release metadata digest mismatch");
            }
        }
        data.Position=0;
        if(data.ReadByte()!=0x4d||data.ReadByte()!=0x5a)
            throw new InvalidDataException("Installer PE signature missing");
    }
    private static async Task StartSupervisor(string installer,string folder,string tag)
    {
        var ps=Path.Combine(folder,"CompleteDeskSpaceUpdate.ps1");
        await using(var input=typeof(DeskSpaceUpdater).Assembly
            .GetManifestResourceStream("DeskSpace.DeskSpaceUpdateSupervisor.ps1")
            ??throw new IOException("Trusted update supervisor missing"))
        await using(var output=new FileStream(ps,FileMode.Create,FileAccess.Write,
            FileShare.None))
            await input.CopyToAsync(output);
        var child=new ProcessStartInfo("powershell.exe"){
            UseShellExecute=false,CreateNoWindow=true
        };
        foreach(var arg in new[]{
            "-NoProfile","-NonInteractive","-WindowStyle","Hidden",
            "-ExecutionPolicy","Bypass","-File",ps,
            "-Installer",installer,
            "-TargetExe",Path.Combine(AppContext.BaseDirectory,"DeskSpace.exe"),
            "-StatusLog",Path.Combine(Local,"update-status.log"),
            "-OldPid",Environment.ProcessId.ToString()
        })child.ArgumentList.Add(arg);
        if(Process.Start(child)==null)
            throw new IOException("Cannot start update supervisor");
        Log("Verified "+tag+" handed to independent installer supervisor");
        timer?.Stop();
        Application.Current.Shutdown();
    }
    public static async Task CheckAsync(Window owner,bool automatic=false)
    {
        if(!await Gate.WaitAsync(0))return;
        bool supervisorLaunched=false;
        try
        {
            // Also run when offline: finished installs should not leave their
            // old ~60 MB setup packages behind indefinitely.
            PruneUpdateCache(Version.Parse(DeskSpaceUpdater.CurrentVersion));
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(6));
            string channel=Channel;
            using var answer=await Http.GetAsync(
                channel=="tester"?TesterApi:StableApi,timeout.Token);
            answer.EnsureSuccessStatusCode();
            using var doc=JsonDocument.Parse(
                await answer.Content.ReadAsStringAsync(timeout.Token));
            JsonElement root;
            Version? candidateVersion=null;
            if(channel=="tester")
            {
                root=default;
                foreach(var release in doc.RootElement.EnumerateArray())
                {
                    if(release.GetProperty("draft").GetBoolean() ||
                       !release.GetProperty("prerelease").GetBoolean())continue;
                    string label=release.GetProperty("tag_name").GetString()??"";
                    if(!Regex.IsMatch(label,@"^tester-v\d+\.\d+\.\d+$") ||
                       !Version.TryParse(label.Substring(8),out var revision))continue;
                    if(candidateVersion==null || revision>candidateVersion)
                    {
                        root=release;candidateVersion=revision;
                    }
                }
                if(candidateVersion==null)
                    throw new InvalidDataException(
                        "No published DeskSpace tester build yet. Stable channel is unchanged.");
            }
            else
            {
                root=doc.RootElement;
                if(root.GetProperty("draft").GetBoolean() ||
                   root.GetProperty("prerelease").GetBoolean())
                    throw new InvalidDataException("Latest release is not stable");
            }
            string tag=root.GetProperty("tag_name").GetString()??"";
            string pattern=channel=="tester"?
                @"^tester-v\d+\.\d+\.\d+$":@"^v\d+\.\d+\.\d+$";
            string versionLabel=channel=="tester"?
                (tag.Length>=8?tag.Substring(8):""):
                (tag.Length>=1?tag.Substring(1):"");
            if(!Regex.IsMatch(tag,pattern) ||
               !Version.TryParse(versionLabel,out var latest))
                throw new InvalidDataException("Unexpected "+channel+" version tag");
            var installed=Version.Parse(DeskSpaceUpdater.CurrentVersion);
            if(latest<=installed)
            {
                if(!automatic)MessageBox.Show(owner,
                    "DeskSpace is up to date ("+installed+"). Channel: "+channel+".",
                    "DeskSpace automatic updates",MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
            var shaFile=FindAsset(root,"DeskSpace_Setup.sha256.txt");
            var folder=Path.Combine(Local,"Updates",tag);
            Directory.CreateDirectory(folder);
            var installer=Path.Combine(folder,"DeskSpace_Setup.exe");
            DeskSpaceUpdateProgress.Publish("Downloading",0);
            var checksum=await Checksum(shaFile,timeout.Token);
            await Download(setup,installer,timeout.Token);
            DeskSpaceUpdateProgress.Publish("Verifying",100);
            Verify(installer,checksum,root);
            DeskSpaceUpdateProgress.Publish("Restarting",100);
            await StartSupervisor(installer,folder,tag);
            supervisorLaunched=true;
        }
        catch(Exception ex)
        {
            Log("Update check failed: "+ex.GetType().Name+" "+ex.Message);
            if(!automatic)MessageBox.Show(owner,
                "Automatic update failed: "+ex.Message+
                "\nThe existing installation has not been replaced.",
                "DeskSpace updates",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
        finally
        {
            if(!supervisorLaunched)DeskSpaceUpdateProgress.Clear();
            Gate.Release();
        }
    }
}

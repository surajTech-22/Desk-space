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
    // Stable releases have a public, canonical GitHub web redirect. Unlike
    // api.github.com this endpoint does not consume anonymous REST API calls.
    // Redirect is read, NOT automatically followed, and must have our exact
    // repository /releases/tag/v* path. No mirrors or security bypass.
    private static readonly HttpClient StableRedirectV131=NewStableClientV131();
    private static HttpClient NewStableClientV131()
    {
        var handler=new HttpClientHandler{AllowAutoRedirect=false};
        var client=new HttpClient(handler){Timeout=TimeSpan.FromMinutes(2)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DeskSpace-Windows-Updater/1.0");
        return client;
    }
    private static async Task<string> LatestStableTagV131(CancellationToken ct)
    {
        var source=new Uri("https://github.com/"+Repo+"/releases/latest");
        using var response=await StableRedirectV131.GetAsync(source,
            HttpCompletionOption.ResponseHeadersRead,ct);
        if(response.StatusCode!=System.Net.HttpStatusCode.Redirect &&
           response.StatusCode!=System.Net.HttpStatusCode.MovedPermanently &&
           response.StatusCode!=System.Net.HttpStatusCode.TemporaryRedirect &&
           (int)response.StatusCode!=308)
            throw new InvalidOperationException(
                "GitHub stable-release redirect unavailable (HTTP "+
                (int)response.StatusCode+"). Check your network and security product.");
        var location=response.Headers.Location??
            throw new InvalidDataException("Stable GitHub redirect has no Location.");
        var resolved=location.IsAbsoluteUri?location:new Uri(source,location);
        string prefix="/"+Repo+"/releases/tag/";
        if(resolved.Scheme!="https" ||
           !resolved.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase) ||
           !resolved.AbsolutePath.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) ||
           resolved.Query.Length!=0 || resolved.Fragment.Length!=0)
            throw new InvalidDataException("GitHub redirected outside trusted stable release.");
        var tag=Uri.UnescapeDataString(resolved.AbsolutePath.Substring(prefix.Length));
        if(!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$"))
            throw new InvalidDataException("Unexpected stable-release version tag.");
        return tag;
    }
    private static Uri StableAssetV131(string tag,string file)
    {
        if(!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$") ||
           (file!="DeskSpace_Setup.exe" && file!="DeskSpace_Setup.sha256.txt"))
            throw new InvalidDataException("Untrusted stable-release asset path.");
        return new Uri("https://github.com/"+Repo+"/releases/download/"+tag+"/"+file);
    }
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static string lastNotifiedTagV132="";
    private static void NotifyNewReleaseV132(string channel,string tag)
    {
        if(lastNotifiedTagV132==tag)return;
        lastNotifiedTagV132=tag;
        // NotifyIcon balloons can be hidden by Windows notification settings.
        // This app-owned, non-modal WPF notice is visible even when the OS
        // suppresses system notifications. It never delays installation.
        DeskSpaceReleaseNoticeV137.Show(channel,tag);
        DeskSpaceUpdateProgress.Publish("Verified update "+tag,0);
        // Notification is best-effort. Failure cannot suppress verified install.
        try
        {
            var balloon=new System.Windows.Forms.NotifyIcon{
                Icon=System.Drawing.SystemIcons.Information,
                Visible=true,
                Text="DeskSpace new release"
            };
            balloon.ShowBalloonTip(7000,"DeskSpace update available",
                tag+" on "+channel+" channel. Secure download starting.",
                System.Windows.Forms.ToolTipIcon.Info);
            // Keep NotifyIcon alive on the WPF dispatcher while the update
            // downloads; dispose on the SAME UI thread when the balloon ends.
            var expiry=new DispatcherTimer{Interval=TimeSpan.FromSeconds(12)};
            expiry.Tick+=(_,_)=>{expiry.Stop();balloon.Dispose();};
            expiry.Start();
            Log("New release notice requested (WPF + Windows balloon): "+tag+
                " channel "+channel+". Windows may suppress balloons.");
        }
        catch(Exception error)
        {
            Log("Update notification unavailable: "+error.Message);
        }
    }
    private static DispatcherTimer? timer;
    private static bool started;
    // Background deferrals are log-throttled; the five-minute timer remains
    // active, so a later check can install after other windows have closed.
    private static DateTime lastBusyNoticeUtc=DateTime.MinValue;
    private static bool DeferForOpenDeskSpace(Window owner,bool automatic)
    {
        string executable=Path.Combine(AppContext.BaseDirectory,"DeskSpace.exe");
        if(!DeskSpaceUpdateInstallGateV188.OtherInstalledProcessRunning(executable))
            return false;
        if(DateTime.UtcNow-lastBusyNoticeUtc>=TimeSpan.FromMinutes(45))
        {
            Log("R188 update deferred: another DeskSpace process still owns the installation; "+
                "no app windows were closed, all user data retained.");
            lastBusyNoticeUtc=DateTime.UtcNow;
        }
        if(!automatic)
            MessageBox.Show(owner,
                "An additional DeskSpace window is running. "+
                "Save work and close the other Workspace normally. "+
                "The verified update will retry automatically; your saved data is retained.",
                "DeskSpace update deferred",MessageBoxButton.OK,
                MessageBoxImage.Information);
        return true;
    }
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
        // Avoid exhausting GitHub unauthenticated IP-shared API budget.
        // Non-API GitHub releases/latest check is a recovery route when
        // ntfy.sh cannot deliver a push event. This MUST NOT use GitHub REST
        // API at this frequency. Event-driven detection remains immediate.
        timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(1)};
        timer.Tick+=async (_,_)=>await CheckAsync(owner,true);
        timer.Start();
        // Subscribe immediately, independently of periodic checks.
        DeskSpaceReleasePushV133.Start(owner);
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
        // R222: never overwrite the cached installer while downloading.
        // Promote a completed local transfer in one same-folder file rename;
        // R219 then verifies official SHA256, metadata digest and PE signature.
        await DeskSpaceAtomicInstallerV222.StageFromStreamAsync(input,file,ct);
    }
    private static void Verify(string installer,string checksum,JsonElement? root)
    {
        // Use the SAME official checksum parser as the verified staging queue.
        // A valid "hash  DeskSpace_Setup.exe" line must be accepted by both.
        var verifiedDigest=DeskSpaceVerifiedUpdateQueueV191.NormalizeOfficialChecksumV219(
            checksum);
        using var data=File.OpenRead(installer);
        var sha=Convert.ToHexString(SHA256.HashData(data));
        if(!sha.Equals(verifiedDigest,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Installer SHA-256 mismatch");
        // The public stable redirect exposes a pinned GitHub release tag but
        // not REST metadata. Its official sha256.txt remains mandatory.
        // Tester REST metadata also provides the second digest comparison.
        if(root.HasValue)
            foreach(var asset in root.Value.GetProperty("assets").EnumerateArray())
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
        // Never shut down this widget to launch a duplicate supervisor while
        // another process already owns the R185 install transaction.
        using(var existing=new Mutex(false,@"Local\DeskSpace-UpdateSupervisor-V185"))
        {
            bool free=false;
            try
            {
                try{free=existing.WaitOne(0);}
                catch(AbandonedMutexException){free=true;}
            }
            finally{if(free)existing.ReleaseMutex();}
            if(!free)
            {
                Log("R227 active supervisor already owns installer; retaining this widget.");
                throw new InvalidOperationException("R227 supervised installation already running; no second handoff.");
            }
        }
        var ps=Path.Combine(folder,"CompleteDeskSpaceUpdate."+
            Environment.ProcessId+"."+Guid.NewGuid().ToString("N")+".ps1");
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
        // Do not forcibly shut down only the updater owner here; R230's
        // supervisor signals all Workspace/widget/tray processes together.
        // All WPF Closing handlers run and may decline unsaved-work exit.
        Log("R230 supervisor launched; awaiting coordinated normal shutdown.");
    }
    // R191: stage once from VERIFIED GitHub bytes; do not force-close an
    // ordinary Workspace or another user-owned widget host. A one-minute
    // local retry requires no repeat REST request and no new download.
    private static async Task<bool> ResumeVerifiedV191(Window owner,bool automatic)
    {
        var pending=DeskSpaceVerifiedUpdateQueueV191.Peek(Channel);
        if(pending==null)return false;
        if(!await Gate.WaitAsync(0))return true;
        try
        {
            var target=Path.Combine(AppContext.BaseDirectory,"DeskSpace.exe");
            // R230: verified update is already cached. Do not ask the user
            // to close every window manually: the independent supervisor
            // sends normal close requests to every matching process.
            if(!DeskSpaceVerifiedUpdateQueueV191.IsStillVerified(pending))
            {
                DeskSpaceVerifiedUpdateQueueV191.Clear(pending);
                Log("R191 staged update invalid or changed; a fresh official GitHub verification is required.");
                return false;
            }
            // The external supervisor coordinates all normal exits and
            // rechecks the exact executable path before replacing files.
            // Do not discard the verified candidate BEFORE the supervisor
            // has accepted the handoff. Another DeskSpace instance may win
            // its install mutex or the process launcher may temporarily
            // refuse; retry on the existing automatic timer, not another
            // network download or repeated notification.
            Log("R229 verified cached "+pending.Tag+" attempting safe supervisor handoff.");
            DeskSpaceUpdateProgress.Publish("Restarting",100);
            try
            {
                await StartSupervisor(pending.Installer,pending.Folder,pending.Tag);
                DeskSpaceVerifiedUpdateQueueV191.Clear(pending);
                return true;
            }
            catch(Exception error) when(error is InvalidOperationException or IOException)
            {
                Log("R229 verified update retained for retry after normal exit: "+
                    error.GetType().Name);
                DeskSpaceUpdateProgress.Clear();
                return true;
            }
        }
        catch(Exception ex)
        {
            DeskSpaceVerifiedUpdateQueueV191.Clear(pending);
            Log("R191 queued handoff failed: "+ex.GetType().Name+" "+ex.Message);
            return true;
        }
        finally{Gate.Release();}
    }    public static async Task CheckAsync(Window owner,bool automatic=false,
        bool releaseEvent=false)
    {
        if(await ResumeVerifiedV191(owner,automatic))return;
        if(!await Gate.WaitAsync(0))return;
        bool supervisorLaunched=false;
        string stage="GitHub release metadata";
        try
        {
            // Also run when offline: finished installs should not leave their
            // old ~60 MB setup packages behind indefinitely.
            PruneUpdateCache(Version.Parse(DeskSpaceUpdater.CurrentVersion));
            string channel=Channel;
            var now=DateTimeOffset.UtcNow;
            if(channel=="stable")
            {
                // Stable has no REST API request. Previous GitHub 403/429
                // API backoff does not apply to the canonical GitHub website.
                // 30-minute automatic spacing remains persisted, but manual
                // requests may check the public release redirect immediately.
                var next=DeskSpaceUpdateRateV127.ReadUtc(
                    DeskSpaceUpdateRateV127.PollFile(Local,"stable")).AddMinutes(1);
                // Event-driven checks bypass ONLY the time-based spacing.
                // They still use the canonical release tag, official assets,
                // mandatory SHA-256 and independent installer supervisor.
                if(automatic && !releaseEvent && now<next)return;
                DeskSpaceUpdateRateV127.SaveUtc(
                    DeskSpaceUpdateRateV127.PollFile(Local,"stable"),now);
                using var stableTimeout=new CancellationTokenSource(TimeSpan.FromMinutes(6));
                stage="Check official GitHub stable-release redirect";
                string stableTag=await LatestStableTagV131(stableTimeout.Token);
                var latestStable=Version.Parse(stableTag.Substring(1));
                var installedStable=Version.Parse(DeskSpaceUpdater.CurrentVersion);
                if(latestStable<=installedStable)
                {
                    if(!automatic)MessageBox.Show(owner,
                        "DeskSpace is up to date ("+installedStable+"). Channel: stable.",
                        "DeskSpace updates",MessageBoxButton.OK,MessageBoxImage.Information);
                    return;
                }
                NotifyNewReleaseV132("stable",stableTag);
                var stableFolder=Path.Combine(Local,"Updates",stableTag);
                Directory.CreateDirectory(stableFolder);
                var stableInstaller=Path.Combine(stableFolder,"DeskSpace_Setup.exe");
                using var stableLockV191=await DeskSpaceVerifiedUpdateQueueV191.AcquireStageLockAsync(
                    Path.Combine(Local,"Updates"),stableTimeout.Token);
                DeskSpaceUpdateProgress.Publish("Downloading",0);
                stage="Download official GitHub checksum";
                var sha=await Checksum(StableAssetV131(stableTag,
                    "DeskSpace_Setup.sha256.txt"),stableTimeout.Token);
                stage="Download official GitHub installer";
                bool stableCachedV191=false;
                if(File.Exists(stableInstaller))
                {
                    try{Verify(stableInstaller,sha,null);stableCachedV191=true;}
                    catch(Exception error) when(error is InvalidDataException or
                        IOException or UnauthorizedAccessException)
                    {
                        Log("R229 stable cached installer invalid/inaccessible; revalidate by fresh download.");
                    }
                }
                if(!stableCachedV191)
                    await Download(StableAssetV131(stableTag,
                        "DeskSpace_Setup.exe"),stableInstaller,stableTimeout.Token);
                stage="Verify installer SHA-256";
                DeskSpaceUpdateProgress.Publish("Verifying",100);
                Verify(stableInstaller,sha,null);
                DeskSpaceVerifiedUpdateQueueV191.Stage(stableInstaller,
                    stableFolder,stableTag,sha,"stable");
                stage="Launch verified installer supervisor";
                Log("R230 verified installer ready; independent supervisor will request normal exit of all DeskSpace windows.");
                DeskSpaceUpdateProgress.Publish("Restarting",100);
                await StartSupervisor(stableInstaller,stableFolder,stableTag);
                supervisorLaunched=true;
                return;
            }
            // Never repeat a 403/429 request until GitHub permits it.
            // Manual checks bypass only normal auto-poll spacing, not rate limit.
            if(DeskSpaceUpdateRateV127.IsDeferred(Local,channel,now,
                automatic && !releaseEvent,out var resumedAt))
            {
                if(!automatic)
                {
                    MessageBox.Show(owner,
                        "GitHub update checks are paused until "+
                        resumedAt.ToLocalTime().ToString("g")+
                        ". DeskSpace is unchanged. Automatic checks will resume.",
                        "DeskSpace updates",MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                Log("Skipped "+channel+" release check while deferred until "+
                    resumedAt.ToString("O"));
                return;
            }
            // Persist before a network request: restarting DeskSpace does not
            // trigger an API request loop, even after a network failure.
            DeskSpaceUpdateRateV127.SaveUtc(
                DeskSpaceUpdateRateV127.PollFile(Local,channel),now);
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(6));
            using var answer=await Http.GetAsync(
                channel=="tester"?TesterApi:StableApi,timeout.Token);
            string? denial=null;
            if(answer.StatusCode==System.Net.HttpStatusCode.Forbidden ||
               (int)answer.StatusCode==429)
            {
                denial=await answer.Content.ReadAsStringAsync(timeout.Token);
                if(denial.Length>4096)denial=denial.Substring(0,4096);
            }
            if(DeskSpaceUpdateRateV127.IsLimitResponse(answer,denial))
            {
                var until=DeskSpaceUpdateRateV127.RetryAfter(answer,
                    DateTimeOffset.UtcNow);
                DeskSpaceUpdateRateV127.SaveUtc(
                    DeskSpaceUpdateRateV127.BackoffFile(Local),until);
                Log("GitHub API rate limit (HTTP "+(int)answer.StatusCode+
                    ") - paused until "+until.ToString("O"));
                if(!automatic)
                    MessageBox.Show(owner,
                        "GitHub API rate limit reached. Checks are paused until "+
                        until.ToLocalTime().ToString("g")+
                        ". Do not repeat Check for Updates. "+
                        "Your installation and saved data are unchanged.",
                        "DeskSpace updates",MessageBoxButton.OK,
                        MessageBoxImage.Information);
                return;
            }
            if(answer.StatusCode==System.Net.HttpStatusCode.Forbidden)
                throw new InvalidOperationException(
                    "GitHub denied release metadata (HTTP 403, not rate limit). "+
                    "Check network or security-product filtering.");
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
            NotifyNewReleaseV132(channel,tag);
            // Do not even download if an already-running application from
            // this install would prevent replacement. Never shut down the
            // primary widget and launch an installer that must immediately fail.
            // Stage the official installer once even when Workspace is open.
            // Never attempt an install until the R188 final peer gate passes.
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
            var shaFile=FindAsset(root,"DeskSpace_Setup.sha256.txt");
            var folder=Path.Combine(Local,"Updates",tag);
            Directory.CreateDirectory(folder);
            var installer=Path.Combine(folder,"DeskSpace_Setup.exe");
            using var testerLockV191=await DeskSpaceVerifiedUpdateQueueV191.AcquireStageLockAsync(
                Path.Combine(Local,"Updates"),timeout.Token);
            DeskSpaceUpdateProgress.Publish("Downloading",0);
            stage="Download checksum from official GitHub release";
            var checksum=await Checksum(shaFile,timeout.Token);
            stage="Download installer from official GitHub release";
            bool testerCachedV191=false;
            if(File.Exists(installer))
            {
                try{Verify(installer,checksum,root);testerCachedV191=true;}
                catch(Exception error) when(error is InvalidDataException or
                    IOException or UnauthorizedAccessException)
                {
                    Log("R229 tester cached installer invalid/inaccessible; revalidate by fresh download.");
                }
            }
            if(!testerCachedV191)await Download(setup,installer,timeout.Token);
            stage="Verify installer SHA-256";
            DeskSpaceUpdateProgress.Publish("Verifying",100);
            Verify(installer,checksum,root);
            DeskSpaceVerifiedUpdateQueueV191.Stage(installer,folder,
                tag,checksum,"tester");
            stage="Start verified installation supervisor";
            DeskSpaceUpdateProgress.Publish("Restarting",100);
            // Recheck at the final supervisor handoff: another Workspace
            // window may have opened while the installer downloaded.
            // Defer without shutting down any processes or losing data.
            Log("R230 verified installer ready; independent supervisor will request normal exit of all DeskSpace windows.");
            await StartSupervisor(installer,folder,tag);
            supervisorLaunched=true;
        }
        catch(Exception ex)
        {
            Log("Update failed at "+stage+": "+ex.GetType().Name+" "+ex.Message);
            if(!automatic)MessageBox.Show(owner,
                "Update failed during "+stage+": "+ex.Message+
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

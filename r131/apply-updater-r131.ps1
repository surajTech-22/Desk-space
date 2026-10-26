$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$file='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
function Swap([string]$from,[string]$to,[string]$label){
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $source=[IO.File]::ReadAllText($script:file).Replace($crlf,$lf)
 $a=$from.Replace($crlf,$lf);$b=$to.Replace($crlf,$lf)
 $count=([regex]::Matches($source,[regex]::Escape($a))).Count
 if($count -ne 1){throw "R131 $label expected 1 occurrence, got $count"}
 [IO.File]::WriteAllText($script:file,$source.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
Swap @'
    private static readonly HttpClient Http=NewClient();
'@ @'
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
'@ 'trusted public redirect for stable releases'
Swap @'
    private static void Verify(string installer,string checksum,JsonElement root)
'@ @'
    private static void Verify(string installer,string checksum,JsonElement? root)
'@ 'optional metadata digest for web-only stable'
Swap @'
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
'@ @'
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
'@ 'keep release SHA verification and tester metadata digest'
Swap @'
            string channel=Channel;
            var now=DateTimeOffset.UtcNow;
'@ @'
            string channel=Channel;
            var now=DateTimeOffset.UtcNow;
            if(channel=="stable")
            {
                // Stable has no REST API request. Previous GitHub 403/429
                // API backoff does not apply to the canonical GitHub website.
                // 30-minute automatic spacing remains persisted, but manual
                // requests may check the public release redirect immediately.
                var next=DeskSpaceUpdateRateV127.ReadUtc(
                    DeskSpaceUpdateRateV127.PollFile(Local,"stable")).AddMinutes(30);
                if(automatic && now<next)return;
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
                var stableFolder=Path.Combine(Local,"Updates",stableTag);
                Directory.CreateDirectory(stableFolder);
                var installer=Path.Combine(stableFolder,"DeskSpace_Setup.exe");
                DeskSpaceUpdateProgress.Publish("Downloading",0);
                stage="Download official GitHub checksum";
                var sha=await Checksum(StableAssetV131(stableTag,
                    "DeskSpace_Setup.sha256.txt"),stableTimeout.Token);
                stage="Download official GitHub installer";
                await Download(StableAssetV131(stableTag,
                    "DeskSpace_Setup.exe"),installer,stableTimeout.Token);
                stage="Verify installer SHA-256";
                DeskSpaceUpdateProgress.Publish("Verifying",100);
                Verify(installer,sha,null);
                stage="Launch verified installer supervisor";
                DeskSpaceUpdateProgress.Publish("Restarting",100);
                await StartSupervisor(installer,stableFolder,stableTag);
                supervisorLaunched=true;
                return;
            }
'@ 'stable auto update without REST API rate-limit'
Write-Host 'R131 stable updater: Github.com official stable release redirect, strict repo/tag/asset URL allowlist, SHA-256 file verification, 30-minute persisted auto interval; tester remains rate-safe GitHub API. No security bypass.'

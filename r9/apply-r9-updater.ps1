$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$src='DeskSpace_Windows_R1/DeskSpace/DeskSpaceUpdater.cs'
$text=[IO.File]::ReadAllText($src).Replace("`r`n","`n")
function ReplaceExact([string]$a,[string]$b) {
    $a=$a.Replace("`r`n","`n")
    $b=$b.Replace("`r`n","`n")
    if(!$script:text.Contains($a)){throw "R9 updater anchor missing: $a"}
    $script:text=$script:text.Replace($a,$b)
}
Copy-Item 'r9/DeskSpaceUpdateProgress.cs' 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceUpdateProgress.cs' -Force
ReplaceExact '        if (!await Gate.WaitAsync(0)) return;' @'
        if (!await Gate.WaitAsync(0)) return;
        bool handedOff = false;
'@
ReplaceExact @'
            if (available <= InstalledVersion)
'@ @'
            if (available <= InstalledVersion)
'@
ReplaceExact @'
            // Never run a downloaded installer before its SHA-256 has been checked.
'@ @'
            DeskSpaceUpdateProgress.Publish("New version " + tag);
            // Never run a downloaded installer before its SHA-256 has been checked.
'@
$start='            var download = await RunGh(new[] { "release", "download", tag, "--repo", Repository,'
$finish='            if (download.ExitCode != 0)'
$a=$text.IndexOf($start,[StringComparison]::Ordinal)
$b=$text.IndexOf($finish,[StringComparison]::Ordinal)
if($a -lt 0 -or $b -le $a){throw 'R9 updater download anchor missing'}
$replacement=@'
            DeskSpaceUpdateProgress.Publish("Downloading",0);
            // gh runs asynchronously. Poll at 1 Hz; no busy loop, UI blocking,
            // extra process or per-frame disk IO.
            long expectedSize=0;
            foreach(var asset in json.RootElement.GetProperty("assets").EnumerateArray())
                if(asset.GetProperty("name").GetString() == "DeskSpace_Setup.exe" &&
                    asset.TryGetProperty("size",out var size) && size.TryGetInt64(out var assetSize))
                    expectedSize=assetSize;
            var downloadTask = RunGh(new[] { "release", "download", tag, "--repo", Repository,
                "--pattern", "DeskSpace_Setup.exe", "--pattern", "DeskSpace_Setup.sha256.txt", "--dir", targetDir });
            long lastBytes = -1;
            while (!downloadTask.IsCompleted)
            {
                await Task.Delay(1000);
                long bytes = File.Exists(installer) ? new FileInfo(installer).Length : 0;
                if (bytes == lastBytes) continue;
                lastBytes = bytes;
                if (expectedSize > 0)
                    DeskSpaceUpdateProgress.Publish("Downloading",
                        (int)Math.Clamp(bytes * 100 / expectedSize,0,99));
                else DeskSpaceUpdateProgress.Publish("Downloading " + (bytes / 1048576) + " MB");
            }
            var download=await downloadTask;
'@
$text=$text.Substring(0,$a)+$replacement+$text.Substring($b)
ReplaceExact '            await using (var input = File.OpenRead(installer))' @'
            DeskSpaceUpdateProgress.Publish("Verifying",100);
            await using (var input = File.OpenRead(installer))
'@
ReplaceExact '            var supervisor = Path.Combine(targetDir, "CompleteDeskSpaceUpdate.ps1");' @'
            DeskSpaceUpdateProgress.Publish("Installing",100);
            var supervisor = Path.Combine(targetDir, "CompleteDeskSpaceUpdate.ps1");
'@
ReplaceExact '            Record("Verified installer handed to independent supervisor for " + tag + "; closing old widget.");' @'
            Record("Verified installer handed to independent supervisor for " + tag + "; closing old widget.");
            DeskSpaceUpdateProgress.Publish("Restarting",100);
            handedOff = true;
'@
ReplaceExact '        finally { Gate.Release(); }' @'
        finally
        {
            if (!handedOff) DeskSpaceUpdateProgress.Clear();
            Gate.Release();
        }
'@
[IO.File]::WriteAllText($src,$text,[Text.UTF8Encoding]::new($false))
Write-Host 'R9 update center reports stage, downloaded bytes and verified installation handoff.'

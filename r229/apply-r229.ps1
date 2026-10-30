$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R229 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
            if(DeferForOpenDeskSpace(owner,automatic))return;
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
'@ @'
            // R229: a second DeskSpace Workspace may contain unsaved work.
            // Never stop downloading just because it is open. Download and
            // cryptographically verify FIRST, then defer only the install.
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
'@ 'download occurs before deferred exit'
Swap @'
            await Download(setup,installer,timeout.Token);
            DeskSpaceUpdateProgress.Publish("Verifying",100);
            Verify(installer,checksum,root);
'@ @'
            // An earlier attempt may already have a complete verified setup,
            // because the user kept another Workspace window open. Reuse
            // it ONLY after fresh official SHA256 + GitHub metadata checks.
            // Do not repeatedly transfer sixty megabytes every auto check.
            bool cachedVerified=false;
            if(File.Exists(installer))
            {
                try
                {
                    Verify(installer,checksum,root);
                    cachedVerified=true;
                    Log("R229 verified installer cache reused; no redownload.");
                }
                catch(Exception error) when(error is IOException or
                    UnauthorizedAccessException or InvalidDataException)
                {
                    Log("R229 outdated/incomplete installer cache will be redownloaded.");
                }
            }
            if(!cachedVerified)
                await Download(setup,installer,timeout.Token);
            DeskSpaceUpdateProgress.Publish("Verifying",100);
            Verify(installer,checksum,root);
'@ 'verified cache prevents repeat downloads'
Swap @'
            if(DeferForOpenDeskSpace(owner,automatic))return;
            await StartSupervisor(installer,folder,tag);
'@ @'
            // Never terminate a Workspace containing unsaved work. Keep
            // freshly downloaded/verified installer bytes for the next
            // automatic check after the user closes other windows normally.
            if(DeferForOpenDeskSpace(owner,automatic))
            {
                Log("R229 verified update ready; installation deferred until other DeskSpace windows exit normally.");
                return;
            }
            await StartSupervisor(installer,folder,tag);
'@ 'install only after normal peer shutdown'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R229: always download and verify before workspace deferral; safe cached SHA256 reuse avoids redownload; preserve normal shutdown and supervised install.'

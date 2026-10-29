$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r188/DeskSpaceUpdateInstallGateV188.cs' "$root/DeskSpaceUpdateInstallGateV188.cs" -Force
$path="$root/DeskSpacePublicUpdaterV44.cs"
$nl=[string][char]10;$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($path).Replace($crlf,$nl)
function One([string]$a,[string]$b,[string]$label)
{
  $a=$a.Replace($crlf,$nl);$b=$b.Replace($crlf,$nl)
  $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
  if($n -ne 1){throw "R188 ${label}: expected exactly one anchor, found $n"}
  $script:s=$script:s.Replace($a,$b)
}
One @'
    private static bool started;
'@ @'
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
                "Close other DeskSpace windows normally and check again. "+
                "Your current widget and unsaved work will not be closed.",
                "DeskSpace update deferred",MessageBoxButton.OK,
                MessageBoxImage.Information);
        return true;
    }
'@ 'nonmodal background defer helper'
One @'
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
'@ @'
            // Do not even download if an already-running application from
            // this install would prevent replacement. Never shut down the
            // primary widget and launch an installer that must immediately fail.
            if(DeferForOpenDeskSpace(owner,automatic))return;
            var setup=FindAsset(root,"DeskSpace_Setup.exe");
'@ 'pre-download safe deferral'
One @'
            await StartSupervisor(installer,folder,tag);
'@ @'
            // Recheck at the final supervisor handoff: another Workspace
            // window may have opened while the installer downloaded.
            // Defer without shutting down any processes or losing data.
            if(DeferForOpenDeskSpace(owner,automatic))return;
            await StartSupervisor(installer,folder,tag);
'@ 'immediate pre-shutdown race defense'
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R188: do not terminate current widget when another DeskSpace process is running; avoid unsafe update and retry on existing automatic checks.'

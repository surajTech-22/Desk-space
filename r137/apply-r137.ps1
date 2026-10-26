$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$worker="$r/DeskSpacePublicUpdaterV44.cs"
$subscriber="$r/DeskSpaceReleasePushV133.cs"
Copy-Item 'r137/DeskSpaceReleaseNoticeV137.cs' "$r/DeskSpaceReleaseNoticeV137.cs" -Force
function Swap([string]$file,[string]$before,[string]$after,[string]$label) {
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($count -ne 1){throw "R137 $($label): wanted one anchor; found $count"}
 [IO.File]::WriteAllText($file,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
Swap $subscriber @'
        "https://ntfy.sh/"+Topic+"/json?since=latest");
'@ @'
        // Reconnect must recover releases sent during Windows sleep, network
        // outage or the independent installer restart. ntfy caches events;
        // the canonical GitHub check still authenticates the actual release.
        "https://ntfy.sh/"+Topic+"/json?since=1h");
'@ 'replay missed event during reconnect'
Swap $subscriber @'
        if(lastAttemptedTag==tag &&
           now-lastAttemptUtc<TimeSpan.FromMinutes(1))return;
'@ @'
        if(lastAttemptedTag==tag &&
           now-lastAttemptUtc<TimeSpan.FromMinutes(2))return;
        // A public ntfy message is untrusted: bound how often external hints
        // can force a GitHub metadata check, even with changing fake tags.
        if(lastAttemptUtc!=DateTime.MinValue &&
           now-lastAttemptUtc<TimeSpan.FromSeconds(45))return;
'@ 'bound untrusted event-triggered API usage'
Swap $subscriber @'
                Log("Release-event stream connected.");
'@ @'
                Log("Release-event stream connected; replaying last hour of cached release events.");
'@ 'distinguish recovery from a fresh subscription'
Swap $subscriber @'
                    if(TryReleaseEvent(line,DeskSpacePublicUpdaterV44.Channel,
                        out var tag))
                        Dispatch(owner,tag);
'@ @'
                    if(TryReleaseEvent(line,DeskSpacePublicUpdaterV44.Channel,
                        out var tag))
                        Dispatch(owner,tag);
                    else if(line.Contains("\"event\":\"message\"",
                        StringComparison.Ordinal))
                        Log("Release event ignored: different channel, prior release, or malformed event.");
'@ 'diagnostic for missed event channel/version'
Swap $worker @'
        lastNotifiedTagV132=tag;
        // Notification is best-effort. Failure cannot suppress verified install.
'@ @'
        lastNotifiedTagV132=tag;
        // NotifyIcon balloons can be hidden by Windows notification settings.
        // This app-owned, non-modal WPF notice is visible even when the OS
        // suppresses system notifications. It never delays installation.
        DeskSpaceReleaseNoticeV137.Show(channel,tag);
        DeskSpaceUpdateProgress.Publish("Verified update "+tag,0);
        // Notification is best-effort. Failure cannot suppress verified install.
'@ 'in-app notice independent of Windows notification center'
Swap $worker @'
            Log("New release notification shown: "+tag+" channel "+channel);
'@ @'
            Log("New release notice requested (WPF + Windows balloon): "+tag+
                " channel "+channel+". Windows may suppress balloons.");
'@ 'accurate notification log wording'
Write-Host 'R137: recover recently cached release events, log rejected/received notifications, rate-bound untrusted hints, and show app-owned nonmodal verified release notice before automatic installation.'

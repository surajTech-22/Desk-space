$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$updater="$r/DeskSpacePublicUpdaterV44.cs"
Copy-Item -LiteralPath 'r133/DeskSpaceReleasePushV133.cs' -Destination "$r/DeskSpaceReleasePushV133.cs" -Force
function Swap([string]$old,[string]$new,[string]$label){
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $s=[IO.File]::ReadAllText($script:updater).Replace($crlf,$lf)
 $a=$old.Replace($crlf,$lf);$b=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R133 $label: expected one insertion, got $n"}
 [IO.File]::WriteAllText($script:updater,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
Swap @'
        timer.Start();
        if(owner.IsLoaded)Schedule();
'@ @'
        timer.Start();
        // Subscribe immediately, independently of periodic checks.
        DeskSpaceReleasePushV133.Start(owner);
        if(owner.IsLoaded)Schedule();
'@ 'start release event listener from existing updater host'
Swap @'
    public static async Task CheckAsync(Window owner,bool automatic=false)
'@ @'
    public static async Task CheckAsync(Window owner,bool automatic=false,
        bool releaseEvent=false)
'@ 'explicit event-triggered check path'
Swap @'
                if(automatic && now<next)return;
'@ @'
                // Event-driven checks bypass ONLY the time-based spacing.
                // They still use the canonical release tag, official assets,
                // mandatory SHA-256 and independent installer supervisor.
                if(automatic && !releaseEvent && now<next)return;
'@ 'stable event bypass of periodic check interval'
Swap @'
            if(DeskSpaceUpdateRateV127.IsDeferred(Local,channel,now,
                automatic,out var resumedAt))
'@ @'
            if(DeskSpaceUpdateRateV127.IsDeferred(Local,channel,now,
                automatic && !releaseEvent,out var resumedAt))
'@ 'tester event bypasses regular spacing but respects API 403/429 backoff'
Write-Host 'R133 release-event receiver installed. Github.com release and checksum verification unchanged, anonymous REST rate backoff retained, periodic recovery retained.'
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$file='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$label) {
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R138 $($label) expected one anchor, got $n"}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
        timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(5)};
'@ @'
        // Non-API GitHub releases/latest check is a recovery route when
        // ntfy.sh cannot deliver a push event. This MUST NOT use GitHub REST
        // API at this frequency. Event-driven detection remains immediate.
        timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(1)};
'@ 'one minute non-API stable recovery heartbeat'
Swap @'
                    DeskSpaceUpdateRateV127.PollFile(Local,"stable")).AddMinutes(5);
'@ @'
                    DeskSpaceUpdateRateV127.PollFile(Local,"stable")).AddMinutes(1);
'@ 'persist 1m stable check throttle across restarts'
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R138: stable uses immediate release stream plus 1-minute trusted public release redirect recovery; tester still respects its existing rate-limit rules; installer SHA256 and update supervisor unmodified.'
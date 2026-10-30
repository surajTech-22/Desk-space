$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r190/DeskSpaceMonitorDockV190.cs' -Destination "$root/DeskSpaceMonitorDockV190.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='        DeskSpaceMonitorFenceV153.TrackPopup(host,popup);'
$new=@'
        // The previous bottom placement was relative to the virtual desktop
        // and WPF could flip/clip the dock onto the adjacent monitor.
        // Keep independent dock HWNDs on their OWN monitor, with a scrollable
        // viewport. Staged content remains shared across all dock views.
        DeskSpaceMonitorDockV190.Attach(host,popup,panel);
        DeskSpaceMonitorFenceV153.TrackPopup(host,popup);
'@
$old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R190 dock hookup requires one existing popup fence, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R190 dock: monitor-aware WPF Custom positioning, per-widget scroll viewport, existing native popup fence retained.'

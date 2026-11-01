$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r239/DeskSpaceMonitorDiagnosticsV239.cs' -Destination "$root/DeskSpaceMonitorDiagnosticsV239.cs" -Force
$p="$root/DeskSpaceFeatureHubV199.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
        Section("Health and recovery");
        Add("Component status",
'@.Replace($crlf,$lf)
$new=@'
        Section("Health and recovery");
        Add("Monitor diagnostics",
            "See connected monitors, coordinates, Windows-reported DPI and per-screen Deck positions.",
            ()=>DeskSpaceMonitorDiagnosticsV239.Show(this));
        Add("Component status",
'@.Replace($crlf,$lf)
$count=([regex]::Matches($s,[regex]::Escape($old))).Count
if($count -ne 1){throw ("R239 feature insertion requires exact one anchor, got "+$count)}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R239: on-demand per-monitor diagnostics exposed under Features > Health and recovery.'

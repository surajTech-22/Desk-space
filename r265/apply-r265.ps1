$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r265/DeskSpaceDeckSaveGuardV265.cs' -Destination "$root/DeskSpaceDeckSaveGuardV265.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[int]$count,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $found=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($found -ne $count){throw ("R265 "+$label+": expected "+$count+" anchors, got "+$found)}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),
     [Text.UTF8Encoding]::new($false))
}
# R235's OS mutex remains the authority for serializing two widget processes.
# A 2-second wait inside PreviewMouseLeftButtonUp can freeze the WPF UI.
# A brief contention is instead handled as a recoverable failed save.
Swap "$root/DeskSpaceDeckOffsetV228.cs" @'
crossProcess.WaitOne(TimeSpan.FromSeconds(2));
'@ @'
crossProcess.WaitOne(TimeSpan.FromMilliseconds(250));
'@ 1 'bounded WPF UI mutex contention'
# Preserve the actual calls to Save(Device(),shift) for existing R228/R234
# safety checks while intercepting expected persistence exceptions.
Swap "$root/DeskSpaceMonitorDockV190.cs" @'
DeskSpaceDeckOffsetV228.Save(Device(),shift);
'@ @'
DeskSpaceDeckSaveGuardV265.TrySave(() => DeskSpaceDeckOffsetV228.Save(Device(),shift));
'@ 3 'protect both drag completion paths and this-monitor-only reset'
# R235's historical static regression must now expect the intentionally
# shortened bounded wait. Preserve its other mutex/atomic-file assertions.
Swap 'r235/test-r235.ps1' @'
crossProcess.WaitOne(TimeSpan.FromSeconds(2));
'@ @'
crossProcess.WaitOne(TimeSpan.FromMilliseconds(250));
'@ 1 'update prior mutex timeout regression'

Write-Host 'R265: bounded cross-process save contention; WPF Deck drag/reset no longer crash on expected storage errors.'

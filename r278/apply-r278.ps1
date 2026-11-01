$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$legacy=[IO.File]::ReadAllText("$root/DeskSpaceMonitorSessionsV154.cs")
foreach($gate in @(
 'private static readonly object FileGate=new();',
 'private static List<SavedPosition> ReadAll()',
 'return !s.Primary;'
)){
 if(!$legacy.Contains($gate)){throw ('R278 source baseline mismatch: '+$gate)}
}
Copy-Item -LiteralPath 'r278/DeskSpaceMonitorPositionStoreV278.cs' -Destination "$root/DeskSpaceMonitorPositionStoreV278.cs" -Force
Copy-Item -LiteralPath 'r278/DeskSpaceMonitorSessionsV278.cs' -Destination "$root/DeskSpaceMonitorSessionsV154.cs" -Force
Write-Host 'R278 P0: legacy layout format retained, cross-process atomic save and fail-closed parser installed.'

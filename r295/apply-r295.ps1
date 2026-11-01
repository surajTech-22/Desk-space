$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
if(!(Test-Path "$root/DeskSpaceMonitorDockV190.cs")){throw 'Missing consolidated Deck implementation'}
Copy-Item 'r295/DeskSpaceMonitorDockV295.cs' "$root/DeskSpaceMonitorDockV190.cs" -Force
$s=Get-Content "$root/DeskSpaceMonitorDockV190.cs" -Raw
foreach($token in @('AttachNativeObserverV295(', 'DetachNativeObserverV295()',
 'WindowPosChangedV295=0x0047', 'DpiChangedV295=0x02e0',
 'src.AddHook(nativeMoveHook);', 'RepairLater();',
 'NativeCorrectionV292(', 'FitBelowV292(')){
 if(!$s.Contains($token)){throw ("R295 Deck collision watchdog missing: "+$token)}
}
Write-Host 'R295 installed: event-based native HWND overlap recovery, no polling.'

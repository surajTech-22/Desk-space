$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
if(!(Test-Path "$root/DeskSpaceMonitorDockV190.cs") -or
   !(Test-Path "$root/DeskSpaceDropShelfV15.cs")){
 throw 'R292 canonical Windows DeskSpace source not staged'
}
Copy-Item -LiteralPath 'r292/DeskSpaceMonitorDockV292.cs' -Destination "$root/DeskSpaceMonitorDockV190.cs" -Force
Copy-Item -LiteralPath 'r292/DeskSpaceDropShelfV292.cs' -Destination "$root/DeskSpaceDropShelfV15.cs" -Force
$code=Get-Content "$root/DeskSpaceMonitorDockV190.cs" -Raw
$shelf=Get-Content "$root/DeskSpaceDropShelfV15.cs" -Raw
foreach($tag in @('FitBelowV292(', 'NativeCorrectionV292(', 'RepairVisible()',
   'SetWindowPos(source.Handle,IntPtr.Zero,', 'border.SizeChanged+=(_,_)=>RepairLater();',
   'popup.IsOpen=false;')){
 if(!$code.Contains($tag)){throw ('R292 native Deck collision guard missing '+$tag)}
}
if(!$shelf.Contains('VerticalOffset=0,StaysOpen=true,') -or
   !$shelf.Contains('if(popup.VerticalOffset!=0)popup.VerticalOffset=0;')){
 throw 'R292 legacy media vertical displacement must be removed'
}
Write-Host 'R292: apply native popup HWND geometry/resize/monitor collision guard and disable vertical Media Deck offsets.'

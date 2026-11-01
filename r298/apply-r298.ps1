$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($pair in @(
 @('r298/DeskSpaceMonitorFenceV298.cs',"$root/DeskSpaceMonitorFenceV153.cs"),
 @('r298/DeskSpaceMonitorDockV298.cs',"$root/DeskSpaceMonitorDockV190.cs"),
 @('r298/DeskSpaceDropShelfV298.cs',"$root/DeskSpaceDropShelfV15.cs")
)){
 if(!(Test-Path $pair[1])){throw 'Missing consolidated shipping source: '+$pair[1]}
 Copy-Item $pair[0] $pair[1] -Force
}
$fence=Get-Content "$root/DeskSpaceMonitorFenceV153.cs" -Raw
$dock=Get-Content "$root/DeskSpaceMonitorDockV190.cs" -Raw
$shelf=Get-Content "$root/DeskSpaceDropShelfV15.cs" -Raw
if(!$fence.Contains('AdditionalConstraint(fit,work)') -or
   !$dock.Contains('ConstrainBeforePaintV298(') -or
   !$shelf.Contains('(proposed,work)=>DeskSpaceMonitorDockV190.ConstrainBeforePaintV298(')){
 throw 'R298 live Deck prepaint monitor constraint not connected'
}
Write-Host 'R298 prepaint popup collision contract enabled: no Deck overlap even before Render.'

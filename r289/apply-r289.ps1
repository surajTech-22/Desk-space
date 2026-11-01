$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($pair in @(
    @('r289/DeskSpaceMonitorDockV289.cs',"$root/DeskSpaceMonitorDockV190.cs"),
    @('r289/DeskSpaceSmoothScrollV289.cs',"$root/DeskSpaceSmoothScrollV41.cs")
)){
    if(!(Test-Path $pair[1])){throw "R289 missing shipping source: $($pair[1])"}
    Copy-Item -LiteralPath $pair[0] -Destination $pair[1] -Force
}
$dock=Get-Content "$root/DeskSpaceMonitorDockV190.cs" -Raw
$wheel=Get-Content "$root/DeskSpaceSmoothScrollV41.cs" -Raw
foreach($must in @('PlanBelowV289','CalculateBelowV289','RepairBelowV289',
    'EnsureBelowV289','HasDeckDragIntentV289','CanContentScroll=false',
    'var result=CalculateBelowV289(ownerBounds,screen,'))
{
    if(!$dock.Contains($must)){throw "R289 below-only Deck integration absent: $must"}
}
foreach($must in @('PixelsForWheelV289','HorizontalGoal',
    'bool horizontal=(mods&ModifierKeys.Shift)!=0'))
{
    if(!$wheel.Contains($must)){throw "R289 laptop touchpad integration absent: $must"}
}
Write-Host 'R289: below-only scrollable Deck and fractional laptop touchpad integration applied.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($pair in @(
 @('r290/DeskSpaceMiddleScrollV290.cs',"$root/DeskSpaceMiddleScrollV63.cs"),
 @('r290/DeskSpaceSmoothScrollV290.cs',"$root/DeskSpaceSmoothScrollV41.cs")
)){
 if(!(Test-Path -LiteralPath $pair[1])){throw "R290 missing original source: $($pair[1])"}
 Copy-Item -LiteralPath $pair[0] -Destination $pair[1] -Force
}
$menu=Get-Content "$root/DeskSpaceMiddleScrollV63.cs" -Raw
$smooth=Get-Content "$root/DeskSpaceSmoothScrollV41.cs" -Raw
if(!$menu.Contains('private readonly PrecisionWheelV290 precisionWheel=new();') -or
   !$menu.Contains('int amount=precisionWheel.Step(e.Delta);') -or
   !$menu.Contains('if(remainder!=0 && Math.Sign(remainder)!=Math.Sign(delta))')){
 throw 'R290 native precision touchpad menu scroll not wired into controller'
}
if(!$smooth.Contains('if(target.CanContentScroll && !horizontal)return;')){
 throw 'R290 virtualized scrolling must keep native WPF wheel behavior'
}
Write-Host 'R290: high-resolution touchpad scrolling for sub-donuts; native virtualized lists and modifier gestures preserved.'

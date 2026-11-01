$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($pair in @(
 @('r290/DeskSpaceMiddleScrollV290.cs',"$root/DeskSpaceMiddleScrollV63.cs"),
 @('r290/DeskSpaceSmoothScrollV290.cs',"$root/DeskSpaceSmoothScrollV41.cs"),
 @('r290/DeskSpaceMonitorDockV290.cs',"$root/DeskSpaceMonitorDockV190.cs")
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
$dock=Get-Content "$root/DeskSpaceMonitorDockV190.cs" -Raw
if(!$dock.Contains('shift=new DeskSpaceDeckOffsetV228.Offset(saved.X,0);') -or
   !$dock.Contains('startOffset.X+now.X-startCursor.X,0)') -or
   !$dock.Contains('Math.Abs(dx)>=8;')){
 throw 'R290 Deck must reject vertical drag and legacy saved Y offsets'
}
Write-Host 'R290: high-resolution touchpad scrolling for sub-donuts; native virtualized lists and modifier gestures preserved.'

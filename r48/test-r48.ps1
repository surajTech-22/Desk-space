$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$geometry=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceSubArcGeometryV48.cs'))
$wheel=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelWheelV42.cs'))
$info=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceInformationV36.cs'))
foreach($t in @('CX=325,CY=325','HostOuter=195',
    'RootInner=HostOuter+Gap','BetweenBands=2',
    'Half(int count)=>count<=1?17:Math.Min(57,12.0*count)',
    'Sector(int depth,double from,double to)',
    'DeskSpaceDonutGeometryV43.PopupOffset(owner,CX,CY',
    'FitAngle(Window owner,int depth','Rect Bounds(')){
    if(!$geometry.Contains($t)){throw "R48 missing unified circular arc geometry $t"}
}
foreach($t in @('rootStartAngle=ArcCenter-half',
    'rootStep=2*half/count',
    'DeskSpaceSubArcGeometryV48.Sector(',
    'DeskSpaceSubArcGeometryV48.Clip(',
    'DeskSpaceSubArcGeometryV48.Bounds(',
    'DeskSpaceSubArcGeometryV48.PopupOffset(owner,rootBounds)',
    'DeskSpaceSubArcGeometryV48.PopupOffset(owner,ring.Bounds)',
    'ring.StartAngle=centre-half','ring.StepAngle=2*half/count',
    'ring.Bounds.Left','ring.Bounds.Top',
    'CloseFrom(ring.Depth+1)',
    'var ring=new ChildRing(depth,group,angle,owner)')){
    if(!$wheel.Contains($t)){throw "R48 disconnected or missing Excel submenu path $t"}
}
foreach($wrong in @('PlaceWheel(','MiniOuter=77',
    'rootCenterX=root.X','ring.CenterX','KeepConcentricRingOnScreen(')){
    if($wheel.Contains($wrong)){throw "R48 detached-wheel regression $wrong"}
}
if(!$info.Contains('double halfSweep=count==1?17:Math.Min(57,12.0*count)')){
    throw 'R48 must match original Information circle sector sweep'
}
if(!$wheel.Contains('shelf.VerticalOffset=top+')){
    throw 'R48 shelf is not anchored outside shared circle'
}
Write-Host 'R48 PASS: Excel submenu mirrors Information partial-ring sector geometry, reuses one parent centre, screens against detached popups, shelf below wheel.'

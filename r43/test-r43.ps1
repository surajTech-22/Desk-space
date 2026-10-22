$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$geometry=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceDonutGeometryV43.cs'))
$wheel=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceExcelWheelV42.cs'))
$info=[IO.File]::ReadAllText((Join-Path $root 'DeskSpaceInformationV36.cs'))
$hostSource=[IO.File]::ReadAllText((Join-Path $root 'WidgetHost.cs'))
foreach($t in @('HostDiameter=420','ChildInner=207','ChildOuter=260',
'OuterSectorCenter','PopupOffset(Window owner')){
 if(!$geometry.Contains($t)){throw "R43 missing shared geometry: $t"}
}
foreach($t in @('Outer=260,Inner=207','OuterSectorCenter(6)',
'DeskSpaceDonutGeometryV43.Scale(owner)',
'DeskSpaceSubArcGeometryV48.PopupOffset(owner,rootBounds)',
'HideArc(); // Parent collapsed')){
 if(!$wheel.Contains($t)){throw "R43 bad Excel arc: $t"}
}
foreach($t in @('PositionArc();','ArcCenterX,ArcCenterY,-5,-6',
'HideArc(); // Prevent detached Information fragments')){
 if(!$info.Contains($t)){throw "R43 bad Information arc: $t"}
}
foreach($method in @('private void CollapseRing()','private void BuildCompact()')){
 $i=$hostSource.IndexOf($method,[StringComparison]::Ordinal)
 if($i -lt 0){throw "R43 missing lifecycle: $method"}
 $window=$hostSource.Substring($i,[Math]::Min(380,$hostSource.Length-$i))
 if(!$window.Contains('excelWheel?.HideArc();') -or
    !$window.Contains('informationOverlay.HideArc();')){
  throw "R43 child popups are not closed before $method"
 }
}
$rootWheel=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelWheelV42.cs')
foreach($part in @('DeskSpaceSubArcGeometryV48.Half(count)',
    'DeskSpaceSubArcGeometryV48.Sector(',
    'DeskSpaceSubArcGeometryV48.PopupOffset(owner,rootBounds)',
    'DeskSpaceSubArcGeometryV48.PopupOffset(owner,ring.Bounds)',
    'rootStep=2*half/count',
    'ring.StartAngle=centre-half',
    'ring.StepAngle=2*half/count')){
    if(!$rootWheel.Contains($part)){
        throw "R48 Information-reference radial submenu missing: $part"
    }
}
if($rootWheel.Contains('MiniOuter=77') -or
   $rootWheel.Contains('KeepConcentricRingOnScreen(')){
    throw 'R48 legacy detached mini-bubbles or fullscreen band returned'
}
Write-Host 'R48 PASS: Information-reference attached partial arcs, one shared circle centre and hierarchy teardown.'

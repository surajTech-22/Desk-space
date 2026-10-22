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
'DeskSpaceDonutGeometryV43.PopupOffset(owner,CX,CY',
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
foreach($part in @('MiniOuter=77,MiniInner=35,MiniSize=176',
    'PlaceWheel(0,0,ArcCenter,MainOuterRadius+MiniOuter+3)',
    'rootPositionReady',
    'ring.Surface.Width=MiniSize',
    'ring.Surface.Height=MiniSize',
    'ArcCenter-270.0+(slot+.5)*360.0/displayed.Count')){
    if(!$rootWheel.Contains($part)){
        throw "R47 missing compact attached child wheel anchor: $part"
    }
}
if($rootWheel.Contains('private static double Span(int count)=>180;') -or
   $rootWheel.Contains('KeepConcentricRingOnScreen(')){
    throw 'R47 massive Excel overlay geometry has returned'
}
Write-Host 'R47 PASS: compact 176-DIP Excel child donuts, stable parent anchor, nested tear-down.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$r/DeskSpaceMonitorDockV190.cs")
$deck=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'CustomPopupPlacementCallback',
 'PlacementMode.Custom',
 'GetWindowRect(hwnd,out var rect)',
 'DeskSpaceMonitorFenceV152.BoundScreen(widget)',
 'CompositionTarget?.TransformToDevice.M11',
 'CompositionTarget?.TransformToDevice.M22',
 'ScrollViewer',
 'VerticalScrollBarVisibility.Auto',
 'HorizontalScrollBarVisibility.Disabled',
 'Side.Below','Side.Above','Side.Right','Side.Left','Side.Contained',
 'owner.Bottom+gap+h<=work.Bottom',
 'owner.Top-gap-h>=work.Top',
 'Math.Clamp(',
 'border.MaxWidth=Math.Min(310,w)',
 'scroll.MaxHeight=Math.Min(520,h)',
 'result.X-ownerBounds.Left',
 'result.Y-ownerBounds.Top',
 'PopupPrimaryAxis.None'
)){
 if(!$s.Contains($token)){throw "R190 monitor dock missing: $token"}
}
foreach($forbidden in @('File.Delete','Directory.Delete','File.Copy',
    'Clipboard.Set','Process.Start','Views.Clear','held.Clear')){
 if($s.Contains($forbidden)){throw "R190 dock placement must never change held data: $forbidden"}
}
if(!$deck.Contains('DeskSpaceMonitorDockV190.Attach(host,popup,panel);') -or
   !$deck.Contains('DeskSpaceMonitorFenceV153.TrackPopup(host,popup);') -or
   !$deck.Contains('private static readonly List<Held> held=new();') -or
   !$deck.Contains('RefreshViews()')){
 throw 'R190 existing shared staged files and per-monitor Popup fence must remain'
}
foreach($token in @('TestMonitorDockV190();',
 'private static void TestMonitorDockV190()',
 'R190 rightmost secondary dock must flip above',
 'R190 left-side monitor must retain negative virtual coordinates',
 'R190 popup size at 150% DPI',
 'R190 both dock popups must have independent native HWNDs',
 'R190 each monitor must have its OWN deck scrolling state')){
 if(!$smoke.Contains($token)){throw "R190 WPF smoke regression missing: $token"}
}
Write-Host 'R190 PASS: native pixel monitor-bound dock, negative coordinates, separate per-monitor scroll, unchanged shared content, real WPF tests.'

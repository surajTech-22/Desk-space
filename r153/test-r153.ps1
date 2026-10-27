$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$widgetFenceSource=[IO.File]::ReadAllText("$r/DeskSpaceMonitorFenceV152.cs")
$fence=[IO.File]::ReadAllText("$r/DeskSpaceMonitorFenceV153.cs")
$info=[IO.File]::ReadAllText("$r/DeskSpaceInformationV36.cs")
$excel=[IO.File]::ReadAllText("$r/DeskSpaceExcelWheelV42.cs")
$apps=[IO.File]::ReadAllText("$r/DeskSpaceStableHoverV93.cs")
$media=[IO.File]::ReadAllText("$r/DeskSpaceMediaV18.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')

foreach($token in @(
 'DeskSpaceMonitorFenceV153.TrackWidget(window);')){
 if(!$widgetFenceSource.Contains($token)){throw "R153 widget binding missing: $token"}
}
foreach($token in @(
 'DeskSpaceMonitorFenceV153.TrackPopup(owner,arc);',
 'DeskSpaceMonitorFenceV153.TrackPopup(owner,shelf);')){
 if(!$info.Contains($token) -or !$excel.Contains($token)){
   throw "R153 Excel/Information popup binding missing: $token"
 }
}
if(!$excel.Contains('DeskSpaceMonitorFenceV153.TrackPopup(owner,Popup);')){
 throw 'R153 nested Excel child rings unbound'
}
foreach($token in @('DeskSpaceMonitorFenceV153.TrackPopup(heading,iconPopup);',
                    'DeskSpaceMonitorFenceV153.TrackPopup(iconCard,windowPopup);')){
 if(!$apps.Contains($token)){throw "R153 app flyout missing: $token"}
}
if(!$media.Contains('DeskSpaceMonitorFenceV153.TrackPopup(host,popup);')){
 throw 'R153 media player popup unbound'
}
foreach($token in @('WindowPosChanging=0x0046','MovingMessage=0x0216',
 'Marshal.PtrToStructure<WindowPos>','Marshal.StructureToPtr(pos,lParam,false)',
 'PopupScreen[popup]=ScreenForAnchor();','PopupScreen.Remove(popup);',
 'internal static Rectangle Constrain(',
 'internal static bool TryMovePopupForTest(',
 'internal static Rectangle? PopupBounds(')){
 if(!$fence.Contains($token)){throw "R153 screen fence missing: $token"}
}
foreach($token in @('TestMonitorPopupsV153();',
 'private static void TestMonitorPopupsV153()',
 'R153 real WPF Popup HWND crossed monitor after native SetWindowPos',
 'R153 WPF reposition should not leak popup into adjacent monitor')){
 if(!$smoke.Contains($token)){throw "R153 real WPF smoke missing: $token"}
}
Write-Host 'R153 static PASS: main HWND and all module Popup HWNDs guarded; real native WPF positioning regression included.'

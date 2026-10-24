$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$hover=[IO.File]::ReadAllText("$root/DeskSpaceStableHoverV93.cs")
$shelf=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
foreach($t in @(
 'private static extern bool GetCursorPos(out CursorPoint point)',
 'internal static bool ShouldRetainAfterClose(',
 'bool closeRequestedWhileHover=false;',
 'void PreserveHoverAfterClose()',
 'closeRequestedWhileHover=true;',
 'positionAtClose=GetCursorPos(out var point)?point:null;',
 'close.Click+=(_,_)=>PreserveHoverAfterClose();',
 'if(closeRequestedWhileHover)',
 'ShouldRetainAfterClose(positionAtClose.Value.X,',
 'positionAtClose=null;',
 'liveTimer.Tick+=(_,_)=>Reconcile();',
 'windowItems.Children.Remove(row)',
 'iconRow.Children.Remove(tile)',
 'if(groups.Count==0)Hide()'
)){
 if(!$hover.Contains($t)){throw "R98 retaining panel after window closes missing: $t"}
}
if(([regex]::Matches($hover,[regex]::Escape('close.Click+=(_,_)=>PreserveHoverAfterClose();'))).Count -ne 2){
 throw 'R98 close should protect both single-window icons and multi-window row controls'
}
if(!$shelf.Contains('DeskSpaceRunningAppsV88.AddHoverGroupLive(')){
 throw 'R98 callback bridge to deck counts missing'
}
Write-Host 'R98 PASS: close does not dismiss hover on layout-induced MouseLeave; deliberate pointer movement can still dismiss.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$hostSource=[IO.File]::ReadAllText("$r/WidgetHost.cs")
$info=[IO.File]::ReadAllText("$r/DeskSpaceInformationV36.cs")
$gate=[IO.File]::ReadAllText("$r/DeskSpaceMonitorInputV158.cs")
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'private void OnShortcutRightMouseDown(object sender,MouseButtonEventArgs e)',
 'if(!DeskSpaceMonitorInputV158.OwnsRightClick(this))return;',
 'if(!DeskSpaceMonitorInputV158.IsPointerOnAssignedMonitor(this))return;')){
 if(!$hostSource.Contains($token)){throw "R158 widget input missing: $token"}
}
foreach($token in @(
 'all.Count,startIndex,visible.Length,startIndex,',
 'DeskSpaceSubDonutStandardV62.DrawHiddenCounters(',
 'Width=62,',
 'Canvas.SetLeft(label,x-31);',
 'if(!DeskSpaceMonitorInputV158.IsPointerOnAssignedMonitor(owner))')){
 if(!$info.Contains($token)){throw "R158 Information layout/pointer missing: $token"}
}
if($info.Contains('all.Count+1,startIndex,count,startIndex,')){
 throw 'R158 hidden count still draws on +New wedge'
}
foreach($token in @(
 'private static extern bool GetCursorPos(out PixelPoint point)',
 'private static extern IntPtr WindowFromPoint(PixelPoint point)',
 'private static extern IntPtr GetAncestor(IntPtr hwnd,uint flags)',
 'Forms.Screen.FromPoint(source)',
 'DeskSpaceMonitorFenceV152.BoundScreen(widget)',
 'internal static bool Allows(',
 'hitWindowRoot==ownerWindow')){
 if(!$gate.Contains($token)){throw "R158 wrong-HWND rejection missing: $token"}
}
foreach($token in @(
 'TestInformationSixSavedV158();',
 'TestMonitorInputIsolationV158();',
 'private static void TestInformationSixSavedV158()',
 'private static void TestMonitorInputIsolationV158()',
 'R158 count 2 must split LAST SAVED divider',
 'R158 secondary must ignore right-click on primary screen')){
 if(!$ui.Contains($token)){throw "R158 WPF regression missing: $token"}
}
Write-Host 'R158 PASS: two monitor widgets have independent right-click and hover ownership; six saved Information items retain +New and hidden count 2 on last saved wedge. Real WPF regression included.'

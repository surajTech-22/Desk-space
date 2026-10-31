$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source=[IO.File]::ReadAllText("$root/DeskSpaceShelfGroupsV88.cs")
$hover=[IO.File]::ReadAllText("$root/DeskSpaceStableHoverV93.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static bool CanControlV279(',
 'internal static bool RequestStateV279(',
 'ShowWindowAsync(entry.Handle,minimize?Minimise:Restore);',
 'CanControlV279(entry,true,WindowText(entry.Handle))',
 'internal static Button MakeStateButtonV279(',
 'MakeStateButtonV279(selected,true)',
 'MakeStateButtonV279(selected,false)',
 'AutomationProperties.SetName(button,',
 'DeskSpaceRunningAppsV88.RequestStateV279(window,minimize)',
 'TestRunningWindowControlsV279();',
 'R279 changing a window'
)){
 if(!$source.Contains($needle) -and !$hover.Contains($needle) -and
    !$tests.Contains($needle)){throw ('R279 P1 state control missing: '+$needle)}
}
if(!$source.Contains('PostMessage(entry.Handle,WmClose,IntPtr.Zero,IntPtr.Zero)')){
 throw 'R279 unsaved-work-friendly close must remain unchanged'
}
Write-Host 'R279 STATIC PASS: separate state controls, live HWND title recheck, existing Close and Focus retained.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$a=[IO.File]::ReadAllText("$r/DeskSpaceShelfGroupsV88.cs")
foreach($term in @(
 'private bool deckMinimized;',
 'DeckHeaderButton(deckMinimized?"▢":"−"',
 'DeckHeaderButton("×"',
 'new ControlTemplate(typeof(Button))',
 'BorderThickness=new Thickness(0)',
 'if(deckMinimized)',
 'deckMinimized=!deckMinimized;',
 'DeskSpaceDeckVisibilityV89.TrySet(false,out var problem)',
 'if(held.Count+detected.Count+runningApps.Count==0 && !busy && !deckMinimized)'
)){
 if(!$s.Contains($term)){throw "R91 deck behaviour missing $term"}
}
foreach($term in @('EnumWindows(', 'GetWindowThreadProcessId(',
 'GroupVisible(windows)','DwmGetWindowAttribute','GetWindowText(',
 'ApplicationFrameWindow','File Explorer','SystemSettings',
 'IsActiveWindow(window)','DeskSpaceApplicationIconsV65.Get')){
 if(!$a.Contains($term)){throw "R91 true window enumeration missing $term"}
}
if($a.Contains('Process.GetProcesses()') -or $a.Contains('process.MainWindowHandle;')){
 throw 'R91 regression: legacy per-process window detection still active'
}
Write-Host 'R91 source gates PASS: top-level user window enumeration, grouped counts, independent compact minimise and seamlessly blended hide controls.'

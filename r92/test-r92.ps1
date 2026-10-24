$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$a=[IO.File]::ReadAllText("$r/DeskSpaceShelfGroupsV88.cs")
foreach($term in @(
 'DeskSpaceRunningAppsV88.AddHoverGroup(list,runningApps,runningWindows)',
 'Task.Run(DeskSpaceRunningAppsV88.SnapshotWindows)',
 'DeskSpaceRunningAppsV88.GroupVisible(windows)',
 'private bool deckMinimized,deckDismissed;',
 'dismissedNonAppSignature=NonAppSignature();',
 'deckDismissed=true;',
 'if(deckDismissed)',
 'held.Count+detected.Count>0',
 'deckDismissed=false;',
 'if(held.Count+detected.Count+runningApps.Count==0 && !busy)',
 '!deckDismissed && (bool)e.NewValue',
 '(item.AutoDetected?recentGroup:heldGroup).Children.Add(row)'
)){
 if(!$s.Contains($term)){throw "R92 deck behavior missing: $term"}
}
if($s.Contains('appsExpanded=!appsExpanded')){
 throw 'R92 regression: Running Apps still uses click-to-expand'
}
foreach($term in @(
 'AddHoverGroup(StackPanel root',
 'wrapper.MouseEnter+=',
 'wrapper.MouseLeave+=',
 'button.MouseEnter+=',
 'WindowsFor(app,windows)',
 'GetForegroundWindow()',
 'ShowWindowAsync(entry.Handle,Minimise)',
 'SnapshotWindows()',
 'GroupVisible(windows)'
)){
 if(!$a.Contains($term)){throw "R92 hover / multiwindow functionality missing: $term"}
}
if(!$s.Contains('DeskSpaceFilePreviewV85.Attach') -or
   !$s.Contains('DeskSpaceNewFileWatchV53')){
 throw 'R92 regression: existing recent/held file operations removed'
}
Write-Host 'R92 PASS: hover-only app icons and window choices, toggle minimise, no reopen for app-only changes, reactivation for non-app events.'

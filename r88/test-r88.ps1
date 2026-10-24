$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$g=[IO.File]::ReadAllText("$r/DeskSpaceShelfGroupsV88.cs")
foreach($term in @('Running Apps','Held Files','Recent Files',
 'runningAppsTimer.Stop()','heldExpanded','recentExpanded',
 'Task.Run(DeskSpaceRunningAppsV88.Snapshot)',
 '(item.AutoDetected?recentGroup:heldGroup).Children.Add(row)',
 'DeskSpaceRunningAppsV88.Fill'))
{if(!$s.Contains($term)){throw "R88 missing live shelf integration: $term"}}
foreach($term in @('Process.GetProcesses()','MainWindowHandle',
 'DeskSpaceApplicationIconsV65.Get','SetForegroundWindow','IsWindow',
 'if(count<=0)return content'))
{if(!$g.Contains($term)){throw "R88 missing group/app behavior: $term"}}
if(!$s.Contains('DeskSpaceFilePreviewV85.Attach'))
{throw 'R88 regression: existing recent-file preview lost'}
if(!$s.Contains('DeskSpaceNewFileWatchV53'))
{throw 'R88 regression: original file watcher lost'}
Write-Host 'R88 PREFLIGHT PASS: existing shelf, event-driven files and previews retained, compact independent app/file groups wired.'

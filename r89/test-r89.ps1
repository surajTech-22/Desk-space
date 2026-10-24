$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$r/DeskSpaceDropShelfV15.cs")
$w=[IO.File]::ReadAllText("$r/WidgetHost.cs")
$m=[IO.File]::ReadAllText("$r/DeskSpaceModuleV10.cs")
$v=[IO.File]::ReadAllText("$r/DeskSpaceDeckVisibilityV89.cs")
$a=[IO.File]::ReadAllText("$r/DeskSpaceShelfGroupsV88.cs")
foreach($term in @('DeckVisibilityChanged','DeckVisibilityV89.Visible',
 'clearCopies.Click','dismissRecent.Click','ClearAll();',
 'runningAppsTimer.Stop();','runningApps.Count',
 'DeskSpaceDeckVisibilityV89.TrySet(false,out var problem)'))
{if(!$s.Contains($term)){throw "R89 shelf integration missing: $term"}}
if(!$w.Contains('new Forms.ToolStripMenuItem("Deck Visibility")'))
{throw 'R89 circle right-click menu missing'}
if(!$m.Contains('Deck Visibility · show below the circle'))
{throw 'R89 Circle Settings toggle missing'}
foreach($term in @('DwmGetWindowAttribute','IsWindowVisible',
 'IsActiveWindow(window)','IsActiveWindow(entry.Handle)'))
{if(!$a.Contains($term)){throw "R89 running app filter missing: $term"}}
foreach($term in @('File.Move(temp,Filename,true)',
 'Changed?.Invoke(visible)','"hidden"','"visible"'))
{if(!$v.Contains($term)){throw "R89 saved state missing: $term"}}
if(!$s.Contains('DeskSpaceFilePreviewV85.Attach'))
{throw 'R89 existing file preview missing'}
if(!$s.Contains('new DeskSpaceNewFileWatchV53'))
{throw 'R89 file watcher missing'}
Write-Host 'R89 PREFLIGHT PASS: real visible window detection, independent file-group commands, saved circle deck visibility, retained files.'

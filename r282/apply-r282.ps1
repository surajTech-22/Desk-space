$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$apps=[IO.File]::ReadAllText("$root/DeskSpaceShelfGroupsV88.cs")
$hover=[IO.File]::ReadAllText("$root/DeskSpaceStableHoverV93.cs")
$dock=[IO.File]::ReadAllText("$root/DeskSpaceMonitorDockV190.cs")
foreach($x in @(
 'internal static bool CanControlV279(',
 'internal static IReadOnlyList<Entry> SnapshotWindows()',
 'windows.GroupBy(x=>x.Handle)',
 'internal static void Focus(Entry entry)'
)){if(!$apps.Contains($x)){throw ('R282 Running Apps baseline drift: '+$x)}}
foreach($x in @(
 'Content=selected.WindowTitle,FontSize=11,',
 'internal static Button MakeStateButtonV279('
)){if(!$hover.Contains($x)){throw ('R282 app flyout baseline drift: '+$x)}}
foreach($x in @(
 'internal static PlacementResult Calculate(',
 'internal static void Attach(Window owner,Popup popup,Border border)',
 'DeskSpaceDeckOffsetV228.Offset shift=new(0,0);',
 'scroll.MaxHeight=Math.Min(520,h);'
)){if(!$dock.Contains($x)){throw ('R282 monitor dock baseline drift: '+$x)}}
Copy-Item -LiteralPath 'r282/DeskSpaceShelfGroupsV282.cs' -Destination "$root/DeskSpaceShelfGroupsV88.cs" -Force
Copy-Item -LiteralPath 'r282/DeskSpaceStableHoverV282.cs' -Destination "$root/DeskSpaceStableHoverV93.cs" -Force
Copy-Item -LiteralPath 'r282/DeskSpaceMonitorDockV282.cs' -Destination "$root/DeskSpaceMonitorDockV190.cs" -Force
Write-Host 'R282: count distinct executables and entitled taskbar windows, preserve same-app grouping, prevent Deck-clock overlap.'

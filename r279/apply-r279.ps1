$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source=[IO.File]::ReadAllText("$root/DeskSpaceShelfGroupsV88.cs")
$hover=[IO.File]::ReadAllText("$root/DeskSpaceStableHoverV93.cs")
if(!$source.Contains('internal static void Focus(Entry entry)') -or
   !$source.Contains('internal static bool RequestClose(Entry entry)') -or
   !$hover.Contains('internal static Button MakeCloseButton(') -or
   !$hover.Contains('row.Children.Add(close);')){
 throw 'R279 P1 Running Apps legacy behavior unexpectedly changed'
}
Copy-Item -LiteralPath 'r279/DeskSpaceShelfGroupsV279.cs' -Destination "$root/DeskSpaceShelfGroupsV88.cs" -Force
Copy-Item -LiteralPath 'r279/DeskSpaceStableHoverV279.cs' -Destination "$root/DeskSpaceStableHoverV93.cs" -Force
Write-Host 'R279: Running Apps per-window Minimize/Restore with exact title handle guards and existing close preserved.'

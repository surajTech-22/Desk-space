$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$r/DeskSpaceShelfGroupsV88.cs")
$h=[IO.File]::ReadAllText("$r/DeskSpaceStableHoverV93.cs")
if(!$s.Contains('DeskSpaceStableHoverV93.Add(root,apps,windows)')){
 throw 'R93 deck not routed into external flyout'
}
foreach($part in @(
 'new Popup{','Placement=PlacementMode.Right',
 'PlacementTarget=heading','windowPopup.PlacementTarget=appButton',
 'MaxHeight=292','MaxHeight=230','heading.Tag=iconPopup',
 'heading.Unloaded+=','heading.IsVisibleChanged+=',
 'Interval=TimeSpan.FromMilliseconds(340)',
 'closeDelay.Tick+=','iconCard.MouseLeave+=','windowCard.MouseEnter+=',
 'DeskSpaceRunningAppsV88.WindowsFor',
 'DeskSpaceRunningAppsV88.Focus(selected)')){
 if(!$h.Contains($part)){throw "R93 missing behavior: $part"}
}
if($h.Contains('root.Children.Add(windowItems)') -or
   $h.Contains('detail.Visibility=Visibility.Visible')){
 throw 'R93 still expands the deck internally'
}
Write-Host 'R93 PASS: independent flyouts, fixed deck layout, mouse gap debounce, popup cleanup and app/window selection.'

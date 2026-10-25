$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$hostFile='DeskSpace_Windows_R1/DeskSpace/DeskSpaceStableHoverV93.cs'
$s=[IO.File]::ReadAllText($hostFile)
$wpf=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'ChooseReachableFlyoutV118(',
 'WindowFlyoutReachableV118(',
 'bool preferAbove=tileY<=iconCard.ActualHeight/2;',
 'bool deckToRight=true;',
 'placement=ChooseReachableFlyoutV118(',
 'new CustomPopupPlacement(WindowFlyoutReachableV118(',
 'var iconRow=new Grid',
 'Grid.SetRow(tile,slot.Row)',
 'Grid.SetColumn(tile,slot.Column)'
)){
 if(!$s.Contains($needle)){throw "R118 missing live UI integration: $needle"}
}
foreach($needle in @(
 'TestReachableFlyoutV118();',
 'R118 Excel top-row popup must open ABOVE',
 'R118 bottom-row multi-window app opens BELOW',
 'R118 first-row flyout must touch top edge'
)){
 if(!$wpf.Contains($needle)){throw "R118 missing WPF regression: $needle"}
}
Write-Host 'R118 static source tests PASS; compiled WPF tests will verify physical placements.'

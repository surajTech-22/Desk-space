param([string]$Source='src/DeskSpace/DeskSpaceDropShelfV15.cs')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(!(Test-Path $Source)){throw "Deck implementation missing at $Source"}
$s=[IO.File]::ReadAllText((Resolve-Path $Source))
foreach($forbidden in @('deckMinimized','Minimise deck to header','Expand deck','title.Children.Add(minimise);')){
 if($s.Contains($forbidden)){throw "Deck still has forbidden collapsed state/control: $forbidden"}
}
foreach($required in @(
 'DeskSpaceRunningAppsV88.AddHoverGroupLive(',
 'DeskSpaceShelfGroupsV88.Add(list,"Held Files"',
 'DeskSpaceShelfGroupsV88.Add(list,"Recent Files"',
 'DeskSpaceDeckVisibilityV304.Attach(host,popup,panel);',
 'DeskSpaceMonitorDockV190.ConstrainBeforePaintV298('
)){
 if(!$s.Contains($required)){throw "R306 missing expanded content or monitor Deck guard: $required"}
}
if(([regex]::Matches($s,'DeckHeaderButton[(]')).Count -ne 3){
 # One declaration plus exactly TWO controls (preview and dismiss).
 throw 'R306 Deck should have only the close and preview header buttons'
}
Write-Host 'R306 PASS: no collapse state/button; Running Apps, Held and Recent content permanently rendered in the live full Deck.'

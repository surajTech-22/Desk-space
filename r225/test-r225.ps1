$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$hostSourceV225=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$search=[IO.File]::ReadAllText("$root/DeskSpaceSearchV11.cs")
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$model=[IO.File]::ReadAllText("$root/DeskSpaceSearchDeckLayoutV225.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($part in @(
 'SearchWidth(limits.Width)',
 'SearchWidth(ModuleArea().Width)',
 'attachedShelf?.SetSearchActiveV225(name=="Search");',
 'attachedShelf?.SetSearchActiveV225(false);'
)){
 if(!$hostSourceV225.Contains($part)){throw ("R225 Search lifecycle not connected: "+$part)}
}
if($hostSourceV225.Contains('Width=Math.Min(limits.Width,CompactDiameter);Height=48;')){
 throw 'R225 Search still shrinks to tiny circle diameter'
}
if(!$search.Contains('GridLength(42)') -or
   !$search.Contains('GridLength(30)')){
 throw 'R225 Search icon and Close columns overlap text field'
}
foreach($part in @(
 'private bool searchActiveV225;',
 'internal void SetSearchActiveV225(bool active)',
 'if(searchActiveV225)',
 'deckPreview?.Close();',
 'popup.IsOpen=false;',
 'DeskSpaceSearchDeckLayoutV225.ShowDeck(',
 'searchActiveV225,contents')){
 if(!$deck.Contains($part)){throw ("R225 Deck overlay suppression missing: "+$part)}
}
if(!$model.Contains('enabled && hostVisible && !searchActive && hasItems')){
 throw 'R225 search must hide Deck only while in foreground'
}
if(!$smoke.Contains('TestSearchDeckLayoutV225();')){
 throw 'R225 native Search/Deck acceptance missing'
}
Write-Host 'R225 STATIC PASS: full-width Search, fixed action icon column, reversible Deck popup ownership.'

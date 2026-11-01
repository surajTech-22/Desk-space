param([string]$Source='src/DeskSpace/DeskSpaceDropShelfV15.cs')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(!(Test-Path -LiteralPath $Source)){throw "Missing R308 Deck source $Source"}
$s=[IO.File]::ReadAllText((Resolve-Path $Source))
foreach($required in @(
 'private bool deckMinimized,deckDismissed,appRenderPending;',
 'var minimise=DeckHeaderButton(deckMinimized?',
 'deckMinimized?"Expand deck":"Minimise deck to header"',
 'deckMinimized=!deckMinimized;',
 'if(deckMinimized)',
 'if(!popup.IsOpen)deckMinimized=false;',
 'deckMinimized=false;',
 'title.Children.Add(minimise);',
 'title.Children.Add(previewMode);',
 'title.Children.Add(close);',
 'DeskSpaceRunningAppsV88.AddHoverGroupLive(',
 'DeskSpaceDeckVisibilityV304.Attach(host,popup,panel);',
 'DeskSpaceMonitorDockV190.ConstrainBeforePaintV298('
)){
 if(!$s.Contains($required)){throw "Missing R308 expected button or default-expanded code: $required"}
}
if(([regex]::Matches($s,'DeckHeaderButton[(]')).Count -ne 4){
 throw 'R308 header must have 3 controls plus helper declaration: minimize, preview, dismiss'
}
if($s.Contains('// R306 permanently expanded: no header-only collapse control.')){
 throw 'R308 legacy no-buttons mode unexpectedly still active'
}
Write-Host 'R308 PASS: default expanded; all three header buttons; user-only minimize and reopened expansion; prior Deck safety retained.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceMediaV18.cs')
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'internal bool DeckDismissedV272=>visible&&dismissedV123&&!disposed;',
 'internal bool RestoreDeckV272()',
 'if(disposed||!visible||!dismissedV123)return false;',
 'if(disposed||!visible||dismissedV123)return;',
 'if(command is "previous" or "toggle" or "next" or "stop")',
 'if(activeMedia?.DeckDismissedV272==true)',
 'Content="♫  Show media controls"',
 'restore.Click+=',
 'activeMedia.RestoreDeckV272()',
 'TestMediaDeckRestoreV272();',
 'R272 a deliberate restore reopens same-track controls exactly once'
)){
 if(!$p.Contains($token) -and !$s.Contains($token) -and !$t.Contains($token)){
  throw ('R272 Media restoration missing: '+$token)
 }
}
if(!$p.Contains('internal bool DeckVisibleV123=>visible&&!dismissedV123&&!disposed;')){
 throw 'R272 already-disposed media must never be displayed in Deck'
}
Write-Host 'R272 STATIC PASS: close hides transports, explicit same-track restore and stale-control guard.'

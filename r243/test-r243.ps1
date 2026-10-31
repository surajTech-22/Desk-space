$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'Views.Remove(this);',
 'DeskSpaceMediaV18.DeckIntegrationV123=Views.Count>0;',
 'mediaDeckTimerV123.Stop();',
 'private static readonly HashSet<DeskSpaceDropShelfV15> Views=new();'
)){if(!$s.Contains($needle)){throw ("R243 shared media Deck lifecycle missing: "+$needle)}}
if($s.Contains('DeskSpaceMediaV18.DeckIntegrationV123=false;'))
{throw 'R243 another monitor's Deck still disabled by a per-widget Close'}
if(!$t.Contains('TestTwoMonitorMediaOwnershipV243();'))
{throw 'R243 missing native WPF second-monitor media lifecycle regression'}
Write-Host 'R243 STATIC PASS: shared media integration persists until last independent monitor Deck closes.'

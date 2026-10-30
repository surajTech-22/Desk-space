$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
$d=[IO.File]::ReadAllText($p)
$m=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckHeaderV226.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($part in @(
 'DeskSpaceDeckHeaderV226.Format(busy,runningApps.Count,',
 'heading.Text=DeskSpaceDeckHeaderV226.Format(',
 'heading.TextTrimming=TextTrimming.CharacterEllipsis;',
 'heading.MaxWidth=Math.Max(70,panel.MaxWidth-103);',
 'heading.MinWidth=0;',
 'SetSearchActiveV225(bool active)'
)){if(!$d.Contains($part)){throw ("R226 header action missing "+$part)}}
if(!$m.Contains('if(held>0)') -or !$m.Contains('if(recent>0)')){
 throw 'R226 zero-count clutter still not removed'
}
if(!$t.Contains('TestCompactDeckHeaderV226();')){
 throw 'R226 native UI tests are not registered'
}
Write-Host 'R226 STATIC PASS: concise Deck title, bounded TextBlock and Search overlay suspension preserved.'

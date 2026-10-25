$ErrorActionPreference='Stop'
$root='DeskSpace_Windows_R1/DeskSpace'
$format=[IO.File]::ReadAllText("$root/DeskSpaceExcelFormatApplyV109.cs")
$media=[IO.File]::ReadAllText("$root/DeskSpaceMediaV18.cs")
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($need in @('StructuralHeaderV123(', 'HeadersIndependentV123(',
 'region.Cells[1,i+1].Value2=StructuralHeaderV123(i)',
 'HeadersIndependentV123(x.ColumnNames,expectedTable.ColumnNames)',
 'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
 'sheet.ProtectContents','book.SaveCopyAs(backup)','PopulateBlankFormulas(live)',
 'return "Verified "')){
 if(!$format.Contains($need)){throw "R123 format missing safety/path: $need"}
}
foreach($forbid in @(
 'region.Cells[1,i+1].Value2=table.ColumnNames[i]',
 'existing column headers differ from captured structural headers',
 'Destination Excel Table header layout does not match its source.',
 'x.ColumnNames.SequenceEqual(expectedTable.ColumnNames)')){
 if($format.Contains($forbid)){throw "R123 formatting still requires source header data: $forbid"}
}
foreach($need in @('DeckIntegrationV123','DeckVisibleV123','DeckCommandV123',
 'DismissPanelV123','Close media player (playback continues)',
 'if(DeckIntegrationV123)','if(ReferenceEquals(ActiveV123,this))ActiveV123=null;')){
 if(!$media.Contains($need)){throw "R123 media integration missing: $need"}
}
foreach($need in @('DeskSpaceMediaV18.DeckIntegrationV123=true;',
 'DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123',
 'var activeMedia=DeskSpaceMediaV18.ActiveV123;',
 'Content="×"','activeMedia.DeckCommandV123(command)',
 'activeMedia.DismissPanelV123();Render();',
 'mediaDeckTimerV123.Stop();')){
 if(!$deck.Contains($need)){throw "R123 shared deck media integration missing: $need"}
}
if(!$smoke.Contains('TestFormatHeadersAndDeckV123();')){throw 'R123 WPF regression not wired'}
if(!$format.Contains('Only runs after verifying the entire destination as Empty or Fully Matching')){
 throw 'R123 must retain empty/full gate for formatting'
}
Write-Host 'R123 static PASS: format paste independent of SOURCE labels; no regression to empty/full safety, media in shared deck with close.'

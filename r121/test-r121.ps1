$ErrorActionPreference='Stop'
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceExcelStoreV16.cs")
$ui=[IO.File]::ReadAllText("$root/DeskSpaceExcelEditorV42.cs")
$b=[IO.File]::ReadAllText("$root/DeskSpaceExcelBridgeV16.cs")
foreach($needle in @('formats.dpapi','LoadFormats()','SaveFormats(','templates.dpapi','.previous')){
 if(!$s.Contains($needle)){throw "R121 missing independent DPAPI storage: $needle"}
}
foreach($needle in @(
 'Capture formats & formulas',
 'Preview format destination',
 'Apply formats & formulas',
 'DeskSpaceExcelStoreV16.SaveFormats(current)',
 'captured.Cells.Clear();',
 'captured.Anchors.Clear();',
 'selectedFormatV121',
 'private void CaptureFormatV121()',
 'private void DeleteFormatV121()'
)){
 if(!$ui.Contains($needle)){throw "R121 format-only path missing: $needle"}
}
foreach($needle in @(
 'SaveEditorV42()', 'Save collection & data',
 'Apply autofill','Preview matching cells',
 'DeskSpaceExcelStoreV16.Save(disk)'
)){
 if(!$ui.Contains($needle)){throw "R121 header/value regression: $needle"}
}
if(!$b.Contains('MatchHeaderSignatureV120') -or
   !$b.Contains('HeaderValueDecisionV120')){
 throw 'R121 must preserve verified R120 header/value paste'
}
if(!$ui.Contains('DeskSpaceExcelWorkerV41.ApplyFormatAsync(entry,approved)')){
 throw 'R121 format path must reuse full-range safety and live readback'
}
Write-Host 'R121 PASS static: two independent encrypted libraries, distinct capture/save and apply, R120 value mapping unaffected.'

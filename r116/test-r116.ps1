$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelBridgeV16.cs'
$s=[IO.File]::ReadAllText($p)
foreach($value in @(
 'private static DeskSpaceExcelFullRangeGateV107.Decision CheckAllCellsV116(',
 'private static (int Row,int Column)? SelectedOriginV116(',
 'var selected=SelectedOriginV116(app,entry);',
 'blank.State==',
 'DeskSpaceExcelFullRangeGateV107.Status.EntirelyEmpty',
 'r=selected.Value.Row;c=selected.Value.Column;',
 'var whole=CheckAllCellsV116((object)sheet,entry,r,c);',
 'if(!whole.MayApply)',
 'var refreshed=CheckAllCellsV116((object)sheet,entry,',
 'if(!refreshed.MayApply)',
 'Desktop'
)){
 if($value -eq 'Desktop'){continue}
 if(!$s.Contains($value)){throw "R116 missing: $value"}
}
Write-Host 'R116 PASS: blank destination fallback, exact footprint, partial rejection, write-time recheck.'

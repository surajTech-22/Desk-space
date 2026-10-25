$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$sheet=[IO.File]::ReadAllText("$r/DeskSpaceExcelSheetViewV101.cs")
$semantic=[IO.File]::ReadAllText("$r/DeskSpaceExcelSemanticV106.cs")
$bridge=[IO.File]::ReadAllText("$r/DeskSpaceExcelBridgeV16.cs")
$worker=[IO.File]::ReadAllText("$r/DeskSpaceExcelWorkerV41.cs")
$deck=[IO.File]::ReadAllText("$r/DeskSpaceStableHoverV93.cs")
foreach($needle in @('Excel capture failed while','string currentCell="selection"',
  'catch(ArgumentException)','Source formatting was NOT captured.',
  'view.Cells.Count!=rows*columns','DeskSpaceExcelSemanticV106.CaptureCell')){
 if(!$sheet.Contains($needle)){throw "R114 required capture safety missing $needle"}
}
foreach($needle in @('ListObject property returned an invalid range',
    'Excel ListObjects unavailable from selection.')){
 if(!$semantic.Contains($needle)){throw "R114 table metadata fallback missing $needle"}
}
foreach($needle in @('Excel selection capture failed reading the value at',
    'No workbook cells were edited.')){
 if(!$bridge.Contains($needle)){throw "R114 selection failure context missing $needle"}
}
if(!$worker.Contains('DeskSpace_Excel_COM_Error.log') -or
   !$worker.Contains('ex.StackTrace')){
 throw 'R114 Excel COM diagnostic log missing'
}
foreach($needle in @('OpenPending(confirmedPointerEntry:true);',
  'Click+=(_,_)=>{','MouseEnter+=(_,_)=>{',
  'if(remaining.Length>1)','WindowFlyoutX(outward,target.Width,size.Width,6)')){
 if(!$deck.Contains($needle)){throw "R114 grouped window action missing $needle"}
}
if(([regex]::Matches($deck,'OpenPending\(confirmedPointerEntry:true\);')).Count -lt 2){
 throw 'R114 grouped window hover and click not both connected'
}
Write-Host 'R114 static tests PASS: capture diagnostics/table metadata and grouped window hover/click.'

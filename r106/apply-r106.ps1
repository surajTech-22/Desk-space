$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
foreach($name in @(
  @('r106/DeskSpaceExcelSheetViewV106.cs',"$r/DeskSpaceExcelSheetViewV101.cs"),
  @('r106/DeskSpaceExcelSemanticV106.cs',"$r/DeskSpaceExcelSemanticV106.cs"),
  @('r107/DeskSpaceExcelFullRangeGateV107.cs',"$r/DeskSpaceExcelFullRangeGateV107.cs"),
  @('r108/DeskSpaceExcelDestinationV108.cs',"$r/DeskSpaceExcelDestinationV108.cs")
)){
  if(!(Test-Path -LiteralPath $name[0])){throw "Missing $($name[0])"}
  Copy-Item -LiteralPath $name[0] -Destination $name[1] -Force
}
Write-Host 'R106 development build only: captured Excel cell semantics/table identity and strict target eligibility guard. No macro recorder, no format writing released.'

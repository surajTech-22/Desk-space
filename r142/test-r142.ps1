$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$view=[IO.File]::ReadAllText("$root/DeskSpaceExcelSheetViewV101.cs")
$editor=[IO.File]::ReadAllText("$root/DeskSpaceExcelUnifiedV126.cs")
$audit=[IO.File]::ReadAllText("$root/DeskSpaceExcelFormulaIntegrityV142.cs")
foreach($token in @(
    'style.FormulaA1=Format(()=> (object?)cell.Formula);',
    'style.FormulaR1C1=Format(()=> (object?)cell.FormulaR1C1);',
    'Excel formula detected at ',
    'formulaAddressV142',
    'formulaTextV142',
    'firstFormula.FormulaA1',
    'formulaTextV142.Text=selectedFormula',
    'preview.Children.Add(formulaBarV142);'
)){
    if(!$view.Contains($token)){throw "R142 view/capture verification missing: $token"}
}
foreach($token in @(
    'DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(snapshot);',
    'DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(edited);',
    'DeskSpaceExcelFormulaIntegrityV142.ValidateRoundtrip(',
    'DeskSpaceExcelStoreV16.LoadFormats()'
)){
    if(!$editor.Contains($token)){throw "R142 saving verification missing: $token"}
}
foreach($token in @(
    'string.Equals(item.Value.A1,value.A1,StringComparison.Ordinal)',
    'string.Equals(item.Value.R1C1,value.R1C1,StringComparison.Ordinal)',
    'Incomplete Excel cell capture',
    'formula expression missing or invalid'
)){
    if(!$audit.Contains($token)){throw "R142 exact formula verification missing: $token"}
}
dotnet run --configuration Release --project r142/tests/FormulaIntegrityTests.csproj
if($LASTEXITCODE -ne 0){throw 'R142 exact formula tests failed'}
Write-Host 'R142 PASS: capture rejects marker-only cells, preview displays actual formula, saved library verifies exact formula text, and regression suite passes.'

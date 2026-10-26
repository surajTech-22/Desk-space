$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$view=[IO.File]::ReadAllText("$r/DeskSpaceExcelSheetViewV101.cs")
$editor=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$unified=[IO.File]::ReadAllText("$r/DeskSpaceExcelUnifiedV126.cs")
$sta=[IO.File]::ReadAllText("$r/DeskSpaceExcelWorkerV41.cs")
$com=[IO.File]::ReadAllText("$r/DeskSpaceExcelPasteSpecialV144.cs")
$expand=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormulaExpanderV144.cs")
$format=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
foreach($token in @(
    'DeskSpaceExcelFormulaExpanderV144.Attach(border,surface,',
    'item.FormulaA1.Length>0?item.FormulaA1:item.FormulaR1C1')){
    if(!$view.Contains($token)){throw "R144 no Excel-style expansion hook: $token"}
}
foreach($token in @('PlacementTarget=cell','Placement=PlacementMode.Bottom',
    'Text=formula','TextWrapping=TextWrapping.Wrap',
    'cell.MouseLeftButtonDown+=(_,e)=>',
    'e.ClickCount>=2','StaysOpen=false')){
    if(!$expand.Contains($token)){throw "R144 full-formula popup is incomplete: $token"}
}
foreach($token in @('Paste Special · Formulas','PasteSpecialFormulasV144')){
    if(!$editor.Contains($token)){throw "R144 formula paste button missing: $token"}
    if(!$unified.Contains($token)){throw "R144 button binding missing: $token"}
}
if(!$sta.Contains('Sta(()=>DeskSpaceExcelPasteSpecialV144.Paste(entry))')){
    throw 'R144 dedicated Excel STA formula COM operation is not connected'
}
foreach($token in @('FormulaR1C1=item.R1C1;','workbook.SaveCopyAs(copy);',
    'DeskSpaceExcelFormulaOnlyPolicyV144.Plan(',
    'Inserted formulas were cleared.',
    'AutoFillFormulasInLists=false','excel.EnableEvents=false;')){
    if(!$com.Contains($token)){throw "R144 native Excel safety control missing: $token"}
}
if(!$format.Contains('KeepBlankDestinationValuesV136(live);')){
    throw 'R144 format-only no-zero route was accidentally modified'
}
if($view.Contains('formulaBarV142') -or $view.Contains('Text=isFormula?"ƒx"')){
    throw 'R144 reintroduced obsolete FX or detached formula pane'
}
dotnet run -c Release --project r144/tests/FormulaOnlyTests.csproj
if($LASTEXITCODE -ne 0){throw 'R144 formula planner regression failed'}
Write-Host 'R144 PASS: expanding actual formulas inside preview, real dedicated formula-only Excel COM route, strict no-overwrite policy, backup/rollback, and independent formatting safety gate.'

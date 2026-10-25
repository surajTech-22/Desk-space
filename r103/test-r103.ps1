$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$ed=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$visual=[IO.File]::ReadAllText("$r/DeskSpaceExcelSheetViewV101.cs")
$kind=[IO.File]::ReadAllText("$r/DeskSpaceExcelInputTypeV101.cs")
foreach($v in @(
 'Content=previewPane',
 'Content=mappingPane',
 'mappingPane.Children.Add(footer)',
 'previewPane.Children.Add(sheetPreviewHostV101)',
 'Expand sheet preview',
 'SheetVisualV101=DeskSpaceExcelSheetViewV101.MergeCapturedVisuals(')){
 if(!$ed.Contains($v)){throw "R103 sheet layout missing: $v"}
}
if($ed.Contains('typeTools.Children.Add(dateInputOrderV102)') -or
   $ed.Contains('right.Children.Add(footer);')){
 throw 'R103 date-order dropdown/toolbar still occupying sheet preview'
}
foreach($v in @(
 'FontFamily=new FontFamily(',
 'Underline=Number(()=> (object?)cell.Font.Underline',
 'StrikeThrough=Flag(()=> (object?)cell.Font.Strikethrough)',
 'IndentLevel=Math.Clamp(',
 'style.ConditionalAppearance=HasConditionalAppearance(cell)',
 'cell.DisplayFormat.Interior.Color',
 'BorderRightColor=BorderColor(cell,10)',
 'AddBorderLine(',
 'Text=isFormula?',
 'Placeholder(item)',
 'MergeCapturedVisuals(',
 'Measure(()=> (object?)range.Columns[c].Width',
 'Math.Clamp(Measure(()=> (object?)cell.Font.Size,11),7,60)',
 'NumberFormat=Format(()=> (object?)cell.NumberFormat')){
 if(!$visual.Contains($v)){throw "R103 accurate Excel rendering missing: $v"}
}
if(!$kind.Contains('DateOrderFromFormat(originalNumberFormat)')){
 throw 'R103 source format must establish date order when known'
}
Write-Host 'R103 PASS: full-height preview, all fundamental font/border styles shown, capture-append merges style snapshots, dates inferred per cell.'

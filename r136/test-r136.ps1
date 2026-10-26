$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$w=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$p=[IO.File]::ReadAllText("$r/DeskSpaceExcelBlankFormatPolicyV136.cs")
$h=[IO.File]::ReadAllText('r111/LiveExcelAcceptance/Program.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
function Must([string]$doc,[string]$term,[string]$tag){
 if(!$doc.Contains($term)){throw "R136 missing $tag : $term"}
}
foreach($term in @(
 'DeskSpaceExcelBlankFormatPolicyV136.',
 'KeepBlankDestinationValuesV136(live);',
 'if(IsFormula(cell) || (object?)cell.Value2!=null)',
 'cell.ClearContents();',
 'if(IsFormula(cell) ||',
 'No input values may be pasted.',
 'Blank input cells stayed blank.',
 'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
 'PreflightTableStylesV131(',
 'book.SaveCopyAs(backup);',
 'RestoreAfterFailure(live,before,',
 'BuildMissingStructure(live,originallyEmpty);',
 'SetTableStyleV131(',
 'wanted.UnderlineStyle!=observed.UnderlineStyle',
 'An existing Excel value/formula changed unexpectedly.',
 'x.TableStyle==expectedTable.TableStyle',
 'DeskSpaceExcelRenderedPolicyV135.RequiresExactRenderedMatch(')){
 Must $w $term 'Excel writer'
}
if($w.Contains('PopulateBlankFormulas(live);') -or
   $w.Contains('Recorded Excel formula could not be reconstructed.')){
 throw 'R136 still injects source formulas or expects them in blank destinations'
}
foreach($term in @(
 'table.HasHeaders',
 '? table.ColumnNames[index] : ""',
 'return "";',
 'col>=table.Column+table.ColumnCount')){
 Must $p $term 'blank structural-only policy'
}
foreach($term in @(
 'all body cells blank with zero formula injection.',
 'Same-workbook format paste introduced a formula/zero',
 'Full-data format apply altered a business value',
 'A partially populated destination was accepted',
 'Expect(!(bool)source.Range["J17"].HasFormula',
 'Expected exactly one captured table')){
 Must $h $term 'real Excel acceptance'
}
foreach($term in @(
 'TestBlankExcelFormatPolicyV136();',
 'only genuine table headers may be populated',
 'Format-only pastes must never copy source formula or value')){
 Must $t $term 'compiled WPF regression'
}
Write-Host 'R136 STATIC PASS: blank destination receives only required real-Table headers; no zeros or captured formulas. Complete/full-data and partial gate, rollback, saved captured metadata and styling unchanged.'
Write-Warning 'A passing compiled test is not the same as a real Office COM paste; confirm on a Windows system with Microsoft Excel.'
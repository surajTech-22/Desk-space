$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$model=[IO.File]::ReadAllText("$r/DeskSpaceExcelSheetViewV101.cs")
$writer=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$policy=[IO.File]::ReadAllText("$r/DeskSpaceExcelDirectReadbackV139.cs")
$feature=[IO.File]::ReadAllText("$r/DeskSpaceExcelSemanticV106.cs")
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
$office=[IO.File]::ReadAllText('r111/LiveExcelAcceptance/Program.cs')
function Has([string]$doc,[string]$piece,[string]$name){
 if(!$doc.Contains($piece)){throw "R140 $($name) missing $piece"}
}
foreach($x in @('DirectFontColorIndex=Number(',
'DirectFontThemeColor=Number(',
'DirectFillPattern=Number(',
'DirectFillThemeColor=Number(',
'DirectFontTintAndShade=Tint(',
'DirectFillTintAndShade=Tint(',
'DisplayFontBgr=Number(',
'FormulaR1C1=Format(',
'StoredType=DeskSpaceExcelInputTypeV101.Stored(',
'WrapText=Flag(','IndentLevel=Math.Clamp(',
'BorderBottomColor=BorderColor(',
'NativeRowHeights.Add(','NativeColumnWidths.Add(')){
 Has $model $x 'source capture'
}
foreach($x in @('if(s.DirectFontColorIndex==-4105)',
'else if(s.DirectFontThemeColor>0)',
'cell.Font.ThemeColor=s.DirectFontThemeColor;',
'cell.Font.TintAndShade=s.DirectFontTintAndShade.Value;',
'cell.Interior.TintAndShade=s.DirectFillTintAndShade.Value;',
'cell.Orientation=s.TextOrientation;',
'cell.HorizontalAlignment=s.HorizontalAlignment;',
'cell.VerticalAlignment=s.VerticalAlignment;',
'cell.WrapText=s.WrapText;',
'cell.ShrinkToFit=s.ShrinkToFit;',
'cell.IndentLevel=s.IndentLevel;',
'cell.Font.Superscript=feature.Superscript;',
'cell.Font.Subscript=feature.Subscript;',
'cell.Locked=feature.Locked;',
'cell.FormulaHidden=feature.FormulaHidden;',
'border.Color=new[]{',
'if(weight!=0)border.Weight=weight;',
'KeepBlankDestinationValuesV136(live);',
'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
'book.SaveCopyAs(backup);',
'RestoreAfterFailure(live,before,',
'DeskSpaceExcelDirectReadbackV139.Differences(s,a)',
'DeskSpaceExcelRenderedPolicyV135.RequiresExactRenderedMatch(',
'wanted.Superscript!=observed.Superscript',
'wanted.Subscript!=observed.Subscript',
'wanted.UnderlineStyle!=observed.UnderlineStyle',
'x.TableStyle==expectedTable.TableStyle',
'signature.Formula!=(IsFormula(cell)?',
'An existing Excel value/formula changed unexpectedly.',
'Excel row height readback mismatch.',
'Excel native column width readback mismatch.')){
 Has $writer $x 'paste/readback/rollback'
}
foreach($x in @('if(source.DirectFontColorIndex==-4105)',
'else if(source.DirectFontThemeColor>0)',
'Font.ThemeColor','Font.TintAndShade','Interior.TintAndShade',
'Font.Color (explicit RGB)','Interior.Color (explicit RGB)',
'Interior.Pattern','HorizontalAlignment','VerticalAlignment',
'TextOrientation','IndentLevel','WrapText','ShrinkToFit',
'NumberFormat','FontName','FontSize','Bold','Italic',
'Underline','StrikeThrough','BorderLeftColor','BorderLeftWeight',
'BorderRightColor','BorderRightWeight','BorderTopColor','BorderTopWeight',
'BorderBottomColor','BorderBottomWeight')){
 Has $policy $x 'strict style readback'
}
foreach($x in @('Superscript=Flag(','Subscript=Flag(',
'Formula2','ValidationType','ConditionalRuleCount','FormulaHidden',
'TableStyleName(')){Has $feature $x 'advanced features capture'}
foreach($x in @('TestFullExcelFormatMatrixV140();',
'Automatic Excel font must not compare its inherited ThemeColor=2.',
'Fill tint mismatch must fail.',
'BorderBottomColor','BorderTopWeight','WrapText','FontSize',
'Explicit font colour mismatch must be rejected',
'An invalid saved table style must not bypass',
'Format-only pastes must never copy source formula or value')){
 Has $test $x 'compiled WPF style cases'
}
foreach($x in @('Automatic font colour was turned into a direct theme/colour',
'Explicit font theme slot missing','Explicit font tint missing',
'Explicit fill theme slot missing','Explicit fill tint missing',
'Border colour or weight missing','Superscript missing',
'Captured input protection settings not restored',
'all body cells blank with zero formula injection.',
'partial target blocked with no workbook edits.',
'full-data target preserves values and creates real table.')){
 Has $office $x 'live Excel acceptance'
}
if($writer.Contains('s.DisplayFontBgr!=s.FontBgr)')){
 throw 'R140 table visual style still overrides exact captured colour mode'
}
Write-Host 'R140 STATIC PASS: captured and verified font mode/theme/tint, fill mode/theme/tint, all four border styles/colour/weight, row/column size, number/date, font, superscript, cell protection, alignment, wrap/shrink, rotation, real Table and merge. Formula R1C1 saved; blank format paste leaves no values/formulas, existing data preserved. Required backup/rollback active.'
Write-Warning 'Conditional-format rules, arbitrary custom Named Styles, pivots, embedded shapes and VBA are not reproduced; unsupported layouts remain gated. True Excel COM execution requires Excel-installed Windows.'
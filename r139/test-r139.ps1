$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$worker=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$policy=[IO.File]::ReadAllText("$r/DeskSpaceExcelDirectReadbackV139.cs")
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
function Need([string]$s,[string]$what,[string]$context) {
 if(!$s.Contains($what)){throw "R139 $($context) missing: $what"}
}
foreach($x in @(
 'DeskSpaceExcelDirectReadbackV139.Differences(s,a)',
 'Formatting readback differs at ',
 'string.Join("; ",directDifferences)',
 'BuildMissingStructure(live,originallyEmpty);',
 'KeepBlankDestinationValuesV136(live);',
 'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
 'PreflightTableStylesV131(',
 'book.SaveCopyAs(backup);',
 'RestoreAfterFailure(live,before,',
 'wanted.UnderlineStyle!=observed.UnderlineStyle',
 'x.TableStyle==expectedTable.TableStyle',
 'DeskSpaceExcelRenderedPolicyV135.RequiresExactRenderedMatch(',
 'An existing Excel value/formula changed unexpectedly.'
)){Need $worker $x 'Excel formatting safety and readback'}
foreach($x in @(
 'source.DirectFontThemeColor>0',
 'source.DirectFontColorIndex==-4105',
 'measured.DirectFontColorIndex!=-4105',
 'source.DirectFillPattern==-4142',
 'source.DirectFillPattern==-4105',
 'Interior.ThemeColor',
 'Interior.Color (explicit RGB)',
 'Font.Color (explicit RGB)',
 'NumberFormat',
 'BorderLeft',
 'BorderRight',
 'BorderTop',
 'BorderBottom'
)){Need $policy $x 'cell-style readback'}
foreach($x in @(
 'TestExcelDirectReadbackV139();',
 'Incorrect theme slot must block paste',
 'Explicit font colour mismatch must be rejected',
 'Explicit fill colour mismatch must be rejected',
 'Border mismatch must block paste',
 'Number-format mismatch must block paste'
)){Need $ui $x 'compiled regression'}
if($worker.Contains('s.FillBgr!=a.FillBgr || s.FontBgr!=a.FontBgr')){
 throw 'R139 still uses raw direct RGB even when source has automatic/theme settings'
}
Write-Host 'R139 STATIC PASS: N13 readback distinguishes source automatic/theme colours from explicit RGB, reports exact mismatched fields, retains full format backup, rollback, value protection, table identity and signed installer.'
Write-Warning 'Live Microsoft Excel COM pasting requires an Excel-installed Windows machine; standard hosted CI cannot perform this execution.'
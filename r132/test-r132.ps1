$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$fmt=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$ui=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$capture=[IO.File]::ReadAllText("$r/DeskSpaceExcelSemanticV106.cs")
$tests=[IO.File]::ReadAllText('r111/LiveExcelAcceptance/Program.cs')
function Require([string]$s,[string]$needle,[string]$label){
    if(-not $s.Contains($needle)){throw "R132 $label missing: $needle"}
}
foreach($p in @(
  'private static string TableStyleNameV132(object? raw)',
  'TableStyleNameV132((object?)table.TableStyle)',
  'SetTableStyleV131(created,table.TableStyle);',
  'SetTableStyleV131(table,metadata.TableStyle);',
  'PreflightTableStylesV131(app.ActiveWorkbook,features.Tables,problems);',
  'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
  'book.SaveCopyAs(backup)',
  'RestoreAfterFailure(live,before,'
)){Require $fmt $p 'format worker'}
if($fmt.Contains('created.Name=table.Name;')){
  throw 'R132 still duplicates the source ListObject name in same workbook.'
}
if($fmt.Contains('Text((object?)table.TableStyle)')){
  throw 'R132 still calls COM wrapper ToString() on table-style readback.'
}
foreach($p in @(
  'TableStyle=TableStyleName(()=>(object?)table.TableStyle)',
  'Marshal.IsComObject(value)'
)){Require $capture $p 'source COM capture'}
foreach($p in @(
  'var collectionAndData=new Grid',
  'collectionColumn.Children.Add(mappingGroupV42);',
  'dataColumn.Children.Add(mappingNameV42);',
  'editorHead.Children.Add(collectionAndData);',
  'pairedFormatNamesV132',
  'formatHeadV126.Children.Insert(0,pairedFormatNamesV132);',
  'leftFormatNameV132.Children.Add(formatGroupV130);',
  'rightFormatNameV132.Children.Add(formatNameV124);'
)){Require $ui $p 'same-row collection/name editor'}
foreach($p in @(
  'Expect(Eligible(emptyReview)',
  'Expect(!Eligible(partial)',
  'Expect(Eligible(fullReview)',
  'Expect(!Eligible(failureReview)',
  'Same-workbook destination table style differs',
  'Same-workbook format paste duplicated original Excel table identity',
  'PASS: same workbook new table keeps unique ListObject identity'
)){Require $tests $p 'real Excel acceptance'}
Write-Host 'R132 STATIC PASS: COM style handling, source-table identity, original safety gate and rollback, both two-column editors, real Excel scenario harness.'
Write-Warning 'STATIC PASS is NOT live Excel COM paste evidence. Do not publish stable until real Office acceptance passes.'
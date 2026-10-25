$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$engine=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$semantic=[IO.File]::ReadAllText("$r/DeskSpaceExcelSemanticV106.cs")
foreach($needle in @(
  'if(!decision.MayApply)problems.Add(decision.Explanation)',
  'Status.CompleteMatchingLayout',
  'BuildMissingStructure(live,originallyEmpty)',
  'ListObjects.Add(',
  '1,region,Type.Missing,1',
  'if(empty)',
  'region.Cells[1,i+1].Value2=table.ColumnNames[i]',
  'sourceTable.ColumnNames[c]',
  'PopulateBlankFormulas(live)',
  'FormulaR1C1=source.FormulaR1C1',
  'RestoreAfterFailure(live,before,',
  'table.Unlist()',
  'area.UnMerge()',
  'if(originallyEmpty)live.Range.ClearContents()',
  'SaveCopyAs(backup)',
  'Recorded Excel formula could not be reconstructed.',
  'x.ColumnNames.SequenceEqual(expectedTable.ColumnNames)',
  'Merged-cell geometry mismatch',
  'source.FormulaR1C1',
  'TableStyle=metadata.TableStyle'
)){
 if(!$engine.Contains($needle)){throw "R110 missing implementation detail: $needle"}
}
foreach($needle in @(
  'ColumnNames',
  'TableStyle',
  'HasHeaders',
  'HasTotals',
  'ShowAutoFilter',
  'ShowTableStyleFirstColumn',
  'ListColumns[columnIndex].Name'
)){
 if(!$semantic.Contains($needle)){throw "R110 missing exact Table metadata: $needle"}
}
# This is an integration source check, NOT an Excel COM end-to-end test.
# Do not publish to Stable/Tester without an Office-installed Windows test.
Write-Host 'R110 PASS: full-range and exact table/merge/formula source safeguards found. Live Excel acceptance remains required.'

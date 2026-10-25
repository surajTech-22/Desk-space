$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$engine=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$editor=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$worker=[IO.File]::ReadAllText("$r/DeskSpaceExcelWorkerV41.cs")
$visual=[IO.File]::ReadAllText("$r/DeskSpaceExcelSheetViewV101.cs")
foreach($n in @(
  'private static Checked Inspect(',
  'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
  'if(!decision.MayApply)problems.Add',
  'if(targetTableCount!=features.Tables.Count &&',
  'Missing pre-existing merged destination',
  'Backup',
  'SaveCopyAs(backup)',
  'WriteSnapshot(live.Range,live.Visual,true)',
  'DeskSpaceExcelVisualCaptureV101.Capture((object)live.Range)',
  'An existing Excel value/formula changed unexpectedly.',
  'source.FormulaR1C1',
  'return "Verified "+source.Length',
  'WriteSnapshot(live.Range,before,true)'
)){
 if(!$engine.Contains($n)){
  # Case-insensitive checks would disguise a missing critical guard.
  throw "R109 engine missing safeguard: $n"
 }
}
foreach($n in @(
 'pendingFormatReviewV109',
 '"Check selected formatting destination"',
 '"Apply selected format"',
 'PreviewFormatV109()',
 'ApplyFormatV109()',
 'DeskSpaceExcelWorkerV41.ApplyFormatAsync',
 'MessageBoxResult.Yes',
 'hasUnsavedChangesV42'
)){
 if(!$editor.Contains($n)){throw "R109 Excel editor missing: $n"}
}
foreach($n in @('PreviewFormatAsync(','ApplyFormatAsync(','Sta(()=>DeskSpaceExcelFormatApplyV109.Apply')){
 if(!$worker.Contains($n)){throw "R109 STA worker missing: $n"}
}
foreach($n in @('NativeColumnWidths','NativeRowHeights','DisplayStyleAvailable')){
 if(!$visual.Contains($n)){throw "R109 source capture missing: $n"}
}
# A static check cannot prove live COM Excel correctness. Release approval
# separately requires a disposable workbook and real Windows Excel.
Write-Host 'R109 static integration PASS — not a live Excel acceptance test.'

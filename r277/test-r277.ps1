$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source=[IO.File]::ReadAllText("$root/DeskSpaceBatchRenameRecoveryV277.cs")
$ui=[IO.File]::ReadAllText("$root/DeskSpaceBatchRenamePreviewV254.cs")
$hub=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'FileOptions.WriteThrough',
 'output.Flush(flushToDisk:true);',
 'File.Move(temporary,destination,true);',
 'internal static Review Inspect(',
 'internal static DeskSpaceBatchRenameTransactionV275.Outcome Recover(',
 'internal static DeskSpaceBatchRenameTransactionV275.Outcome Apply(',
 'DeskSpaceBatchRenameTransactionV275.Undo(',
 'if(Inspect(root).Found)',
 'if(!state.Found || !state.CanRecover)',
 'if(atBefore && Missing(after))',
 'else if(atAfter && Missing(before))',
 'if(after.Overlaps(before))',
 'Content="Review previous batch"',
 'Content="Recover reviewed batch"',
 'DeskSpaceBatchRenameRecoveryV277.Apply(',
 'DeskSpaceBatchRenameRecoveryV277.Undo(',
 'DeskSpaceBatchRenameRecoveryV277.Recover(',
 'DeskSpaceBatchRenameRecoveryV277.Inspect(',
 'DeskSpaceBatchRenameRecoveryV277.Keep(',
 'internal static DeskSpaceBatchRenameTransactionV275.Outcome Keep(',
 'Content="Keep renamed files"',
 'if(!SameReview(latest,recoveryReview))',
 'Add("Batch rename and recovery"',
 'new("batchrename","Batch rename and recovery"',
 'case "batchrename":',
 'TestBatchRenameCrashJournalV277();',
 'R277 both names occupied must block all recovery without overwrite',
 'R277 explicit Keep must preserve renamed contents'
)){
 if(!$source.Contains($token) -and !$ui.Contains($token) -and
    !$hub.Contains($token) -and !$palette.Contains($token) -and
    !$native.Contains($token)){
  throw ('R277 missing recovery protection: '+$token)
 }
}
foreach($forbidden in @('File.Move(current,original,true)',
    'File.Delete(original)','Process.Start(', 'Clipboard.Get')){
 if($source.Contains($forbidden)){
  throw ('R277 recovery must not overwrite, execute, or read clipboard: '+$forbidden)
 }
}
Write-Host 'R277 STATIC PASS: durable write-ahead journal and explicitly approved, metadata-checked recovery.'

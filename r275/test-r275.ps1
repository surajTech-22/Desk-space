$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$engine=[IO.File]::ReadAllText("$root/DeskSpaceBatchRenameTransactionV275.cs")
$preview=[IO.File]::ReadAllText("$root/DeskSpaceBatchRenamePreviewV254.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'File.Move(oldPath,newPath,false)',
 'File.Move(newPath,oldPath,false)',
 'File.Move(current,original,false)',
 'internal static Outcome Apply(',
 'internal static Outcome Undo(',
 'internal sealed record Receipt(',
 'ValidRoot(root)',
 'SafeSource(file)',
 'actual.Files.Zip(expected.Files',
 'cancel.ThrowIfCancellationRequested();',
 'remaining.Count==0?',
 'Content="Apply approved names"',
 'Content="Undo this batch"',
 'Content="Cancel operation"',
 'window.Closing+=',
 'MessageBoxButton.YesNo',
 'Task.Run(()=>',
 'TestSafeBatchRenameApplyUndoV275();',
 'R275 one locked source must roll back earlier successful renames'
)){
 if(!$engine.Contains($needle) -and !$preview.Contains($needle) -and
    !$native.Contains($needle)){
  throw ('R275 transactional file operation missing: '+$needle)
 }
}
if($engine.Contains('File.Delete(') -or $engine.Contains('File.Move(oldPath,newPath,true)')){
 throw 'R275 batch rename must never delete or overwrite files'
}
Write-Host 'R275 STATIC PASS: preview-drift revalidation, non-overwrite Apply, undo, rollback, source-metadata guards, explicit consent.'

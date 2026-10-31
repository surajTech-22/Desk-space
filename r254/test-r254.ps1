$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceBatchRenamePreviewV254.cs')
$hub=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceFeatureHubV199.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal const int MaxFiles=128',
 'SearchOption.TopDirectoryOnly',
 'FileAttributes.Hidden|FileAttributes.System',
 'IReadOnlyList<Proposed> Files',
 'new HashSet<string>(input,StringComparer.OrdinalIgnoreCase)',
 'Content="Choose folder and preview"',
 'DeskSpaceBatchRenamePreviewV254.Show(this)',
 'TestBatchRenamePreviewV254();'
)){
 if(!$s.Contains($needle) -and !$hub.Contains($needle) -and !$t.Contains($needle)){
  throw ('R254 preview safety missing '+$needle)
 }
}
foreach($forbidden in @('File.Move(','File.Delete(','File.Copy(',
 'File.WriteAll','Directory.Move(','Directory.Delete(',
 'FileSystemWatcher','Process.Start','Clipboard.','HttpClient')){
 if($s.Contains($forbidden)){throw ('R254 preview-only UI must not mutate files: '+$forbidden)}
}
Write-Host 'R254 STATIC PASS: preview-only batch rename, no writes/deletions and no recursion.'

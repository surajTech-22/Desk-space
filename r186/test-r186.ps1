$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$fingerprint=[IO.File]::ReadAllText("$root/DeskSpaceContentDuplicatesV186.cs")
$health=[IO.File]::ReadAllText("$root/DeskSpaceComponentHealthV175.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
    '"DeskSpace","TemporaryShelf"',
    'MaxItemBytes=256L*1024*1024',
    'MaxScanBytes=1024L*1024*1024',
    'MaxEntriesPerFolder=4096',
    'FileAttributes.ReparsePoint',
    'FileOptions.SequentialScan',
    'IncrementalHash.CreateHash(HashAlgorithmName.SHA256)',
    'cancellation.ThrowIfCancellationRequested()',
    'FingerprintFolder(path,root,ref remaining,cancellation)',
    'CheckCore(stagedPaths,Shelf,cancellation)',
    'FileShare.Read'
)){
 if(!$fingerprint.Contains($token)){throw 'R186 staged-only fingerprint guard missing: '+$token}
}
if($fingerprint.Contains('File.Delete(') -or
   $fingerprint.Contains('Directory.Delete(') -or
   $fingerprint.Contains('File.Move(') -or
   $fingerprint.Contains('File.Copy(') -or
   $fingerprint.Contains('Clipboard.Get') -or
   $fingerprint.Contains('HttpClient')){
 throw 'R186 read-only fingerprint scanner unexpectedly contains a mutating/external operation'
}
foreach($token in @(
  'Find duplicate held files (SHA-256)',
  'Cancel scan',
  'scan?.Cancel()',
  'DeskSpaceContentDuplicatesV186.Check(paths,tokenSource.Token)',
  'No items were removed or modified.',
  'window.Closed+=(_,_)=>scan?.Cancel();'
)){
 if(!$health.Contains($token)){throw 'R186 opt-in local WPF duplicate UI missing: '+$token}
}
foreach($token in @(
  'TestStagedContentDuplicatesV186();',
  'private static void TestStagedContentDuplicatesV186()',
  'source restriction, cancellation, non-destructive files and WPF UI',
  'DeskSpace_R186_DuplicateSmoke_')){
 if(!$smoke.Contains($token)){throw 'R186 native WPF regression missing: '+$token}
}
Write-Host 'R186 STATIC PASS: only on-demand, private staging, bounded content read, explicit cancellation, no auto-delete or existing copy/paste change.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceFileDuplicatesV244.cs")
$hub=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal const int MaxFiles=128',
 'internal const long MaxPerFileBytes=64L*1024*1024',
 'internal const long MaxTotalBytes=256L*1024*1024',
 'SearchOption.TopDirectoryOnly',
 'FileAttributes.ReparsePoint|FileAttributes.Hidden',
 'IncrementalHash.CreateHash(HashAlgorithmName.SHA256)',
 'cancel.ThrowIfCancellationRequested();',
 'Task.Run(()=>Scan(directory,scope.Token),scope.Token)',
 'No recursion, ',
 'window.Closed+=(_,_)=>{current?.Cancel();};'
)){
 if(!$s.Contains($needle)){throw ('R244 missing explicit local file scanning guard: '+$needle)}
}
foreach($unsafe in @('File.Delete(', 'File.Move(', 'Directory.Delete(',
 'Directory.Move(', 'File.WriteAll', 'Clipboard.', 'HttpClient',
 'Process.Start(', 'FileSystemWatcher', 'Registry.')){
 if($s.Contains($unsafe)){throw ('R244 duplicate scanner must not modify or share files: '+$unsafe)}
}
if(!$hub.Contains('DeskSpaceFileDuplicatesV244.Show(this)') -or
   !$tests.Contains('TestFileDuplicatesV244();')){
 throw 'R244 missing Features entry or native fixture acceptance'
}
Write-Host 'R244 STATIC PASS: explicit local, bounded, read-only SHA-256 scanner and native WPF fixtures.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceInterruptedCopyV165.cs'
$s=[IO.File]::ReadAllText($p)
foreach($token in @(
 'Environment.SpecialFolder.LocalApplicationData',
 '"DeskSpace","TemporaryShelf"',
 'var normalLayout=IsGuid(Path.GetFileName(item))&&',
 'var ownedLayout=item.StartsWith(',
 'if(!normalLayout&&!ownedLayout)',
 'FileShare.None,4096,FileOptions.WriteThrough',
 'IsEntirelyStale(partial,threshold)',
 'FileAttributes.ReparsePoint'
)){
 if(!$s.Contains($token)){throw "R174 copy recovery ownership gate missing: $token"}
}
Write-Host 'R174 PASS: app-owned staging accepted; unrelated paths still rejected; stale partial/prune safety retained.'

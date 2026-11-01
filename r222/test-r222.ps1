$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceAtomicInstallerV222.cs")
$u=[IO.File]::ReadAllText("$root/DeskSpacePublicUpdaterV44.cs")
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static async Task StageFromStreamAsync(Stream source,',
 'CleanupAbandoned(directory,DateTime.UtcNow);',
 'FileMode.CreateNew',
 'File.Move(partial,final,true);',
 'cancellation.ThrowIfCancellationRequested();',
 'FileAttributes.ReparsePoint',
 'MaxInstallerBytes-read',
 'length<MinInstallerBytes',
 'File.Delete(partial)'
)){
 if(!$s.Contains($needle)){throw ('R222 safe installer download missing '+$needle)}
}
if(!$u.Contains('DeskSpaceAtomicInstallerV222.StageFromStreamAsync(input,file,ct);')){
 throw 'R222 on-disk official installer download path is not atomic'
}
if($u.Contains('new FileStream(file,FileMode.Create,FileAccess.Write,')){
 throw 'R222 updater still truncates the original cached installer on download'
}
if(!$u.Contains('Verify(installer,checksum,root);') -or
   !$u.Contains('Verify(stableInstaller,sha,null);') -or
   !$u.Contains('DeskSpaceVerifiedUpdateQueueV191.Stage(')){
 throw 'R222 must preserve SHA256 verification and both official channel guards'
}
if(!$t.Contains('TestInstallerAtomicV222();')){
 throw 'R222 native WPF atomic-installer test missing'
}
Write-Host 'R222 STATIC PASS: temp-file-only writes, atomic rename and existing GitHub SHA-256/peer gates retained.'

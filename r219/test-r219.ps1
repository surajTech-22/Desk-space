$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$q=[IO.File]::ReadAllText("$root/DeskSpaceVerifiedUpdateQueueV191.cs")
$u=[IO.File]::ReadAllText("$root/DeskSpacePublicUpdaterV44.cs")
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'NormalizeOfficialChecksumV219(string checksum)',
 'DeskSpace_Setup\.exe',
 'RegexOptions.CultureInvariant|RegexOptions.IgnoreCase',
 'string normalized=NormalizeOfficialChecksumV219(checksum);',
 'new Candidate(installer,folder,tag,normalized,channel)',
 'CryptographicOperations.FixedTimeEquals',
 'SHA256.HashData(data)',
 'FileAttributes.ReparsePoint',
 'AcquireStageLockAsync'
)){if(!$q.Contains($needle)){throw ('R219 secure staging requirement missing '+$needle)}}
if($u.Contains('var match=Regex.Match(checksum,@"^\s*')){
 throw 'R219 downloader must not retain an independent permissive checksum parser'
}
foreach($needle in @(
 'DeskSpaceVerifiedUpdateQueueV191.NormalizeOfficialChecksumV219(',
 'Verify(stableInstaller,sha,null);',
 'DeskSpaceVerifiedUpdateQueueV191.Stage(stableInstaller,',
 'Verify(installer,checksum,root);',
 'DeskSpaceVerifiedUpdateQueueV191.Stage(installer,folder,',
 'await StartSupervisor(installer,folder,tag);',
 'if(DeferForOpenDeskSpace(owner,automatic))return;'
)){if(!$u.Contains($needle)){throw ('R219 both-channel verification/handoff missing '+$needle)}}
if(!$t.Contains('TestUpdateChecksumStagingV219();') -or
   !$t.Contains('string published=actual.ToLowerInvariant()')){
 throw 'R219 native reproducer for published checksum format missing'
}
Write-Host 'R219 STATIC PASS: one strict official SHA256 parser, both channels stage, PE/hash protection retained.'

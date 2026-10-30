$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$service=[IO.File]::ReadAllText("$root/DeskSpaceContentDuplicateV211.cs")
$deck=[IO.File]::ReadAllText("$root/DeskSpaceDropShelfV15.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($item in @('MaxFingerprintBytes=128L*1024*1024',
 'CryptographicOperations.FixedTimeEquals(reference,digest)',
 'cancel.ThrowIfCancellationRequested()',
 'FileAttributes.ReparsePoint','FileShare.Read',
 'after.LastWriteTimeUtc!=expectedWrite'))
{
 if(!$service.Contains($item)){throw ('R211 missing duplicate safety '+$item)}
}
foreach($item in @('private static DuplicateChoiceV211? pendingDuplicateV211;',
 'Content="Keep both"','Content="Skip"',
 'DeskSpaceContentDuplicateV211.FindMatching(',
 'Task.WhenAny(decision.Decision.Task,',
 'if(!keepBoth)continue;',
 'Views.Count==0)pendingDuplicateV211?.Decision.TrySetResult(false)'))
{
 if(!$deck.Contains($item)){throw ('R211 shared Deck preflight missing '+$item)}
}
if($deck.IndexOf('DeskSpaceContentDuplicateV211.FindMatching(') -gt
   $deck.IndexOf('var id=Guid.NewGuid().ToString("N");')){
 throw 'R211 preflight must occur before copy/staging'
}
if(!$smoke.Contains('TestContentDuplicatesV211();')){
 throw 'R211 native duplicate acceptance missing'
}
if($service.Contains('File.Delete(') -or
   $service.Contains('File.Move(') -or
   $service.Contains('File.WriteAll') -or
   $service.Contains('Process.Start(')){
 throw 'R211 content fingerprints must be read-only'
}
Write-Host 'R211 STATIC PASS: nonmodal shared choice, content SHA-256 and read-only source safety.'

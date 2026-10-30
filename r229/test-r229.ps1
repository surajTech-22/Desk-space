$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
$s=[IO.File]::ReadAllText($p)
$call='if(DeferForOpenDeskSpace(owner,automatic))'
if(([regex]::Matches($s,[regex]::Escape($call))).Count -ne 1){
 throw 'R229 expected exactly one install-only deferral after verified download'
}
$idxCache=$s.IndexOf('if(File.Exists(installer))')
$idxVerify=$s.IndexOf('Verify(installer,checksum,root);')
$idxDefer=$s.IndexOf($call)
$idxSupervisor=$s.LastIndexOf('await StartSupervisor(installer,folder,tag);')
if($idxCache -lt 0 -or $idxVerify -le $idxCache -or
   $idxDefer -le $idxVerify -or $idxSupervisor -le $idxDefer){
 throw 'R229 downloader, cryptographic verification, deferral, and supervisor are in the wrong order'
}
foreach($part in @(
 'bool cachedVerified=false;',
 'if(!cachedVerified)',
 'await Download(setup,installer,timeout.Token);',
 'Verify(installer,checksum,root);',
 'Log("R229 verified installer cache reused; no redownload.");',
 'Log("R229 verified update ready; installation deferred until other DeskSpace windows exit normally.");',
 'DeskSpaceVerifiedUpdateQueueV191.Stage(',
 'DeskSpaceAtomicInstallerV222.StageFromStreamAsync(',
 'throw new InvalidOperationException("R227 supervised installation already running;',
 'Application.Current.Shutdown();'
)){if(!$s.Contains($part)){throw ('R229 deferred updater safety feature missing '+$part)}}
Write-Host 'R229 PASS: verified download before peer defer, safe SHA256 cached reuse, and normal supervisor exit preserved.'

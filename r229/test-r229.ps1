$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
$s=[IO.File]::ReadAllText($p)
$guard='if(DeferForOpenDeskSpace(owner,automatic))return;'
if(([regex]::Matches($s,[regex]::Escape($guard))).Count -ne 2){
 throw 'R229 both Stable/Tester final install peer checks must remain unchanged'
}
foreach($part in @(
 'DeskSpaceVerifiedUpdateQueueV191.Stage(stableInstaller,',
 'DeskSpaceVerifiedUpdateQueueV191.Stage(installer,folder,',
 'if(!stableCachedV191)',
 'if(!testerCachedV191)',
 'Verify(stableInstaller,sha,null);',
 'Verify(installer,checksum,root);',
 'Log("R229 verified cached "+pending.Tag+" attempting safe supervisor handoff.");',
 'await StartSupervisor(pending.Installer,pending.Folder,pending.Tag);',
 'DeskSpaceVerifiedUpdateQueueV191.Clear(pending);',
 'Log("R229 verified update retained for retry after normal exit: "+',
 'catch(Exception error) when(error is InvalidOperationException or IOException)',
 'DeskSpaceAtomicInstallerV222.StageFromStreamAsync(',
 'throw new InvalidOperationException("R227 supervised installation already running;',
 'Application.Current.Shutdown();'
)){
 if(!$s.Contains($part)){throw ('R229 guarded update staging feature missing '+$part)}
}
$start=$s.IndexOf('Log("R229 verified cached "+pending.Tag+')
$handoff=$s.IndexOf('await StartSupervisor(pending.Installer,pending.Folder,pending.Tag);',$start)
$clear=$s.IndexOf('DeskSpaceVerifiedUpdateQueueV191.Clear(pending);',$handoff)
if($start -lt 0 -or $handoff -le $start -or $clear -le $handoff){
 throw 'R229 must clear verified candidate only after the supervisor actually starts'
}
Write-Host 'R229 STATIC PASS: download first, peer-safe deferral, exact SHA256 cached reuse, transient failure retains verified candidate.'

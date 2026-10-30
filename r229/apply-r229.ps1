$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R229 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
            DeskSpaceVerifiedUpdateQueueV191.Clear(pending);
            Log("R191 verified staged "+pending.Tag+" is now safe to hand off.");
            DeskSpaceUpdateProgress.Publish("Restarting",100);
            await StartSupervisor(pending.Installer,pending.Folder,pending.Tag);
            return true;
'@ @'
            // Do not discard the verified candidate BEFORE the supervisor
            // has accepted the handoff. Another DeskSpace instance may win
            // its install mutex or the process launcher may temporarily
            // refuse; retry on the existing automatic timer, not another
            // network download or repeated notification.
            Log("R229 verified cached "+pending.Tag+" attempting safe supervisor handoff.");
            DeskSpaceUpdateProgress.Publish("Restarting",100);
            try
            {
                await StartSupervisor(pending.Installer,pending.Folder,pending.Tag);
                DeskSpaceVerifiedUpdateQueueV191.Clear(pending);
                return true;
            }
            catch(Exception error) when(error is InvalidOperationException or IOException)
            {
                Log("R229 verified update retained for retry after normal exit: "+
                    error.GetType().Name);
                DeskSpaceUpdateProgress.Clear();
                return true;
            }
'@ 'retain verified candidate if supervisor handoff temporarily blocked'
Swap @'
                if(File.Exists(stableInstaller))
                {
                    try{Verify(stableInstaller,sha,null);stableCachedV191=true;}
                    catch(InvalidDataException){}
                }
'@ @'
                if(File.Exists(stableInstaller))
                {
                    try{Verify(stableInstaller,sha,null);stableCachedV191=true;}
                    catch(Exception error) when(error is InvalidDataException or
                        IOException or UnauthorizedAccessException)
                    {
                        Log("R229 stable cached installer invalid/inaccessible; revalidate by fresh download.");
                    }
                }
'@ 'stable verified-cache recovery from interrupted or locked installer'
Swap @'
            if(File.Exists(installer))
            {
                try{Verify(installer,checksum,root);testerCachedV191=true;}
                catch(InvalidDataException){}
            }
'@ @'
            if(File.Exists(installer))
            {
                try{Verify(installer,checksum,root);testerCachedV191=true;}
                catch(Exception error) when(error is InvalidDataException or
                    IOException or UnauthorizedAccessException)
                {
                    Log("R229 tester cached installer invalid/inaccessible; revalidate by fresh download.");
                }
            }
'@ 'tester verified-cache recovery from interrupted or locked installer'
# Preserve both final safe-install gates (stable and tester). R191 ALREADY
# stages downloads before these checks, and R227 protects the one supervisor.
if(([regex]::Matches($s,[regex]::Escape(
    'if(DeferForOpenDeskSpace(owner,automatic))return;'))).Count -ne 2){
 throw 'R229 both final safe peer-exit gates must remain after verified staging'
}
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R229: retain SHA-verified staged update after transient supervisor contention, allow safe cache recovery and retry installation only following normal Workspace exit.'

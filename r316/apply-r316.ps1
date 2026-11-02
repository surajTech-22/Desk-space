$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$updater=Join-Path $root 'DeskSpacePublicUpdaterV44.cs'
$supervisor=Join-Path $root 'DeskSpaceUpdateSupervisor.ps1'
if(!(Test-Path $updater) -or !(Test-Path $supervisor)){throw 'R316 shipping updater/supervisor source missing'}
$utf=[Text.UTF8Encoding]::new($false)
$u=[IO.File]::ReadAllText($updater).Replace(([string][char]13+[string][char]10),[string][char]10)
$p=[IO.File]::ReadAllText($supervisor).Replace(([string][char]13+[string][char]10),[string][char]10)
function One([string]$old,[string]$new,[string]$name,[string]$target){
  $crlf=([string][char]13+[string][char]10)
  $lf=[string][char]10
  $old=$old.Replace($crlf,$lf)
  $new=$new.Replace($crlf,$lf)
  $contents=Get-Variable -Name $target -ValueOnly
  $count=([regex]::Matches($contents,[regex]::Escape($old))).Count
  if($count -ne 1){throw "R316 unexpected $name anchor count: $count"}
  Set-Variable -Name $target -Value ($contents.Replace($old,$new)) -Scope Script
}
$old=@'
        var ps=Path.Combine(folder,"CompleteDeskSpaceUpdate."+
'@
$new=@'
        // No update is accepted until the single mutex-owning supervisor
        // acknowledges this unique handoff after process startup.
        var handoffNonceV316=Guid.NewGuid().ToString("N");
        var handoffAckV316=Path.Combine(folder,"handoff-"+handoffNonceV316+".ack");
        var ps=Path.Combine(folder,"CompleteDeskSpaceUpdate."+
'@
One $old $new 'nonce-scoped handoff' 'u'
$old=@'
            "-OldPid",Environment.ProcessId.ToString()
'@
$new=@'
            "-OldPid",Environment.ProcessId.ToString(),
            "-AcceptPath",handoffAckV316,
            "-AcceptNonce",handoffNonceV316
'@
One $old $new 'supervisor arguments' 'u'
$old=@'
        if(Process.Start(child)==null)
            throw new IOException("Cannot start update supervisor");
'@
$new=@'
        using var supervisorV316=Process.Start(child)
            ??throw new IOException("Cannot start update supervisor");
        var acceptedV316=false;
        var handoffDeadlineV316=DateTime.UtcNow.AddSeconds(30);
        try
        {
            while(DateTime.UtcNow<handoffDeadlineV316)
            {
                try
                {
                    if(File.Exists(handoffAckV316) &&
                       File.ReadAllText(handoffAckV316).Trim()==handoffNonceV316)
                    {
                        acceptedV316=true;
                        break;
                    }
                }
                catch(IOException){}
                catch(UnauthorizedAccessException){}
                if(supervisorV316.HasExited)break;
                await Task.Delay(250);
            }
        }
        finally
        {
            try{File.Delete(handoffAckV316);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
        if(!acceptedV316)
        {
            Log("R316 supervisor never acknowledged install ownership; verified candidate remains queued.");
            throw new SupervisorBusyV310Exception();
        }
        Log("R316 "+tag+" accepted by the mutex-owning supervisor; handoff confirmed.");
'@
One $old $new 'verified supervisor acceptance' 'u'
# Expected contention must not interrupt the user with a modal popup.
$busyPattern='(?ms)^(\s*)if\(!automatic\)\s*MessageBox\.Show\(owner,\s*"(?:A DeskSpace installation|Another DeskSpace installation).*?"DeskSpace update in progress",MessageBoxButton\.OK,\s*MessageBoxImage\.Information\);'
$hits=[regex]::Matches($u,$busyPattern)
if($hits.Count -ne 2){throw "R316 expected two busy modal blocks, found $($hits.Count)"}
$u=[regex]::Replace($u,$busyPattern,'')
$old=@'
    [Parameter(Mandatory=$true)][int]$OldPid
'@
$new=@'
    [Parameter(Mandatory=$true)][int]$OldPid,
    [string]$AcceptPath='',
    [string]$AcceptNonce=''
'@
One $old $new 'backward compatible supervisor params' 'p'
$old=@'
    [void](Assert-R185InstallScope $install)
    Report ('R185 update transaction '+$txn+' version '+$version+' started.')
'@
$new=@'
    [void](Assert-R185InstallScope $install)
    if($AcceptPath -or $AcceptNonce){
        if($AcceptNonce -cnotmatch '^[0-9a-f]{32}$'){
            throw 'R316 invalid supervisor acceptance token'
        }
        $expected=[IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $Installer) ('handoff-'+$AcceptNonce+'.ack')))
        if(![string]::Equals([IO.Path]::GetFullPath($AcceptPath),$expected,[StringComparison]::OrdinalIgnoreCase)){
            throw 'R316 acceptance file must be inside verified installer staging folder'
        }
        # Ownership acknowledged AFTER acquiring mutex and validating scope,
        # and BEFORE the supervisor requests normal app shutdown.
        [IO.File]::WriteAllText($expected,$AcceptNonce,[Text.UTF8Encoding]::new($false))
        Report 'R316 mutex owner accepted verified update transaction.'
    }
    Report ('R185 update transaction '+$txn+' version '+$version+' started.')
'@
One $old $new 'post-lock acknowledgement' 'p'
if($u -notmatch 'if\(!acceptedV316\)' -or $p -notmatch 'R316 mutex owner accepted'){throw 'R316 missing verified handoff guard'}
if([regex]::Matches($u,'DeskSpace update in progress",MessageBoxButton').Count -ne 0){throw 'R316 still has duplicate installation dialogs'}
[IO.File]::WriteAllText($updater,$u,$utf)
[IO.File]::WriteAllText($supervisor,$p,$utf)
Write-Host 'R316 shipped: supervisor acknowledges named mutex ownership; busy installs retain verified cache and retry silently.'
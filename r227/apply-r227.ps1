$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r227/DeskSpaceReleaseAttemptV227.cs' -Destination "$root/DeskSpaceReleaseAttemptV227.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R227 "+$why+" expected 1 anchor, got "+$n)}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
}
$supervisor="$root/DeskSpaceUpdateSupervisor.ps1"
Swap $supervisor @'
    if(!$ownsMutex){throw 'R185 another supervised install is active'}
'@ @'
    if(!$ownsMutex){
        Report 'R227 active supervisor owns install lock; duplicate exits without relaunch.'
        return
    }
'@ 'mutex loser exits without entering failure fallback'
Swap $supervisor @'
    $running=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object {$_.CommandLine -like '*--widget-host*'})
'@ @'
    $running=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -like '*--widget-host*' -and
            ($null -eq $_.ExecutablePath -or
             $_.ExecutablePath.Equals($original,[StringComparison]::OrdinalIgnoreCase))
        })
'@ 'check only widget hosts for intended executable'
Swap $supervisor @'
    try{Launch-R185Host}catch{Report ('R185 fallback host relaunch unavailable: '+$_.Exception.GetType().Name)}
'@ @'
    if($ownsMutex){
        try{
            $visible=@(Get-CimInstance Win32_Process -Filter "Name='DeskSpace.exe'" -ErrorAction SilentlyContinue |
                Where-Object {$_.ExecutablePath -and
                  $_.ExecutablePath.Equals($original,[StringComparison]::OrdinalIgnoreCase)})
            if($visible.Count -eq 0){Launch-R185Host}
            else{Report 'R227 failure left a live DeskSpace process; preserving it without relaunch.'}
        }catch{Report ('R227 fallback host unavailable: '+$_.Exception.GetType().Name)}
    }
'@ 'fallback only for an owned transaction and no live user app'
Swap $supervisor @'
    if(!$ack){throw 'R185 installed widget did not acknowledge primary WPF Loaded'}
'@ @'
    if(!$ack){
        Report ('R227 installed widget did not produce matching receipt for '+$version+
            '; snapshot retained and live app will not be force-terminated.')
        throw 'R185 installed widget did not acknowledge primary WPF Loaded'
    }
'@ 'precise receipt diagnostics'
$push="$root/DeskSpaceReleasePushV133.cs"
Swap $push @'
        lastAttemptedTag=tag;
        lastAttemptUtc=now;
        Log("Release hint "+tag+" received; requesting immediate verified GitHub check.");
'@ @'
        // ntfy replays cached releases on every reconnect. Use an atomic,
        // per-user tag/time guard across widget processes, without bypassing
        // routine GitHub checks or manual Check for Updates.
        if(!DeskSpaceReleaseAttemptV227.ShouldDispatch(tag))
        {
            Log("R227 replayed release hint suppressed; scheduled checks retained.");
            return;
        }
        lastAttemptedTag=tag;
        lastAttemptUtc=now;
        Log("Release hint "+tag+" received; requesting immediate verified GitHub check.");
'@ 'durable cross-process release hint suppression'
$updater="$root/DeskSpacePublicUpdaterV44.cs"
Swap $updater @'
    private static async Task StartSupervisor(string installer,string folder,string tag)
    {
        var ps=Path.Combine(folder,"CompleteDeskSpaceUpdate.ps1");
'@ @'
    private static async Task StartSupervisor(string installer,string folder,string tag)
    {
        // Never shut down this widget to launch a duplicate supervisor while
        // another process already owns the R185 install transaction.
        using(var existing=new Mutex(false,@"Local\DeskSpace-UpdateSupervisor-V185"))
        {
            bool free=false;
            try
            {
                try{free=existing.WaitOne(0);}
                catch(AbandonedMutexException){free=true;}
            }
            finally{if(free)existing.ReleaseMutex();}
            if(!free)
            {
                Log("R227 active supervisor already owns installer; retaining this widget.");
                return;
            }
        }
        var ps=Path.Combine(folder,"CompleteDeskSpaceUpdate."+
            Environment.ProcessId+"."+Guid.NewGuid().ToString("N")+".ps1");
'@ 'avoid writing shared supervisor script or quitting during mutex contention'
Write-Host 'R227: no mutex-loser widget restart, restart-resistant release-event suppression and race-safe supervisor handoff.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Patch reconstructed shipping source only. Preserve every existing release
# flow, original auto-update trust check and user data directory.
$root='DeskSpace_Windows_R1/DeskSpace'
$lf=[string][char]10
$crlf=([string][char]13)+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$why) {
    $old=$old.Replace($crlf,$lf)
    $new=$new.Replace($crlf,$lf)
    $body=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $matches=([regex]::Matches($body,[regex]::Escape($old))).Count
    if($matches -ne 1){throw "R367 $why requires exactly one shipping anchor, found $matches"}
    [IO.File]::WriteAllText($path,$body.Replace($old,$new),
        [Text.UTF8Encoding]::new($false))
}
$listener=Join-Path $root 'DeskSpaceReleasePushV133.cs'
$updater=Join-Path $root 'DeskSpacePublicUpdaterV44.cs'
if(!(Test-Path -LiteralPath $listener) -or !(Test-Path -LiteralPath $updater)) {
    throw 'R367 trusted shipping updater and listener must exist'
}
Swap $listener @'
            streamOwner=new System.Threading.Mutex(true,
                @"Local\DeskSpace-ReleasePushV133",out bool ownsStream);
            if(!ownsStream)
            {
                Log("Release listener already active in another DeskSpace process.");
                streamOwner.Dispose();
                streamOwner=null;
                return;
            }
        }
        catch(UnauthorizedAccessException)
        {
            Log("Cannot acquire single-instance release listener mutex.");
            return;
        }
        _=ListenAsync(owner);
'@ @'
            // R367: createdNew does not prove ownership; an existing mutex
            // can be unowned or abandoned after a process exit.
            streamOwner=new System.Threading.Mutex(false,
                @"Local\DeskSpace-ReleasePushV133");
            bool acquired;
            try{acquired=streamOwner.WaitOne(0);}
            catch(AbandonedMutexException){acquired=true;}
            if(!acquired)
            {
                Log("Release listener active in another DeskSpace process; "+
                    "will retry ownership on the next periodic tick.");
                streamOwner.Dispose();
                streamOwner=null;
                started=false;
                return;
            }
            Log("R367 release listener ownership acquired.");
        }
        catch(UnauthorizedAccessException)
        {
            streamOwner?.Dispose();
            streamOwner=null;
            started=false;
            Log("Cannot acquire release listener mutex; retry remains enabled.");
            return;
        }
        _=ListenAsync(owner);
'@ 'listener mutex actual ownership'

Swap $updater @'
        timer.Tick+=async (_,_)=>await CheckAsync(owner,true);
'@ @'
        timer.Tick+=async (_,_)=>
        {
            // R367: another Workspace may lose the listener race, but must
            // recover ownership if the prior owner later exits.
            DeskSpaceReleasePushV133.Start(owner);
            await CheckAsync(owner,true);
        };
'@ 'listener takeover on existing periodic timer'

Swap $updater @'
        await using var input=await response.Content.ReadAsStreamAsync(ct);
        // R222: never overwrite the cached installer while downloading.
'@ @'
        await using var input=await response.Content.ReadAsStreamAsync(ct);
        // R367: privacy-safe markers distinguish interrupted network
        // transfers from a supervisor which never started.
        Log("R367 installer transfer started; expected bytes="+
            (response.Content.Headers.ContentLength?.ToString()??"unknown"));
        // R222: never overwrite the cached installer while downloading.
'@ 'transfer start visibility'

Swap $updater @'
        await DeskSpaceAtomicInstallerV222.StageFromStreamAsync(input,file,ct);
    }
'@ @'
        await DeskSpaceAtomicInstallerV222.StageFromStreamAsync(input,file,ct);
        Log("R367 installer transfer completed; bytes="+
            new FileInfo(file).Length+"; awaiting official SHA-256 verification.");
    }
'@ 'transfer completed visibility'

Swap $updater @'
        catch(Exception ex)
        {
            Log("Update failed at "+stage+": "+ex.GetType().Name+" "+ex.Message);
            if(!automatic)MessageBox.Show(owner,
'@ @'
        catch(Exception ex)
        {
            Log("Update failed at "+stage+": "+ex.GetType().Name+" "+ex.Message);
            // R367: a transient failed tester download should not wait
            // another 30 minutes. Retain GitHub API 403/429 backoff and
            // ALL existing installer trust gates. No tight retry loop.
            bool transient=ex is HttpRequestException or IOException or
                OperationCanceledException;
            if(automatic && Channel=="tester" && transient &&
               (stage.StartsWith("Download",StringComparison.Ordinal) ||
                stage=="GitHub release metadata"))
            {
                DeskSpaceUpdateRateV127.SaveUtc(
                    DeskSpaceUpdateRateV127.PollFile(Local,"tester"),
                    DateTimeOffset.UtcNow.AddMinutes(-25));
                Log("R367 transient tester network failure; next automatic "+
                    "release check eligible in approximately five minutes.");
            }
            if(!automatic)MessageBox.Show(owner,
'@ 'five-minute post-network-failure retry'
$listenerText=Get-Content -LiteralPath $listener -Raw
$updaterText=Get-Content -LiteralPath $updater -Raw
foreach($needle in @('streamOwner.WaitOne(0)','catch(AbandonedMutexException)','started=false;')) {
    if(!$listenerText.Contains($needle)){throw ('R367 listener guard missing: '+$needle)}
}
foreach($needle in @('DeskSpaceReleasePushV133.Start(owner)','R367 installer transfer started',
                   'R367 transient tester network failure','DateTimeOffset.UtcNow.AddMinutes(-25)',
                   'DeskSpaceVerifiedUpdateQueueV191.Stage(')) {
    if(!$updaterText.Contains($needle)){throw ('R367 updater trust/repair missing: '+$needle)}
}
Write-Host 'R367 PASS: listener ownership recovery, bounded download retry, and verification retained.'

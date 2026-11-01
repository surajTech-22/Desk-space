$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceGracefulExitV230.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$label)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($count -ne 1){throw ("R233 "+$label+": expected exactly one anchor, found "+$count)}
 $script:s=$script:s.Replace($before,$after)
}
Swap '    private static DateTime lastAttempt=DateTime.MinValue;' @'
    // One shutdown attempt for each rising edge of the supervisor event.
    // A WPF Closing handler can veto unsaved work. The event remains set
    // while the supervisor waits up to 70 seconds; never re-prompt the user
    // every eight seconds while that SAME request is still active.
    private static bool handledCurrentSignal;
    internal static bool ConsumeSignal(bool active)
    {
        if(!active)
        {
            handledCurrentSignal=false;
            return false;
        }
        if(handledCurrentSignal)return false;
        handledCurrentSignal=true;
        return true;
    }
'@ 'one-shot update-close signal latch'
Swap @'
            if(DateTime.UtcNow-lastAttempt<TimeSpan.FromSeconds(8))return;
            try
            {
                using var signal=EventWaitHandle.OpenExisting(EventName(
                    Path.Combine(AppContext.BaseDirectory,"DeskSpace.exe")));
                if(!signal.WaitOne(0))return;
            }
            catch(WaitHandleCannotBeOpenedException){return;}
            catch(UnauthorizedAccessException){return;}
            lastAttempt=DateTime.UtcNow;
'@ @'
            bool active;
            try
            {
                using var signal=EventWaitHandle.OpenExisting(EventName(
                    Path.Combine(AppContext.BaseDirectory,"DeskSpace.exe")));
                active=signal.WaitOne(0);
            }
            catch(WaitHandleCannotBeOpenedException){active=false;}
            catch(UnauthorizedAccessException){active=false;}
            // Re-arm only AFTER the supervisor resets the event. A vetoed
            // close is never retried under the same installation attempt.
            if(!ConsumeSignal(active))return;
'@ 'avoid repeated unsaved-work close prompts'
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R233: scoped updater shutdown requests run once per signal; unsaved-work veto remains respected.'

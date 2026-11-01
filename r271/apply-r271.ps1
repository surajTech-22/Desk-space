$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceReminderScheduleV83.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
        if(snoozed)
        {
            item.LastAlert=IsRecurring(item)?null:now;
            return false;
        }
'@.Replace($crlf,$lf)
$new=@'
        if(snoozed)
        {
            if(IsRecurring(item))
            {
                // The snooze notification is DISPLAY-ONLY. The original
                // recurring Due can be hours/days in the past. Rearm it
                // past the snooze dismissal, or it will immediately fire
                // again (and again) after the user closes the alert.
                if(item.Due<=now)item.Due=Next(item,now);
                item.LastAlert=null;
            }
            else item.LastAlert=now;
            return false;
        }
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw "R271 recurring snooze anchor count $n"}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R271: acknowledge snooze without action and advance recurrence beyond stale Due.'

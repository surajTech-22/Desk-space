$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R271 $why anchor count=$count"}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestQuickNoteValidationV270();' @'
                TestQuickNoteValidationV270();
                TestRecurringSnoozeRecoveryV271();
'@ 'register recurrence test'
$method=@'
    private static void TestRecurringSnoozeRecoveryV271()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceReminderScheduleV83",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var snooze=type.GetMethod("Snooze",f)!;
        var mark=type.GetMethod("MarkAlert",f)!;
        var due=type.GetMethod("Due",f)!;
        var started=new DateTime(2026,11,2,9,30,0);
        var recurring=new ReminderItem{
            Title="Daily audit",Due=started.AddDays(-3),
            RepeatPattern="Daily",AlertAction="Open URL",
            AlertTarget="https://example.org/"
        };
        var ws=new Workspace();
        ws.Reminders.Add(recurring);
        snooze.Invoke(null,new object[]{recurring,started,5});
        var at=started.AddMinutes(5);
        Assert(recurring.SnoozeOnly && recurring.SnoozedUntil==at &&
            !recurring.LastAlert.HasValue,
            "R271 snooze should defer instead of repeating original alert immediately");
        Assert(!(bool)mark.Invoke(null,new object[]{recurring,at})! &&
            recurring.Due>at && recurring.LastAlert==null &&
            recurring.SnoozedUntil==null && !recurring.SnoozeOnly,
            "R271 recurring snooze acknowledgement must not relaunch URL and "+
            "must advance the original recurrence past the snooze time");
        var pending=((System.Collections.IEnumerable)due.Invoke(
            null,new object[]{ws,at.AddSeconds(1)})!).Cast<object>().ToArray();
        Assert(pending.Length==0,
            "R271 snoozed recurring reminder must not fire a second time immediately");
        var originalNext=recurring.Due;
        snooze.Invoke(null,new object[]{recurring,at,10});
        var second=at.AddMinutes(10);
        Assert(!(bool)mark.Invoke(null,new object[]{recurring,second})! &&
               recurring.Due==originalNext &&
               recurring.LastAlert==null,
            "R271 snooze before next recurrence must not skip next scheduled event");
        var once=new ReminderItem{
            Title="One-time",Due=started.AddMinutes(-1),
            AlertAction="Open file",AlertTarget=@"C:\Reports\audit.xlsx"
        };
        snooze.Invoke(null,new object[]{once,started,5});
        Assert(!(bool)mark.Invoke(null,new object[]{once,at})! &&
               once.LastAlert==at && once.SnoozedUntil==null,
            "R271 one-time snooze should be acknowledged without relaunch");
        Console.WriteLine("R271 PASS: recurring snooze rearmed past dismissal "+
            "without double alerts/actions; one-time/future date semantics preserved.");
    }

'@
Swap '    private static void TestQuickNoteValidationV270()' ($method+
 '    private static void TestQuickNoteValidationV270()') 'register implementation'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R271 native WPF tests cover recurring snooze no duplicate, next future occurrence, one-shot no action.'

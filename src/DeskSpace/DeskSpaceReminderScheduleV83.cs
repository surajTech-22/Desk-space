using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskSpace;

// One scheduler shared by the widget alert shelf and both reminder editors.
// Old workspace.json records remain valid: new fields default to None/null.
internal static class DeskSpaceReminderScheduleV83
{
    internal static readonly string[] RecurrenceOptions={
        "None","Daily","Weekdays","Weekly","Monthly","Yearly",
        "Selected days","Every N days"
    };

    internal static bool IsRecurring(ReminderItem item)
        =>!string.IsNullOrWhiteSpace(item.RepeatPattern) &&
          item.RepeatPattern!="None";

    internal static DateTime EffectiveDue(ReminderItem item)
        =>item.SnoozedUntil??item.Due;

    internal static IReadOnlyList<ReminderItem> Due(Workspace ws,DateTime now)
        =>ws.Reminders.Where(r=>!r.Done && !r.LastAlert.HasValue &&
             EffectiveDue(r)<=now)
            .OrderBy(EffectiveDue).Take(20).ToArray();

    private static DateTime NextSelectedDay(DateTime current,
        DateTime now,string spec)
    {
        var permitted=new HashSet<DayOfWeek>();
        foreach(var value in (spec??"").Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries))
        {
            foreach(var day in Enum.GetValues<DayOfWeek>())
                if(day.ToString().StartsWith(value,StringComparison.OrdinalIgnoreCase) &&
                   value.Length>=3){permitted.Add(day);break;}
        }
        if(permitted.Count==0)permitted.Add(current.DayOfWeek);
        var candidate=current;
        for(int i=0;i<3660;i++)
        {
            candidate=candidate.AddDays(1);
            if(candidate>now && permitted.Contains(candidate.DayOfWeek))
                return candidate;
        }
        return now.AddDays(1);
    }

    internal static DateTime Next(ReminderItem item,DateTime now)
    {
        DateTime candidate=item.Due;
        var rule=item.RepeatPattern??"None";
        // Do not replay an entire backlog after a long sleep or holiday.
        // Jump over missed occurrences but keep the original local clock time.
        switch(rule)
        {
            case "Daily":
                candidate=candidate.AddDays(Math.Max(1,
                    (int)Math.Floor((now-candidate).TotalDays)+1));
                break;
            case "Weekly":
                candidate=candidate.AddDays(7*Math.Max(1,
                    (int)Math.Floor((now-candidate).TotalDays/7)+1));
                break;
            case "Weekdays":
                do{candidate=candidate.AddDays(1);}
                while(candidate<=now || candidate.DayOfWeek is
                    (DayOfWeek.Saturday or DayOfWeek.Sunday));
                break;
            case "Monthly":
                for(int i=0;i<1200&&candidate<=now;i++)
                    candidate=candidate.AddMonths(1);
                break;
            case "Yearly":
                for(int i=0;i<200&&candidate<=now;i++)
                    candidate=candidate.AddYears(1);
                break;
            case "Selected days":
                candidate=NextSelectedDay(candidate,now,item.RepeatDays);
                break;
            case "Every N days":
                int step=Math.Clamp(item.RepeatEveryDays,1,365);
                candidate=candidate.AddDays(step*Math.Max(1,
                    (int)Math.Floor((now-candidate).TotalDays/step)+1));
                break;
            default: return candidate;
        }
        return candidate>now?candidate:now.AddMinutes(1);
    }

    // Returns true only for the original occurrence, which may run the
    // configured app/file/URL action exactly once. A snoozed alert is
    // display-only and never launches an action a second time.
    internal static bool MarkAlert(ReminderItem item,DateTime now)
    {
        bool snoozed=item.SnoozeOnly && item.SnoozedUntil.HasValue;
        item.SnoozedUntil=null;item.SnoozeOnly=false;
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
        if(IsRecurring(item))
        {
            item.Due=Next(item,now);
            item.LastAlert=null;
        }
        else item.LastAlert=now;
        return true;
    }

    internal static void Snooze(ReminderItem item,DateTime now,int minutes)
    {
        item.SnoozedUntil=now.AddMinutes(Math.Clamp(minutes,1,1440));
        item.SnoozeOnly=true;
        item.LastAlert=null;
    }
}

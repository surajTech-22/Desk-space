using System;
using System.Globalization;
using System.Linq;

namespace DeskSpace;

// HMMSS / HHMMSS; optional AM/PM changes to 12-hour validation.
// Do not reformat a five-digit prefix during typing because a sixth digit
// may still be coming. Format on focus exit or on Add.
internal static class DeskSpaceReminderTimeV78
{
    internal static bool TryParse(string? input,out TimeSpan time,out string normalized)
    {
        time=default;
        normalized="";
        if(string.IsNullOrWhiteSpace(input))return false;
        string text=input.Trim().ToUpperInvariant().Replace(".","");
        bool suffix=false,pm=false;
        if(text.EndsWith("AM",StringComparison.Ordinal))
        {suffix=true;text=text[..^2].Trim();}
        else if(text.EndsWith("PM",StringComparison.Ordinal))
        {suffix=true;pm=true;text=text[..^2].Trim();}
        text=string.Concat(text.Where(c=>!char.IsWhiteSpace(c)));
        if(text.Length==0)return false;
        int hours,minutes=0,seconds=0;
        if(text.Contains(':'))
        {
            var parts=text.Split(':');
            if(parts.Length<2||parts.Length>3 ||
               parts.Any(p=>p.Length is <1 or >2 ||
                   !p.All(c=>c>='0'&&c<='9')))
                return false;
            if(!int.TryParse(parts[0],NumberStyles.None,
                   CultureInfo.InvariantCulture,out hours) ||
               !int.TryParse(parts[1],NumberStyles.None,
                   CultureInfo.InvariantCulture,out minutes))return false;
            if(parts.Length==3&&!int.TryParse(parts[2],NumberStyles.None,
                  CultureInfo.InvariantCulture,out seconds))return false;
        }
        else
        {
            if(text.Length<1||text.Length>6 ||
               !text.All(c=>c>='0'&&c<='9'))return false;
            int width=text.Length>=5?text.Length-4:
                text.Length>=3?text.Length-2:text.Length;
            if(!int.TryParse(text[..width],NumberStyles.None,
                CultureInfo.InvariantCulture,out hours))return false;
            if(text.Length>=3&&!int.TryParse(text.Substring(width,2),
                NumberStyles.None,CultureInfo.InvariantCulture,out minutes))
                return false;
            if(text.Length>=5&&!int.TryParse(text[^2..],
                NumberStyles.None,CultureInfo.InvariantCulture,out seconds))
                return false;
        }
        if(minutes>59||seconds>59)return false;
        int storedHours=hours;
        if(suffix)
        {
            if(hours<1||hours>12)return false;
            storedHours=hours%12+(pm?12:0);
            normalized=string.Format(CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00} {3}",
                hours,minutes,seconds,pm?"PM":"AM");
        }
        else
        {
            if(hours>23)return false;
            normalized=string.Format(CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00}",hours,minutes,seconds);
        }
        time=new TimeSpan(storedHours,minutes,seconds);
        return true;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Globalization;

namespace DeskSpace;

// Defensive GitHub REST API request policy. No access token, mirror, proxy,
// AV bypass, rate-limit retry storm or modification of installed data.
// The 403/429 back-off persists across app launches. A successful API check
// also enforces a 30 minute automatic-only interval across restarts.
internal static class DeskSpaceUpdateRateV127
{
    internal static string BackoffFile(string local) =>
        Path.Combine(local,"github-api-backoff-utc.txt");

    internal static string PollFile(string local,string channel) =>
        Path.Combine(local,"github-api-last-poll-"+(channel=="tester"?"tester":"stable")+".txt");

    internal static DateTimeOffset ReadUtc(string path)
    {
        try
        {
            if(DateTimeOffset.TryParseExact(File.ReadAllText(path).Trim(),
                "O",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,
                out var parsed))return parsed.ToUniversalTime();
        }
        catch(IOException){} catch(UnauthorizedAccessException){}
        catch(System.Security.SecurityException){}
        return DateTimeOffset.MinValue;
    }
    internal static void SaveUtc(string path,DateTimeOffset value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,value.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture));
        }
        catch(IOException){} catch(UnauthorizedAccessException){}
        catch(System.Security.SecurityException){}
    }
    internal static bool IsDeferred(string local,string channel,
        DateTimeOffset utcNow,bool automatic,out DateTimeOffset next)
    {
        var backoff=ReadUtc(BackoffFile(local));
        var poll=automatic?ReadUtc(PollFile(local,channel)).AddMinutes(30):
            DateTimeOffset.MinValue;
        next=backoff>poll?backoff:poll;
        return utcNow<next;
    }
    internal static bool IsLimitResponse(HttpResponseMessage response,
        string? responseBody)
    {
        if(response.StatusCode!=HttpStatusCode.Forbidden &&
           (int)response.StatusCode!=429)return false;
        if((int)response.StatusCode==429 ||
           response.Headers.RetryAfter!=null)return true;
        if(response.Headers.TryGetValues("x-ratelimit-remaining",out var r) &&
            r.Any(s=>s.Trim()=="0"))return true;
        return responseBody?.IndexOf("rate limit",
            StringComparison.OrdinalIgnoreCase)>=0;
    }
    internal static DateTimeOffset RetryAfter(HttpResponseMessage response,
        DateTimeOffset utcNow)
    {
        // Safe conservative default if rate headers are absent or malformed.
        var after=utcNow.AddMinutes(65);
        var retry=response.Headers.RetryAfter;
        if(retry?.Delta is TimeSpan delay && delay>TimeSpan.Zero &&
           delay<TimeSpan.FromDays(2))
        {
            var candidate=utcNow.Add(delay).AddMinutes(2);
            if(candidate>after)after=candidate;
        }
        if(retry?.Date is DateTimeOffset date && date>utcNow &&
           date<utcNow.AddDays(2))
        {
            var candidate=date.AddMinutes(2);
            if(candidate>after)after=candidate;
        }
        if(response.Headers.TryGetValues("x-ratelimit-remaining",out var remaining) &&
           remaining.Any(s=>s.Trim()=="0") &&
           response.Headers.TryGetValues("x-ratelimit-reset",out var values))
        {
            var resetValue=values.FirstOrDefault();
            if(long.TryParse(resetValue,NumberStyles.Integer,
                CultureInfo.InvariantCulture,out var seconds))
            {
                try
                {
                    var reset=DateTimeOffset.FromUnixTimeSeconds(seconds);
                    if(reset>utcNow && reset<utcNow.AddDays(2))
                    {
                        var candidate=reset.AddMinutes(2);
                        if(candidate>after)after=candidate;
                    }
                }
                catch(ArgumentOutOfRangeException){}
            }
        }
        return after;
    }
}

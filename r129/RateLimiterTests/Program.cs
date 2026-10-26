using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using DeskSpace;

static void Check(bool condition,string message)
{
    if(!condition)throw new Exception("R129 test FAIL: "+message);
}
string local=Path.Combine(Path.GetTempPath(),"DeskSpace-R129-"+
    Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(local);
try
{
    var now=DateTimeOffset.UtcNow;
    Check(!DeskSpaceUpdateRateV127.IsDeferred(local,"stable",now,true,out _),
        "initial check should run");
    DeskSpaceUpdateRateV127.SaveUtc(
        DeskSpaceUpdateRateV127.PollFile(local,"stable"),now);
    Check(DeskSpaceUpdateRateV127.IsDeferred(local,"stable",now.AddMinutes(4),
        true,out var future) && future>=now.AddMinutes(29),
        "automatic requests must remain spaced across restarts");
    Check(!DeskSpaceUpdateRateV127.IsDeferred(local,"stable",
        now.AddMinutes(4),false,out _),
        "manual check bypasses only normal automatic polling interval");
    DeskSpaceUpdateRateV127.SaveUtc(
        DeskSpaceUpdateRateV127.BackoffFile(local),now.AddHours(2));
    Check(DeskSpaceUpdateRateV127.IsDeferred(local,"stable",
        now.AddMinutes(4),false,out var until) && until>now.AddHours(1),
        "manual requests must NEVER bypass a GitHub 403/429 backoff");
    Check(DeskSpaceUpdateRateV127.IsDeferred(local,"tester",
        now.AddMinutes(4),false,out _),
        "rate-limit backoff is shared across channels");
    using var exhausted=new HttpResponseMessage(HttpStatusCode.Forbidden);
    exhausted.Headers.TryAddWithoutValidation("x-ratelimit-remaining","0");
    exhausted.Headers.TryAddWithoutValidation("x-ratelimit-reset",
        now.AddMinutes(70).ToUnixTimeSeconds().ToString());
    Check(DeskSpaceUpdateRateV127.IsLimitResponse(exhausted,null),
        "GitHub 403 with x-ratelimit-remaining=0 must be recognized");
    Check(DeskSpaceUpdateRateV127.RetryAfter(exhausted,now)>
        now.AddMinutes(70),"reset timestamp must extend retry pause");
    using var denied=new HttpResponseMessage(HttpStatusCode.Forbidden);
    Check(!DeskSpaceUpdateRateV127.IsLimitResponse(denied,"Access denied"),
        "security 403 must not be mislabeled as GitHub rate limiting");
    Check(DeskSpaceUpdateRateV127.IsLimitResponse(denied,
        "API rate limit exceeded"),"GitHub rate limit message detected");
    using var tooMany=new HttpResponseMessage((HttpStatusCode)429);
    tooMany.Headers.RetryAfter=
        new System.Net.Http.Headers.RetryConditionHeaderValue(
            TimeSpan.FromMinutes(100));
    Check(DeskSpaceUpdateRateV127.IsLimitResponse(tooMany,null),
        "HTTP 429 must always be rate limit");
    Check(DeskSpaceUpdateRateV127.RetryAfter(tooMany,now)>
        now.AddMinutes(100),"Retry-After must be respected");
    Console.WriteLine("R129 PASS: persistent automatic spacing, manual override constraints, rate 403/429, Retry-After, reset time, unrelated security 403.");
}
finally
{
    try{Directory.Delete(local,true);}catch(IOException){}
}

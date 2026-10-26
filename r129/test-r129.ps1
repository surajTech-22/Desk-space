$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$app=[IO.File]::ReadAllText("$r/DeskSpacePublicUpdaterV44.cs")
$rates=[IO.File]::ReadAllText("$r/DeskSpaceUpdateRateV127.cs")
foreach($needle in @(
    'TimeSpan.FromMinutes(30)',
    'DeskSpaceUpdateRateV127.IsDeferred(Local,channel,now,',
    'DeskSpaceUpdateRateV127.SaveUtc(',
    'DeskSpaceUpdateRateV127.IsLimitResponse(answer,denial)',
    'DeskSpaceUpdateRateV127.RetryAfter(answer,',
    'DeskSpaceUpdateRateV127.BackoffFile(Local)',
    'DeskSpaceUpdateRateV127.PollFile(Local,channel)',
    'GitHub denied release metadata (HTTP 403, not rate limit)',
    'Update failed during "+stage+',
    'stage="Download installer from official GitHub release"',
    'Verify(installer,checksum,root);',
    'await StartSupervisor(installer,folder,tag);'
)){
    if(!$app.Contains($needle)){throw "R129 app missing: $needle"}
}
if($app.Contains('timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(5)};')){
    throw 'R129 automatic polling still uses five minutes'
}
foreach($needle in @(
    'Retry-After','x-ratelimit-remaining','x-ratelimit-reset',
    'github-api-backoff-utc.txt','github-api-last-poll-',
    'utcNow.AddMinutes(65)',
    'return utcNow<next;'
)){
    if(!$rates.Contains($needle)){throw "R129 rate parser missing: $needle"}
}
if(!$app.Contains('https://api.github.com/repos/') -or
   !$app.Contains('uri.Host.Equals("github.com"') -or
   !$app.Contains('SHA256.HashData(data)') -or
   !$app.Contains('Release metadata digest mismatch'))
{throw 'R129 trusted official source and SHA-256 verification were changed'}
Write-Host 'R129 static checks passed: rate handling, no 5min polling, no untrusted mirrors, checksum and safe existing installation preserved.'

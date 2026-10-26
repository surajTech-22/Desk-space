$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$fmt=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$upd=[IO.File]::ReadAllText("$r/DeskSpacePublicUpdaterV44.cs")
$rate=[IO.File]::ReadAllText("$r/DeskSpaceUpdateRateV127.cs")
foreach($needle in @(
    'private static void PreflightTableStylesV131(',
    'dynamic available=workbook.TableStyles[name];',
    'PreflightTableStylesV131(app.ActiveWorkbook,features.Tables,problems);',
    'SetTableStyleV131(created,table.TableStyle)',
    'SetTableStyleV131(table,metadata.TableStyle)',
    'if(string.Equals(current,required,StringComparison.OrdinalIgnoreCase))',
    'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
    'book.SaveCopyAs(backup)',
    'RestoreAfterFailure(live,before,',
    'Do not save the affected workbook until reviewed.'
)){
    if(!$fmt.Contains($needle)){throw "R131 Excel regression: $needle"}
}
if($fmt.Contains('created.TableStyle=table.TableStyle;') -or
   $fmt.Contains('table.TableStyle=metadata.TableStyle;'))
{throw 'R131 old unguarded Excel table-style setter remains'}
# Inspect must validate styles before any backup/mutation.
if($fmt.IndexOf('PreflightTableStylesV131(app.ActiveWorkbook,features.Tables,problems);') -gt
   $fmt.IndexOf('book.SaveCopyAs(backup)'))
{throw 'R131 destination style must be checked BEFORE any workbook write'}
foreach($needle in @(
    'private static async Task<string> LatestStableTagV131(',
    'https://github.com/',
    'AllowAutoRedirect=false',
    'resolved.Host.Equals("github.com"',
    'resolved.AbsolutePath.StartsWith(prefix',
    '^v\d+\.\d+\.\d+$',
    'if(channel=="stable")',
    'Verify(stableInstaller,sha,null)',
    'DeskSpaceUpdateRateV127.PollFile(Local,"stable")',
    'private static void Verify(string installer,string checksum,JsonElement? root)',
    'if(root.HasValue)',
    'SHA256.HashData(data)',
    'await StartSupervisor(stableInstaller,stableFolder,stableTag)',
    'if(DeskSpaceUpdateRateV127.IsLimitResponse(answer,denial))'
)){
    if(!$upd.Contains($needle)){throw "R131 updater regression: $needle"}
}
if(!$rate.Contains('response.Headers.RetryAfter') -or
   !$rate.Contains('x-ratelimit-reset'))
{throw 'R131 tester rate handling missing'}
Write-Host 'R131 PASS: preflight TableStyle availability, no unnecessary setters, untouched full-range gate/backup; stable Github redirect release and mandatory SHA256; tester 403/429 backoff retained.'

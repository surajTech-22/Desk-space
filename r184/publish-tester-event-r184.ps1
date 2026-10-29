$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# This publisher is intentionally tester-only. Never publish a stable
# release or send a stable event from an unmerged feature branch.
$expectedBranch='refs/heads/feat/r175-component-health-and-clipboard-events'
if($env:GITHUB_REF -ne $expectedBranch -or
   $env:GITHUB_REPOSITORY -ne 'surajTech-22/Desk-space' -or
   $env:GITHUB_SHA -notmatch '^[0-9a-f]{40}$'){
    throw 'R184 refuses release events outside the verified tester branch.'
}
$topic='deskspace-verified-release-r133-20261026-9a6c412f'
$versionLine=Select-String -Path 'installer/DeskSpace.iss' -Pattern '^#define AppVersion "([^"]+)"$' | Select-Object -First 1
if(!$versionLine){throw 'R184 build version missing'}
$version=$versionLine.Matches[0].Groups[1].Value
if($version -notmatch '^\d+\.\d+\.\d+$'){
    throw 'R184 invalid build version'
}
$tag='tester-v'+$version
$raw=gh api "repos/$env:GITHUB_REPOSITORY/releases/tags/$tag"
if($LASTEXITCODE -ne 0){throw 'R184 cannot verify published tester release'}
$release=($raw|Out-String)|ConvertFrom-Json
if($release.tag_name -ne $tag -or $release.draft -or
   !$release.prerelease -or $release.target_commitish -ne $env:GITHUB_SHA){
    throw 'R184 release does not match current verified tester commit'
}
$assets=@($release.assets|ForEach-Object{$_.name})
foreach($required in @('DeskSpace_Setup.exe','DeskSpace_Setup.sha256.txt',
                       'Update.exe','Update.sha256.txt')){
    if($assets -notcontains $required){
        throw ('R184 published release is missing verified '+$required)
    }
}
$message='tester|'+$tag
$received=$false
for($n=1;$n -le 4;$n++){
    try
    {
        $reply=Invoke-RestMethod -Uri ("https://ntfy.sh/"+$topic) -Method Post -ContentType 'text/plain; charset=utf-8' -Headers @{Cache='yes'} -Body $message -TimeoutSec 18
        if($reply.topic -ne $topic -or $reply.message -ne $message){
            throw 'Release relay receipt mismatch'
        }
        $received=$true
        Write-Host ('R184 tester auto-update event delivered; id='+$reply.id)
        break
    }
    catch
    {
        Write-Warning ('R184 tester event attempt '+$n+' failed: '+$_.Exception.GetType().Name)
        if($n -lt 4){Start-Sleep -Seconds (2*$n)}
    }
}
if(!$received){
    throw 'Tester release was published, but instant event delivery failed; existing periodic tester fallback remains.'
}
Write-Host ('R184 tester-only release notification PASS: '+$tag)

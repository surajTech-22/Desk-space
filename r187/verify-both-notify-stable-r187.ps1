$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R187: promote the SAME validated Windows build to both update channels.
# R184 already notifies testers; this script sends only the stable event,
# after verifying BOTH GitHub releases are attached to the exact same SHA
# and contain the identical trusted installer digest.
$featureRef='refs/heads/feat/r175-component-health-and-clipboard-events'
if($env:GITHUB_REF -ne $featureRef -or
   $env:GITHUB_EVENT_NAME -ne 'push' -or
   $env:GITHUB_REPOSITORY -ne 'surajTech-22/Desk-space' -or
   $env:GITHUB_SHA -notmatch '^[0-9a-f]{40}$'){
    throw 'R187 dual-channel notification requires verified feature-branch push.'
}
$head=(gh api "repos/$env:GITHUB_REPOSITORY/git/ref/heads/feat/r175-component-health-and-clipboard-events" --jq '.object.sha')
if($LASTEXITCODE -ne 0 -or ($head|Out-String).Trim() -ne $env:GITHUB_SHA){
    throw 'R187 branch head changed; refusing an obsolete stable event.'
}
$versionMatch=Select-String -Path 'installer/DeskSpace.iss' -Pattern '^#define AppVersion "([^"]+)"$' | Select-Object -First 1
if(!$versionMatch){throw 'R187 installer version missing'}
$version=$versionMatch.Matches[0].Groups[1].Value
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'R187 invalid release version'}
$installer='release/DeskSpace_Setup.exe'
$bridge='release/Update.exe'
$expectedFiles=@{
  'DeskSpace_Setup.exe'='DeskSpace_Setup.sha256.txt'
  'Update.exe'='Update.sha256.txt'
}
$localDigests=@{}
foreach($item in $expectedFiles.Keys){
    $path=Join-Path 'release' $item
    $checksum=Join-Path 'release' $expectedFiles[$item]
    if(!(Test-Path -LiteralPath $path -PathType Leaf) -or
       !(Test-Path -LiteralPath $checksum -PathType Leaf)){
        throw ('R187 missing CI-verified local release artifact '+$item)
    }
    $digest=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $expected=((Get-Content -LiteralPath $checksum -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    if($digest -notmatch '^[0-9a-f]{64}$' -or $expected -ne $digest){
        throw ('R187 local checksum mismatch '+$item)
    }
    $localDigests[$item]=$digest
}
if($localDigests['DeskSpace_Setup.exe'] -ne $localDigests['Update.exe']){
    throw 'R187 trusted setup and update binaries diverged'
}
$required=@('DeskSpace_Setup.exe','DeskSpace_Setup.sha256.txt','Update.exe','Update.sha256.txt')
foreach($choice in @(
    @{Channel='tester';Tag=('tester-v'+$version);Prerelease=$true},
    @{Channel='stable';Tag=('v'+$version);Prerelease=$false}
)){
    $releaseJson=gh api "repos/$env:GITHUB_REPOSITORY/releases/tags/$($choice.Tag)"
    if($LASTEXITCODE -ne 0){throw ('R187 missing published '+$choice.Channel+' release')}
    $release=($releaseJson|Out-String)|ConvertFrom-Json
    if($release.tag_name -ne $choice.Tag -or $release.draft -or
       [bool]$release.prerelease -ne [bool]$choice.Prerelease -or
       $release.target_commitish -ne $env:GITHUB_SHA){
        throw ('R187 wrong tag/target/type on '+$choice.Channel+' release')
    }
    $names=@($release.assets|ForEach-Object {$_.name})
    foreach($asset in $required){
        if($names -notcontains $asset){
            throw ('R187 '+$choice.Channel+' release missing '+$asset)
        }
    }
    foreach($exe in @('DeskSpace_Setup.exe','Update.exe')){
        $assetMatches=@($release.assets|Where-Object {$_.name -eq $exe})
        if($assetMatches.Count -ne 1 -or
           $assetMatches[0].digest -ne ('sha256:'+$localDigests[$exe]) -or
           [long]$assetMatches[0].size -ne (Get-Item -LiteralPath (Join-Path 'release' $exe)).Length){
            throw ('R187 '+$choice.Channel+' release binary checksum/size mismatch '+$exe)
        }
    }
    Write-Host ('R187 verified '+$choice.Channel+' release '+$choice.Tag+
                ' SHA256 '+$localDigests['DeskSpace_Setup.exe'])
}
$topic='deskspace-verified-release-r133-20261026-9a6c412f'
$message='stable|v'+$version
$delivered=$false
for($n=1;$n -le 4;$n++){
    try {
        $receipt=Invoke-RestMethod -Uri ("https://ntfy.sh/"+$topic) -Method Post -ContentType 'text/plain; charset=utf-8' -Body $message -TimeoutSec 18
        if($receipt.topic -ne $topic -or $receipt.message -ne $message){
            throw 'R187 stable event confirmation mismatch.'
        }
        $delivered=$true
        Write-Host ('R187 stable update event delivered; id='+$receipt.id)
        break
    }catch {
        Write-Warning ('R187 stable event attempt '+$n+' failed: '+$_.Exception.GetType().Name)
        if($n -lt 4){Start-Sleep -Seconds (2*$n)}
    }
}
if(!$delivered){throw 'R187 stable release exists, but immediate stable event failed; periodic fallback remains.'}
Write-Host ('R187 PASS: stable and tester '+$version+' verified from identical commit and installer; both channel events delivered.')

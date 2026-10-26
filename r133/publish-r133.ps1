$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$topic='deskspace-verified-release-r133-20261026-9a6c412f'
$repo=$env:GITHUB_REPOSITORY
$sha=$env:GITHUB_SHA
if($repo -ne 'surajTech-22/Desk-space' -or
   $sha -notmatch '^[0-9a-f]{40}$'){
 throw 'R133 publisher must run in the approved repo.'
}
$line=Select-String -Path 'installer/DeskSpace.iss' -Pattern '^#define AppVersion "([^"]+)"$' | Select-Object -First 1
if(!$line){throw 'R133 compiled version not found'}
$version=$line.Matches[0].Groups[1].Value
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Untrusted version format'}
foreach($item in @(
  @{ channel='stable';tag='v'+$version },
  @{ channel='tester';tag='tester-v'+$version }
)){
  $tag=$item.tag
  $meta=gh api "repos/$repo/releases/tags/$tag" --jq '{tag:.tag_name,target:.target_commitish,assets:[.assets[].name]}'
  if($LASTEXITCODE -ne 0){throw "R133 release $tag not readable"}
  $release=$meta|ConvertFrom-Json
  if($release.tag -ne $tag -or $release.target -ne $sha){
    throw "R133 event refused: $tag release target mismatch"
  }
  foreach($name in @('DeskSpace_Setup.exe','DeskSpace_Setup.sha256.txt',
                     'Update.exe','Update.sha256.txt')){
    if($release.assets -notcontains $name){
       throw "R133 event refused: $tag missing $name"
    }
  }
  $message=$item.channel+'|'+$tag
  $done=$false
  for($n=1;$n -le 4;$n++){
    try{
      $received=Invoke-RestMethod -Uri ("https://ntfy.sh/"+$topic) -Method Post -ContentType 'text/plain; charset=utf-8' -Body $message -TimeoutSec 18
      if($received.topic -ne $topic -or $received.message -ne $message){
        throw 'Event relay did not confirm exact message.'
      }
      Write-Host ("R133 release-event delivered "+$message+
                  " receipt="+$received.id)
      $done=$true
      break
    }catch{
      Write-Warning ("R133 event delivery attempt "+$n+" failed: "+$_.Exception.Message)
      if($n -lt 4){Start-Sleep -Seconds (3*$n)}
    }
  }
  if(!$done){
    throw "R133 $tag was published but immediate notification could not be delivered. Periodic fallback will recover."
  }
}
Write-Host 'R133 PASS: both channels signalled after verified publication. Events contain only release identifiers.'
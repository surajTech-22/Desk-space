param(
 [ValidateSet('Validate','Release')][string]$Mode='Validate',
 [string]$Manifest='release/batches/R329-feature-batch.json'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(!(Test-Path -LiteralPath $Manifest)){throw "R329 batch manifest absent: $Manifest"}
$data=Get-Content -LiteralPath $Manifest -Raw -Encoding UTF8|ConvertFrom-Json
if($data.schema -ne 'deskspace-ten-feature-release-v1'){
 throw 'R329 release manifest schema unrecognized'
}
$minimum=[int]$data.minimum_verified_features
if($minimum -lt 10){throw "R329 minimum requirement weakened: $minimum"}
$features=@($data.features)
if($features.Count -lt $minimum){throw "R329 has only $($features.Count) distinct planned features; need $minimum"}
$ids=@{}
$titles=@{}
$verified=0
foreach($feature in $features)
{
 $id=([string]$feature.id).Trim()
 $title=([string]$feature.title).Trim()
 $acceptance=([string]$feature.acceptance).Trim()
 $area=([string]$feature.area).Trim()
 $status=([string]$feature.status).Trim()
 if($id -notmatch '^R329-\d{2}$' -or $title.Length -lt 10 -or
    $area.Length -lt 3 -or $acceptance.Length -lt 40){
  throw "R329 feature is not independently user-visible and testable: $id"
 }
 if($ids.ContainsKey($id)){throw "R329 duplicate feature id: $id"}
 if($titles.ContainsKey($title.ToLowerInvariant())){
  throw "R329 duplicate feature title (cannot pad count): $title"
 }
 $ids[$id]=$true
 $titles[$title.ToLowerInvariant()]=$true
 if($status -notin @('planned','implemented','verified')){
  throw "R329 unsupported feature readiness state: $id=$status"
 }
 if($status -eq 'verified'){
  $verified++
  $source=@($feature.implementation_paths)
  $tests=@($feature.test_paths)
  if($source.Count -lt 1 -or $tests.Count -lt 1){
   throw "R329 $id cannot count: no shipping implementation AND native regression paths"
  }
  foreach($path in @($source)+@($tests)){
   $real=([string]$path).Trim()
   if(!$real -or $real.StartsWith('/') -or $real.Contains('..') -or
      !(Test-Path -LiteralPath $real)){
    throw "R329 $id unverified or unsafe evidence path: $real"
   }
  }
  if(([string]$feature.windows_test_run) -notmatch
    '^https://github\.com/surajTech-22/Desk-space/actions/runs/[0-9]+$'){
   throw "R329 $id requires unique native Windows CI evidence URL"
  }
  if([string]::IsNullOrWhiteSpace([string]$feature.release_evidence)){
   throw "R329 $id requires integration/UI acceptance evidence"
  }
  if([string]$feature.on_device_acceptance -eq 'pending'){
   throw "R329 $id cannot be verified until physical-PC test succeeds"
  }
  if([string]$feature.on_device_acceptance -notin @('not-required','passed')){
   throw "R329 $id invalid hardware-acceptance state"
  }
 }elseif($status -eq 'planned' -and
       @($feature.implementation_paths).Count -gt 0){
   throw "R329 $id lists source for a feature still marked planned"
 }
}
if($Mode -eq 'Release')
{
 if($data.release_authorized -ne $true){
  throw "R329 release BLOCKED: release_authorized=false"
 }
 if($verified -lt $minimum){
  throw "R329 release BLOCKED: only $verified verified user-visible features of minimum $minimum"
 }
 $base=([string]$data.baseline_release).Trim()
 if($base -notmatch '^v\d+\.\d+\.\d+$'){
  throw 'R329 requires an explicit verified baseline release'
 }
}
Write-Host "R329 $Mode PASS: $($features.Count) independently described features, $verified verified, minimum $minimum. Publication status: $($data.release_authorized)."
if($Mode -eq 'Validate' -and $verified -lt $minimum){
 Write-Host "R329 planning is valid. Stable/Tester feature publication is deliberately BLOCKED until ten actual features pass."
}

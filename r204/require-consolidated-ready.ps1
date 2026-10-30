$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$manifestPath='docs/CONSOLIDATED_RELEASE_READINESS.json'
if(!(Test-Path -LiteralPath $manifestPath -PathType Leaf)){
 throw 'Consolidated DeskSpace release manifest missing; release blocked.'
}
$m=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
if($m.schema -ne 'deskspace-single-consolidated-release-v1' -or
   $m.baseline -ne 'v1.5.1093' -or
   $m.checks.Count -lt 25){
 throw 'Invalid/incomplete consolidated release manifest; release blocked.'
}
$names=[Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach($check in $m.checks)
{
 if([string]::IsNullOrWhiteSpace($check.id) -or
    !$names.Add([string]$check.id) -or
    $check.status -notin @('pending','implemented','verified')){
    throw 'Invalid/duplicate release checkpoint; release blocked.'
 }
}
$remaining=@($m.checks|Where-Object{
   $_.status -ne 'verified' -or [string]::IsNullOrWhiteSpace($_.evidence)
})
if($remaining.Count -gt 0 -or $m.release_status -ne 'ready')
{
 $sample=($remaining|Select-Object -First 8 -ExpandProperty id) -join ', '
 throw ("SINGLE-RELEASE GATE: "+$remaining.Count+
  " mandatory checkpoints unverified. Examples: "+$sample+
  ". No Stable/Tester update will be published.")
}
Write-Host ('Single consolidated release gate PASS: '+$m.checks.Count+
    ' accepted checkpoints with evidence. Continue to SHA and dual channels.')

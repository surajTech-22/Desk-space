$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$script=Join-Path (Get-Location) 'scripts/assert-ten-feature-release.ps1'
$source='release/batches/R329-feature-batch.json'
if(!(Test-Path $script)){throw 'R329 test missing gate'}
& $script -Mode Validate -Manifest $source
$base=(Get-Content $source -Raw -Encoding UTF8)|ConvertFrom-Json
$directory=Join-Path ([IO.Path]::GetTempPath()) ('DeskSpaceR329-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory|Out-Null
function ShouldBlock([object]$manifest,[string]$reason)
{
 $file=Join-Path $directory ($reason+'.json')
 $manifest|ConvertTo-Json -Depth 16|Set-Content -LiteralPath $file -Encoding UTF8
 $failed=$false
 try {& $script -Mode Release -Manifest $file | Out-Null}
 catch{$failed=$true}
 if(!$failed){throw "R329 gate unexpectedly allowed publication: $reason"}
 Write-Host "R329 PASS: blocked invalid publication ($reason)"
}
try
{
 ShouldBlock $base 'ten-planned'
 $m=($base|ConvertTo-Json -Depth 16|ConvertFrom-Json)
 $m.release_authorized=$true
 ShouldBlock $m 'approval-without-evidence'
 $m=($base|ConvertTo-Json -Depth 16|ConvertFrom-Json)
 $m.features=@($m.features|Select-Object -First 3)
 ShouldBlock $m 'three-feature-release'
 $m=($base|ConvertTo-Json -Depth 16|ConvertFrom-Json)
 $m.features[1].id=$m.features[0].id
 ShouldBlock $m 'duplicate-feature-ids'
 $m=($base|ConvertTo-Json -Depth 16|ConvertFrom-Json)
 $m.features[1].title=$m.features[0].title.ToUpperInvariant()
 ShouldBlock $m 'duplicate-feature-titles'
 $m=($base|ConvertTo-Json -Depth 16|ConvertFrom-Json)
 $m.features[0].status='verified'
 ShouldBlock $m 'unsubstantiated-verified-status'
 $m=($base|ConvertTo-Json -Depth 16|ConvertFrom-Json)
 $m.minimum_verified_features=3
 ShouldBlock $m 'weakened-minimum'
 Write-Host 'R329 PASS: policy validates 10 planned capabilities but blocks every incomplete/duplicate/three-feature Stable/Tester release.'
}
finally
{
 Remove-Item $directory -Recurse -Force -ErrorAction SilentlyContinue
}

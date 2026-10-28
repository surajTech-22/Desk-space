$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
    '--r160-only',
    'R171 diagnostic: isolated R160 beginning',
    'R171 stage A: before Copy action',
    'R171 stage B: Copy action returned',
    'R171 stage C: before Copy-held wait',
    'R171 stage D: Copy-held wait returned',
    'R171 stage E: before ZIP action',
    'R171 stage F: ZIP action returned',
    'R171 stage G: ZIP-held wait returned',
    'R160 held-folder operation timed out'
)){
 if(!$s.Contains($token)){throw "R171 diagnostic integration missing: $token"}
}
Write-Host 'R171 PASS: isolated native R160 regression and precise checkpoint instrumentation installed.'

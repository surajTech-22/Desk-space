$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$script=[IO.File]::ReadAllText('r267/verify-checked-in-source.ps1')
foreach($required in @(
 'src/DeskSpace',
 'DeskSpace-R221-Source.manifest.json',
 "record.file_count",
 'TryAdd(',
 'Get-FileHash -LiteralPath $path -Algorithm SHA256',
 'R267 PASS: '
)){
 if(!$script.Contains($required)){
  throw ('R267 canonical source comparison missing: '+$required)
 }
}
$canonical='src/DeskSpace/DeskSpace.csproj'
if(!(Test-Path -LiteralPath $canonical)){throw 'R267 canonical project missing'}
$workflow=[IO.File]::ReadAllText('.github/workflows/validate-r201-consolidated.yml')
if(!$workflow.Contains('./r267/verify-checked-in-source.ps1') -or
   !$workflow.Contains("dotnet build 'src/DeskSpace/DeskSpace.csproj'")){
 throw 'R267 CI must compare and independently build checked-in source'
}
Write-Host 'R267 STATIC PASS: direct canonical .NET build and exact regenerated source parity are mandatory in CI.'

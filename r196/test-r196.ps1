$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('r196/build-canonical-source.ps1')
foreach($needle in @(
  'DeskSpace-Canonical-Source.zip',
  'DeskSpace-Canonical-Source.sha256.txt',
  'source-manifest.json',
  'DeskSpaceUpdateSupervisor.ps1',
  'Get-FileHash -LiteralPath $archive -Algorithm SHA256',
  'Get-FileHash -LiteralPath $file -Algorithm SHA256',
  '[IO.Compression.ZipFile]::ExtractToDirectory(',
  '& dotnet build $project -c Release',
  'R196 isolated canonical-source compilation failed',
  'no patch scripts executed',
  'DeskSpace-R196-'
)){
 if(!$s.Contains($needle)){throw ("R196 canonical rebuild missing "+$needle)}
}
if($s.Contains('DeskSpace_Windows_R1/DeskSpace/DeskSpace.csproj') -or
   $s.Contains('BUILD_DESKSPACE_SETUP.ps1 -') -or
   $s.Contains('Start-Process DeskSpace.exe')){
 throw 'R196 must never compile original patched source or run installed application'
}
$archiveSource=[IO.File]::ReadAllText('r195/package-canonical-source.ps1')
if(!$archiveSource.Contains("'.ps1'") -or
   !$archiveSource.Contains('DeskSpaceUpdateSupervisor.ps1')){
 throw 'R196 embedded PowerShell dependency must be captured in canonical archive'
}
Write-Host 'R196 STATIC PASS: isolated archive compile, two SHA-256 trust gates, no original source fallback and safe temp root.'

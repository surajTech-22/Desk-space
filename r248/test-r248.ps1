$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$script=[IO.File]::ReadAllText('r248/build-verified-source.ps1')
$pipeline=[IO.File]::ReadAllText('.github/workflows/validate-r201-consolidated.yml')
foreach($needle in @(
 '[string]$ArchivePath',
 '[string]$ManifestPath',
 '[string]$ReceiptPath',
 '& $verify -ArchivePath $zip -ManifestPath $manifest -ReceiptPath $receipt',
 '[IO.Compression.ZipFile]::ExtractToDirectory($zip,$folder,$false)',
 '-p:ContinuousIntegrationBuild=true',
 'if($LASTEXITCODE -ne 0)',
 'Get-FileHash -LiteralPath $zip -Algorithm SHA256',
 'Remove-Item -LiteralPath $folder -Recurse -Force'
)){
 if(!$script.Contains($needle)){throw ('R248 independent source build missing '+$needle)}
}
if($script.Contains('apply-r201') -or $script.Contains('DeskSpace_Setup.exe') -or
   $script.Contains('DeskSpaceUpdateSupervisor.ps1')){
 throw 'R248 must not execute patches or installer'
}
if(!$pipeline.Contains('./r248/build-verified-source.ps1')){
 throw 'R248 verified canonical source build must run in CI'
}
Write-Host 'R248 STATIC PASS: verified ZIP/manifest/receipt; independent rebuild, no patch/installer and isolated test cleanup.'

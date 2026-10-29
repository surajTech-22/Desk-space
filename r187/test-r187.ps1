$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$publisher='r187/verify-both-notify-stable-r187.ps1'
if(!(Test-Path -LiteralPath $publisher -PathType Leaf)){
 throw 'R187 stable event verification script missing'
}
$tokens=$null;$issues=$null
[System.Management.Automation.Language.Parser]::ParseFile(
    $publisher,[ref]$tokens,[ref]$issues)|Out-Null
if($issues.Count -gt 0){
 throw ('R187 publisher PowerShell syntax invalid: '+($issues -join '; '))
}
$p=[IO.File]::ReadAllText($publisher)
$flow=[IO.File]::ReadAllText('.github/workflows/build-installer.yml')
$tester=[IO.File]::ReadAllText('r184/publish-tester-event-r184.ps1')
$updater=[IO.File]::ReadAllText(
 'DeskSpace_Windows_R1/DeskSpace/DeskSpacePublicUpdaterV44.cs')
foreach($token in @(
 'GITHUB_REF -ne $featureRef',
 "GITHUB_EVENT_NAME -ne 'push'",
 'releases/tags/',
 'Get-FileHash -LiteralPath $path -Algorithm SHA256',
 'sha256:',
 'assetMatches[0].digest',
 'assetMatches[0].size',
 'target_commitish -ne $env:GITHUB_SHA',
 'release.prerelease',
 "topic='deskspace-verified-release-r133-20261026-9a6c412f'",
 "message='stable|v'+",
 'Invoke-RestMethod',
 'if(!$delivered)',
 'R187 PASS: stable and tester'
)){
 if(!$p.Contains($token)){throw ('R187 missing dual-release safety gate: '+$token)}
}
if($p.Contains('gh release create') -or $p.Contains('gh release delete')){
 throw 'R187 notification must NEVER create or delete releases'
}
if(!$flow.Contains('Publish SHA-256 verified opt-in tester prerelease') -or
   !$flow.Contains('Publish verified Stable channel automatic release') -or
   !$flow.Contains('R187 verify both release hashes and notify stable clients')){
 throw 'R187 release workflow must publish both after Windows CI gates'
}
if(!$flow.Contains("github.ref == 'refs/heads/feat/r175-component-health-and-clipboard-events'")){
 throw 'R187 feature branch release gate missing'
}
foreach($token in @('tester|','Invoke-RestMethod','!$release.prerelease')){
 if(!$tester.Contains($token)){throw "Existing tester notification missing: $token"}
}
foreach($token in @('private const string StableApi','private const string TesterApi',
 'return selected=="tester"?"tester":"stable"',
 'Verify(installer,checksum,root)')){
 if(!$updater.Contains($token)){throw ('R187 existing client channel routing broken: '+$token)}
}
Write-Host 'R187 PASS: verified dual-GitHub release prerequisites, digest/target checks, tester/stable events and existing client update compatibility.'

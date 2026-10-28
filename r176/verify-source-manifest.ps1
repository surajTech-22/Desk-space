$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Get-Location).Path
$p='build-provenance/source-manifest.json'
if(!(Test-Path $p)){throw 'R176 source manifest missing'}
$m=Get-Content -LiteralPath $p -Raw | ConvertFrom-Json
if($m.schema -ne 'deskspace-source-provenance-v1' -or
   $m.source_file_count -lt 40 -or
   $m.source_file_count -ne @($m.source_files).Count){
    throw 'R176 manifest schema or file count invalid'
}
if($m.commit -ne $env:GITHUB_SHA){throw 'R176 manifest commit does not match build commit'}
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$lines=@()
foreach($entry in $m.source_files){
    $rel=[string]$entry.path
    if([IO.Path]::IsPathRooted($rel) -or $rel.Contains('..') -or
       !$rel.StartsWith('DeskSpace_Windows_R1/DeskSpace/',[StringComparison]::Ordinal) -or
       !$seen.Add($rel)){
        throw 'R176 found duplicate or unsafe manifest path'
    }
    $file=Join-Path $root $rel
    if(!(Test-Path -LiteralPath $file -PathType Leaf)){
        throw 'R176 manifest refers to missing final source file'
    }
    $sha=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if($sha -ne $entry.sha256 -or (Get-Item -LiteralPath $file).Length -ne $entry.bytes){
        throw "R176 final source changed after provenance capture: $rel"
    }
    $lines+=($rel+' '+$sha)
}
$combined=[Text.Encoding]::UTF8.GetBytes(
    [string]::Join([string][char]10,$lines)+[string][char]10)
$recomputed=[Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData($combined)).ToLowerInvariant()
if($m.source_set_sha256 -ne $recomputed){throw 'R176 overall source fingerprint mismatch'}
Write-Host "R176 PASS: $($m.source_file_count) exact source hashes, paths, version, commit and fingerprint verified."

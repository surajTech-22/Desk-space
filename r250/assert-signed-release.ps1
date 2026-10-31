[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$InstallerPath,
  [Parameter(Mandatory=$true)][string]$ExpectedSha256,
  [Parameter(Mandatory=$true)][string]$ExpectedSignerThumbprint,
  [Parameter(Mandatory=$true)][string]$ExpectedSourceCommit,
  [Parameter(Mandatory=$true)][string]$ProvenanceManifestPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Read-only prepublication guard. Never sign, execute, install, upload or publish.
if($ExpectedSha256 -cnotmatch '\A[0-9a-fA-F]{64}\z' -or
   $ExpectedSignerThumbprint -cnotmatch '\A[0-9a-fA-F]{40,64}\z' -or
   $ExpectedSourceCommit -cnotmatch '\A[0-9a-fA-F]{40}\z'){
 throw 'R250 invalid pinned hash, signer identity or source commit'
}
if(!(Test-Path -LiteralPath $InstallerPath -PathType Leaf) -or
   !(Test-Path -LiteralPath $ProvenanceManifestPath -PathType Leaf)){
 throw 'R250 missing candidate installer or provenance manifest'
}
$exe=Get-Item -LiteralPath $InstallerPath
$provenance=Get-Item -LiteralPath $ProvenanceManifestPath
if(($exe.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
   ($provenance.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
 throw 'R250 signer evidence may not be a linked file'
}
if($exe.Length -lt 5MB -or $exe.Length -gt 250MB -or
   $provenance.Length -lt 10 -or $provenance.Length -gt 64KB){
 throw 'R250 signer evidence has implausible size'
}
$sha=(Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if($sha -cne $ExpectedSha256.ToLowerInvariant()){
 throw 'R250 installer SHA-256 changed'
}
$m=Get-Content -LiteralPath $provenance.FullName -Raw|ConvertFrom-Json
if($m.schema -cne 'deskspace-signed-installer-provenance-v1' -or
   $m.commit -cne $ExpectedSourceCommit.ToLowerInvariant() -or
   $m.installer_sha256 -cne $sha -or
   $m.signer_thumbprint -cne $ExpectedSignerThumbprint.ToLowerInvariant()){
 throw 'R250 provenance manifest mismatches pinned source, installer or signer'
}
$signature=Get-AuthenticodeSignature -LiteralPath $exe.FullName
if($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
   $null -eq $signature.SignerCertificate){
 throw 'R250 release is unsigned, untrusted or its Authenticode chain is invalid'
}
$cert=$signature.SignerCertificate
if($cert.Thumbprint.ToLowerInvariant() -cne
   $ExpectedSignerThumbprint.ToLowerInvariant()){
 throw 'R250 Authenticode signer does not match approved publisher certificate'
}
$now=[DateTime]::UtcNow
if($now -lt $cert.NotBefore.ToUniversalTime() -or
   $now -gt $cert.NotAfter.ToUniversalTime()){
 throw 'R250 code signing certificate outside validity window'
}
# Require explicit code signing EKU rather than arbitrary user certs.
$codeSigning=$false
foreach($extension in $cert.Extensions){
 if($extension -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]){
   foreach($oid in $extension.EnhancedKeyUsages){
     if($oid.Value -eq '1.3.6.1.5.5.7.3.3'){$codeSigning=$true}
   }
 }
}
if(!$codeSigning){throw 'R250 certificate not intended for code signing'}
Write-Host 'R250 PASS: published bytes and trusted code signer match pinned SHA, commit and provenance'

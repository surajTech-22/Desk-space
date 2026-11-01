[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Version,
 [Parameter(Mandatory=$true)][string]$SourceSha)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$target='release'
$setup=Join-Path $target 'DeskSpace_Setup.exe'
if(!(Test-Path -LiteralPath $setup)){throw 'R287 built installer absent'}
$size=(Get-Item $setup).Length
if($size -lt 5MB -or $size -gt 250MB){
 throw 'R287 installer size outside bounds'
}
$signed=$false;$thumb=''
if(![string]::IsNullOrWhiteSpace($env:DESKSPACE_SIGNING_PFX_BASE64) -and
   ![string]::IsNullOrWhiteSpace($env:DESKSPACE_SIGNING_PASSWORD) -and
   ![string]::IsNullOrWhiteSpace($env:DESKSPACE_SIGNING_THUMBPRINT))
{
 $temp=Join-Path $env:RUNNER_TEMP ('deskspace-r287-'+[Guid]::NewGuid().ToString('N')+'.pfx')
 try{
  [IO.File]::WriteAllBytes($temp,[Convert]::FromBase64String(
     $env:DESKSPACE_SIGNING_PFX_BASE64))
  $password=ConvertTo-SecureString $env:DESKSPACE_SIGNING_PASSWORD -AsPlainText -Force
  $cert=Import-PfxCertificate -FilePath $temp -CertStoreLocation 'Cert:\CurrentUser\My' -Password $password -Exportable:$false
  if($null -eq $cert -or !$cert.HasPrivateKey){
   throw 'R287 signing certificate has no private key'
  }
  $thumb=$cert.Thumbprint.ToLowerInvariant()
  if($thumb -cne $env:DESKSPACE_SIGNING_THUMBPRINT.ToLowerInvariant()){
   throw 'R287 signing cert is not the pinned publisher'
  }
  $signature=Set-AuthenticodeSignature -FilePath $setup -Certificate $cert -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com'
  if($signature.Status -ne 'Valid'){throw ('R287 signing failed '+$signature.Status)}
  $signed=$true
 }
 finally{
  Remove-Item $temp -Force -ErrorAction SilentlyContinue
 }
}
else{
 Write-Host '::warning::R287 publisher signing credentials unavailable. Installer stays UNRELEASED.'
}
& ./r220/normalize-release-checksum.ps1 -OutputDirectory $target
if($LASTEXITCODE -ne 0){throw 'R287 updater SHA normalization failed'}
Copy-Item -LiteralPath $setup -Destination (Join-Path $target 'Update.exe') -Force
$digest=(Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $target 'Update.sha256.txt'),$digest,[Text.Encoding]::ASCII)
& ./r249/assert-dual-channel-hash.ps1 -StableInstaller $setup -TesterInstaller (Join-Path $target 'Update.exe') -ExpectedSha256 $digest
if($LASTEXITCODE -ne 0){throw 'R287 Stable/Tester parity failed'}
$receipt=((Get-Content (Join-Path $target 'DeskSpace_Setup.sha256.txt') -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
if($receipt -cne $digest){throw 'R287 legacy updater SHA differs'}
$provenance=Join-Path $target 'DeskSpace.provenance.json'
@{
 schema='deskspace-signed-installer-provenance-v1';
 commit=$SourceSha;
 installer_sha256=$digest;
 signer_thumbprint=if($signed){$thumb}else{'UNSIGNED'};
 build_version=$Version;
 validated_source_run_id=41279973819;
 signed=$signed;
} | ConvertTo-Json -Depth 3 | Set-Content $provenance
if($signed){
 & ./r250/assert-signed-release.ps1 -InstallerPath $setup -ExpectedSha256 $digest -ExpectedSignerThumbprint $thumb -ExpectedSourceCommit $SourceSha -ProvenanceManifestPath $provenance
 if($LASTEXITCODE -ne 0){throw 'R287 real Authenticode verification failed'}
}
$ready=Get-Content 'docs/CONSOLIDATED_RELEASE_READINESS.json' -Raw | ConvertFrom-Json
$blocked=@($ready.checks | Where-Object { $_.status -ne 'verified' })
$canPublish=$signed -and $ready.release_status -eq 'ready' -and $blocked.Count -eq 0
if(!$canPublish){
 $why=if(!$signed){'NO_TRUSTED_SIGNING_CERT'}else{'PHYSICAL_RELEASE_ACCEPTANCE_INCOMPLETE'}
 $message="DeskSpace $Version installation candidate is NOT a production release. "+
    "Blocker: $why. Do not deploy to Stable or Tester or auto-install. "+
    "Current Stable remains v1.5.1103."
 [IO.File]::WriteAllText((Join-Path $target 'NOT_RELEASED_README.txt'),$message,[Text.Encoding]::UTF8)
 Write-Host ('::warning::'+$message)
}
@{
 schema='deskspace-r287-candidate-gate-v1';source_commit=$SourceSha;
 version=$Version;installer_sha256=$digest;signed=$signed;
 release_ready=$canPublish;blocker_count=$blocked.Count;
 readiness_status=$ready.release_status;
} | ConvertTo-Json | Set-Content '.candidate/evidence/release-verdict.json'
if($canPublish){'publish=true'|Out-File $env:GITHUB_OUTPUT -Append -Encoding utf8}
else{'publish=false'|Out-File $env:GITHUB_OUTPUT -Append -Encoding utf8}
Write-Host ('R287 candidate SHA256 '+$digest+'; signed='+$signed+
    '; allowed publish='+$canPublish)

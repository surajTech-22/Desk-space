$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Join-Path ([IO.Path]::GetTempPath()) ('deskspace-r250-'+[guid]::NewGuid().ToString('N'))
try{
 [void][IO.Directory]::CreateDirectory($root)
 $exe=Join-Path $root 'unsigned-synthetic-installer.exe'
 $manifest=Join-Path $root 'provenance.json'
 $content=[byte[]]::new(5MB)
 $content[0]=77;$content[1]=90
 [IO.File]::WriteAllBytes($exe,$content)
 $sha=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
 $commit='0123456789abcdef0123456789abcdef01234567'
 $thumb='0123456789abcdef0123456789abcdef01234567'
 function WriteManifest([string]$hash,[string]$source,[string]$signer){
   [IO.File]::WriteAllText($manifest,(
     @{schema='deskspace-signed-installer-provenance-v1';
       installer_sha256=$hash;commit=$source;signer_thumbprint=$signer}|
       ConvertTo-Json -Compress))
 }
 WriteManifest $sha $commit $thumb
 function ExpectBlocked([scriptblock]$probe,[string]$why){
   $blocked=$false
   try{& $probe}catch{$blocked=$true}
   if(!$blocked){throw ('R250 UNSAFE: '+$why+' accepted')}
 }
 $args=@{
  InstallerPath=$exe
  ExpectedSha256=$sha
  ExpectedSignerThumbprint=$thumb
  ExpectedSourceCommit=$commit
  ProvenanceManifestPath=$manifest
 }
 # A synthetic MZ file is deliberately unsigned. It must always fail.
 ExpectBlocked {& 'r250/assert-signed-release.ps1' @args} 'unsigned candidate'
 $args.ExpectedSourceCommit='bad'
 ExpectBlocked {& 'r250/assert-signed-release.ps1' @args} 'invalid commit'
 $args.ExpectedSourceCommit=$commit
 WriteManifest $sha ('f'*40) $thumb
 ExpectBlocked {& 'r250/assert-signed-release.ps1' @args} 'wrong source commit'
 WriteManifest $sha $commit $thumb
 $args.ExpectedSha256='f'*64
 ExpectBlocked {& 'r250/assert-signed-release.ps1' @args} 'incorrect official installer checksum'
 $args.ExpectedSha256=$sha
 Remove-Item -LiteralPath $manifest -Force
 ExpectBlocked {& 'r250/assert-signed-release.ps1' @args} 'missing provenance'
 Write-Host 'R250 PASS: unsigned/untrusted release rejected; commit, checksum, missing provenance checks fail closed.'
}
finally{
 if(Test-Path -LiteralPath $root){Remove-Item -LiteralPath $root -Force -Recurse}
}

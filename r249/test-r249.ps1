$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Join-Path ([IO.Path]::GetTempPath()) ('deskspace-r249-'+[guid]::NewGuid().ToString('N'))
try {
 New-Item -ItemType Directory -Force -Path $root|Out-Null
 $stable=Join-Path $root 'Stable-Setup.exe'
 $tester=Join-Path $root 'Tester-Setup.exe'
 $bytes=[byte[]](1..128|ForEach-Object{$_%251})
 [IO.File]::WriteAllBytes($stable,$bytes)
 [IO.File]::WriteAllBytes($tester,$bytes)
 $sha=(Get-FileHash -LiteralPath $stable -Algorithm SHA256).Hash
 & 'r249/assert-dual-channel-hash.ps1' -StableInstaller $stable -TesterInstaller $tester -ExpectedSha256 $sha
 $wrong=$sha.Substring(1)+'0'
 if($wrong -eq $sha){$wrong=$sha.Substring(1)+'1'}
 function MustBlock([scriptblock]$probe,[string]$label){
   $blocked=$false
   try {& $probe}
   catch {$blocked=$true}
   if(!$blocked){throw ("R249 failed to block "+$label)}
 }
 MustBlock {& 'r249/assert-dual-channel-hash.ps1' -StableInstaller $stable -TesterInstaller $tester -ExpectedSha256 $wrong} 'wrong published checksum'
 MustBlock {& 'r249/assert-dual-channel-hash.ps1' -StableInstaller $stable -TesterInstaller $tester -ExpectedSha256 'not-a-hash'} 'invalid checksum'
 [IO.File]::WriteAllBytes($tester,[byte[]](129,130,131))
 MustBlock {& 'r249/assert-dual-channel-hash.ps1' -StableInstaller $stable -TesterInstaller $tester -ExpectedSha256 $sha} 'mismatched channel bytes'
 Remove-Item -LiteralPath $tester -Force
 MustBlock {& 'r249/assert-dual-channel-hash.ps1' -StableInstaller $stable -TesterInstaller $tester -ExpectedSha256 $sha} 'missing tester installer'
 Write-Host 'R249 PASS: same installer hash accepted; mismatched, tampered, missing channel and malformed SHA blocked.'
}
finally{if(Test-Path -LiteralPath $root){Remove-Item -LiteralPath $root -Force -Recurse}}

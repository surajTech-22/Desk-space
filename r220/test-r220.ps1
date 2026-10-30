$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$script='r220/normalize-release-checksum.ps1'
if(!(Test-Path -LiteralPath $script)){throw 'R220 checksum release bridge absent'}
$root=Join-Path ([IO.Path]::GetTempPath()) ('deskspace-r220-'+[guid]::NewGuid().ToString('N'))
try{
  New-Item -ItemType Directory -Force -Path $root|Out-Null
  $installer=Join-Path $root 'DeskSpace_Setup.exe'
  $receipt=Join-Path $root 'DeskSpace_Setup.sha256.txt'
  # Synthetic PE-like bytes only. No installer process is ever executed.
  $stream=[IO.File]::Open($installer,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write)
  try{
    $stream.SetLength(5000064)
    $stream.Position=0
    $stream.WriteByte(0x4d);$stream.WriteByte(0x5a)
  }finally{$stream.Dispose()}
  $hash=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
  ("$hash  DeskSpace_Setup.exe") | Set-Content -LiteralPath $receipt -Encoding ascii
  $oldInstallerHash=$hash
  & $script -OutputDirectory $root
  if(!$?){throw 'R220 checksum normalization exited unsuccessfully'}
  $raw=[IO.File]::ReadAllBytes($receipt)
  $text=[Text.Encoding]::ASCII.GetString($raw)
  if($raw.Length -ne 64 -or
     $text -cnotmatch '^[0-9a-f]{64}$' -or
     $text -ne $oldInstallerHash){
    throw 'R220 old R191 checksum regex must accept exactly bare SHA with no newline'
  }
  if((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash){
    throw 'R220 must not modify the verified installer'
  }
  # The release bridge must refuse a mismatched original checksum; it may not
  # turn an unverified download into a published checksum.
  [IO.File]::WriteAllText($receipt,(('0'*64)+'  DeskSpace_Setup.exe'),[Text.Encoding]::ASCII)
  $rejected=$false
  try{& $script -OutputDirectory $root}catch{
    if($_.Exception.Message -like '*checksum did not match*'){$rejected=$true}
    else{throw}
  }
  if(!$rejected){throw 'R220 must not bless a mismatched original release asset'}
  Write-Host 'R220 PASS: real old-client staging regex, exact 64-byte legacy SHA and mismatch rejection.'
}
finally{
  if(Test-Path -LiteralPath $root){Remove-Item -LiteralPath $root -Force -Recurse}
}

[CmdletBinding()]
param([string]$OutputDirectory='release')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

# R220: compatibility bridge for DeskSpace versions containing R191.
# That updater validates the official SHA-256 but incorrectly rejects the
# sha256sum "HASH  DeskSpace_Setup.exe" line when enqueuing the verified file.
# Publish exactly 64 ASCII hex digits, WITHOUT a CRLF, so old and fixed
# updaters both accept the SAME verified release. Never alter the installer.
$dir=[IO.Path]::GetFullPath($OutputDirectory)
$installer=Join-Path $dir 'DeskSpace_Setup.exe'
$receipt=Join-Path $dir 'DeskSpace_Setup.sha256.txt'
if(!(Test-Path -LiteralPath $installer -PathType Leaf) -or
   !(Test-Path -LiteralPath $receipt -PathType Leaf)){
    throw 'R220 official installer and published checksum must already exist'
}
$info=Get-Item -LiteralPath $installer
if($info.Length -lt 5000000 -or
   ($info.Attributes -band [IO.FileAttributes]::ReparsePoint)){
    throw 'R220 will not publish a linked or suspiciously small installer'
}
$stream=[IO.File]::OpenRead($installer)
try{
    if($stream.ReadByte() -ne 0x4d -or $stream.ReadByte() -ne 0x5a){
        throw 'R220 installer must retain a Windows PE signature'
    }
}finally{$stream.Dispose()}
$expected=(Get-Content -LiteralPath $receipt -Raw).Trim() -split '\s+'
if($expected.Count -lt 1 -or $expected[0] -notmatch '^[0-9a-fA-F]{64}$'){
    throw 'R220 original official installer SHA-256 receipt malformed'
}
$sha=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
if($sha -ne $expected[0].ToLowerInvariant()){
    throw 'R220 original official checksum did not match packaged setup'
}
# Write only an official release checksum file, no BOM and NO trailing newline.
[IO.File]::WriteAllText($receipt,$sha,[Text.Encoding]::ASCII)
$published=[IO.File]::ReadAllText($receipt,[Text.Encoding]::ASCII)
if($published.Length -ne 64 -or
   $published -cnotmatch '^[0-9a-f]{64}$' -or
   $published -ne (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()){
    throw 'R220 exact legacy-compatible SHA-256 receipt verification failed'
}
Write-Host 'R220 PASS: official SHA-256 published as exactly 64 bare hex bytes; old and new updaters accept it.'

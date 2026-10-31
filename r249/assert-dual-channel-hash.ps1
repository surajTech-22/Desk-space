[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$StableInstaller,
  [Parameter(Mandatory=$true)][string]$TesterInstaller,
  [Parameter(Mandatory=$true)][string]$ExpectedSha256
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Prepublication integrity policy only. This script does not upload,
# publish, sign, install or execute any installer.
if($ExpectedSha256 -cnotmatch '\A[0-9A-Fa-f]{64}\z'){
    throw 'R249 invalid official SHA-256 receipt'
}
$expected=$ExpectedSha256.ToLowerInvariant()
function AssertCandidate([string]$name,[string]$path)
{
    if(!(Test-Path -LiteralPath $path -PathType Leaf)){
        throw ("R249 missing "+$name+" installer candidate")
    }
    $file=Get-Item -LiteralPath $path
    if(($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
        throw ("R249 linked "+$name+" installer not allowed")
    }
    if($file.Length -le 0 -or $file.Length -gt 250MB){
        throw ("R249 invalid "+$name+" installer length")
    }
    return (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$stable=AssertCandidate 'Stable' $StableInstaller
$tester=AssertCandidate 'Tester' $TesterInstaller
if($stable -cne $tester -or $stable -cne $expected){
    throw 'R249 SINGLE ARTIFACT BLOCK: Stable/Tester installer bytes or published SHA-256 differ'
}
Write-Host ('R249 PASS: Stable and Tester candidate bytes match one SHA-256 '+$stable+
    '; no release performed')

# Publish a locally compiled, checksum-verified and installed R42
# Windows installer as a private GitHub pre-release for designated testers.
param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$Commit
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($Commit -notmatch '^[0-9a-fA-F]{40}$'){throw 'Unpinned source revision'}
if(!(Test-Path -LiteralPath $Installer -PathType Leaf)){
    throw 'Verified Windows installer is missing'
}
if([IO.Path]::GetExtension($Installer).ToLowerInvariant() -ne '.exe'){
    throw 'Only a native Windows installer can be published'
}
if((Get-Item -LiteralPath $Installer).Length -lt 5000000){
    throw 'Installer is unexpectedly small'
}
$sha=(Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash.ToLowerInvariant()
$sidecar=$Installer+'.sha256.txt'
if(!(Test-Path -LiteralPath $sidecar)){throw 'Build checksum sidecar is missing'}
$expected=(Get-Content -LiteralPath $sidecar -Raw).Trim().ToLowerInvariant()
if($sha -ne $expected){throw 'SHA-256 mismatch; refusing publication'}
$gh=Get-Command gh.exe -ErrorAction SilentlyContinue
if(!$gh){throw 'GitHub CLI is not available'}
& $gh.Source auth status -h github.com
if($LASTEXITCODE -ne 0){throw 'Private GitHub authorization required'}
$version=[regex]::Match([IO.Path]::GetFileName($Installer),'1\.4\.\d+')
if(!$version.Success){throw 'Unrecognized local installer version'}
$tag='r42-test-'+$Commit.Substring(0,12)+'-v'+$version.Value
$repo='surajTech-22/Desk-space'
$prior=& $gh.Source release view $tag --repo $repo 2>$null
if($LASTEXITCODE -eq 0){
    throw "Test release $tag already exists; do not overwrite verified files"
}
$notes="DeskSpace R42 Windows TEST candidate; pinned source $Commit. Locally built, WPF smoke tested, checksum verified and guarded-install succeeded. Not a stable release. Use disposable workbooks and invented records. See docs/TESTING_GUIDE.md."
$args1=@('release','create',$tag,$Installer,$sidecar,'--repo',$repo,
    '--target',$Commit,'--title',"DeskSpace R42 test candidate $($version.Value)",
    '--notes',$notes,'--prerelease','--latest=false')
& $gh.Source @args1
if($LASTEXITCODE -ne 0){throw 'GitHub private test installer publishing failed'}
Write-Host ('Private Windows test installer: https://github.com/'+$repo+'/releases/tag/'+$tag)

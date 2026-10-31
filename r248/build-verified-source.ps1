[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ArchivePath,
    [Parameter(Mandatory=$true)][string]$ManifestPath,
    [Parameter(Mandatory=$true)][string]$ReceiptPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

# R248: one independently buildable and per-file verified canonical WPF source
# bundle. Never execute patch scripts, installer logic or app data migration.
$root=Split-Path -Parent $PSScriptRoot
$verify=Join-Path $root 'r221/verify-single-source.ps1'
if(!(Test-Path -LiteralPath $verify -PathType Leaf)){
    throw 'R248 source integrity verifier is missing'
}
$zip=[IO.Path]::GetFullPath($ArchivePath)
$manifest=[IO.Path]::GetFullPath($ManifestPath)
$receipt=[IO.Path]::GetFullPath($ReceiptPath)
foreach($file in @($zip,$manifest,$receipt)){
    if(!(Test-Path -LiteralPath $file -PathType Leaf)){
        throw 'R248 verified source ZIP, manifest or SHA-256 receipt missing'
    }
    if((Get-Item -LiteralPath $file).Attributes -band [IO.FileAttributes]::ReparsePoint){
        throw 'R248 source integrity input must not be a symbolic link'
    }
}
& $verify -ArchivePath $zip -ManifestPath $manifest -ReceiptPath $receipt
if($LASTEXITCODE -ne 0){throw 'R248 independent source verification failed'}

$base=if($env:RUNNER_TEMP){$env:RUNNER_TEMP}else{[IO.Path]::GetTempPath()}
$folder=Join-Path $base ('DeskSpace-R248-VerifiedBuild-'+[guid]::NewGuid().ToString('N'))
try{
    [void][IO.Directory]::CreateDirectory($folder)
    # R221 verification rejects path traversal, case conflicts, unverified
    # entries, duplicates, oversized contents and altered manifest/receipt.
    [IO.Compression.ZipFile]::ExtractToDirectory($zip,$folder,$false)
    $project=Join-Path $folder 'DeskSpace/DeskSpace.csproj'
    if(!(Test-Path -LiteralPath $project -PathType Leaf)){
        throw 'R248 verified source missing WPF project file'
    }
    & dotnet build $project -c Release --nologo -p:ContinuousIntegrationBuild=true
    if($LASTEXITCODE -ne 0){throw 'R248 canonical WPF source could not rebuild independently'}
    $sha=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host ('R248 PASS: independently rebuilt only verified canonical source '+$sha)
}
finally{
    if(Test-Path -LiteralPath $folder){
        Remove-Item -LiteralPath $folder -Recurse -Force
    }
}

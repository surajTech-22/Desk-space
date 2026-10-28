$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Get-Location).Path
$product=Join-Path $root 'DeskSpace_Windows_R1/DeskSpace'
if(!(Test-Path $product)){throw 'R176 final source tree not reconstructed'}
$allowed=@('.cs','.xaml','.csproj','.resx','.config')
$sourceFiles=@(Get-ChildItem -LiteralPath $product -Recurse -File |
    Where-Object {
        $_.Extension -in $allowed -and
        $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]'
    } | Sort-Object FullName)
if($sourceFiles.Count -lt 40){throw "R176 expected substantial final WPF source tree, found $($sourceFiles.Count) files"}
$entries=@($sourceFiles | ForEach-Object {
    $relative=$_.FullName.Substring($root.Length+1).Replace('\','/')
    [ordered]@{
        path=$relative
        bytes=$_.Length
        sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$iss='installer/DeskSpace.iss'
$v=Select-String -Path $iss -Pattern '^#define AppVersion "([^"]+)"$' |
    Select-Object -First 1
if(!$v){throw 'R176 cannot find assigned application version'}
$version=$v.Matches[0].Groups[1].Value
$inputLines=@($entries | ForEach-Object { $_.path+' '+$_.sha256 })
$blob=[Text.Encoding]::UTF8.GetBytes(
    [string]::Join([string][char]10,$inputLines)+[string][char]10)
$overall=[Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData($blob)).ToLowerInvariant()
$manifest=[ordered]@{
    schema='deskspace-source-provenance-v1'
    commit=$env:GITHUB_SHA
    version=$version
    source_file_count=$entries.Count
    source_set_sha256=$overall
    source_files=$entries
}
$folder=Join-Path $root 'build-provenance'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$target=Join-Path $folder 'source-manifest.json'
[IO.File]::WriteAllText($target,
    ($manifest|ConvertTo-Json -Depth 8),
    [Text.UTF8Encoding]::new($false))
Write-Host "R176: source manifest ($($entries.Count) files) created, source SHA256=$overall"

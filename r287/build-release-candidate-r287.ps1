[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SourceRunId,
      [Parameter(Mandatory=$true)][string]$ExpectedSourceSha,
      [Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($Version -cnotmatch '^1\.5\.(\d+)$'){throw 'R287 invalid version'}
$buildNumber=[int]([regex]::Match($Version,'^1\.5\.(\d+)$')).Groups[1].Value
if($ExpectedSourceSha -cnotmatch '^[0-9a-f]{40}$'){throw 'R287 invalid validated SHA'}
$repo='surajTech-22/Desk-space'
$pass=(gh api "repos/$repo/actions/runs/$SourceRunId" | Out-String | ConvertFrom-Json)
if($LASTEXITCODE -ne 0 -or $pass.conclusion -ne 'success' -or
   $pass.head_sha -cne $ExpectedSourceSha){
 throw 'R287 source CI was not a successful and exact frozen commit'
}
New-Item -ItemType Directory -Force '.candidate/source' | Out-Null
gh run download $SourceRunId -R $repo -n 'DeskSpace-R221-Consolidated-Verified-Source' -D '.candidate/source'
if($LASTEXITCODE -ne 0){throw 'R287 verified source artifact missing'}
& ./r221/verify-single-source.ps1 -ArchivePath '.candidate/source/DeskSpace-R221-Source.zip' -ManifestPath '.candidate/source/DeskSpace-R221-Source.manifest.json' -ReceiptPath '.candidate/source/DeskSpace-R221-Source.sha256.txt'
$manifest=Get-Content '.candidate/source/DeskSpace-R221-Source.manifest.json' -Raw|ConvertFrom-Json
if($manifest.commit -cne $ExpectedSourceSha){
 throw 'R287 source artifact commit is not verified head'
}
# Installer tooling comes from an independently checksummed original source
# artifact. Never overwrite the validated R286 application with legacy code.
New-Item -ItemType Directory -Force '.candidate/legacy'|Out-Null
gh run download 41002762293 -R $repo -n 'DeskSpace-R195-Canonical-WPF-Source' -D '.candidate/legacy'
if($LASTEXITCODE -ne 0){throw 'R287 legacy installer tool artifact missing'}
$archive='.candidate/legacy/DeskSpace-Canonical-Source.zip'
$expected=((Get-Content '.candidate/legacy/DeskSpace-Canonical-Source.sha256.txt' -Raw).Trim() -split '\s+')[0]
$actual=(Get-FileHash $archive -Algorithm SHA256).Hash
if(!$actual.Equals($expected,[StringComparison]::OrdinalIgnoreCase)){
 throw 'R287 legacy installer source hash mismatch'
}
Expand-Archive $archive -DestinationPath '.candidate/legacy-unpacked'
foreach($source in @('installer/DeskSpace.iss','BUILD_DESKSPACE_SETUP.ps1')){
 $filename=Join-Path '.candidate/legacy-unpacked' $source
 if(!(Test-Path $filename)){throw ('R287 missing legacy tool '+$source)}
 $directory=Split-Path $source
 if($directory){New-Item -ItemType Directory -Force $directory | Out-Null}
 Copy-Item $filename $source -Force
}
# Restore only original TEST project and dependent resource files from R1;
# the application source itself comes exclusively from the CI-verified ZIP.
$parts=for($i=1;$i -le 4;$i++){
 $path="assets/source.part$i.txt"
 if(!(Test-Path $path)){throw ('R287 missing original model tests part '+$path)}
 (Get-Content -LiteralPath $path -Raw).Trim()
}
$tar='.candidate/legacy-source.tar.xz'
[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($tar),
    [Convert]::FromBase64String($parts -join ''))
if((Get-FileHash $tar -Algorithm SHA256).Hash.ToLowerInvariant() -cne
  'de852a020013de604cdb9b206e38f4ec1359e7f2169bc898e9ca60299a0249a3'){
 throw 'R287 original base source hash mismatch'
}
tar -xJf $tar
if($LASTEXITCODE -ne 0){throw 'R287 original tests extraction failed'}
$tests='DeskSpace_Windows_R1/DeskSpace.Tests/DeskSpace.Tests.csproj'
if(!(Test-Path $tests)){throw 'R287 original tests project missing'}
$testContent=[IO.File]::ReadAllText($tests)
$target='<TargetFramework>net8\.0-windows(?:[0-9.]+)?</TargetFramework>'
if(([regex]::Matches($testContent,$target)).Count -ne 1){
 throw 'R287 ambiguous original unit test framework'
}
$testContent=[regex]::Replace($testContent,$target,
 '<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>')
[IO.File]::WriteAllText($tests,$testContent,[Text.UTF8Encoding]::new($false))
Remove-Item 'DeskSpace_Windows_R1/DeskSpace' -Recurse -Force
Expand-Archive '.candidate/source/DeskSpace-R221-Source.zip' -DestinationPath '.candidate/verified'
Move-Item '.candidate/verified/DeskSpace' 'DeskSpace_Windows_R1/DeskSpace'
$code='DeskSpace_Windows_R1/DeskSpace/DeskSpaceUpdater.cs'
$c=[IO.File]::ReadAllText($code)
$anchor='private static readonly Version InstalledVersion = new(1, 5, 1093);'
if(([regex]::Matches($c,[regex]::Escape($anchor))).Count -ne 1){
 throw 'R287 updater version anchor absent, do not produce invalid auto-update'
}
$c=$c.Replace($anchor,
 ('private static readonly Version InstalledVersion = new(1, 5, '+$buildNumber+');'))
[IO.File]::WriteAllText($code,$c,[Text.UTF8Encoding]::new($false))
$setup='installer/DeskSpace.iss'
$content=[IO.File]::ReadAllText($setup)
# Exactly one legitimate Inno preprocessor declaration. Show version
# declarations (never secrets) if a historic archive uses another layout.
$versions=[regex]::Matches($content,
 '(?im)^[ \t]*#define[ \t]+AppVersion[ \t]+"[0-9]+(?:\.[0-9]+){1,3}"')
if($versions.Count -ne 1){
 $observed=@($content -split '\r?\n'|Where-Object{
  $_ -match '(?i)appversion|myappversion' }|Select-Object -First 7)
 throw ('R287 installer version declarations unexpected (count '+
  $versions.Count+'): '+($observed -join ' | '))
}
$versionDeclaration=$versions[0].Value
$newVersion='#define AppVersion "'+$Version+'"'
$content=$content.Remove($versions[0].Index,$versions[0].Length).
 Insert($versions[0].Index,$newVersion)
[IO.File]::WriteAllText($setup,$content,[Text.UTF8Encoding]::new($false))
New-Item -ItemType Directory -Force '.candidate/evidence'|Out-Null
@{
 schema='deskspace-r287-source-build-v1';
 source_commit=$ExpectedSourceSha;
 source_run=[long]$SourceRunId;
 version=$Version;
 source_zip_sha256=(Get-FileHash '.candidate/source/DeskSpace-R221-Source.zip' -Algorithm SHA256).Hash.ToLowerInvariant();
 version_only_app_change='DeskSpaceUpdater.cs';
 version_only_installer_change='installer/DeskSpace.iss';
} | ConvertTo-Json | Set-Content '.candidate/evidence/source-build.json'
Write-Host ('R287 verified canonical R286 source staged for '+$Version)

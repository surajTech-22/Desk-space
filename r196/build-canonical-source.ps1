# R196: Independently compile R195's SHA-verified canonical source archive.
# Fails closed: no fallback to the patch-reconstructed working tree.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=(Get-Location).Path
$dir=Join-Path $repo 'build-provenance'
$archive=Join-Path $dir 'DeskSpace-Canonical-Source.zip'
$record=Join-Path $dir 'DeskSpace-Canonical-Source.sha256.txt'
if(!(Test-Path -LiteralPath $archive -PathType Leaf) -or
   !(Test-Path -LiteralPath $record -PathType Leaf)){
  throw 'R196 verified source archive and checksum required'
}
$expected=((Get-Content -LiteralPath $record -Raw).Trim() -split '\s+')[0]
if($expected -notmatch '^[a-fA-F0-9]{64}$'){throw 'R196 bad source SHA256'}
$actual=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
if(!$actual.Equals($expected,[StringComparison]::OrdinalIgnoreCase)){
  throw 'R196 canonical source archive hash mismatch'
}
if((Get-Item -LiteralPath $archive).Length -gt 100MB){
 throw 'R196 source archive unexpectedly large'
}
$prefix='DeskSpace/'
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try{
  $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  if($zip.Entries.Count -lt 45 -or $zip.Entries.Count -gt 4000){
    throw 'R196 canonical entry count out of bounds'
  }
  foreach($entry in $zip.Entries){
    $name=$entry.FullName
    if($name.Contains('\') -or $name.StartsWith('/') -or
       $name.Contains('..') -or $name.Contains(':') -or
       !$seen.Add($name)){
      throw 'R196 unsafe or duplicate canonical ZIP entry'
    }
    $allowed=$name.StartsWith($prefix,[StringComparison]::Ordinal) -or
       $name -eq 'source-manifest.json' -or
       $name -eq 'BUILD_DESKSPACE_SETUP.ps1' -or
       $name -eq 'installer/DeskSpace.iss'
    if(!$allowed){throw 'R196 unexpected ZIP entry outside source scope'}
    if($entry.Length -gt 12MB){throw 'R196 oversized canonical source item'}
  }
}finally{$zip.Dispose()}
$root=Join-Path $env:RUNNER_TEMP ('DeskSpace-R196-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root|Out-Null
try{
  [IO.Compression.ZipFile]::ExtractToDirectory($archive,$root,$false)
  $manifest=Join-Path $root 'source-manifest.json'
  if(!(Test-Path $manifest)){throw 'R196 missing embedded provenance'}
  $m=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
  if($m.schema -ne 'deskspace-source-provenance-v1' -or
     [int]$m.source_file_count -lt 40){
      throw 'R196 invalid embedded source manifest'
  }
  $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  $checks=0
  foreach($item in $m.source_files){
    $rel=[string]$item.path
    $base='DeskSpace_Windows_R1/DeskSpace/'
    if(!$rel.StartsWith($base,[StringComparison]::Ordinal) -or
       $rel.Contains('..')){
      throw 'R196 invalid embedded source relative path'
    }
    $key='DeskSpace/'+$rel.Substring($base.Length)
    if(!$seen.Add($key)){throw 'R196 duplicated manifest source'}
    $file=Join-Path $root $key
    if(!(Test-Path -LiteralPath $file -PathType Leaf)){throw 'R196 source missing'}
    if((Get-Item -LiteralPath $file).Length -ne [long]$item.bytes -or
       !((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.Equals(
            [string]$item.sha256,[StringComparison]::OrdinalIgnoreCase))){
       throw 'R196 extracted source hash or length mismatch'
    }
    $checks++
  }
  if($checks -ne [int]$m.source_file_count){
    throw 'R196 source manifest count mismatch'
  }
  $project=Join-Path $root 'DeskSpace/DeskSpace.csproj'
  if(!(Test-Path -LiteralPath $project)){
      throw 'R196 independent WPF project missing'
  }
  $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
  & dotnet build $project -c Release --nologo -p:ContinuousIntegrationBuild=true
  if($LASTEXITCODE -ne 0){throw 'R196 isolated canonical-source compilation failed'}
  $dll=@(Get-ChildItem (Join-Path $root 'DeskSpace/bin') -File -Recurse -Filter DeskSpace.dll)
  if($dll.Count -lt 1 -or ($dll | Measure-Object -Property Length -Maximum).Maximum -lt 100000){
     throw 'R196 independent compiled application DLL missing or empty'
  }
  Write-Host ('R196 PASS: independently rebuilt DeskSpace.dll from '+$checks+
     ' SHA-verified source files extracted from canonical ZIP; no patch scripts executed.')
}
finally{
  try{Remove-Item -LiteralPath $root -Force -Recurse -ErrorAction Stop}
  catch{
    Write-Warning 'R196 temporary isolated source cleanup incomplete; no installed app/user data affected.'
  }
}

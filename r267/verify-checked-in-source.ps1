[CmdletBinding()]
param(
 [string]$Canonical='src/DeskSpace',
 [string]$Manifest='r221/output/DeskSpace-R221-Source.manifest.json'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$base=[IO.Path]::GetFullPath($Canonical)
$manifestPath=[IO.Path]::GetFullPath($Manifest)
if(!(Test-Path -LiteralPath "$base/DeskSpace.csproj" -PathType Leaf) -or
   !(Test-Path -LiteralPath $manifestPath -PathType Leaf)){
 throw 'R267 canonical project or verified regenerated manifest is unavailable'
}
$record=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
if($record.schema -ne 'deskspace-consolidated-source-v1' -or
   $record.file_count -lt 140 -or $record.file_count -gt 4000){
 throw 'R267 regenerated source manifest has invalid schema/count'
}
$allow=@('.cs','.xaml','.csproj','.ps1','.psm1','.ico','.png',
 '.json','.xml','.svg','.txt','.resx','.config','.props','.targets','.manifest')
$actual=@(Get-ChildItem -LiteralPath $base -Recurse -Force -File|
 Where-Object {
  $_.Extension.ToLowerInvariant() -in $allow -and
  $_.FullName -notmatch '[\\/](?:bin|obj|\.git|\.vs|packages)[\\/]'
 })
if($actual.Count -ne $record.file_count){
 throw ('R267 canonical WPF files differ from regenerated source count: '+
    $actual.Count+' != '+$record.file_count)
}
$map=[Collections.Generic.Dictionary[string,string]]::new(
 [StringComparer]::OrdinalIgnoreCase)
foreach($file in $actual){
 if(($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
  throw 'R267 canonical source symbolic links are forbidden'
 }
 $relative=[IO.Path]::GetRelativePath($base,$file.FullName).Replace([IO.Path]::DirectorySeparatorChar,[char]'/')
 if(!$map.TryAdd('DeskSpace/'+$relative,$file.FullName)){
  throw 'R267 canonical source contains case-conflicting file names'
 }
}
# Git on Windows can normalize CRLF/LF during commit/checkout, even
# when the byte-verified artifact is identical before staging. Compare
# canonical TEXT by exact characters after newline normalization only.
# Every generated source member must still match its original SHA256.
$generated=[IO.Path]::GetFullPath('DeskSpace_Windows_R1/DeskSpace')
$textExt=@('.cs','.xaml','.csproj','.ps1','.psm1','.json',
 '.xml','.svg','.txt','.resx','.config','.props','.targets','.manifest')
$normalized=0
foreach($item in $record.source_files){
 $path=''
 if(!$map.TryGetValue([string]$item.path,[ref]$path)){
  throw ('R267 canonical source does not contain expected file: '+$item.path)
 }
 $relative=([string]$item.path).Substring('DeskSpace/'.Length)
 $original=Join-Path $generated ($relative.Replace('/',[IO.Path]::DirectorySeparatorChar))
 if(!(Test-Path -LiteralPath $original -PathType Leaf)){
  throw ('R267 missing rebuilt reference source: '+$item.path)
 }
 if((Get-Item -LiteralPath $original).Length -ne [long]$item.bytes -or
    ![string]::Equals((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash,
        [string]$item.sha256,[StringComparison]::OrdinalIgnoreCase)){
  throw ('R267 rebuilt source does not match SHA-256 manifest: '+$item.path)
 }
 $currentHash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
 if([string]::Equals($currentHash,[string]$item.sha256,
     [StringComparison]::OrdinalIgnoreCase)){continue}
 if([IO.Path]::GetExtension($path).ToLowerInvariant() -notin $textExt){
  throw ('R267 non-text canonical source differs in bytes: '+$item.path)
 }
 $current=[IO.File]::ReadAllText($path).Replace("`r`n","`n")
 $reference=[IO.File]::ReadAllText($original).Replace("`r`n","`n")
 if(![string]::Equals($current,$reference,[StringComparison]::Ordinal)){
  throw ('R267 checked-in source differs beyond newline representation: '+$item.path)
 }
 $normalized++
}
Write-Host ('R267 PASS: '+$record.file_count+
 ' checked-in files reproduce SHA-verified source (newline-only Git conversion in '+
 $normalized+' text files); non-text assets byte-for-byte unchanged.')

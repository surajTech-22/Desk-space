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
foreach($item in $record.source_files){
 $path=''
 if(!$map.TryGetValue([string]$item.path,[ref]$path)){
  throw ('R267 canonical source does not contain expected file: '+$item.path)
 }
 if((Get-Item -LiteralPath $path).Length -ne [long]$item.bytes){
  throw ('R267 canonical source byte size differs from generated: '+$item.path)
 }
 if(![string]::Equals((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash,
    [string]$item.sha256,[StringComparison]::OrdinalIgnoreCase)){
  throw ('R267 canonical source content diverged from regenerated: '+$item.path)
 }
}
Write-Host ('R267 PASS: checked-in canonical project exactly equals '+
 $record.file_count+' independently regenerated, SHA-256 verified source files.')

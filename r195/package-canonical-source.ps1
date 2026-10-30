# R195: Capture the EXACT post-patch application source as one verifiable,
# ready-for-consolidation snapshot. This is CI-only and never reads Desktop,
# Downloads, APPDATA or existing DeskSpace user content.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
./r176/verify-source-manifest.ps1
$repo=(Get-Location).Path
$src=Join-Path $repo 'DeskSpace_Windows_R1/DeskSpace'
$provenance=Join-Path $repo 'build-provenance/source-manifest.json'
$manifest=Get-Content -LiteralPath $provenance -Raw | ConvertFrom-Json
$allow=@('.cs','.xaml','.csproj','.resx','.config','.ico','.png','.json','.xml','.svg','.txt')
$files=@(Get-ChildItem -LiteralPath $src -Recurse -File -Force |
  Where-Object { $_.Extension.ToLowerInvariant() -in $allow -and
    $_.FullName -notmatch '[\\/](?:bin|obj|\.git|packages)[\\/]' } |
  Sort-Object FullName)
if($files.Count -lt [int]$manifest.source_file_count){
  throw 'R195 source snapshot is missing declared source files'
}
if($files.Count -gt 4000){throw 'R195 unexpectedly large source file count'}
$root=([IO.Path]::GetFullPath($src)).TrimEnd('\','/')+
  [IO.Path]::DirectorySeparatorChar
$items=[Collections.Generic.List[object]]::new()
[long]$total=0
foreach($f in $files){
 if(($f.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
    throw "R195 source symlink or junction rejected"
 }
 if($f.Length -gt 12MB){throw 'R195 unexpected oversized source asset'}
 $total+=$f.Length
 if($total -gt 100MB){throw 'R195 source snapshot exceeded 100 MiB'}
 $relative=$f.FullName.Substring($root.Length).Replace('\','/')
 if([IO.Path]::IsPathRooted($relative) -or $relative.Contains('..') -or
    $relative.StartsWith('/') -or $relative.Contains(':')){
     throw 'R195 invalid relative source path'
 }
 $items.Add([pscustomobject]@{
   From=$f.FullName
   ZipPath='DeskSpace/'+$relative
   Hash=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
 })
}
foreach($a in @(
 @('build-provenance/source-manifest.json','source-manifest.json'),
 @('installer/DeskSpace.iss','installer/DeskSpace.iss'),
 @('BUILD_DESKSPACE_SETUP.ps1','BUILD_DESKSPACE_SETUP.ps1'))){
  $candidate=Join-Path $repo $a[0]
  if(!(Test-Path -LiteralPath $candidate -PathType Leaf)){
      throw "R195 missing assembly source dependency $($a[0])"
  }
  $items.Add([pscustomobject]@{
     From=$candidate
     ZipPath=$a[1]
     Hash=(Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash.ToLowerInvariant()
  })
}
$sourceKeys=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($item in $items){
    if(!$sourceKeys.Add($item.ZipPath)){throw 'R195 case-insensitive path collision'}
}
if($items.Count -lt 45){throw 'R195 incomplete canonical source snapshot'}
$folder=Join-Path $repo 'build-provenance'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$target=Join-Path $folder 'DeskSpace-Canonical-Source.zip'
$temp=Join-Path $folder ([IO.Path]::GetRandomFileName()+'.partial')
try{
    $stream=[IO.File]::Open($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,
        [IO.FileShare]::None)
    try{
      $zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
      try{
        foreach($item in $items|Sort-Object ZipPath){
          $entry=$zip.CreateEntry($item.ZipPath,
              [IO.Compression.CompressionLevel]::Optimal)
          # Fixed ZIP timestamps make identical snapshots reproducible.
          $entry.LastWriteTime=[DateTimeOffset]::new(1980,1,1,0,0,0,[TimeSpan]::Zero)
          $read=[IO.File]::OpenRead($item.From)
          try{
            $write=$entry.Open()
            try{ $read.CopyTo($write) }
            finally{ $write.Dispose() }
          } finally{ $read.Dispose() }
        }
      }finally{$zip.Dispose()}
    }finally{$stream.Dispose()}
    if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target -Force}
    [IO.File]::Move($temp,$target)
}finally{
    if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force}
}
# Validate every item by hashing the bytes FROM the ZIP, not its on-disk twin.
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$archive=[IO.Compression.ZipFile]::OpenRead($target)
try{
    if($archive.Entries.Count -ne $items.Count){throw 'R195 ZIP entry count mismatch'}
    foreach($item in $items){
        $entry=$archive.GetEntry($item.ZipPath)
        if($null -eq $entry -or !$seen.Add($entry.FullName)){
            throw 'R195 missing or duplicate ZIP member'
        }
        $in=$entry.Open()
        try{
            $actual=[Convert]::ToHexString(
                [Security.Cryptography.SHA256]::HashData($in)).ToLowerInvariant()
            if($actual -ne $item.Hash){throw 'R195 source ZIP member SHA256 mismatch'}
        }finally{$in.Dispose()}
    }
}finally{$archive.Dispose()}
$sha=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $folder 'DeskSpace-Canonical-Source.sha256.txt'),
  ($sha+'  DeskSpace-Canonical-Source.zip'+[Environment]::NewLine),
  [Text.UTF8Encoding]::new($false))
Write-Host ("R195 PASS: "+$items.Count+" canonical source assets, "+$manifest.source_file_count+
  " manifest-tracked WPF files, all ZIP member hashes verified; bundle SHA256 "+$sha)

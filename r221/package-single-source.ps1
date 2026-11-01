[CmdletBinding()]
param(
    [string]$Source='DeskSpace_Windows_R1/DeskSpace',
    [string]$Output='r221/output'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R221: freeze the fully patched source to an independently rebuildable
# atomic archive. Operates only on the checked-out repository. Never touches
# Downloads, the installed app, LocalAppData, or personal workbook data.
$base=[IO.Path]::GetFullPath($Source).TrimEnd('\','/')
$target=[IO.Path]::GetFullPath($Output)
if(!(Test-Path -LiteralPath (Join-Path $base 'DeskSpace.csproj') -PathType Leaf) -or
   !(Test-Path -LiteralPath (Join-Path $base 'DeskSpaceUpdateSupervisor.ps1') -PathType Leaf)){
   throw 'R221 complete verified DeskSpace application source tree required'
}
$allow=@('.cs','.xaml','.csproj','.ps1','.psm1','.ico','.png','.json',
    '.xml','.svg','.txt','.resx','.config','.props','.targets','.manifest')
$files=@(Get-ChildItem -LiteralPath $base -File -Recurse -Force |
  Where-Object { $_.Extension.ToLowerInvariant() -in $allow -and
    $_.FullName -notmatch '[\\/](?:bin|obj|\.git|\.vs|packages)[\\/]' } |
  Sort-Object -Property FullName)
if($files.Count -lt 140 -or $files.Count -gt 4000){
    throw "R221 incomplete or oversized patched source count ($($files.Count))"
}
$entries=[Collections.Generic.List[object]]::new()
$unique=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[long]$total=0
$prefix=$base+[IO.Path]::DirectorySeparatorChar
foreach($f in $files){
    if(($f.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
       throw 'R221 linked source files forbidden'
    }
    if($f.Length -gt 12MB){throw 'R221 oversized WPF source asset'}
    $total+=$f.Length
    if($total -gt 100MB){throw 'R221 source exceeded 100MiB budget'}
    if(!$f.FullName.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){
       throw 'R221 source file outside canonical tree'
    }
    $rel=$f.FullName.Substring($prefix.Length).Replace('\','/')
    if($rel.Contains('..') -or $rel.Contains(':') -or $rel.StartsWith('/') -or
       $rel.EndsWith('/') -or $rel.Contains('//')){
       throw 'R221 invalid archive member name'
    }
    $key='DeskSpace/'+$rel
    if(!$unique.Add($key)){throw 'R221 ambiguous case-insensitive source path'}
    $entries.Add([pscustomobject]@{
       path=$key
       bytes=[long]$f.Length
       sha256=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
       from=$f.FullName
    })
}
New-Item -ItemType Directory -Force -Path $target|Out-Null
$archive=Join-Path $target 'DeskSpace-R221-Source.zip'
$manifest=Join-Path $target 'DeskSpace-R221-Source.manifest.json'
$receipt=Join-Path $target 'DeskSpace-R221-Source.sha256.txt'
$record=[ordered]@{
    schema='deskspace-consolidated-source-v1'
    baseline='v1.5.1093'
    commit=if($env:GITHUB_SHA){$env:GITHUB_SHA}else{'local-test'}
    file_count=$entries.Count
    bytes=$total
    source_files=@($entries|ForEach-Object {
      [ordered]@{path=$_.path;bytes=$_.bytes;sha256=$_.sha256}
    })
}
$manifestJson=ConvertTo-Json -InputObject $record -Depth 7 -Compress
[IO.File]::WriteAllText($manifest,$manifestJson,[Text.UTF8Encoding]::new($false))
$partial=Join-Path $target ([IO.Path]::GetRandomFileName()+'.partial')
try{
    $stream=[IO.File]::Open($partial,[IO.FileMode]::CreateNew,
        [IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try{
        $zip=[IO.Compression.ZipArchive]::new($stream,
            [IO.Compression.ZipArchiveMode]::Create,$true)
        try{
            foreach($item in $entries|Sort-Object path){
                $member=$zip.CreateEntry($item.path,[IO.Compression.CompressionLevel]::Optimal)
                $member.LastWriteTime=[DateTimeOffset]::new(
                    1980,1,1,0,0,0,[TimeSpan]::Zero)
                $sourceStream=[IO.File]::OpenRead($item.from)
                try{
                    $entryStream=$member.Open()
                    try{$sourceStream.CopyTo($entryStream)}
                    finally{$entryStream.Dispose()}
                }finally{$sourceStream.Dispose()}
            }
            $m=$zip.CreateEntry('manifest.json',[IO.Compression.CompressionLevel]::Optimal)
            $m.LastWriteTime=[DateTimeOffset]::new(1980,1,1,0,0,0,[TimeSpan]::Zero)
            $write=[IO.StreamWriter]::new($m.Open(),[Text.UTF8Encoding]::new($false))
            try{$write.Write($manifestJson)}finally{$write.Dispose()}
        }finally{$zip.Dispose()}
    }finally{$stream.Dispose()}
    # Check the ZIP before replacing the old successful archive.
    [void](& 'r221/verify-single-source.ps1' -ArchivePath $partial -ManifestPath $manifest -SkipReceipt)
    [IO.File]::Move($partial,$archive,$true)
}finally{
    if(Test-Path -LiteralPath $partial){Remove-Item -LiteralPath $partial -Force}
}
$sha=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($receipt,
    ($sha+'  DeskSpace-R221-Source.zip'+[Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))
[void](& 'r221/verify-single-source.ps1' -ArchivePath $archive -ManifestPath $manifest -ReceiptPath $receipt)
Write-Host ("R221 PASS: "+$entries.Count+" hash-verified patched sources, "+$total+
    " bytes, reproducible ZIP, SHA-256 "+$sha)

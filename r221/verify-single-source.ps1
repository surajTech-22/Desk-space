[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$ArchivePath,
  [Parameter(Mandatory=$true)][string]$ManifestPath,
  [string]$ReceiptPath='',
  [switch]$SkipReceipt
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(!(Test-Path -LiteralPath $ArchivePath -PathType Leaf) -or
   !(Test-Path -LiteralPath $ManifestPath -PathType Leaf)){
    throw 'R221 source archive or manifest missing'
}
if((Get-Item -LiteralPath $ArchivePath).Length -gt 100MB){
    throw 'R221 archive exceeded 100MiB compressed limit'
}
if(!$SkipReceipt){
    if(!(Test-Path -LiteralPath $ReceiptPath -PathType Leaf)){
        throw 'R221 source archive SHA-256 receipt missing'
    }
    $line=[IO.File]::ReadAllText([IO.Path]::GetFullPath($ReceiptPath))
    if($line -cnotmatch '\A([a-f0-9]{64})  DeskSpace-R221-Source\.zip\r?\n?\z'){
        throw 'R221 malformed immutable archive receipt'
    }
    $found=(Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if($found -cne $Matches[1]){
       throw 'R221 archive checksum mismatch'
    }
}
$raw=[IO.File]::ReadAllText([IO.Path]::GetFullPath($ManifestPath))
$m=$raw|ConvertFrom-Json
if($m.schema -ne 'deskspace-consolidated-source-v1' -or
   $m.baseline -ne 'v1.5.1093' -or
   [int]$m.file_count -lt 140 -or [int]$m.file_count -gt 4000){
   throw 'R221 malformed canonical source manifest'
}
$expected=@{}
foreach($item in $m.source_files){
    $key=[string]$item.path
    if(!$key.StartsWith('DeskSpace/',[StringComparison]::Ordinal) -or
       $key.Contains('..') -or $key.Contains(':') -or
       $key.Contains('\') -or $key.StartsWith('/') -or
       $key.EndsWith('/') -or [long]$item.bytes -lt 0 -or
       [long]$item.bytes -gt 12MB -or
       [string]$item.sha256 -cnotmatch '^[0-9a-f]{64}$'){
        throw 'R221 unsafe or invalid source manifest member'
    }
    if($expected.ContainsKey($key.ToLowerInvariant())){
        throw 'R221 duplicate manifest member'
    }
    $expected[$key.ToLowerInvariant()]=$item
}
if($expected.Count -ne [int]$m.file_count){
    throw 'R221 manifest source count mismatch'
}
if(!$expected.ContainsKey('deskspace/deskspace.csproj') -or
   !$expected.ContainsKey('deskspace/deskspaceupdatesupervisor.ps1') -or
   !$expected.ContainsKey('deskspace/deskspaceverifiedupdatequeuev191.cs')){
    throw 'R221 critical app/updater source is missing'
}
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$zip=[IO.Compression.ZipFile]::OpenRead($ArchivePath)
try{
    if($zip.Entries.Count -ne $expected.Count+1){
        throw 'R221 extra or missing ZIP members'
    }
    foreach($entry in $zip.Entries){
        $key=$entry.FullName
        if(!$seen.Add($key) -or $key.Contains('..') -or
           $key.Contains('\') -or $key.StartsWith('/') -or
           $key.Contains(':') -or $entry.Length -gt 12MB){
            throw 'R221 unsafe archive entry'
        }
        $in=$entry.Open()
        try{
            if($key -ceq 'manifest.json'){
                $reader=[IO.StreamReader]::new($in,[Text.Encoding]::UTF8,$true)
                try{$embedded=$reader.ReadToEnd()}
                finally{$reader.Dispose()}
                if($embedded -cne $raw){throw 'R221 external/embedded manifest mismatch'}
                continue
            }
            $name=$key.ToLowerInvariant()
            if(!$expected.ContainsKey($name)){throw 'R221 unknown archive entry'}
            $item=$expected[$name]
            if($entry.Length -ne [long]$item.bytes){
                throw 'R221 compressed member size does not match manifest'
            }
            $sha=[Convert]::ToHexString(
                [Security.Cryptography.SHA256]::HashData($in)).ToLowerInvariant()
            if($sha -cne [string]$item.sha256){
                throw 'R221 source member content hash mismatch'
            }
        }finally{$in.Dispose()}
    }
}finally{$zip.Dispose()}
Write-Host "R221 PASS: verified $($expected.Count) source members, full SHA-256 archive and embedded manifest."

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$archive='r221/output/DeskSpace-R221-Source.zip'
$manifest='r221/output/DeskSpace-R221-Source.manifest.json'
$receipt='r221/output/DeskSpace-R221-Source.sha256.txt'
& 'r221/verify-single-source.ps1' -ArchivePath $archive -ManifestPath $manifest -ReceiptPath $receipt
$root=Join-Path ([IO.Path]::GetTempPath()) ('deskspace-r221-'+[guid]::NewGuid().ToString('N'))
try{
    New-Item -ItemType Directory -Path $root -Force|Out-Null
    $copied=Join-Path $root 'DeskSpace-R221-Source.zip'
    $m=Join-Path $root 'DeskSpace-R221-Source.manifest.json'
    $sha=Join-Path $root 'DeskSpace-R221-Source.sha256.txt'
    Copy-Item -LiteralPath $archive -Destination $copied
    Copy-Item -LiteralPath $manifest -Destination $m
    Copy-Item -LiteralPath $receipt -Destination $sha
    [IO.File]::AppendAllText($sha,'0')
    $blocked=$false
    try{& 'r221/verify-single-source.ps1' -ArchivePath $copied -ManifestPath $m -ReceiptPath $sha}
    catch{if($_.Exception.Message -like '*malformed immutable archive receipt*'){$blocked=$true}else{throw}}
    if(!$blocked){throw 'R221 tampered archive receipt must be rejected'}
    Copy-Item -LiteralPath $receipt -Destination $sha -Force
    [IO.File]::AppendAllText($m,' ')
    $blocked=$false
    try{& 'r221/verify-single-source.ps1' -ArchivePath $copied -ManifestPath $m -ReceiptPath $sha}
    catch{if($_.Exception.Message -like '*external/embedded manifest mismatch*'){$blocked=$true}else{throw}}
    if(!$blocked){throw 'R221 modified source manifest must be rejected'}
    Write-Host 'R221 PASS: SHA receipt and out-of-band manifest tampering rejected.'
}
finally{
    if(Test-Path -LiteralPath $root){Remove-Item -LiteralPath $root -Force -Recurse}
}

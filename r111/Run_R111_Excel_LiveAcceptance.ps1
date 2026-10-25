$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$bundle = Split-Path -Parent $MyInvocation.MyCommand.Path
$test = Join-Path $bundle 'R111Test/LiveExcelAcceptance.exe'
$app = Join-Path $bundle 'DeskSpaceTestApp/DeskSpace.dll'
if(!(Test-Path -LiteralPath $test) -or !(Test-Path -LiteralPath $app)){
    throw 'Incomplete R111 portable test package. No user workbook was touched.'
}
$running = @(Get-Process EXCEL -ErrorAction SilentlyContinue)
if($running.Count -gt 0){
    throw 'Close all open Microsoft Excel windows normally and save changes before running this isolated test. No tests were run.'
}
if(-not [Type]::GetTypeFromProgID('Excel.Application')){
    throw 'Desktop Microsoft Excel is not installed. This is NOT a passing acceptance test.'
}
$downloads=Join-Path $env:USERPROFILE 'Downloads'
[IO.Directory]::CreateDirectory($downloads) | Out-Null
$stamp=Get-Date -Format 'yyyyMMdd_HHmmss'
$log=Join-Path $downloads "DeskSpace_R111_LiveExcel_$stamp.log"
$diag=Join-Path $downloads "DeskSpace_R111_LiveExcel_$stamp.zip"
"DeskSpace R111 isolated COM acceptance" | Set-Content -LiteralPath $log
"UTC: $([DateTime]::UtcNow.ToString('o'))" | Add-Content $log
"SHA256 DeskSpace.dll: $((Get-FileHash $app -Algorithm SHA256).Hash)" | Add-Content $log
"SHA256 Acceptance.exe: $((Get-FileHash $test -Algorithm SHA256).Hash)" | Add-Content $log
"Windows: $([Environment]::OSVersion.VersionString)" | Add-Content $log
try {
    $liveOutput = (& $test $app *>&1 | Out-String)
    $exit=$LASTEXITCODE
    $liveOutput | Add-Content -LiteralPath $log
    Write-Host $liveOutput
    if($exit -ne 0){throw "Live Excel COM acceptance failed; exit code $exit"}
    "RESULT: PASS (actual Microsoft Excel COM)" | Add-Content -LiteralPath $log
}
catch {
    "RESULT: FAILED or BLOCKED: $($_.Exception.Message)" | Add-Content -LiteralPath $log
    Write-Warning "Acceptance did not pass. Evidence: $log"
}
finally{
    Compress-Archive -LiteralPath $log -DestinationPath $diag -CompressionLevel Optimal -Force
    Write-Host "R111 diagnostic ZIP: $diag"
    Write-Host 'This test does not install/update DeskSpace. No user workbook or mappings are intentionally modified.'
}

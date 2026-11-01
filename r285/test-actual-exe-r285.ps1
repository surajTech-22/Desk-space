$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$project='DeskSpace_Windows_R1/DeskSpace'
$executable=Join-Path $project 'bin/Release/net8.0-windows10.0.19041.0/DeskSpace.exe'
if(!(Test-Path -LiteralPath $executable)){
 throw ('R285 actual compiled DeskSpace.exe missing: '+$executable)
}
$downloads=Join-Path $env:USERPROFILE 'Downloads'
New-Item -ItemType Directory -Path $downloads -Force | Out-Null
$started=Get-Date
$process=Start-Process -FilePath (Resolve-Path $executable).Path -ArgumentList '--dpi-audit' -PassThru -Wait
if($process.ExitCode -ne 0){
 throw ('R285 actual EXE DPI report exited with code '+$process.ExitCode)
}
$recent=@(Get-ChildItem -LiteralPath $downloads -File -Filter 'DeskSpace_Monitor_DPI_*.json' | Where-Object {
 $_.LastWriteTime -ge $started.AddSeconds(-3)
} | Sort-Object LastWriteTime -Descending)
if($recent.Count -lt 1){
 throw 'R285 actual EXE failed to produce on-demand Downloads DPI audit'
}
$report=Get-Content -LiteralPath $recent[0].FullName -Raw | ConvertFrom-Json
if($report.Schema -ne 'deskspace-dpi-audit-v285'){
 throw 'R285 actual EXE report schema mismatch'
}
if(!$report.PerMonitorV2 -or $report.Awareness -ne 'PerMonitorV2'){
 throw ('R285 actual DeskSpace.exe NOT PMv2. Awareness='+$report.Awareness+
  '; do not claim physical mixed-DPI behavior')
}
if($report.MonitorCount -lt 1 -or
   $report.MonitorCount -ne @($report.Monitors).Count){
 throw 'R285 actual Windows runner screen enumeration failed'
}
foreach($screen in @($report.Monitors)){
 if([string]::IsNullOrWhiteSpace($screen.Device) -or
  $screen.Width -lt 1 -or $screen.Height -lt 1){
  throw 'R285 actual Windows screen bounds invalid'
 }
}
New-Item -ItemType Directory -Force '.r201/evidence' | Out-Null
Copy-Item -LiteralPath $recent[0].FullName -Destination '.r201/evidence/r285_actual_exe_dpi.json' -Force
Write-Host ('R285 PASS: ACTUAL DeskSpace.exe is PMv2, '+$report.MonitorCount+
 ' Windows runner display(s) audited. Physical dual-monitor acceptance pending.')

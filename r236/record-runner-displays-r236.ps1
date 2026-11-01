$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Windows.Forms
$devices=@([System.Windows.Forms.Screen]::AllScreens | ForEach-Object {
  [pscustomobject]@{
    device=$_.DeviceName
    primary=$_.Primary
    bounds=[pscustomobject]@{
      x=$_.Bounds.X;y=$_.Bounds.Y
      width=$_.Bounds.Width;height=$_.Bounds.Height}
    work=[pscustomobject]@{
      x=$_.WorkingArea.X;y=$_.WorkingArea.Y
      width=$_.WorkingArea.Width;height=$_.WorkingArea.Height}
  }
})
$report=[ordered]@{
  kind="DeskSpace R236 Windows GitHub runner monitor enumeration"
  recorded_at_utc=(Get-Date).ToUniversalTime().ToString('o')
  runner_os=$env:RUNNER_OS
  runner_name=$env:RUNNER_NAME
  actual_reported_displays=$devices.Count
  devices=$devices
  synthetic_two_monitor_matrix="covered by native WPF test, regardless of actual display count"
  real_physical_dual_monitor_acceptance="NOT_VERIFIED"
  caution="Runner screens are observational; two monitors in a VM do not prove user's physical monitor behavior"
}
New-Item -ItemType Directory -Force '.r201/evidence'|Out-Null
$target='.r201/evidence/monitor-enumeration-r236.json'
$report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $target -Encoding utf8
Write-Host ("R236 enumerated "+$devices.Count+" Windows runner monitor(s); report: "+$target)
Write-Host 'R236 native WPF uses synthetic monitors for geometry. Real device hardware acceptance: NOT VERIFIED.'

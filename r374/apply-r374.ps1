$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R374 - once-per-second network-speed readout, only inside existing Deck.
# Apply after R373. The 5s Running Apps timer and 5s NIC cache are untouched.
$deck='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
if(!(Test-Path -LiteralPath $deck)){throw 'R374 expected DeskSpace shipping Deck source'}
$source=[IO.File]::ReadAllText($deck)
function Swap-R374([string]$old,[string]$replacement,[string]$why){
    $count=([regex]::Matches($script:source,[regex]::Escape($old))).Count
    if($count -ne 1){throw ('R374 '+$why+': expected exactly one match, got '+$count)}
    $script:source=$script:source.Replace($old,$replacement)
}
Swap-R374 'Interval=TimeSpan.FromMilliseconds(100)};' 'Interval=TimeSpan.FromSeconds(1)};' 'network speed refresh interval'
Swap-R374 'ToolTip="Live download ↓ and upload ↑; refreshed every 100 milliseconds while the Deck is open"' 'ToolTip="Live download ↓ and upload ↑; refreshed once per second while the Deck is open"' 'network speed tooltip'
[IO.File]::WriteAllText($deck,$script:source,[Text.UTF8Encoding]::new($false))
foreach($guard in @(
  'Interval=TimeSpan.FromSeconds(1)};',
  'runningAppsTimer.Tick+=(_,_)=>RefreshRunningApps();',
  'Interval=TimeSpan.FromSeconds(5)',
  'networkSpeedTimerV373.Tick+=(_,_)=>RefreshNetworkSpeedV372();',
  'networkSpeedTimerV373.Stop();',
  '!searchActiveV225 && !deckMinimized',
  'networkSpeedLabelV372=new TextBlock{',
  'ToolTip="Live download ↓ and upload ↑; refreshed once per second while the Deck is open"')){
    if(!$script:source.Contains($guard)){throw ('R374 lost safety guard '+$guard)}
}
if($script:source.Contains('Interval=TimeSpan.FromMilliseconds(100)};')){
    throw 'R374 network readout still refreshes at 100 milliseconds'
}
Write-Host 'R374 PASS: network speed in original Deck updates once each second; existing 5-second Running Apps and hidden/minimized lifecycle preserved.'

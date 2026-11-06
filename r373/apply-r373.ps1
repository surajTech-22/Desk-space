$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R373: make the R372 network speed strip live at 100ms only while the
# original Deck is visible and expanded. Do NOT change Running Apps
# refresh, create an extra overlay, or run a busy-loop background worker.
$deck='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
$sourceMeter='src/DeskSpace/DeskSpaceDeckNetworkSpeedV372.cs'
$shippingMeter='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckNetworkSpeedV372.cs'
if(!(Test-Path $deck) -or !(Test-Path $sourceMeter) -or
   !(Test-Path $shippingMeter)){throw 'R373 requires installed R372 Deck source'}
$lf=[string][char]10
$crlf=([string][char]13)+$lf
$script:code=[IO.File]::ReadAllText($deck).Replace($crlf,$lf)
function Swap-R373([string]$old,[string]$replacement,[string]$why){
    $old=$old.Replace($crlf,$lf)
    $replacement=$replacement.Replace($crlf,$lf)
    $count=([regex]::Matches($script:code,[regex]::Escape($old))).Count
    if($count -ne 1){throw ('R373 '+$why+': expected exactly one anchor, got '+$count)}
    $script:code=$script:code.Replace($old,$replacement)
}
Swap-R373 @'
    // R372: rates are inside existing Deck; no extra window or timer.
    private readonly DeskSpaceDeckNetworkSpeedV372 networkSpeedV372=new();
'@ @'
    // R373: 100ms sampling only while this monitor's Deck is open and
    // expanded. This is NOT a 1-microsecond CPU-burning busy loop.
    private readonly DispatcherTimer networkSpeedTimerV373=new(
        DispatcherPriority.Background){
            Interval=TimeSpan.FromMilliseconds(100)};
    private readonly DeskSpaceDeckNetworkSpeedV372 networkSpeedV372=new();
    private void SyncNetworkSpeedTimerV373()
    {
        bool active=!disposed && host.IsVisible &&
            DeskSpaceDeckVisibilityV89.Visible && popup.IsOpen &&
            !searchActiveV225 && !deckMinimized;
        if(active)
        {
            if(networkSpeedTimerV373.IsEnabled)return;
            networkSpeedV372.Reset();
            networkSpeedV372.Sample();
            networkSpeedTimerV373.Start();
        }
        else if(networkSpeedTimerV373.IsEnabled)
        {
            networkSpeedTimerV373.Stop();
            networkSpeedV372.Reset();
        }
    }
'@ 'Deck meter 100ms sampling and visibility state'
Swap-R373 @'
        DeskSpaceMonitorDockV190.Attach(host,popup,panel);
'@ @'
        // Bind to the actual Deck Popup once, regardless of its custom
        // placement algorithm in this installer-source generation.
        networkSpeedTimerV373.Tick+=(_,_)=>RefreshNetworkSpeedV372();
        popup.Opened+=(_,_)=>SyncNetworkSpeedTimerV373();
        popup.Closed+=(_,_)=>SyncNetworkSpeedTimerV373();
        DeskSpaceMonitorDockV190.Attach(host,popup,panel);
'@ 'hook original Deck Popup lifecycle without changing positioning'
Swap-R373 @'
            runningAppsTimer.Tick+=(_,_)=>{
                RefreshNetworkSpeedV372();
                RefreshRunningApps();
            };
'@ @'
            runningAppsTimer.Tick+=(_,_)=>RefreshRunningApps();
'@ 'preserve five-second Running Apps refresh'
Swap-R373 @'
        newFiles.Start();
        Render();
        RefreshNetworkSpeedV372();
'@ @'
        newFiles.Start();
        Render();
        SyncNetworkSpeedTimerV373();
'@ 'start meter after original Deck render'
Swap-R373 @'
            runningAppsTimer.Stop();
            networkSpeedV372.Reset();
'@ @'
            runningAppsTimer.Stop();
            SyncNetworkSpeedTimerV373();
'@ 'stop hidden Deck meter'
Swap-R373 @'
                if(deckMinimized)deckPreview?.Close();
                Render();
                if(!deckMinimized)
'@ @'
                if(deckMinimized)deckPreview?.Close();
                Render();
                SyncNetworkSpeedTimerV373();
                if(!deckMinimized)
'@ 'stop meter if Deck minimized'
Swap-R373 @'
            popup.IsOpen=host.IsVisible;
            return;
        }
'@ @'
            popup.IsOpen=host.IsVisible;
            SyncNetworkSpeedTimerV373();
            return;
        }
'@ 'minimized original header-only return'
Swap-R373 @'
ToolTip="Live download ↓ and upload ↑; active network adapters, refreshed every 5 seconds"
'@ @'
ToolTip="Live download ↓ and upload ↑; refreshed every 100 milliseconds while the Deck is open"
'@ 'network speed tooltip'
Swap-R373 @'
        DeskSpaceMonitorDockV190.RequestReflowV314(panel,openingV314);
    }
    public void Dispose()
'@ @'
        DeskSpaceMonitorDockV190.RequestReflowV314(panel,openingV314);
        SyncNetworkSpeedTimerV373();
    }
    public void Dispose()
'@ 'toggle sampler on ordinary Deck re-render'
Swap-R373 @'
        popup.IsOpen=false;
        recentRenderTimer.Stop();
'@ @'
        popup.IsOpen=false;
        networkSpeedTimerV373.Stop();
        recentRenderTimer.Stop();
'@ 'stop meter when owning widget is disposed'
[IO.File]::WriteAllText($deck,$script:code,[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $sourceMeter -Destination $shippingMeter -Force
if((Get-FileHash $sourceMeter -Algorithm SHA256).Hash -ne
   (Get-FileHash $shippingMeter -Algorithm SHA256).Hash){
    throw 'R373 cached-adapter meter source does not match shipping bytes'
}
$watch=Get-Content $deck -Raw
$meter=Get-Content $shippingMeter -Raw
foreach($guard in @('Interval=TimeSpan.FromMilliseconds(100)',
    'networkSpeedTimerV373.Tick+=',
    'popup.Opened+=','popup.Closed+=',
    '!searchActiveV225 && !deckMinimized',
    'networkSpeedTimerV373.Stop();','runningAppsTimer.Tick+=(_,_)=>RefreshRunningApps()',
    'networkSpeedLabelV372=new TextBlock{')){
    if(!$watch.Contains($guard)){throw ('R373 Deck live meter guard missing: '+$guard)}
}
foreach($guard in @('cachedAdapters=Array.Empty<NetworkInterface>()',
    'now-adaptersRefreshedTicks>=5*Stopwatch.Frequency',
    'GetIPv4Statistics()')){
    if(!$meter.Contains($guard)){throw ('R373 cached adapter guard missing: '+$guard)}
}
if($watch.Contains('runningAppsTimer.Tick+=(_,_)=>{')){
    throw 'R373 may not throttle download/upload using Running Apps tick'
}
Write-Host 'R373 PASS: 100ms in-Deck network display; cached NIC enumeration every 5s; no polling while hidden or minimized; Running Apps remains 5s.'

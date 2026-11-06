$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R372: Stopwatch Back always returns to COMPACT circle; network traffic
# stays INSIDE existing Deck. Run after R369/R370/R371 shipping patches.
$lf=[string][char]10
$crlf=([string][char]13)+$lf
function Begin-Patch([string]$path){
    if(!(Test-Path -LiteralPath $path)){throw ('R372 missing source: '+$path)}
    $script:code=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
}
function Swap-R372([string]$old,[string]$replacement,[string]$context){
    $old=$old.Replace($crlf,$lf)
    $replacement=$replacement.Replace($crlf,$lf)
    $count=([regex]::Matches($script:code,[regex]::Escape($old))).Count
    if($count -ne 1){
        throw ('R372 '+$context+': expected one shipping anchor, found '+$count)
    }
    $script:code=$script:code.Replace($old,$replacement)
}
function Finish-Patch([string]$path){
    [IO.File]::WriteAllText($path,$script:code,
        [Text.UTF8Encoding]::new($false))
}
$widget='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
Begin-Patch $widget
Swap-R372 @'
    private string? stopwatchReturnCenterV371;
    private bool stopwatchReturnRingV371;

    private void OpenStopwatchFromCircleV371(bool fromRing=false)
'@ @'
    private string? stopwatchReturnCenterV371;

    // R372: Back always restores the former COMPACT circle, not donut.
    private void OpenStopwatchFromCircleV371()
'@ 'obsolete donut return state'
Swap-R372 '        stopwatchReturnRingV371=fromRing;' '' 'obsolete ring return flag'
Swap-R372 '        bool returnToRing=wasStopwatch && stopwatchReturnRingV371;' '' 'obsolete ring return flag use'
Swap-R372 '            stopwatchReturnRingV371=false;' '' 'obsolete ring reset'
Swap-R372 @'
        if(returnToRing)
            Dispatcher.BeginInvoke(new Action(()=>{
                if(!shutdown && activeModule==null &&
                   !ringExpanded && !closingMorph && !hiddenAtEdge)
                    ToggleRing();
            }),System.Windows.Threading.DispatcherPriority.Background);
'@ @'
        // R372: all Stopwatch Back actions return to the compact circle.
        // Only an explicit user action may expand the twelve-sector donut.
'@ 'stop automatic ring expansion after stopwatch'
Swap-R372 '            OpenStopwatchFromCircleV371(fromRing:true);' '            OpenStopwatchFromCircleV371();' 'donut stopwatch sector routing'
Finish-Patch $widget

$deck='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
Begin-Patch $deck
Swap-R372 @'
    private bool runningAppsTimerConnected,refreshingRunningApps;
'@ @'
    private bool runningAppsTimerConnected,refreshingRunningApps;
    // R372: rates are inside existing Deck; no extra window or timer.
    private readonly DeskSpaceDeckNetworkSpeedV372 networkSpeedV372=new();
    private TextBlock? networkSpeedLabelV372;
    private void RefreshNetworkSpeedV372()
    {
        if(disposed || searchActiveV225 || deckMinimized || !popup.IsOpen)
            return;
        networkSpeedV372.Sample();
        if(networkSpeedLabelV372!=null &&
           networkSpeedLabelV372.Text!=networkSpeedV372.Display)
            networkSpeedLabelV372.Text=networkSpeedV372.Display;
    }
'@ 'Deck network sampler lifecycle'
Swap-R372 '            runningAppsTimer.Tick+=(_,_)=>RefreshRunningApps();' @'
            runningAppsTimer.Tick+=(_,_)=>{
                RefreshNetworkSpeedV372();
                RefreshRunningApps();
            };
'@ 'reuse existing Deck timer'
Swap-R372 @'
        newFiles.Start();
        Render();
'@ @'
        newFiles.Start();
        Render();
        RefreshNetworkSpeedV372();
'@ 'initial Deck rate baseline'
Swap-R372 @'
            runningAppsTimer.Stop();
        }
        else
        {
'@ @'
            runningAppsTimer.Stop();
            networkSpeedV372.Reset();
        }
        else
        {
'@ 'stop network rate on hidden Deck'
# The shipping Deck-visibility handler differs among R304/R366 builds;
# its existing runningAppsTimer resumes when shown, which will sample
# traffic on the next existing tick. Do not change Deck visibility logic.
Swap-R372 @'
        list.Children.Clear();
        if(pendingDuplicateV211
'@ @'
        list.Children.Clear();
        networkSpeedLabelV372=null;
        if(pendingDuplicateV211
'@ 'discard old text during Deck re-render'
Swap-R372 @'
        if(deckMinimized)
        {
            deckPreview?.Close();
            popup.IsOpen=host.IsVisible;
            return;
        }
        // Running Apps is hover-only;
'@ @'
        if(deckMinimized)
        {
            deckPreview?.Close();
            popup.IsOpen=host.IsVisible;
            return;
        }
        // R372: this slim speed row is INSIDE the Deck, below its
        // header and above Running Apps; never in the outside gap.
        networkSpeedLabelV372=new TextBlock{
            Text=networkSpeedV372.Display,
            FontSize=10,FontWeight=FontWeights.Medium,
            FontFamily=new FontFamily("Segoe UI"),
            Foreground=new SolidColorBrush(Color.FromRgb(161,217,249)),
            VerticalAlignment=VerticalAlignment.Center,
            HorizontalAlignment=HorizontalAlignment.Center,
            TextTrimming=TextTrimming.CharacterEllipsis,
            TextAlignment=TextAlignment.Center,
            Margin=new Thickness(5,2,5,2),
            ToolTip="Live download ↓ and upload ↑; active network adapters, refreshed every 5 seconds"
        };
        list.Children.Add(new Border{
            CornerRadius=new CornerRadius(6),
            Background=new SolidColorBrush(Color.FromArgb(165,27,48,77)),
            BorderBrush=new SolidColorBrush(Color.FromArgb(85,109,159,207)),
            BorderThickness=new Thickness(1),
            Margin=new Thickness(5,0,5,5),
            MinHeight=21,
            Child=networkSpeedLabelV372
        });
        // Running Apps is hover-only;
'@ 'network status inside Deck only'
Finish-Patch $deck
$source='src/DeskSpace/DeskSpaceDeckNetworkSpeedV372.cs'
$destination='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckNetworkSpeedV372.cs'
if(!(Test-Path $source)){throw 'R372 source meter missing'}
Copy-Item -LiteralPath $source -Destination $destination -Force
if((Get-FileHash $source -Algorithm SHA256).Hash -ne
   (Get-FileHash $destination -Algorithm SHA256).Hash){
    throw 'R372 source and shipping network meter checksums differ'
}
$w=Get-Content $widget -Raw
$d=Get-Content $deck -Raw
$n=Get-Content $destination -Raw
if($w.Contains('stopwatchReturnRingV371') -or
   $w.Contains('OpenStopwatchFromCircleV371(fromRing:true)') -or
   $w.Contains('if(returnToRing)') -or
   !$w.Contains('prefs.CenterMode=stopwatchReturnCenterV371!')){
    throw 'R372 Back reopens donut or does not restore previous compact centre'
}
foreach($t in @('RefreshNetworkSpeedV372();',
    'networkSpeedLabelV372',
    'DeskSpaceDeckNetworkSpeedV372 networkSpeedV372=new()')){
    if(!$d.Contains($t)){throw ('R372 Deck readout missing: '+$t)}
}
if($n.Contains('HttpClient') -or $n.Contains('new DispatcherTimer')){
    throw 'R372 network meter must not fetch the internet or add a new timer'
}
Write-Host 'R372 PASS: Stopwatch Back returns to compact centre; download/upload speed stays INSIDE original Deck with reused five-second timer.'

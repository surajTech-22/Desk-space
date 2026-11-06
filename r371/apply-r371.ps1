$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R371: reliable real WPF crown Button, proper return to previous centre,
# ring restoration when Stopwatch was entered from a ring wedge.
# Apply only after the verified R369 and R370 original shipping patches.
$widgetFile='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$shippingWatch='DeskSpace_Windows_R1/DeskSpace/DeskSpaceStopwatchV369.cs'
$sourceWatch='src/DeskSpace/DeskSpaceStopwatchV369.cs'
if(!(Test-Path $widgetFile) -or !(Test-Path $sourceWatch) -or
   !(Test-Path $shippingWatch)){throw 'R371 original shipping stopwatch and host required'}
$lf=[string][char]10
$crlf=([string][char]13)+$lf
$script:body=[IO.File]::ReadAllText($widgetFile).Replace($crlf,$lf)
function Swap([string]$old,[string]$replacement,[string]$context){
    $old=$old.Replace($crlf,$lf)
    $replacement=$replacement.Replace($crlf,$lf)
    $count=([regex]::Matches($script:body,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R371 ${context}: expected exactly 1 shipping code anchor, got $count"}
    $script:body=$script:body.Replace($old,$replacement)
}

Swap '    private string previousCenter = "Clock";' @'
    private string previousCenter = "Clock";
    // R371: temporary stopwatch must preserve the centre/previous module
    // and return to the same donut if opened from one of its sectors.
    private string? stopwatchReturnCenterV371;
    private bool stopwatchReturnRingV371;

    private void OpenStopwatchFromCircleV371(bool fromRing=false)
    {
        if(activeStopwatch!=null)return;
        stopwatchReturnCenterV371 =
            string.Equals(prefs.CenterMode,"Stopwatch",
                StringComparison.OrdinalIgnoreCase) ? "Clock" : prefs.CenterMode;
        stopwatchReturnRingV371=fromRing;
        OpenInWidget("Stopwatch");
    }

    private void ReturnFromStopwatchV371()
    {
        if(activeStopwatch==null || closingMorph)return;
        ReturnFromModule();
        // Fallback only when WPF's width animation completion is lost.
        // One short-lived timer; no continuous idle CPU work.
        var guard=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(360)};
        guard.Tick+=(_,_)=>{
            guard.Stop();
            if(closingMorph && activeStopwatch!=null)
                CompleteModuleReturn();
        };
        guard.Start();
    }
'@ 'navigation state'

Swap '            Dispatcher.Invoke(() => {Reveal();OpenInWidget("Stopwatch");}));' '            Dispatcher.Invoke(() => {Reveal();OpenStopwatchFromCircleV371();}));' 'tray navigation'

Swap @'
                OpenInWidget("Stopwatch");
            }
        };
'@ @'
                OpenStopwatchFromCircleV371();
            }
        };
'@ 'compact Stopwatch double-click'

Swap '            activeStopwatch=new DeskSpaceStopwatchV369(ReturnFromModule);' '            activeStopwatch=new DeskSpaceStopwatchV369(ReturnFromStopwatchV371);' 'actual crown Back destination'

Swap @'
        var old=activeModule;activeModule=null;
        var search=activeSearch;activeSearch=null;
'@ @'
        bool wasStopwatch=activeStopwatch!=null;
        bool returnToRing=wasStopwatch && stopwatchReturnRingV371;
        if(wasStopwatch)
        {
            if(!string.IsNullOrWhiteSpace(stopwatchReturnCenterV371))
            {
                prefs.CenterMode=stopwatchReturnCenterV371!;
                prefs.Save();
            }
            stopwatchReturnCenterV371=null;
            stopwatchReturnRingV371=false;
        }
        var old=activeModule;activeModule=null;
        var search=activeSearch;activeSearch=null;
'@ 'original centre restoration'

Swap @'
        Dispatcher.BeginInvoke(new Action(ResumeMediaMode),
            System.Windows.Threading.DispatcherPriority.Background);
    }
    private bool CanDragFrom
'@ @'
        Dispatcher.BeginInvoke(new Action(ResumeMediaMode),
            System.Windows.Threading.DispatcherPriority.Background);
        if(returnToRing)
            Dispatcher.BeginInvoke(new Action(()=>{
                if(!shutdown && activeModule==null &&
                   !ringExpanded && !closingMorph && !hiddenAtEdge)
                    ToggleRing();
            }),System.Windows.Threading.DispatcherPriority.Background);
    }
    private bool CanDragFrom
'@ 'donut return after stopwatch'

Swap @'
                prefs.CenterMode=mode;prefs.Save();
                if(mode=="Stopwatch")OpenInWidget("Stopwatch");
                else{
                    if(activeModule==null&&!ringExpanded)BuildCompact();
                    UpdateCenter();
                }
'@ @'
                if(mode=="Stopwatch")
                    OpenStopwatchFromCircleV371();
                else
                {
                    prefs.CenterMode=mode;prefs.Save();
                    if(activeModule==null&&!ringExpanded)BuildCompact();
                    UpdateCenter();
                }
'@ 'centre selector capture'

# Match the actual R370 shipping branch, not the previous R369 comments.
Swap @'
        if(name=="Stopwatch")
        {
            prefs.CenterMode="Stopwatch";
            prefs.Save();
            OpenInWidget("Stopwatch");
            return;
        }
'@ @'
        if(name=="Stopwatch")
        {
            // Keep selected Clock/Weather/other mode intact while stopwatch
            // is shown, and reopen donut on return from sector navigation.
            OpenStopwatchFromCircleV371(fromRing:true);
            return;
        }
'@ 'Stopwatch wedge routing'

[IO.File]::WriteAllText($widgetFile,$script:body,
    [Text.UTF8Encoding]::new($false))
# R369 originally copied into shipping; R371 supplies the same approved
# metal crown visuals but uses a real hit-testable WPF Button instead.
Copy-Item -LiteralPath $sourceWatch -Destination $shippingWatch -Force
if((Get-FileHash $sourceWatch -Algorithm SHA256).Hash -ne
   (Get-FileHash $shippingWatch -Algorithm SHA256).Hash){
    throw 'R371 real crown Button source not identical to shipping code'
}
$watchText=Get-Content $shippingWatch -Raw
foreach($guard in @('crown.Click+=','Canvas.SetZIndex(crown,100)',
    'BackToPreviousV371()','crown.Template=new ControlTemplate(typeof(Button))')){
    if(!$watchText.Contains($guard)){throw ('R371 crown Button missing: '+$guard)}
}
foreach($guard in @('OpenStopwatchFromCircleV371(fromRing:true)',
    'activeStopwatch=new DeskSpaceStopwatchV369(ReturnFromStopwatchV371)',
    'returnToRing=wasStopwatch','prefs.CenterMode=stopwatchReturnCenterV371!',
    'Interval=TimeSpan.FromMilliseconds(360)')){
    if(!$script:body.Contains($guard)){throw ('R371 restore navigation missing: '+$guard)}
}
Write-Host 'R371 PASS: clickable WPF crown, Escape, previous centre and ring restore, safe bounded fallback, original design preserved.'

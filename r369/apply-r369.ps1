$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R369 implements the approved chronograph in the original widget window.
# Existing circle, donut, media, search, updater, and saved state stay intact.
$root='DeskSpace_Windows_R1/DeskSpace'
$hostPath=Join-Path $root 'WidgetHost.cs'
$watchPath=Join-Path $root 'DeskSpaceStopwatchV369.cs'
$source='src/DeskSpace/DeskSpaceStopwatchV369.cs'
if(!(Test-Path -LiteralPath $hostPath) -or
   !(Test-Path -LiteralPath $source)){
    throw 'R369 expected original shipping widget source and stopwatch module'
}
$crlf=[string][char]13+[string][char]10
$lf=[string][char]10
$script:widgetCode=[IO.File]::ReadAllText($hostPath).Replace($crlf,$lf)
function Swap-R369([string]$old,[string]$new,[string]$label){
    $old=$old.Replace($crlf,$lf)
    $new=$new.Replace($crlf,$lf)
    $hits=([regex]::Matches($script:widgetCode,[regex]::Escape($old))).Count
    if($hits -ne 1){
        throw "R369 shipping integration must find ONE $label anchor; found $hits"
    }
    $script:widgetCode=$script:widgetCode.Replace($old,$new)
}
Swap-R369 '    private DeskSpaceModuleV10? activeModule;' @'
    private DeskSpaceModuleV10? activeModule;
    private DeskSpaceStopwatchV369? activeStopwatch;
'@ 'stopwatch view lifecycle'
Swap-R369 '{ "Clock", "Date", "Calendar", "Notes", "Reminders", "Tasks" }' '{ "Clock", "Stopwatch", "Date", "Calendar", "Notes", "Reminders", "Tasks" }' 'centre mode allowed'
Swap-R369 '        menu.Items.Add("Choose centre display", null, (_, _) => Dispatcher.Invoke(ChooseCenter));' @'
        menu.Items.Add("Choose centre display", null, (_, _) => Dispatcher.Invoke(ChooseCenter));
        menu.Items.Add("Stopwatch · chronograph", null, (_, _) =>
            Dispatcher.Invoke(() => {Reveal();OpenInWidget("Stopwatch");}));
'@ 'tray menu access'
Swap-R369 @'
            media.Dispose();
            activeModule?.Dispose(); informationOverlay?.Dispose();
'@ @'
            media.Dispose();
            activeStopwatch?.Dispose();
            activeModule?.Dispose(); informationOverlay?.Dispose();
'@ 'shutdown cleanup'
Swap-R369 @'
        else if(name=="Excel")
        {
            var area=ModuleArea();
'@ @'
        else if(name=="Stopwatch")
        {
            // R369: use existing widget module surface; never an extra app.
            var area=ModuleArea();
            Width=Math.Min(area.Width,530);
            Height=Math.Min(area.Height,574);
            activeStopwatch=new DeskSpaceStopwatchV369(ReturnFromModule);
            surface=new Viewbox{Stretch=Stretch.Uniform,
                Child=activeStopwatch.Create()};
        }
        else if(name=="Excel")
        {
            var area=ModuleArea();
'@ 'existing module surface integration'
Swap-R369 @'
        activeExcel?.Dispose();activeExcel=null;
        old?.Dispose();
'@ @'
        activeExcel?.Dispose();activeExcel=null;
        activeStopwatch?.Dispose();activeStopwatch=null;
        old?.Dispose();
'@ 'module return disposal'
Swap-R369 @'
        var circle = new Ellipse { Width = 272, Height = 272, Fill = ClockFace(),
            Stroke = DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
            StrokeThickness = 7,
            Cursor = Cursors.Hand,
            ToolTip = null };
'@ @'
        var circle = new Ellipse { Width = 272, Height = 272, Fill = ClockFace(),
            Stroke = DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
            StrokeThickness = 7,
            Cursor = Cursors.Hand,
            ToolTip = prefs.CenterMode=="Stopwatch"
                ? "Double-click to open stopwatch" : null };
'@ 'compact stopwatch tooltip'
Swap-R369 @'
        content.FontSize = 35;
        content.FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
'@ @'
        content.FontSize = prefs.CenterMode=="Stopwatch"?28:35;
        content.FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
'@ 'compact stopwatch font'
Swap-R369 @'
        content.FontSize = ringExpanded ? 18 :
            (prefs.CenterMode=="Clock" && prefs.ShowWeather &&
             DeskSpaceWeather.Latest!=null ? 29 : (prefs.CenterMode=="Notes"?30:32));
'@ @'
        content.FontSize = ringExpanded ? 18 :
            (prefs.CenterMode=="Stopwatch"?27:
             prefs.CenterMode=="Clock" && prefs.ShowWeather &&
             DeskSpaceWeather.Latest!=null ? 29 : (prefs.CenterMode=="Notes"?30:32));
'@ 'stopwatch centre font'
Swap-R369 @'
            "Tasks" => workspace.Reminders.Count(x=>!x.Done)+" pending\ntasks",
            _=> ClockWithWeather()
'@ @'
            "Tasks" => workspace.Reminders.Count(x=>!x.Done)+" pending\ntasks",
            "Stopwatch" => DeskSpaceStopwatchV369.CompactElapsed,
            _=> ClockWithWeather()
'@ 'compact stopwatch elapsed'
Swap-R369 '        foreach (string mode in new[]{"Clock","Date","Calendar","Notes","Reminders"})' '        foreach (string mode in new[]{"Clock","Stopwatch","Date","Calendar","Notes","Reminders"})' 'centre menu'
Swap-R369 '            item.Click+=(_,_)=>{prefs.CenterMode=mode;prefs.Save();UpdateCenter();};' @'
            item.Click+=(_,_)=>{
                prefs.CenterMode=mode;prefs.Save();
                if(mode=="Stopwatch")OpenInWidget("Stopwatch");
                else{
                    if(activeModule==null&&!ringExpanded)BuildCompact();
                    UpdateCenter();
                }
            };
'@ 'chooser action'
Swap-R369 '        PreviewKeyDown+=KeyboardDonutKeyV207;' @'
        PreviewKeyDown+=KeyboardDonutKeyV207;
        MouseDoubleClick+=(_,e)=>{
            if(activeModule==null && !ringExpanded &&
               prefs.CenterMode=="Stopwatch" && e.ChangedButton==MouseButton.Left)
            {
                e.Handled=true;
                OpenInWidget("Stopwatch");
            }
        };
'@ 'compact circle double-click'
[IO.File]::WriteAllText($hostPath,$script:widgetCode,
    [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $source -Destination $watchPath -Force
$sourceSha=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$shippedSha=(Get-FileHash -LiteralPath $watchPath -Algorithm SHA256).Hash
if($sourceSha -ne $shippedSha){throw 'R369 shipping visual/behavior source digest mismatch'}
foreach($guard in @('DeskSpaceStopwatchV369(ReturnFromModule)',
                  'activeStopwatch?.Dispose();',
                  'DeskSpaceStopwatchV369.CompactElapsed',
                  'menu.Items.Add("Stopwatch · chronograph"')){
    if(!$script:widgetCode.Contains($guard)){
        throw ('R369 stopwatch integration guard missing: '+$guard)
    }
}
Write-Host 'R369 PASS: approved circular chronograph, original widget integration and real control code installed in shipping source'

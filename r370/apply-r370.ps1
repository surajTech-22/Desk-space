$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R370: the user rejected the enlarged stopwatch panel. Keep the stopwatch
# inside the SAME expanded 408-DIP DeskSpace circle as the original donut,
# merge the redundant Clock/Weather sectors and reuse Weather's slot.
$host='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
if(!(Test-Path -LiteralPath $host)){throw 'R370 shipping WidgetHost missing'}
$lf=[string][char]10
$crlf=[string][char]13+$lf
$script:body=[IO.File]::ReadAllText($host).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
    $old=$old.Replace($crlf,$lf)
    $new=$new.Replace($crlf,$lf)
    $count=([regex]::Matches($script:body,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R370 $why needs exactly one anchor, found $count"}
    $script:body=$script:body.Replace($old,$new)
}
Swap @'
        "Search", "Calendar", "Calculator", "Clock", "Weather", "Settings",
'@ @'
        "Search", "Calendar", "Calculator", "Clock & Weather", "Stopwatch", "Settings",
'@ 'merged clock and weather, stopwatch replaces sector four'

Swap @'
        "⌕", "▦", "⊞", "◷", "☀", "⚙",
'@ @'
        "⌕", "▦", "⊞", "◷", "⏱", "⚙",
'@ 'inner stopwatch icon'

Swap @'
        else if(name=="Stopwatch")
        {
            // R369: the chronograph uses the existing circular module surface.
            // No extra desktop window, tray, monitor identity or update worker.
            var area=ModuleArea();
            Width=Math.Min(area.Width,530);
            Height=Math.Min(area.Height,574);
            activeStopwatch=new DeskSpaceStopwatchV369(ReturnFromModule);
            surface=new Viewbox{Stretch=Stretch.Uniform,
                Child=activeStopwatch.Create()};
        }
'@ @'
        else if(name=="Stopwatch")
        {
            // R370: Stopwatch lives in the ORIGINAL expanded DeskSpace
            // circle, never the earlier 530x574 separate-looking panel.
            // Keep all real WPF buttons uniformly scaled inside the circle.
            var area=ModuleArea();
            double diameter=Math.Min(OpenDiameter,
                Math.Min(area.Width,area.Height));
            Width=Height=diameter;
            activeStopwatch=new DeskSpaceStopwatchV369(ReturnFromModule);
            surface=new Viewbox{
                Stretch=Stretch.Uniform,
                StretchDirection=StretchDirection.DownOnly,
                ClipToBounds=true,
                Child=activeStopwatch.Create()
            };
        }
'@ 'circular stopwatch dimensions and surface'

Swap @'
                Text=SegmentIcons[i]+"\n"+Segments[i],FontSize=outer?12:11,
'@ @'
                Text=SegmentIcons[i]+"\n"+
                    (Segments[i]=="Clock & Weather"?"Clock\nWeather":Segments[i]),
                FontSize=outer?12:11,
'@ 'merged parent-clock wedge text'

Swap @'
        if(name is "Clock" or "Weather")
        {
            prefs.CenterMode=name=="Tasks"?"Tasks":name=="Date"?"Date":"Clock";
            prefs.Save();CollapseRing();BuildCompact();UpdateCenter();
            return;
        }
'@ @'
        if(name is "Clock" or "Weather" or "Clock & Weather")
        {
            prefs.CenterMode="Clock";
            prefs.Save();CollapseRing();BuildCompact();UpdateCenter();
            return;
        }
        if(name=="Stopwatch")
        {
            prefs.CenterMode="Stopwatch";
            prefs.Save();
            OpenInWidget("Stopwatch");
            return;
        }
'@ 'clock-weather and stopwatch click routing'

[IO.File]::WriteAllText($host,$script:body,[Text.UTF8Encoding]::new($false))
$check=[IO.File]::ReadAllText($host)
foreach($token in @(
    '"Clock & Weather", "Stopwatch", "Settings"',
    'Segments[i]=="Clock & Weather"',
    'Width=Height=diameter;',
    'Math.Min(OpenDiameter,',
    'StretchDirection=StretchDirection.DownOnly',
    'name=="Stopwatch"',
    'prefs.CenterMode="Stopwatch"',
    'DeskSpaceStopwatchV369(ReturnFromModule)',
    'DeskSpaceStopwatchV369.CompactElapsed',
    'activeStopwatch?.Dispose();'))
{
    if(!$check.Contains($token)){throw ('R370 shipping guard missing: '+$token)}
}
if($check.Contains('Width=Math.Min(area.Width,530)') -or
   $check.Contains('Height=Math.Min(area.Height,574)')){
    throw 'R370 oversized stopwatch panel is still reachable'
}
Write-Host 'R370 PASS: combined clock/weather wedge, actual stopwatch controls on original expanded circle, no enlarged stopwatch panel.'

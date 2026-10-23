$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$info=Get-Content "$root/DeskSpaceInformationV36.cs" -Raw
$wheel=Get-Content "$root/DeskSpaceMiddleScrollV63.cs" -Raw
$weather=Get-Content "$root/DeskSpaceWeatherContrastV64.cs" -Raw
$host=Get-Content "$root/WidgetHost.cs" -Raw
$ui=Get-Content 'r36/UiSmoke/Program.cs' -Raw
foreach($t in @('private bool transientPreview;',
    'private void CloseTransientPreview()',
    'transientPreview=true;',
    'transientPreview=false; // an explicit selection persists',
    'if(shelf.IsOpen)CloseShelf();',
    'CloseTransientPreview();',
    'int overItem=SlotAtPoint(point);',
    'if(overItem>=0)',
    'HoverChild(overItem,arcHitZones[overItem].id);')){
 if(!$info.Contains($t)){throw "R64 Information preview/wheel fix missing: $t"}
}
$from=$info.IndexOf('    private void HoverChild(')
$to=$info.IndexOf('    private void EndChildHover(',[Math]::Max(0,$from))
if($from -lt 0 -or $to -le $from){throw 'Information HoverChild not found'}
$hover=$info.Substring($from,$to-$from)
if(!$hover.Contains('OpenCollection(id.Value)')){
 throw 'R64 hover must display a live Information preview'
}
if($hover.Contains('newHoverTimer.Start()') -or
   $hover.Contains('CommitChildSelection()')){
 throw 'R64 Information hover must never commit itself'
}
foreach($t in @('surface.PreviewMouseWheel+=OnWheel;',
    'InputManager.Current.PreProcessInput+=OnPreProcessWheel;',
    'wheel.RoutedEvent==Mouse.PreviewMouseWheelEvent',
    'DeskSpaceWheelHoverV35.MouseIn(surface)',
    'surface.InputHitTest(at)!=null',
    'normalWheel.Step(e.Delta)',
    'e.Handled=true;')){
 if(!$wheel.Contains($t)){throw "R64 child wedge-wheel routing missing: $t"}
}
foreach($t in @('LightScene(WeatherReading? weather,bool showWeather)',
    'code==3||code==45||code==48',
    'void Apply(DependencyObject? element',
    'foreach(Inline item in text.Inlines)',
    'text.Foreground=ink;',
    'internal static double ContrastRatio(')){
 if(!$weather.Contains($t)){throw "R64 weather contrast missing: $t"}
}
if($host.Contains('weather.City.Equals(prefs.WeatherCity')){
 throw 'R64 weather text must not use city-name equality to select contrast'
}
foreach($t in @('DeskSpaceWeatherContrastV64.Apply(content,weather,prefs.ShowWeather)',
    'private void ApplyWeatherContrast()','ApplyWeatherContrast();')){
 if(!$host.Contains($t)){throw "R64 clock contrast runtime missing: $t"}
}
foreach($t in @('TestInformationSelectionPreviewGate()',
    'R64 WPF PASS: immediate Information hover preview',
    'TestWeatherClockContrast()','R64 WPF PASS: bright/day')){
 if(!$ui.Contains($t)){throw "R64 Windows behavioral checks missing: $t"}
}
Write-Host 'R64 PASS: transient hover previews restored, mouse-wheel routed over actual wedges, scene-backed weather clock ink re-evaluated.'

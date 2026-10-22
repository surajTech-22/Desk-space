$ErrorActionPreference='Stop'
./r38/apply-r38.ps1
$root='DeskSpace_Windows_R1/DeskSpace'
$code=[IO.File]::ReadAllText("$root/DeskSpaceWeatherSceneV11.cs")
foreach($anchor in @(
    'Tag="CloudRibbon"',
    'Tag="CloudPuff"',
    'Tag="CloudAttachedRain"',
    'Tag="CloudAttachedSnow"',
    'Clip=new EllipseGeometry(new Point(diameter/2,diameter/2)',
    'private void AddCloudRain(',
    'private void AddCloudSnow(',
    'RainEmitterCount++',
    'SnowEmitterCount++',
    'var ribbon=new Canvas',
    'ribbon.RenderTransform=glide;',
    'new Rect(left,unit*cloudBase,width,limit-unit*cloudBase)',
    'LinearLoop(0,d,seconds)',
    'AutoReverse=false,RepeatBehavior=RepeatBehavior.Forever',
    'bool rain=(code>=51 && code<=67)',
    'if(rain)',
    'else if(snow)',
    'if(!day)DrawMoonAndStars(',
    'public void StopMotion()'
)){if(!$code.Contains($anchor)){throw "R38 cloud precipitation regression: $anchor"}}
foreach($legacy in @('Draw(drops,d*.10,d*.24)','Flow(-d*.65,d*.64,3.8)',
    'new Canvas{Width=d*.88,Height=d*.61','rainMovement.BeginAnimation')){
    if($code.Contains($legacy)){throw "R38 obsolete independent rain layer: $legacy"}
}
foreach($original in @('DeskSpaceVaultV10.cs','DeskSpaceExcelV16.cs',
   'DeskSpaceInformationV36.cs','DeskSpaceUpdater.cs')){
    if(!(Test-Path "$root/$original")){throw "R38 preserved module missing: $original"}
}
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($label in @('"rain-night"','"thunder"','"drizzle"','"snow"',
    '"CloudAttachedRain"','"CloudAttachedSnow"',
    'Precipitation is not owned by a visible cloud')){
    if(!$smoke.Contains($label)){throw "R38 missing real WPF smoke test: $label"}
}
Write-Host 'R38 source, condition model, cloud-owned emitters, Excel/vault/updater preservation gates PASS'


# The Excel workflow was already implemented in R16, but the ring currently
# has exactly 12 occupied slots. Protect its existing tray, in-widget and
# main-app entrypoints instead of silently replacing another module.
$hostSource=[IO.File]::ReadAllText("$root/WidgetHost.cs")
foreach($key in @(
   'private static readonly string[] Segments',
   '"Information", "Notes", "Reminders", "Clipboard", "Files", "Tasks"',
   'private DeskSpaceExcelV16? activeExcel;',
   'new DeskSpaceExcelV16(ReturnFromModule)',
   'OpenInWidget("Excel")'
)){
   if(!$hostSource.Contains($key)){throw "R38 existing Excel route not preserved: $key"}
}
Write-Host 'R38 existing Excel routes and 12 original donut entries preserved'

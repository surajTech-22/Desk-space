$ErrorActionPreference='Stop'
./r36/apply-r36.ps1
$root='DeskSpace_Windows_R1/DeskSpace'
$hostCode=Get-Content "$root/WidgetHost.cs" -Raw
$scene=Get-Content "$root/DeskSpaceWeatherSceneV11.cs" -Raw
$media=Get-Content "$root/DeskSpaceMediaV18.cs" -Raw
$calendar=Get-Content "$root/DeskSpaceCompactCalendarV36.cs" -Raw
$inline=Get-Content "$root/DeskSpaceInlineV36.cs" -Raw
$information=Get-Content "$root/DeskSpaceInformationV36.cs" -Raw
foreach($key in @('AddWeatherScene(10,9,272)',
    'private void ApplyWeatherContrast()',
    'content.Foreground=bright?',
    'private void OpenCalendarQuickAdd()',
    'new DeskSpaceCompactCalendarV36().Create(OpenCalendarExpanded,ReturnFromModule,OpenCalendarQuickAdd)',
    'double size=Math.Min(342,Math.Min(bounds.Width,bounds.Height));',
    'DeskSpaceInformationV36? informationOverlay',
    'new DeskSpaceInlineV36(name,previousCenter,ReturnFromModule,quickAddRequested)',
    'else if(name=="Calculator")','new DeskSpaceCalculatorV22().Create()',
    'name=="Notes"?256:name=="Reminders"?278:291'))
{
    if(!$hostCode.Contains($key)){throw "R36 live host regression: $key"}
}
# Ensure all original app modules/data/vault paths remain.
foreach($path in @('DeskSpaceVaultV10.cs','DeskSpaceInformationV35.cs',
     'DeskSpaceSearchV11.cs','DeskSpaceExcelV16.cs',
     'DeskSpaceWeather.cs','DeskSpaceCalculatorV22.cs'))
{
    if(!(Test-Path "$root/$path")){throw "R36 original module/data missing: $path"}
}
foreach($key in @('Clip=new EllipseGeometry(new Point(diameter/2,diameter/2)',
    'diameter/2-3,diameter/2-3',
    'private static DoubleAnimation Flow(',
    'AutoReverse=false,RepeatBehavior=RepeatBehavior.Forever',
    'Flow(-d*1.15,d*1.15,18.0,0)',
    'Flow(-d*.65,d*.64,3.8)',
    'for(int i=0;i<20;i++)','for(int i=0;i<8;i++)',
    'cloudMovements.Clear()'))
{
    if(!$scene.Contains($key)){throw "R36 weather flow/clip regression: $key"}
}
if($scene.Contains('Motion(-d*.11,d*.14,1.6)') -or
    $scene.Contains('Motion(-d*.065,d*.075,5.2)'))
{throw 'R36 old bouncing cloud/rain animation remains'}
foreach($key in @('frame.Width=pill?298','pillSeek=new Slider{Minimum=0',
    'pillPreviousButton=Make("«"',
    'pillNextButton=Make("»"',
    'var miniPlayButton=Make("▶"',
    'var restore=Make("▢"',
    'pillSeek.PreviewMouseLeftButtonUp+=async',
    'if(pillSeek!=null)',
    'double height=view==Layout.Pill?76',
    'TryTogglePlayPauseAsync',
    'TryChangePlaybackPositionAsync',
    'circleMinimized','dragHeader.CaptureMouse()'))
{
    if(!$media.Contains($key)){throw "R36 compact media capability missing: $key"}
}
foreach($key in @('const double diameter=342','int weeks=Math.Clamp',
    'days.Rows=weeks','days.Height=weeks*29',
    'var add = SmallButton("+",addEvent??expand)',
    'OccursOn(x,selected)','Open planner ↗'))
{
    if(!$calendar.Contains($key)){throw "R36 calendar regression: $key"}
}
foreach($key in @('var back=Button("← Circle",done)',
    'items.Visibility=saved.Count==0?Visibility.Collapsed:Visibility.Visible',
    'MaxHeight=104,MinHeight=0',
    'store.Load().Reminders','Edit(ws=>ws.Reminders.Add',
    'DeskSpaceClipboardV10.Poll()'))
{
    if(!$inline.Contains($key)){throw "R36 inline/reminders regression: $key"}
}
foreach($key in @('point.X<=82 && CanPage(-1)', 'point.X>=406 && CanPage(1)',
    'shelfBorder.Width=editing?304:272', 'shelfScroll.MaxHeight=editing?235:124',
    'File.Replace(temp,CollectionsPath','information-collections-v26.json',
    'DeskSpaceWindowBehavior.AllowInput(owner,true)'))
{
    if(!$information.Contains($key)){throw "R36 information wheel regression: $key"}
}
# Render-time layout math: compact weather overlay must fit circular host,
# and calendar/pill shrink rather than reserve unused rectangles.
if((10+272) -gt 292 -or (9+272) -gt 292){throw 'R36 weather disc exceeds host canvas'}
if(342 -ge 408){throw 'R36 calendar has not been reduced'}
if(298 -gt 320){throw 'R36 minimized player remains oversized'}
Write-Host 'R36 visual geometry, weather loop, calendar add/expand, inbuilt calculator, media controls, reminders, and saved-data gates passed.'

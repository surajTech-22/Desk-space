$ErrorActionPreference='Stop'
./r37/apply-r37.ps1
$root='DeskSpace_Windows_R1/DeskSpace'
$media=[IO.File]::ReadAllText("$root/DeskSpaceMediaV18.cs")
$tip='Mute or unmute system sound'
$occurrences=([regex]::Matches($media,[regex]::Escape($tip))).Count
if($occurrences -lt 3){throw "R37 mute missing from one of the three media layouts (found $occurrences)"}
foreach($anchor in @(
    'transport.Children.Add(Make("Mute"',
    'var pillMute=Make("Mute"',
    'DockPanel.SetDock(pillMute,Dock.Right);pillRow.Children.Add(pillMute);',
    'volume.Children.Add(Make("Mute"',
    'Volume(0xAD)',
    'frame.Height=pill?76',
    'frame.Width=pill?298',
    'pillSeek=new Slider{Minimum=0',
    'minimizedButton=Make("−"',
    'TryTogglePlayPauseAsync',
    'pillHeader.MouseLeftButtonDown+=DragDown'
)){if(!$media.Contains($anchor)){throw "R37 media regression or missing mute: $anchor"}}
if(!(Test-Path "$root/DeskSpaceVaultV10.cs")){throw 'Encrypted passwords source not preserved'}
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
if(!$smoke.Contains('Assert(muteButtons.Length>=3,')){throw 'R37 actual WPF media mute smoke gate missing'}
Write-Host 'R37 media mute checks passed. Windows compile and real WPF screenshot gates follow.'

$calendar=[IO.File]::ReadAllText("$root/DeskSpaceCompactCalendarV36.cs")
foreach($key in @(
    'Width = 240',
    'new GridLength(32)',
    'new GridLength(30)',
    'Selected=new DateTime(',
    'days.Height=weeks*21',
    'Width=29,Height=19',
    'Back to DeskSpace'
)){
    # The correct casing and code are checked below for source-backed UI.
    if($key -eq 'Selected=new DateTime(' -or $key -eq 'Back to DeskSpace'){continue}
    if(!$calendar.Contains($key)){throw "R37 calendar regression: $key"}
}
foreach($key in @(
    'selected=new DateTime(month.Year,month.Month,',
    'var exit = SmallButton("←",close);',
    'exit.ToolTip="Return to DeskSpace circle"',
    'var expandButton = SmallButton("↗",expand)',
    'int weeks=Math.Clamp',
    'OccursOn(x,selected)'
)){if(!$calendar.Contains($key)){throw "R37 calendar navigation/return missing: $key"}}
$sky=[IO.File]::ReadAllText("$root/DeskSpaceWeatherSceneV11.cs")
foreach($key in @(
    'Clip=new EllipseGeometry(new Point(diameter/2,diameter/2)',
    'for(int k=0;k<3;k++)',
    'Flow(0,d,15.5+cloudMovements.Count*4.2)',
    'CloudCore(d,d*.55,d*.53,.92)',
    'CloudCore(d,d*.53,d*.24,.72,true)',
    'Draw(Disc(diameter*.93,',
    'AutoReverse=false,RepeatBehavior=RepeatBehavior.Forever'
)){if(!$sky.Contains($key)){throw "R37 live sky regression: $key"}}
if($sky.Contains('movement.BeginAnimation(TranslateTransform.XProperty,'+[char]10+'            Flow(-d*1.15')){
    throw 'R37 old empty-start cloud animation is still running'
}
$hostSource=[IO.File]::ReadAllText("$root/WidgetHost.cs")
foreach($key in @(
    'private void LaunchWindowsCalculator()',
    'FileName="calc.exe",UseShellExecute=true',
    'Content="← Circle"',
    'new Action(LaunchWindowsCalculator)',
    'back.Click+=(_,_)=>ReturnFromModule()'
)){if(!$hostSource.Contains($key)){throw "R37 Windows calculator route missing: $key"}}
if($hostSource.Contains('Child=new DeskSpaceCalculatorV22().Create()')){
    throw 'R37 calculator incorrectly launches the internal DeskSpace arithmetic UI'
}
Write-Host 'R37 calendar navigation and circle geometry, Windows Calculator, seamless realistic sky and mute source gates passed.'

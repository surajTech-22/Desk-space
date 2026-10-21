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

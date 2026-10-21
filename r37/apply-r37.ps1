$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
# R36 has already assembled the existing Windows media player. Replace only
# that media source; do not touch the vault, workspaces or other modules.
if(!(Test-Path "$root/DeskSpaceMediaV18.cs")){throw 'Existing media source missing'}
Copy-Item 'r37/DeskSpaceMediaV37.cs' "$root/DeskSpaceMediaV18.cs" -Force

# Exercise the actual WPF controls in the existing Windows UI smoke test.
$smokePath='r36/UiSmoke/Program.cs'
$smoke=[IO.File]::ReadAllText($smokePath)
$anchor='            var buttons=Walk(frame).OfType<Button>().Select(b=>b.ToolTip?.ToString())'
if(!$smoke.Contains($anchor)){throw 'WPF smoke insertion anchor missing'}
$extra=@'
            var muteButtons=Walk(frame).OfType<Button>()
                .Where(b=>(b.ToolTip?.ToString()??"")=="Mute or unmute system sound")
                .ToArray();
            Assert(muteButtons.Length>=3,
                "System mute must be accessible in compact, minimized and advanced media views");
'@
$smoke=$smoke.Replace($anchor,$extra+$anchor)
[IO.File]::WriteAllText($smokePath,$smoke,[Text.UTF8Encoding]::new($false))
Write-Host 'R37: restore existing Windows system mute on compact transport and minimized media pill; WPF smoke assertion installed.'

# R37 compact calendar: keep every control inside the circular mask, and
# maintain month/selected-day consistency when paging.
Copy-Item 'r37/DeskSpaceCompactCalendarV37.cs' "$root/DeskSpaceCompactCalendarV36.cs" -Force

# R37 Option A: fully offline seamless animated sky in the live weather disc.
Copy-Item 'r37/DeskSpaceWeatherSceneV37.cs' "$root/DeskSpaceWeatherSceneV11.cs" -Force

# Replace the R36 in-app calculator branch with the actual built-in Windows
# Calculator, retaining an obvious "Circle" return button on DeskSpace.
$hostPath="$root/WidgetHost.cs"
$hostSource=[IO.File]::ReadAllText($hostPath).Replace([string][char]13+[char]10,[string][char]10)
$oldCalculator=@'
        else if(name=="Calculator")
        {
            var space=ModuleArea();
            Width=Math.Min(space.Width,324);
            Height=Math.Min(space.Height,395);
            surface=new Border{
                Background=new SolidColorBrush(Color.FromRgb(248,249,253)),
                CornerRadius=new CornerRadius(19),
                ClipToBounds=true,
                Child=new DeskSpaceCalculatorV22().Create()
            };
        }
'@
$newCalculator=@'
        else if(name=="Calculator")
        {
            // The Windows-native Calculator is the requested application,
            // not DeskSpace's legacy internal arithmetic keypad.
            var space=ModuleArea();
            Width=Math.Min(space.Width,282);
            Height=Math.Min(space.Height,132);
            var controls=new StackPanel{Margin=new Thickness(12)};
            controls.Children.Add(new TextBlock{
                Text="Windows Calculator",FontSize=17,
                Foreground=new SolidColorBrush(Color.FromRgb(34,43,75)),
                FontWeight=FontWeights.SemiBold,
                Margin=new Thickness(0,0,0,10)});
            var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};
            var back=new Button{Content="← Circle",Padding=new Thickness(9,6,9,6),
                Margin=new Thickness(2),MinWidth=94};
            back.Click+=(_,_)=>ReturnFromModule();
            var open=new Button{Content="Open Calculator ↗",
                Padding=new Thickness(9,6,9,6),Margin=new Thickness(2),MinWidth=133};
            open.Click+=(_,_)=>LaunchWindowsCalculator();
            buttons.Children.Add(back);buttons.Children.Add(open);
            controls.Children.Add(buttons);
            surface=new Border{
                Background=new SolidColorBrush(Color.FromRgb(248,249,253)),
                CornerRadius=new CornerRadius(19),ClipToBounds=true,
                Child=controls
            };
            // Launch the genuine Microsoft/Windows built-in app immediately,
            // while leaving the DeskSpace Circle back control available.
            Dispatcher.BeginInvoke(new Action(LaunchWindowsCalculator),
                System.Windows.Threading.DispatcherPriority.Background);
        }
'@
$oldCalculator=$oldCalculator.Replace([string][char]13+[char]10,[string][char]10)
$newCalculator=$newCalculator.Replace([string][char]13+[char]10,[string][char]10)
if(!$hostSource.Contains($oldCalculator)){throw 'R37 expected R36 calculator branch missing'}
$hostSource=$hostSource.Replace($oldCalculator,$newCalculator)
$launcher=@'
    private void LaunchWindowsCalculator()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{
                FileName="calc.exe",UseShellExecute=true});
        }
        catch(Exception ex)
        {
            MessageBox.Show("Windows Calculator could not be opened: "+ex.Message,
                "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }

'@
$launcher=$launcher.Replace([string][char]13+[char]10,[string][char]10)
$methodAnchor='    private void BuildCompact()'
if(!$hostSource.Contains($methodAnchor)){throw 'R37 calculator helper anchor missing'}
$hostSource=$hostSource.Replace($methodAnchor,$launcher+$methodAnchor)
[IO.File]::WriteAllText($hostPath,$hostSource,[Text.UTF8Encoding]::new($false))
Write-Host 'R37: original Windows Calculator launch + circle back; circular-safe calendar and seamless offline sky.' 

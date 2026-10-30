$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r199/DeskSpaceFeatureHubV199.cs' -Destination "$root/DeskSpaceFeatureHubV199.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$p="$root/MainWindow.xaml.cs"
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='InitializeComponent();'
$count=([regex]::Matches($s,[regex]::Escape($old))).Count
if($count -ne 1){throw ("R199 Workspace initialization expected 1 anchor, got "+$count)}
$s=$s.Replace($old,$old+$lf+'        InitializeFeatureHubV199();')
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))

# Search is already opened directly from the donut. Add an obvious action
# icon IN that existing bar; never require widget keyboard focus or tray.
$p="$root/DeskSpaceSearchV11.cs"
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='        Grid.SetColumn(field,1);top.Children.Add(field);'
$new=@'
        Grid.SetColumn(field,1);top.Children.Add(field);
        // Visible launcher on the existing donut Search bar.
        // Window.GetWindow selects THIS monitor's originating widget.
        var allActions=new Button{
            Content="✦",FontSize=20,Padding=new Thickness(0),
            ToolTip="Open DeskSpace Features and Action Palette",
            MinWidth=34,MinHeight=34,Background=Brushes.Transparent,
            BorderThickness=new Thickness(0),
            Foreground=new SolidColorBrush(Color.FromRgb(104,80,185))
        };
        System.Windows.Automation.AutomationProperties.SetName(
            allActions,"Open DeskSpace Action Palette");
        allActions.Click+=(_,e)=>{
            var owner=Window.GetWindow(frame);
            if(owner!=null)DeskSpaceActionPaletteV197.Show(owner,false);
            e.Handled=true;
        };
        Grid.SetColumn(allActions,0);top.Children.Add(allActions);
'@.Replace($crlf,$lf).TrimEnd()
$count=([regex]::Matches($s,[regex]::Escape($old))).Count
if($count -ne 1){throw ("R199 Search input expected 1 anchor, got "+$count)}
$s=$s.Replace($old,$new)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R199: visible Features tab in Workspace; visible Action launcher within donut Search; Ctrl+K in Workspace.'

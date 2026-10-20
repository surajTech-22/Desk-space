$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = 'DeskSpace_Windows_R1/DeskSpace'
function Edit([string]$file,[string]$before,[string]$after) {
    if (!(Test-Path -LiteralPath $file)) { throw "R8 source missing $file" }
    $text = [IO.File]::ReadAllText($file)
    if (!$text.Contains($before)) { throw "R8 missing patch anchor: $file -- $before" }
    $text = $text.Replace($before,$after)
    [IO.File]::WriteAllText($file,$text,[Text.UTF8Encoding]::new($false))
}
Copy-Item -LiteralPath 'r8/DeskSpaceWeather.cs' -Destination "$root/DeskSpaceWeather.cs" -Force
Copy-Item -LiteralPath 'r8/DeskSpaceWindowBehavior.cs' -Destination "$root/DeskSpaceWindowBehavior.cs" -Force
$w = "$root/WidgetHost.cs"
Edit $w '    public bool Expanded { get; set; }' @'
    public bool ShowWeather { get; set; } = true;
    public string WeatherCity { get; set; } = "Bengaluru";
    public bool Expanded { get; set; }
'@
Edit $w '    private readonly TextBlock content = new()' @'
    private Ellipse? clockFace;
    private readonly TextBlock content = new()
'@
Edit $w '        Topmost = true; WindowStartupLocation = WindowStartupLocation.Manual;' @'
        Topmost = true; ShowActivated = false; WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += (_, _) => DeskSpaceWindowBehavior.Apply(this);
        MouseLeave += (_, _) =>
        {
            // Moving away without holding must close the opened donut.
            // With a button held, the existing outer-boundary gesture takes
            // precedence and moves the widget without choosing any module.
            if (ringExpanded && !ringGesture && !draggingGesture &&
                Mouse.LeftButton != MouseButtonState.Pressed) CollapseRing();
        };
'@
Edit $w '        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application,' @'
        tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(
            Environment.ProcessPath ?? "") ?? System.Drawing.SystemIcons.Application,
'@
Edit $w '        menu.Items.Add("Open DeskSpace", null, (_, _) => Dispatcher.Invoke(OpenMain));' @'
        menu.Items.Add("About / Version", null, (_, _) => Dispatcher.Invoke(ShowAbout));
        menu.Items.Add("Open DeskSpace", null, (_, _) => Dispatcher.Invoke(OpenMain));
'@
Edit $w '        Closing += (_, _) => { shutdown = true;' @'
        Loaded += (_, _) =>
        {
            DeskSpaceWeather.Updated += OnWeatherUpdated;
            if (prefs.ShowWeather) _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
        };
        Closing += (_, _) => { DeskSpaceWeather.Updated -= OnWeatherUpdated; shutdown = true;
'@
# Only the ring selection gesture is allowed to activate a sector. Invisible
# wedge buttons caused unrequested Clock detail windows and keyboard focus theft.
Edit $w '            int index=i;' '            // Display-only ring segment. Mouse release decides the selection.'
$startMarker = '            var btn = new Button { Width=59,Height=33,Opacity=0,Cursor=Cursors.Hand,Content=Segments[i] };'
$lastMarker = '            btn.Click += (_,_)=> ClickSegment(Segments[index]); canvas.Children.Add(btn);'
$text=[IO.File]::ReadAllText($w)
$a=$text.IndexOf($startMarker,[StringComparison]::Ordinal)
$b=$text.IndexOf($lastMarker,[StringComparison]::Ordinal)
if($a -lt 0 -or $b -lt $a){throw 'R8 invisible ring button anchors missing'}
$b += $lastMarker.Length
$text=$text.Substring(0,$a) + '            // No button overlay: merely pointing at a module cannot open it.' + $text.Substring($b)
[IO.File]::WriteAllText($w,$text,[Text.UTF8Encoding]::new($false))
# A circle is not its rectangular HWND. Leaving the round boundary inside
# the square window must collapse the ring even before MouseLeave occurs.
Edit $w '        if (Mouse.LeftButton != MouseButtonState.Pressed || draggingGesture) return;' @'
        if (ringExpanded && !ringGesture && !draggingGesture &&
            Mouse.LeftButton != MouseButtonState.Pressed)
        {
            var location = e.GetPosition(this);
            var cursorCentre = new Point(Width / 2, Height / 2);
            if ((location - cursorCentre).Length >
                Math.Min(Width, Height) * 137.0 / 292.0 + 3)
            {
                CollapseRing();
                return;
            }
        }
        if (Mouse.LeftButton != MouseButtonState.Pressed || draggingGesture) return;
'@
Edit $w '            ClickSegment(Segments[selection]);' @'
            Dispatcher.BeginInvoke(new Action(() => ClickSegment(Segments[selection])));
'@
Edit $w 'case "Notes": case "Calendar": case "Reminders": case "Clock": prefs.CenterMode=name;prefs.Save();UpdateCenter();ToggleDetails(true);CollapseRing();break;' @'
case "Notes": case "Calendar": case "Reminders": case "Clock":
                // A module selection changes live centre information only.
                // No separate window until the user explicitly chooses Expand.
                prefs.CenterMode=name;prefs.Save();UpdateCenter();CollapseRing();break;
'@
Edit $w 'var circle = new Ellipse { Width = 272, Height = 272, Fill = accent,' @'
var circle = new Ellipse { Width = 272, Height = 272, Fill = ClockFace(),
'@
Edit $w '        canvas.Children.Add(circle);' @'
        canvas.Children.Add(circle);
        clockFace = circle;
'@
Edit $w '        var centre = new Ellipse { Width=134,Height=134,Fill=accent,Stroke=Brushes.White,StrokeThickness=3 };' @'
        clockFace = null;
        var centre = new Ellipse { Width=134,Height=134,Fill=ClockFace(),Stroke=Brushes.White,StrokeThickness=3 };
'@
Edit $w '            _=> DateTime.Now.ToString("HH:mm:ss")+"\n"+DateTime.Now.ToString("ddd dd MMM")' @'
            _=> ClockWithWeather()
'@
# Explicit functions are inserted before the center-display selector; do not
# change any module or accounting data models.
$needle = '    private void ChooseCenter()'
$extra = @'
    private string ClockWithWeather()
    {
        var time=DateTime.Now.ToString("hh:mm tt");
        var value=DeskSpaceWeather.Latest;
        if (!prefs.ShowWeather || value == null ||
            !value.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase))
            return time + "\n" + DateTime.Now.ToString("ddd dd MMM");
        var degrees=Math.Round(value.Celsius).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return time + "\n" + DeskSpaceWeather.Icon(value.Code,value.IsDay) +
            " " + degrees + "°C\n" + DateTime.Now.ToString("ddd dd MMM");
    }
    private Brush ClockFace()
    {
        if (prefs.CenterMode != "Clock" || !prefs.ShowWeather) return accent;
        var value=DeskSpaceWeather.Latest;
        if (value == null || !value.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase))
            return accent;
        Color top, bottom;
        if (!value.IsDay) {top=Color.FromRgb(25,42,90);bottom=Color.FromRgb(73,61,137);}
        else if (value.Code >= 51) {top=Color.FromRgb(33,88,156);bottom=Color.FromRgb(46,61,99);}
        else if (value.Code <= 1) {top=Color.FromRgb(234,135,42);bottom=Color.FromRgb(124,77,189);}
        else {top=Color.FromRgb(49,136,209);bottom=Color.FromRgb(81,86,148);}
        return new LinearGradientBrush(top,bottom,90);
    }
    private void OnWeatherUpdated()
    {
        if (shutdown) return;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (shutdown) return;
            if (clockFace != null) clockFace.Fill = ClockFace();
            UpdateCenter();
        }));
    }
    private void ShowAbout()
    {
        MessageBox.Show("DeskSpace " + DeskSpaceUpdater.CurrentVersion +
            "\n\nLive donut widget, universal search, modules, reminders and optional weather." +
            "\nUpdates: authenticated private GitHub releases" +
            "\nLocal workspace: " + WorkspaceStore.Root,
            "About DeskSpace",MessageBoxButton.OK,MessageBoxImage.Information);
    }
'@
Edit $w $needle ($extra+$needle)
Edit $w '        if ((DateTime.Now-lastDataLoad).TotalSeconds>=4)' @'
        if (prefs.CenterMode == "Clock" && prefs.ShowWeather)
            _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
        if ((DateTime.Now-lastDataLoad).TotalSeconds>=4)
'@
Edit $w '        Show();Topmost=true;Activate();' '        Show();Topmost=true;'
Edit $w '            WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true};' @'
            WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowActivated=false};
        w.SourceInitialized += (_, _) => DeskSpaceWindowBehavior.Apply(w);
'@
Edit $w '        content.FontSize = ringExpanded ? 18 : (prefs.CenterMode == "Notes" ? 30 : 35);' @'
        content.FontSize = ringExpanded ? 18 : (prefs.CenterMode == "Notes" ? 30 : 32);
        if (clockFace != null) clockFace.Fill = ClockFace();
'@
Edit $w '    private void ApplySettings() {prefs.Save();prefs.ApplyWindowsStartup();UpdateCenter();}' @'
    private void ApplySettings()
    {
        prefs.Save();prefs.ApplyWindowsStartup();UpdateCenter();
        if (prefs.ShowWeather) _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
    }
'@
$panel = "$root/WidgetPanels.cs"
Edit $panel 'Title="DeskSpace · Donut options";Width=410;Height=390;Topmost=true;' 'Title="DeskSpace · Donut options";Width=410;Height=490;Topmost=true;'
Edit $panel '        panel.Children.Add(new TextBlock{Text="Centre display",Margin=new Thickness(0,8,0,3)});' @'
        AddToggle("Show live weather in clock",prefs.ShowWeather,v=>prefs.ShowWeather=v);
        panel.Children.Add(new TextBlock{Text="Weather city (no precise location used)",Margin=new Thickness(0,8,0,3)});
        var cities=new ComboBox{ItemsSource=DeskSpaceWeather.Cities.Select(c=>c.Name).ToArray(),SelectedItem=prefs.WeatherCity};
        cities.SelectionChanged+=(_,_)=>{if(cities.SelectedItem is string city)prefs.WeatherCity=city;};
        panel.Children.Add(cities);
        panel.Children.Add(new TextBlock{Text="Centre display",Margin=new Thickness(0,8,0,3)});
'@
$project = "$root/DeskSpace.csproj"
Edit $project '<UseWindowsForms>true</UseWindowsForms>' @'
<UseWindowsForms>true</UseWindowsForms>
    <ApplicationIcon>DeskSpace.ico</ApplicationIcon>
'@
$main = "$root/MainWindow.xaml"
Edit $main '<TextBlock Text="DeskSpace" FontSize="23" FontWeight="Bold" Foreground="White" DockPanel.Dock="Left"/>' @'
<StackPanel Orientation="Horizontal" DockPanel.Dock="Left">
          <TextBlock Text="▦  DeskSpace" FontSize="23" FontWeight="Bold" Foreground="White"/>
          <Button x:Name="VersionButton" Click="AppVersion_Click" Padding="10,2" Margin="12,0,0,0"
                  Background="#31496F" Foreground="White" BorderThickness="0" VerticalAlignment="Center"
                  ToolTip="Show installed DeskSpace version and update information"/>
        </StackPanel>
'@
$mainCode = "$root/MainWindow.xaml.cs"
Edit $mainCode '        InitializeComponent();' @'
        InitializeComponent();
        VersionButton.Content = "v" + DeskSpaceUpdater.CurrentVersion;
        Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "")!.Handle,
            Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
'@
Edit $mainCode '    private void SafeSave()' @'
    private void AppVersion_Click(object sender,RoutedEventArgs e)
    {
        MessageBox.Show("DeskSpace " + DeskSpaceUpdater.CurrentVersion +
            "\n\nAutomatic updates: Enabled via private GitHub releases" +
            "\nYour workspace data stays in " + WorkspaceStore.Root,
            "DeskSpace version information",MessageBoxButton.OK,MessageBoxImage.Information);
    }
    private void SafeSave()
'@
$updater="$root/DeskSpaceUpdater.cs"
Edit $updater '    private static readonly Version InstalledVersion = new(1, 4, 0);' @'
    private static readonly Version InstalledVersion = new(1, 4, 0);
    public static string CurrentVersion => InstalledVersion.ToString();
'@
$iss='installer/DeskSpace.iss'
Edit $iss 'AppVerName=DeskSpace R7' 'AppVerName=DeskSpace R8'
Write-Host 'R8 behavior, weather, version information and branding source applied.'

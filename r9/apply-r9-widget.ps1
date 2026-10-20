$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$text=[IO.File]::ReadAllText($path)
function Change([string]$before,[string]$after) {
    if(!$script:text.Contains($before)){throw "R9 widget anchor missing: $before"}
    $script:text=$script:text.Replace($before,$after)
}
# Do not open any module merely on hover; button overlays are removed by R8.
if($text.Contains('btn.Click += (_,_)=> ClickSegment')){throw 'R8 invisible wedge buttons remain: unsafe baseline'}
# Right click is a direct ring toggle, not a context-menu or module activation.
Change '        PreviewMouseLeftButtonDown += OnGestureDown;' @'
        PreviewMouseRightButtonUp += (_, e) =>
        {
            if (hiddenAtEdge) Reveal();
            if (ringExpanded) CollapseRing();
            else ToggleRing();
            e.Handled = true;
        };
        PreviewMouseLeftButtonDown += OnGestureDown;
'@
# Outer-boundary drag: collapse BEFORE native move starts. Never open an action.
Change @'
            ReleaseMouseCapture();
            try { DragMove(); } catch (InvalidOperationException) { }
            KeepOnVisibleDesktop();
            CollapseRing();
            SavePosition();
'@ @'
            ReleaseMouseCapture();
            CollapseRing();
            try { DragMove(); } catch (InvalidOperationException) { }
            KeepOnVisibleDesktop();
            if (IsNearScreenEdge()) DockToEdge(manual: true);
            else SavePosition();
'@
Change '    private void SavePosition()' @'
    // The tiny edge handle is independent of the circle.
    // Dock when any display boundary is reached, after mouse release.
    private bool IsNearScreenEdge()
    {
        var centre = new System.Drawing.Point((int)(Left + Width / 2), (int)(Top + Height / 2));
        var bounds = Forms.Screen.FromPoint(centre).WorkingArea;
        const double snap = 22;
        return Left <= bounds.Left + snap ||
               Top <= bounds.Top + snap ||
               Left + Width >= bounds.Right - snap ||
               Top + Height >= bounds.Bottom - snap;
    }

    private void SavePosition()
'@
# Use a correct edge on whichever display the widget was dragged to.
Change @'
        var area=SystemParameters.WorkArea;
        double leftDist=Math.Abs(Left-area.Left),rightDist=Math.Abs(area.Right-(Left+Width));
'@ @'
        var p = new System.Drawing.Point((int)(Left+Width/2), (int)(Top+Height/2));
        var monitor=Forms.Screen.FromPoint(p).WorkingArea;
        var area=new Rect(monitor.Left, monitor.Top, monitor.Width, monitor.Height);
        double leftDist=Math.Abs(Left-area.Left),rightDist=Math.Abs(area.Right-(Left+Width));
'@
# There should be no mini title-bar buttons across the donut!
$start='        var change=new Button { Content="◉"'
$finish='        hide.Click += (_,_)=>DockToEdge(manual: true);'
$a=$text.IndexOf($start,[StringComparison]::Ordinal)
$b=$text.IndexOf($finish,[StringComparison]::Ordinal)
if($a -lt 0 -or $b -le $a){throw 'R9 floating v/collapse/minimize button sequence missing'}
$b+=$finish.Length
$text=$text.Substring(0,$a) + @'
        // No mini-window controls in the ring. Right-click toggles the ring.
        // Centre selection and details remain explicit tray/setting actions.
'@ + $text.Substring($b)
Change '        menu.Items.Add("Expand / collapse ring", null, (_, _) => Dispatcher.Invoke(() => { Reveal(); ToggleRing(); }));' @'
        menu.Items.Add("Expand / collapse ring", null, (_, _) => Dispatcher.Invoke(() => { Reveal(); ToggleRing(); }));
        menu.Items.Add("Choose centre display", null, (_, _) => Dispatcher.Invoke(ChooseCenter));
        menu.Items.Add("Expand selected information", null, (_, _) => Dispatcher.Invoke(() => ToggleDetails()));
'@
# Selection icons correspond to actual modules, unlike the static illustration.
Change '    private static readonly string[] Segments = { "Notes", "Calendar", "Files", "Search", "Apps", "Settings", "Reminders", "Clock" };' @'
    private static readonly string[] Segments = { "Notes", "Calendar", "Files", "Search", "Apps", "Settings", "Reminders", "Clock" };
    private static readonly string[] SegmentIcons = { "✎", "▦", "▤", "⌕", "▣", "⚙", "◉", "◷" };
'@
Change 'Text=Segments[i],FontSize=12' 'Text=SegmentIcons[i]+" "+Segments[i],FontSize=11'
# Keep cached workspace data; don't reread JSON every four seconds.
Change '        if ((DateTime.Now-lastDataLoad).TotalSeconds>=4)' @'
        if ((DateTime.Now-lastDataLoad).TotalSeconds>=15)
'@
# Only query expensive full-screen window state every two seconds.
Change '    private DateTime lastDataLoad = DateTime.MinValue;' @'
    private DateTime lastDataLoad = DateTime.MinValue;
    private DateTime lastFullscreenPoll = DateTime.MinValue;
    private bool previousFullscreen;
    private DateTime lastWeatherPoll = DateTime.MinValue;
    private Ellipse? weatherHalo;
'@
Change '        bool isFullscreen = FullscreenDetector.IsOtherFullscreen();' @'
        if ((DateTime.UtcNow-lastFullscreenPoll).TotalSeconds>=2)
        {
            previousFullscreen=FullscreenDetector.IsOtherFullscreen();
            lastFullscreenPoll=DateTime.UtcNow;
        }
        bool isFullscreen=previousFullscreen;
'@
Change @'
        if (prefs.CenterMode == "Clock" && prefs.ShowWeather)
            _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
'@ @'
        if (prefs.CenterMode == "Clock" && prefs.ShowWeather &&
            (DateTime.UtcNow-lastWeatherPoll).TotalMinutes>=1)
        {
            lastWeatherPoll=DateTime.UtcNow;
            _ = DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
        }
'@
# Status events show in centre only during active downloading/verification.
Change '            DeskSpaceWeather.Updated += OnWeatherUpdated;' @'
            DeskSpaceWeather.Updated += OnWeatherUpdated;
            DeskSpaceUpdateProgress.Changed += OnUpdateProgress;
'@
Change '        Closing += (_, _) => { DeskSpaceWeather.Updated -= OnWeatherUpdated; shutdown = true;' @'
        Closing += (_, _) => { DeskSpaceWeather.Updated -= OnWeatherUpdated;
            DeskSpaceUpdateProgress.Changed -= OnUpdateProgress; shutdown = true;
'@
Change '    private void OnWeatherUpdated()' @'
    private void OnUpdateProgress()
    {
        if (shutdown) return;
        _ = Dispatcher.BeginInvoke(new Action(() => { if (!shutdown) UpdateCenter(); }));
    }

    private void OnWeatherUpdated()
'@
Change '        content.Text=prefs.CenterMode switch {' @'
        var progress=DeskSpaceUpdateProgress.Snapshot();
        if (progress.Active)
        {
            var message = "UPDATE\n" + progress.Label +
                (progress.Percent.HasValue ? "\n" + progress.Percent.Value + "%" : "");
            if (content.Text != message) content.Text = message;
            content.FontSize=ringExpanded?16:23;
            return;
        }
        string nextCentreText=prefs.CenterMode switch {
'@
# Replace only the end of the centre-display switch with conditional assignment.
$anchor='        string nextCentreText=prefs.CenterMode switch {'
$idx=$text.IndexOf($anchor,[StringComparison]::Ordinal)
if($idx -lt 0){throw 'R9 center switch missing'}
$tail=$text.IndexOf('        };',$idx,[StringComparison]::Ordinal)
if($tail -lt 0){throw 'R9 center switch end missing'}
$tail+=('        };').Length
$text=$text.Substring(0,$tail)+@'

        if (!string.Equals(content.Text,nextCentreText,StringComparison.Ordinal))
            content.Text=nextCentreText;
'@+$text.Substring($tail)
# Reassign animated gradient only when weather actually changes.
# Clock face halo is animated only when visible. Animation is compositor driven;
# no new thread, no busy-wait, no per-frame file IO or weather HTTP traffic.
Change '        canvas.Children.Add(circle);'+[Environment]::NewLine+'        content.FontSize' @'
        canvas.Children.Add(circle);
        AddWeatherHalo();
        content.FontSize
'@
Change '    private void BuildDonut()' @'
    private void AddWeatherHalo()
    {
        if (prefs.CenterMode != "Clock" || !prefs.ShowWeather || hiddenAtEdge) return;
        var halo = new Ellipse
        {
            Width=258, Height=258,
            Fill=Brushes.Transparent,
            Stroke=new SolidColorBrush(Color.FromArgb(165,255,255,255)),
            StrokeThickness=4,
            IsHitTestVisible=false,
            Opacity=0.4
        };
        Canvas.SetLeft(halo,17);Canvas.SetTop(halo,17);
        canvas.Children.Add(halo);weatherHalo=halo;
        var motion = new System.Windows.Media.Animation.DoubleAnimation(0.28,0.75,
            new Duration(TimeSpan.FromSeconds(9)))
        {
            AutoReverse=true,
            RepeatBehavior=System.Windows.Media.Animation.RepeatBehavior.Forever
        };
        halo.BeginAnimation(UIElement.OpacityProperty,motion);
    }
    private void SuspendWeatherMotion()
    {
        weatherHalo?.BeginAnimation(UIElement.OpacityProperty,null);
        weatherHalo=null;
    }

    private void BuildDonut()
'@
Change '        canvas.Children.Clear();'+[Environment]::NewLine+'        ringPaths.Clear();'+[Environment]::NewLine+'        highlightedSector = -1;'+[Environment]::NewLine+'        content.FontSize = 18;' @'
        SuspendWeatherMotion();
        canvas.Children.Clear();
        ringPaths.Clear();
        highlightedSector = -1;
        content.FontSize = 18;
'@
Change '        hiddenAtEdge=true;Hide();edgeHandle.Show();edgeHandle.Topmost=true;' @'
        SuspendWeatherMotion();
        hiddenAtEdge=true;Hide();edgeHandle.Show();edgeHandle.Topmost=true;
'@
Change '        Show();Topmost=true;' @'
        Show();Topmost=true;
        if (!ringExpanded) BuildCompact(); // resume paused weather animation
'@
Change '        canvas.Children.Clear();'+[Environment]::NewLine+'        ringPaths.Clear();'+[Environment]::NewLine+'        highlightedSector = -1;'+[Environment]::NewLine+'        canvas.Width = canvas.Height = 292;' @'
        SuspendWeatherMotion();
        canvas.Children.Clear();
        ringPaths.Clear();
        highlightedSector = -1;
        canvas.Width = canvas.Height = 292;
'@
# Avoid repeated weather gradient allocation for unchanged clock text each second.
Change '        if (clockFace != null) clockFace.Fill = ClockFace();'+[Environment]::NewLine+'    }' @'
    }
'@
[IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))
Write-Host 'R9 widget: right-click, edge-auto-dock, drag-close, clean ring, module icons, update progress and idle optimization.'

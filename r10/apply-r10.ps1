$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$dir='DeskSpace_Windows_R1/DeskSpace'
foreach($file in @('DeskSpaceModuleV10.cs','DeskSpaceVaultV10.cs','DeskSpaceClipboardV10.cs')){
    Copy-Item -LiteralPath "r10/$file" -Destination "$dir/$file" -Force
}
$lf=[Environment]::NewLine
function Edit([string]$path,[string]$old,[string]$replacement){
    $s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $replacement=$replacement.Replace([string][char]13+[char]10,[string][char]10)
    if(!$s.Contains($old)){throw "R10 source anchor missing: $path : $old"}
    [IO.File]::WriteAllText($path,$s.Replace($old,$replacement),[Text.UTF8Encoding]::new($false))
}
# Extra recurrence is optional and backward-compatible with saved events.
$model="$dir/Models.cs"
Edit $model '    public DateTime Date { get; set; } = DateTime.Today;' @'
    public DateTime Date { get; set; } = DateTime.Today;
    public string Recurrence { get; set; } = "None";
'@
$native="$dir/DeskSpaceWindowBehavior.cs"
Edit $native '    internal static void Apply(Window window)' @'
    private static readonly System.Collections.Generic.HashSet<IntPtr> InputWindows = new();
    internal static void AllowInput(Window window,bool enabled)
    {
        var hwnd=new WindowInteropHelper(window).Handle;
        if(hwnd==IntPtr.Zero)return;
        long style=GetLong(hwnd,GWL_EXSTYLE).ToInt64();
        if(enabled){InputWindows.Add(hwnd);style &= ~WS_EX_NOACTIVATE;}
        else{InputWindows.Remove(hwnd);style |= WS_EX_NOACTIVATE;}
        SetLong(hwnd,GWL_EXSTYLE,new IntPtr(style));
    }
    internal static void Apply(Window window)
'@
Edit $native 'if (msg == WM_MOUSEACTIVATE)' 'if (msg == WM_MOUSEACTIVATE && !InputWindows.Contains(hwnd))'
$path="$dir/WidgetHost.cs"
$s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
function Change([string]$old,[string]$new){
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:s.Contains($old)){throw "R10 WidgetHost anchor missing: $old"}
    $script:s=$script:s.Replace($old,$new)
}
function ReplaceMethod([string]$first,[string]$last,[string]$body){
    $a=$script:s.IndexOf($first,[StringComparison]::Ordinal)
    if($a -lt 0){throw "Method missing: $first"}
    $b=$script:s.IndexOf($last,$a,[StringComparison]::Ordinal)
    if($b -le $a){throw "End boundary missing: $last"}
    $script:s=$script:s.Substring(0,$a)+$body+[Environment]::NewLine+$script:s.Substring($b)
}
Change 'private const double OpenDiameter = 244;' 'private const double OpenDiameter = 408;'
Change @'
    private static readonly string[] Segments = { "Notes", "Calendar", "Files", "Search", "Apps", "Settings", "Reminders", "Clock" };
    private static readonly string[] SegmentIcons = { "✎", "▦", "▤", "⌕", "▣", "⚙", "◉", "◷" };
'@ @'
    private static readonly string[] Segments = { "Search", "Notes", "Reminders", "Calendar", "Clipboard", "Password",
        "Calculator", "Clock", "Date", "Tasks", "Weather", "Settings" };
    private static readonly string[] SegmentIcons = { "⌕", "✎", "◉", "▦", "▣", "◆", "⊞", "◷", "▤", "☑", "☀", "⚙" };
    private DeskSpaceModuleV10? activeModule;
    private string previousCenter = "Clock";
    private double restoreLeft, restoreTop;
'@
Change '    private bool CanDragFrom(MouseButtonEventArgs e)' @'
    private void OpenInWidget(string name)
    {
        if(activeModule!=null)ReturnFromModule();
        CollapseRing();
        previousCenter=prefs.CenterMode;
        restoreLeft=Left;restoreTop=Top;
        activeModule=new DeskSpaceModuleV10(prefs,previousCenter,ReturnFromModule);
        Width=602;Height=686;
        KeepOnVisibleDesktop();
        Content=activeModule.Build(name);
        DeskSpaceWindowBehavior.AllowInput(this,true);
        ShowActivated=true;
        Show();Activate();
    }
    private void ReturnFromModule()
    {
        if(activeModule==null)return;
        var old=activeModule;activeModule=null;old.Dispose();
        DeskSpaceWindowBehavior.AllowInput(this,false);
        ShowActivated=false;
        Content=new Viewbox{Child=canvas,Stretch=Stretch.Uniform};
        Width=Height=CompactDiameter;
        Left=restoreLeft;Top=restoreTop;
        KeepOnVisibleDesktop();
        BuildCompact();SavePosition();UpdateCenter();
    }

    private bool CanDragFrom(MouseButtonEventArgs e)
'@
Change '        if (!ringExpanded) return true;' @'
        if(activeModule!=null)return false;
        if (!ringExpanded) return true;
'@
Change 'Width * 69.0 / 292.0' 'Width * 69.0 / 420.0'
Change 'Math.Min(Width, Height) * 137.0 / 292.0' 'Math.Min(Width, Height) * 196.0 / 420.0'
Change '        if (e.ChangedButton != MouseButton.Left || hiddenAtEdge || !CanDragFrom(e)) return;' @'
        if (activeModule!=null || e.ChangedButton != MouseButton.Left || hiddenAtEdge || !CanDragFrom(e)) return;
'@
Change '        if (Mouse.LeftButton != MouseButtonState.Pressed || draggingGesture) return;' @'
        if(activeModule!=null || Mouse.LeftButton != MouseButtonState.Pressed || draggingGesture) return;
'@
Change '        if (e.ChangedButton != MouseButton.Left || (!holdPending && !ringGesture)) return;' @'
        if(activeModule!=null || e.ChangedButton != MouseButton.Left || (!holdPending && !ringGesture)) return;
'@
Change @'
            if (hiddenAtEdge) Reveal();
            if (ringExpanded) CollapseRing();
'@ @'
            if(activeModule!=null)return;
            if (hiddenAtEdge) Reveal();
            if (ringExpanded) CollapseRing();
'@
Change '        Closing += (_, _) => { DeskSpaceWeather.Updated -= OnWeatherUpdated;' @'
        Closing += (_,_)=>activeModule?.Dispose();
        Closing += (_, _) => { DeskSpaceWeather.Updated -= OnWeatherUpdated;
'@
# Clipboard history is bounded in memory and scanned at most every two seconds.
Change '        if ((DateTime.Now-lastDataLoad).TotalSeconds>=15)' @'
        DeskSpaceClipboardV10.Poll();
        if(activeModule!=null)return;
        if ((DateTime.Now-lastDataLoad).TotalSeconds>=15)
'@
$sector=@'
    private int SectorAt(Point p)
    {
        if(!ringExpanded || activeModule!=null)return -1;
        double scale=420.0/Width;
        double dx=(p.X-Width/2)*scale,dy=(p.Y-Height/2)*scale;
        double distance=Math.Sqrt(dx*dx+dy*dy);
        bool inner=distance>=70&&distance<=124;
        bool outer=distance>=132&&distance<=195;
        if(!inner&&!outer)return -1;
        double angle=(Math.Atan2(dy,dx)*180/Math.PI+120+720)%360;
        if(angle%60>58.7)return -1;
        int index=Math.Min((int)(angle/60),5);
        return (outer?6:0)+index;
    }
'@
ReplaceMethod '    private int SectorAt(Point p)' '    private void UpdateHighlightedSector(' $sector
$paint=@'
    private void UpdateHighlightedSector(Point p)
    {
        int selected=SectorAt(p);
        if(selected==highlightedSector)return;
        highlightedSector=selected;
        for(int i=0;i<ringPaths.Count;i++)
            ringPaths[i].Fill=i==selected?accent:(i%2==0?light:new SolidColorBrush(Color.FromRgb(227,221,246)));
    }
'@
ReplaceMethod '    private void UpdateHighlightedSector(Point position)' '    private void BuildCompact()' $paint
$donut=@'
    private void BuildDonut()
    {
        SuspendWeatherMotion();
        canvas.Children.Clear();
        ringPaths.Clear();
        highlightedSector=-1;
        canvas.Width=canvas.Height=420;
        var shadow=new Ellipse{Width=408,Height=408,Fill=new SolidColorBrush(Color.FromArgb(36,25,20,60)),IsHitTestVisible=false};
        Canvas.SetLeft(shadow,6);Canvas.SetTop(shadow,7);canvas.Children.Add(shadow);
        for(int i=0;i<12;i++)
        {
            bool outer=i>=6;int slot=i%6;
            double first=-120+slot*60,last=first+58.5;
            var wedge=new System.Windows.Shapes.Path{
                Data=Sector(210,210,outer?132:70,outer?195:124,first,last),
                Fill=i%2==0?light:new SolidColorBrush(Color.FromRgb(227,221,246)),
                Stroke=Brushes.White,StrokeThickness=2,IsHitTestVisible=false
            };
            canvas.Children.Add(wedge);ringPaths.Add(wedge);
            var angle=(-90+slot*60)*Math.PI/180;
            var label=new TextBlock{
                Text=SegmentIcons[i]+"\n"+Segments[i],FontSize=outer?13:12,
                TextAlignment=TextAlignment.Center,FontWeight=FontWeights.SemiBold,
                Foreground=new SolidColorBrush(Color.FromRgb(63,52,99)),IsHitTestVisible=false
            };
            label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
            double radius=outer?162:98;
            Canvas.SetLeft(label,210+radius*Math.Cos(angle)-label.DesiredSize.Width/2);
            Canvas.SetTop(label,210+radius*Math.Sin(angle)-label.DesiredSize.Height/2);
            canvas.Children.Add(label);
        }
        var centre=new Ellipse{Width=130,Height=130,Fill=ClockFace(),Stroke=Brushes.White,StrokeThickness=3,IsHitTestVisible=false};
        Canvas.SetLeft(centre,145);Canvas.SetTop(centre,145);canvas.Children.Add(centre);
        clockFace=centre;
        content.FontSize=20;content.Width=116;content.Height=98;
        Canvas.SetLeft(content,152);Canvas.SetTop(content,161);canvas.Children.Add(content);
        UpdateCenter();
    }
'@
ReplaceMethod '    private void BuildDonut()' '    private static Geometry Sector(' $donut
$module=@'
    private void ClickSegment(string name)
    {
        if(name is "Clock" or "Date" or "Tasks" or "Weather")
        {
            prefs.CenterMode=name=="Tasks"?"Tasks":name=="Date"?"Date":"Clock";
            prefs.Save();CollapseRing();BuildCompact();UpdateCenter();
            return;
        }
        if(name is "Search" or "Notes" or "Reminders" or "Calendar" or "Clipboard" or "Password" or "Calculator" or "Settings")
            OpenInWidget(name);
    }
'@
ReplaceMethod '    private void ClickSegment(string name)' '    private void ApplySettings()' $module
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
$iss="installer/DeskSpace.iss"
Edit $iss 'AppVerName=DeskSpace R9' 'AppVerName=DeskSpace R10'
Write-Host 'R10 transformed widgets, 12-sector two-ring donut, encrypted vault, planner and clipboard integrated.'

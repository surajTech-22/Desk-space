$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r13/DeskSpaceSearchIndexV13.cs' -Destination "$root/DeskSpaceSearchIndexV13.cs" -Force
function ReadSource([string]$path) {
    [IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
}
function SaveSource([string]$path,[string]$content) {
    [IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false))
}
function ReplaceRequired([string]$before,[string]$after) {
    $before=$before.Replace([string][char]13+[char]10,[string][char]10)
    $after=$after.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:content.Contains($before)){throw "R13 patch anchor absent: $before"}
    $script:content=$script:content.Replace($before,$after)
}
$path="$root/DeskSpaceModuleV10.cs"
$content=ReadSource $path
$a=$content.IndexOf('    internal static List<SearchHit> SearchNow(',[StringComparison]::Ordinal)
$b=$content.IndexOf('    private void BuildNotes(',[Math]::Max(0,$a),[StringComparison]::Ordinal)
if($a -lt 0 -or $b -le $a){throw 'R13 search engine replacement boundaries not found'}
$content=$content.Substring(0,$a)+@'
    internal static List<SearchHit> SearchNow(string term,Workspace ws,System.Threading.CancellationToken token)
        => DeskSpaceSearchIndexV13.Search(term,ws,token);

'@+$content.Substring($b)
SaveSource $path $content

$path="$root/DeskSpaceSearchV11.cs"
$content=ReadSource $path
ReplaceRequired '    private DispatcherTimer? debounce;' @'
    private DispatcherTimer? debounce;
    private DispatcherTimer? indexRefresh;
'@
ReplaceRequired 'var top=new Grid { Height=68,Margin=new Thickness(12,1,8,1) };' 'var top=new Grid { Height=54,Margin=new Thickness(8,1,5,1) };'
ReplaceRequired 'var field=new TextBox { FontSize=19,' 'var field=new TextBox { FontSize=16,'
ReplaceRequired 'var scroller=new ScrollViewer {MaxHeight=440,' 'var scroller=new ScrollViewer {MaxHeight=310,'
ReplaceRequired '? 92+Math.Min(440,Math.Max(1,count)*64):72);' '? 70+Math.Min(310,Math.Max(1,count)*49):58);'
ReplaceRequired 'foreach(var hit in found.Take(20))' 'foreach(var hit in found.Take(8))'
ReplaceRequired 'MinHeight=57};' 'MinHeight=44};'
ReplaceRequired 'FontSize=14,FontWeight=FontWeights.SemiBold,' 'FontSize=13,FontWeight=FontWeights.SemiBold,'
ReplaceRequired 'TimeSpan.FromMilliseconds(270)' 'TimeSpan.FromMilliseconds(180)'
ReplaceRequired 'status.Text=found.Count+" results · Hover a row for actions";' @'
                status.Text=found.Count+" matches · hover for actions"+
                    (DeskSpaceSearchIndexV13.IsIndexing?" · indexing this PC":"");
'@
ReplaceRequired 'field.TextChanged+=(_,_)=>{debounce.Stop();debounce.Start();};' @'
        indexRefresh=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1.7)};
        indexRefresh.Tick+=(_,_)=>{
            if(disposed || string.IsNullOrWhiteSpace(field.Text))
            {indexRefresh.Stop();return;}
            // One final refresh is required after the index completes.
            debounce.Stop();debounce.Start();
            if(!DeskSpaceSearchIndexV13.IsIndexing)indexRefresh.Stop();
        };
        field.TextChanged+=(_,_)=>{
            debounce.Stop();debounce.Start();
            if(!indexRefresh.IsEnabled)indexRefresh.Start();
        };
'@
ReplaceRequired 'frame.Loaded+=(_,_)=>{resize(72);field.Focus();Keyboard.Focus(field);};' 'frame.Loaded+=(_,_)=>{resize(58);field.Focus();Keyboard.Focus(field);};'
ReplaceRequired 'disposed=true;debounce?.Stop();pending?.Cancel();pending?.Dispose();' 'disposed=true;debounce?.Stop();indexRefresh?.Stop();pending?.Cancel();pending?.Dispose();'
SaveSource $path $content

$path="$root/DeskSpaceInlineV11.cs"
$content=ReadSource $path
ReplaceRequired 'var memo=Field("Edit note",235);' 'var memo=Field("Edit note",Math.Clamp(SystemParameters.WorkArea.Height*.18,95,165) is double h ? (int)h : 140);'
ReplaceRequired 'var items=new ListBox{MaxHeight=186,MinHeight=115,' 'var items=new ListBox{MaxHeight=122,MinHeight=70,'
ReplaceRequired 'var history=new ListBox{MaxHeight=170,MinHeight=100,' 'var history=new ListBox{MaxHeight=115,MinHeight=66,'
ReplaceRequired 'var shelf=new ListBox{MaxHeight=125,MinHeight=70,' 'var shelf=new ListBox{MaxHeight=88,MinHeight=56,'
SaveSource $path $content

$path="$root/WidgetHost.cs"
$content=ReadSource $path
ReplaceRequired @'
            Width=540;Height=72;
'@ @'
            var limits=ModuleArea();
            Width=Math.Min(limits.Width,Math.Clamp(limits.Width*.32,270,390));Height=58;
'@
ReplaceRequired @'
            Width=name=="Notes"?382:name=="Clipboard"?440:415;
            Height=name=="Notes"?420:name=="Clipboard"?460:440;
'@ @'
            var limits=ModuleArea();
            Width=Math.Min(limits.Width,Math.Clamp(limits.Width*.31,275,380));
            Height=Math.Min(limits.Height,Math.Clamp(limits.Height*
                (name=="Notes"?.40:name=="Reminders"?.41:.43),250,390));
'@
ReplaceRequired @'
    private void ResizeLiveSearch(double height)
'@ @'
    // Per-monitor working area in WPF device-independent pixels.
    private Size ModuleArea()
    {
        var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var screen=Forms.Screen.FromHandle(handle);
        var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(this);
        return new Size(Math.Max(230,screen.WorkingArea.Width/dpi.DpiScaleX-28),
            Math.Max(220,screen.WorkingArea.Height/dpi.DpiScaleY-28));
    }
    private void ResizeLiveSearch(double height)
'@
ReplaceRequired 'Height=Math.Clamp(height,72,525);' 'Height=Math.Min(ModuleArea().Height,Math.Clamp(height,58,390));'
ReplaceRequired 'AddWeatherScene(10,9,272);' 'AddWeatherScene(53,6,185);'
# The scene supplies the weather artwork, so do not overlay a second Unicode icon.
$a=$content.IndexOf('    private string ClockWithWeather()', [StringComparison]::Ordinal)
$b=if($a -ge 0){$content.IndexOf('    private Brush ClockFace()', $a,[StringComparison]::Ordinal)}else{-1}
if($a -lt 0 -or $b -le $a){throw 'R13 weather clock method boundary missing'}
$content=$content.Substring(0,$a)+@'
    private string ClockWithWeather()
    {
        var time=DateTime.Now.ToString("hh:mm tt");
        var value=DeskSpaceWeather.Latest;
        if(!prefs.ShowWeather || value==null ||
            !value.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase))
            return time+"\n"+DateTime.Now.ToString("ddd dd MMM");
        var degrees=Math.Round(value.Celsius)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        return time+"\n"+degrees+"°C\n"+DateTime.Now.ToString("ddd dd MMM");
    }
'@+[Environment]::NewLine+$content.Substring($b)
ReplaceRequired @'
        content.FontSize = ringExpanded ? 18 :
            (prefs.CenterMode=="Clock" && prefs.ShowWeather &&
             DeskSpaceWeather.Latest!=null ? 27 : (prefs.CenterMode=="Notes"?30:32));
'@ @'
        content.FontSize = ringExpanded ? 18 :
            (prefs.CenterMode=="Clock" && prefs.ShowWeather &&
             DeskSpaceWeather.Latest!=null ? 29 : (prefs.CenterMode=="Notes"?30:32));
'@
SaveSource $path $content

$path="$root/DeskSpaceWeatherSceneV11.cs"
$content=ReadSource $path
ReplaceRequired @'
        if(!reading.IsDay)Night(diameter);
        else if(code>=51)Rain(diameter);
        else if(code<=1)Sunny(diameter);
        else Cloudy(diameter);
'@ @'
        if(code>=95)Storm(diameter);
        else if(code>=71 && code<=86)Snow(diameter);
        else if(code>=51)Rain(diameter);
        else if(code is 45 or 48)Fog(diameter);
        else if(!reading.IsDay)Night(diameter);
        else if(code<=1)Sunny(diameter);
        else Cloudy(diameter);
'@
ReplaceRequired '    private void Night(double d)' @'
    private void Snow(double d)
    {
        CloudCore(d,d*.14,d*.06,1.30,true);
        for(int i=0;i<9;i++)
        {
            double x=d*(.15+i*.075),y=d*(.42+(i%3)*.105);
            var flake=Disc(d*.029,Brushes.White,.92);
            Draw(flake,x,y);
            if(i==2) {
                rainMovement=new TranslateTransform();
                flake.RenderTransform=rainMovement;
                rainMovement.BeginAnimation(TranslateTransform.YProperty,
                    Motion(-d*.10,d*.15,2.0));
            }
        }
    }
    private void Fog(double d)
    {
        CloudCore(d,d*.12,d*.10,1.16,true);
        for(int i=0;i<3;i++)
        {
            var bar=new System.Windows.Shapes.Rectangle{
                Width=d*.54,Height=Math.Max(2,d*.025),
                RadiusX=d*.012,RadiusY=d*.012,
                Fill=new SolidColorBrush(Color.FromArgb(150,230,237,255))};
            Draw(bar,d*.19,d*(.52+i*.105));
        }
    }
    private void Storm(double d)
    {
        Rain(d);
        var bolt=new System.Windows.Shapes.Polygon{
            Points=new PointCollection{
                new Point(d*.60,d*.37),new Point(d*.48,d*.58),
                new Point(d*.60,d*.58),new Point(d*.49,d*.81),
                new Point(d*.74,d*.50),new Point(d*.61,d*.50)},
            Fill=new SolidColorBrush(Color.FromRgb(255,221,96)),
            Opacity=.96};
        Children.Add(bolt);
    }
    private void Night(double d)
'@
SaveSource $path $content
Write-Host 'R13: progressive full-drive search, real app shortcut ranking, responsive inline editors and condition-appropriate visible weather.'

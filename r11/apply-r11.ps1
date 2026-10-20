$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($name in @('DeskSpaceSearchV11.cs','DeskSpaceInlineV11.cs','DeskSpaceWeatherSceneV11.cs')){
    Copy-Item -LiteralPath "r11/$name" -Destination "$root/$name" -Force
}
function LoadSource([string]$file) {
    return [IO.File]::ReadAllText($file).Replace(([string][char]13+[char]10),([string][char]10))
}
function SaveSource([string]$file,[string]$source) {
    [IO.File]::WriteAllText($file,$source,[Text.UTF8Encoding]::new($false))
}
function Swap([string]$old,[string]$new) {
    $old=$old.Replace(([string][char]13+[char]10),([string][char]10))
    $new=$new.Replace(([string][char]13+[char]10),([string][char]10))
    if(!$script:widget.Contains($old)){throw "R11 widget patch anchor missing: $old"}
    $script:widget=$script:widget.Replace($old,$new)
}
$module="$root/DeskSpaceModuleV10.cs"
$code=LoadSource $module
$begin=$code.IndexOf('    private void BuildSearch(StackPanel body)',[StringComparison]::Ordinal)
$end=$code.IndexOf('    private static List<SearchHit> SearchNow(',[Math]::Max($begin,0),[StringComparison]::Ordinal)
if($begin -lt 0 -or $end -le $begin){throw 'R11 old large search panel not found'}
$code=$code.Substring(0,$begin)+$code.Substring($end)
if(!$code.Contains('private static List<SearchHit> SearchNow(')){throw 'R11 shared filesystem search engine unavailable'}
$code=$code.Replace('private static List<SearchHit> SearchNow(','internal static List<SearchHit> SearchNow(')
$code=$code.Replace('            case "Search": BuildSearch(body);break;','')
$code=$code.Replace('            case "Calculator": BuildCalculator(body);break;','')
$begin=$code.IndexOf('    private void BuildCalculator(StackPanel body)',[StringComparison]::Ordinal)
$end=$code.IndexOf('    public void Dispose()', [Math]::Max($begin,0),[StringComparison]::Ordinal)
if($begin -lt 0 -or $end -le $begin){throw 'R11 embedded calculator method not found'}
$code=$code.Substring(0,$begin)+$code.Substring($end)
SaveSource $module $code

$source="$root/WidgetHost.cs"
$widget=LoadSource $source
Swap '    private DeskSpaceModuleV10? activeModule;' @'
    private DeskSpaceModuleV10? activeModule;
    private DeskSpaceSearchV11? activeSearch;
    private DeskSpaceInlineV11? activeInline;
    private DeskSpaceWeatherSceneV11? weatherScene;
    private object? previousSurface;
'@
Swap @'
        activeModule=new DeskSpaceModuleV10(prefs,previousCenter,ReturnFromModule);
        Width=602;Height=686;
        KeepOnVisibleDesktop();
        Content=activeModule.Build(name);
        DeskSpaceWindowBehavior.AllowInput(this,true);
'@ @'
        previousSurface=Content;
        activeModule=new DeskSpaceModuleV10(prefs,previousCenter,ReturnFromModule);
        FrameworkElement surface;
        if(name=="Search")
        {
            Width=540;Height=72;
            activeSearch=new DeskSpaceSearchV11(prefs,ReturnFromModule,ResizeLiveSearch);
            surface=activeSearch.Create();
        }
        else if(name is "Notes" or "Reminders" or "Clipboard")
        {
            Width=name=="Notes"?382:name=="Clipboard"?440:415;
            Height=name=="Notes"?420:name=="Clipboard"?460:440;
            activeInline=new DeskSpaceInlineV11(name,previousCenter,ReturnFromModule);
            surface=activeInline.Create();
        }
        else
        {
            Width=602;Height=686;
            surface=activeModule.Build(name);
        }
        KeepOnVisibleDesktop();
        Content=surface;
        DeskSpaceWindowBehavior.AllowInput(this,true);
'@
Swap @'
        var old=activeModule;activeModule=null;old.Dispose();
        DeskSpaceWindowBehavior.AllowInput(this,false);
        ShowActivated=false;
        Content=canvas.Parent as Viewbox ?? new Viewbox{Child=canvas,Stretch=Stretch.Uniform};
'@ @'
        var old=activeModule;activeModule=null;
        var search=activeSearch;activeSearch=null;
        var inline=activeInline;activeInline=null;
        search?.Dispose();inline?.Dispose();old.Dispose();
        DeskSpaceWindowBehavior.AllowInput(this,false);
        ShowActivated=false;
        Content=previousSurface ?? (canvas.Parent as Viewbox ??
            new Viewbox{Child=canvas,Stretch=Stretch.Uniform});
        previousSurface=null;
'@
Swap '    private void ReturnFromModule()' @'
    private void ResizeLiveSearch(double height)
    {
        if(activeSearch==null || activeModule==null)return;
        Height=Math.Clamp(height,72,525);
        KeepOnVisibleDesktop();
    }
    private void ReturnFromModule()
'@
Swap @'
        if(name is "Search" or "Notes" or "Reminders" or "Calendar" or "Clipboard" or "Password" or "Calculator" or "Settings")
            OpenInWidget(name);
'@ @'
        if(name=="Calculator")
        {
            CollapseRing();
            try {
                Process.Start(new ProcessStartInfo("calc.exe"){UseShellExecute=true});
            }
            catch(Exception ex) {
                MessageBox.Show("Windows Calculator could not start: "+ex.Message,
                    "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Warning);
            }
            return;
        }
        if(name is "Search" or "Notes" or "Reminders" or "Calendar" or "Clipboard" or "Password" or "Settings")
            OpenInWidget(name);
'@

# The scene is part of the original circle's canvas, clipped to its true edge.
Swap '    private Ellipse? weatherHalo;' @'
    private Ellipse? weatherHalo;
    private void AddWeatherScene(double x,double y,double diameter)
    {
        if(activeModule!=null || hiddenAtEdge || prefs.CenterMode!="Clock" || !prefs.ShowWeather)
            return;
        var reading=DeskSpaceWeather.Latest;
        if(reading==null || !reading.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase))
            return;
        weatherScene=new DeskSpaceWeatherSceneV11(reading,diameter);
        Canvas.SetLeft(weatherScene,x);
        Canvas.SetTop(weatherScene,y);
        canvas.Children.Add(weatherScene);
    }
'@
Swap '        AddWeatherHalo();' '        AddWeatherScene(10,9,272);'
Swap @'
        Canvas.SetLeft(centre,145);Canvas.SetTop(centre,145);canvas.Children.Add(centre);
        clockFace=centre;
'@ @'
        Canvas.SetLeft(centre,145);Canvas.SetTop(centre,145);canvas.Children.Add(centre);
        clockFace=centre;
        AddWeatherScene(145,145,130);
'@
Swap @'
    private void SuspendWeatherMotion()
    {
        weatherHalo?.BeginAnimation(UIElement.OpacityProperty,null);
        weatherHalo=null;
    }
'@ @'
    private void SuspendWeatherMotion()
    {
        weatherHalo?.BeginAnimation(UIElement.OpacityProperty,null);
        weatherHalo=null;
        weatherScene?.StopMotion();
        weatherScene=null;
    }
'@
Swap @'
            if (clockFace != null) clockFace.Fill = ClockFace();
            UpdateCenter();
'@ @'
            if(activeModule==null)
            {
                if(ringExpanded)BuildDonut();
                else BuildCompact();
                UpdateCenter();
            }
'@
SaveSource $source $widget
Write-Host 'R11 inline morphs wired: live Search, Note, Reminder, Clipboard, Windows Calculator, animated clock scene.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r22/DeskSpaceSearchIndexV22.cs' "$root/DeskSpaceSearchIndexV13.cs" -Force
Copy-Item 'r22/DeskSpaceWeatherV22.cs' "$root/DeskSpaceWeather.cs" -Force
Copy-Item 'r22/DeskSpaceInformationV22.cs' "$root/DeskSpaceInformationV22.cs" -Force
Copy-Item 'r22/DeskSpaceCalculatorV22.cs' "$root/DeskSpaceCalculatorV22.cs" -Force

function ReadSource([string]$path){
    [IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
}
function SaveSource([string]$path,[string]$content){
    [IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false))
}
function Must([string]$before,[string]$after){
    $before=$before.Replace([string][char]13+[char]10,[string][char]10)
    $after=$after.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:s.Contains($before)){throw "R22 expected source anchor missing: $before"}
    $script:s=$script:s.Replace($before,$after)
}

# Search engine retains everything: apps / settings / notes / reminders /
# calendar / information, plus Windows Search index and all-drive fallback.
$p="$root/DeskSpaceSearchV11.cs";$s=ReadSource $p
Must 'if(hit.Category=="Notes")Clipboard.SetText(hit.NoteText??"");' @'
                        if(hit.Category is "Notes" or "Reminders" or "Calendar" or "Information")
                            Clipboard.SetText(hit.NoteText??hit.Name);
'@
Must 'status.Text=found.Count+" results"+' @'
                var groups=string.Join(" · ",found
                    .GroupBy(h=>h.Category,StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g=>g.Key)
                    .Select(g=>g.Key+": "+g.Count()));
                status.ToolTip=groups+" | "+DeskSpaceSearchIndexV13.Coverage;
                status.Text=found.Count+" results · all categories"+
'@
# The 45 scrollable rows and all-drive index are retained from R18-R21.
SaveSource $p $s

# Existing encrypted security vault is retained byte-for-byte.
# Ordinary information never enters password-vault-v10.enc.
$p="$root/DeskSpaceModuleV10.cs";$s=ReadSource $p
Must '            case "Password": BuildPassword(body);break;' @'
            case "Information": BuildInformation(body);break;
            case "Password": BuildInformation(body);break;
'@
Must '            case "Settings": body.Children.Add(Label("Widget preferences and quick tools",20));body.Children.Add(Btn("Open widget settings",()=>new WidgetOptionsWindow(prefs,()=>prefs.Save()).Show()));break;' '            case "Settings": BuildSettings(body);break;'
Must '            case "Calendar": BuildCalendar(body);break;' @'
            case "Calendar": BuildCalendar(body);break;
            case "Calculator": body.Children.Add(new DeskSpaceCalculatorV22().Create());break;
'@
Must '    private void BuildPassword(StackPanel body)' @'
    private void BuildSettings(StackPanel body)
    {
        body.Children.Add(Label("Widget Settings",20));
        body.Children.Add(Label("These controls are inside DeskSpace. Your previous settings and permissions remain unchanged.",12));
        var weather=new CheckBox{Content="Show weather in the clock circle",
            IsChecked=prefs.ShowWeather,Margin=new Thickness(0,7,0,10)};
        body.Children.Add(weather);
        var city=new ComboBox{
            ItemsSource=DeskSpaceWeather.Cities.Select(x=>x.Name).ToList(),
            SelectedItem=prefs.WeatherCity,MinHeight=34,
            Margin=new Thickness(0,3,0,9)};
        body.Children.Add(Label("Weather city"));
        body.Children.Add(city);
        var state=Label(DeskSpaceWeather.WeatherStatus,12);
        body.Children.Add(state);
        var row=Row();body.Children.Add(row);
        row.Children.Add(Btn("Save",()=>{
            prefs.ShowWeather=weather.IsChecked==true;
            if(city.SelectedItem is string chosen)prefs.WeatherCity=chosen;
            prefs.Save();
            state.Text="Preferences saved · "+DeskSpaceWeather.WeatherStatus;
        }));
        row.Children.Add(Btn("Refresh online weather",async ()=>{
            await DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
            state.Text=DeskSpaceWeather.WeatherStatus;
        }));
        body.Children.Add(Label("Additional settings are available on demand.",12));
        body.Children.Add(Btn("Advanced settings",()=>{
            new WidgetOptionsWindow(prefs,()=>prefs.Save()).Show();
        }));
    }

    private void BuildPassword(StackPanel body)
'@
Must '    private void BuildPassword(StackPanel body)' @'
    private void BuildInformation(StackPanel body)
    {
        body.Children.Add(Label("Information Manager",20));
        body.Children.Add(Label(
            "Choose what to save. Passwords use the encrypted Security Vault; "+
            "other information stays in your regular local information store.",12));
        var mode=new ComboBox{
            ItemsSource=new[]{"Information","Password · Security Vault"},
            SelectedIndex=0,MinHeight=33,Margin=new Thickness(0,5,0,9)
        };
        body.Children.Add(mode);
        var area=new StackPanel();
        body.Children.Add(area);
        void LoadMode()
        {
            vaultTimeout?.Stop();
            vaultTimeout=null;
            vault?.Dispose();vault=null;
            area.Children.Clear();
            if(mode.SelectedIndex==1)
            {
                BuildPassword(area);
            }
            else
            {
                area.Children.Add(new DeskSpaceInformationV22().Create());
            }
        }
        mode.SelectionChanged+=(_,_)=>LoadMode();
        LoadMode();
    }

    private void BuildPassword(StackPanel body)
'@
Must 'var back=Btn("← "+previous,done);' 'var back=Btn("← Circle",done);'
Must '        return root;' @'
        return new Border{
            Background=new SolidColorBrush(Color.FromRgb(248,249,253)),
            CornerRadius=new CornerRadius(22),ClipToBounds=true,Child=root
        };
'@
SaveSource $p $s

# The SAME WidgetShellWindow morphs outward from the centre circle and back;
# modules do not create independent WPF windows.
$p="$root/WidgetHost.cs";$s=ReadSource $p
Must '"Clipboard", "Password",' '"Clipboard", "Information",'
Must 'or "Password" or' 'or "Information" or'
# Calculator must not launch a separate Windows Calculator process.
Must @'
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
'@ @'
        if(name=="Calculator")
        {
            OpenInWidget(name);
            return;
        }
'@
Must '        Content=surface;' @'
        Content=surface;
        AnimateModuleOpen(surface);
'@
Must '    private void ResizeLiveSearch(double height)' @'
    private void AnimateModuleOpen(FrameworkElement surface)
    {
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut};
        var duration=new Duration(TimeSpan.FromMilliseconds(255));
        var w=new System.Windows.Media.Animation.DoubleAnimation(
            CompactDiameter,Width,duration){EasingFunction=easing};
        var h=new System.Windows.Media.Animation.DoubleAnimation(
            CompactDiameter,Height,duration){EasingFunction=easing};
        BeginAnimation(Window.WidthProperty,w);
        BeginAnimation(Window.HeightProperty,h);
        surface.RenderTransformOrigin=new Point(.5,.5);
        var transform=new ScaleTransform(.72,.72);
        surface.RenderTransform=transform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(.72,1,duration){EasingFunction=easing});
        transform.BeginAnimation(ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(.72,1,duration){EasingFunction=easing});
        surface.BeginAnimation(UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0,1,duration));
    }
    private void AnimateModuleClose(double oldWidth,double oldHeight)
    {
        var duration=new Duration(TimeSpan.FromMilliseconds(235));
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut};
        BeginAnimation(Window.WidthProperty,
            new System.Windows.Media.Animation.DoubleAnimation(oldWidth,CompactDiameter,duration){
                EasingFunction=easing});
        BeginAnimation(Window.HeightProperty,
            new System.Windows.Media.Animation.DoubleAnimation(oldHeight,CompactDiameter,duration){
                EasingFunction=easing});
    }
    private void ResizeLiveSearch(double height)
'@
Must @'
        var old=activeModule;activeModule=null;
'@ @'
        var previousWidth=Width;
        var previousHeight=Height;
        var old=activeModule;activeModule=null;
'@
Must '        BuildCompact();SavePosition();UpdateCenter();' @'
        BuildCompact();SavePosition();UpdateCenter();
        AnimateModuleClose(previousWidth,previousHeight);
'@
# The clock shows last known REAL weather offline, marked as saved; it never
# synthesizes current weather when offline.
Must '        return time+"\n"+degrees+"°C\n"+DateTime.Now.ToString("ddd dd MMM");' @'
        bool cached=DeskSpaceWeather.IsOffline ||
            DateTime.UtcNow-value.FetchedUtc>TimeSpan.FromMinutes(40);
        return time+"\n"+degrees+"°C"+(cached?" · saved":"")+
            "\n"+DateTime.Now.ToString("ddd dd MMM");
'@
Must '        content.FontSize = ringExpanded ? 18 :' @'
        content.ToolTip=DeskSpaceWeather.WeatherStatus;
        content.FontSize = ringExpanded ? 18 :
'@
SaveSource $p $s
Write-Host 'R22: all-category case-insensitive search, offline cached real weather, separate Information Manager & encrypted vault, same-window circle morph for modules.'

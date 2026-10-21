$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r25/DeskSpaceInformationV25.cs' "$root/DeskSpaceInformationV25.cs" -Force
function Load([string]$p) {[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$p,[string]$s) {[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
function Swap([string]$before,[string]$after) {
    if(!$script:s.Contains($before)){throw "R25 required anchor missing: $before"}
    $script:s=$script:s.Replace($before,$after)
}
function ReplaceMethod([string]$begin,[string]$end,[string]$replacement) {
    $a=$script:s.IndexOf($begin,[StringComparison]::Ordinal)
    $b=if($a -ge 0){$script:s.IndexOf($end,$a,[StringComparison]::Ordinal)}else{-1}
    if($a -lt 0 -or $b -le $a){throw "R25 method boundaries missing $begin"}
    $script:s=$script:s.Substring(0,$a)+$replacement+[Environment]::NewLine+$script:s.Substring($b)
}

$p="$root/DeskSpaceModuleV10.cs"
$s=Load $p
# The ordinary information editor is a compact list. UI and PW are the two
# predefined labels; Other permits a custom label. PW invokes the SAME AES-GCM
# vault as before, never writes to ordinary JSON storage.
$info=@'
    private void BuildInformation(StackPanel body)
    {
        body.Margin=new Thickness(12,8,12,8);
        var contents=new StackPanel();
        body.Children.Add(contents);
        void ShowOrdinary()
        {
            vaultTimeout?.Stop();vaultTimeout=null;
            vault?.Dispose();vault=null;
            contents.Children.Clear();
            contents.Children.Add(new DeskSpaceInformationV25().Create(ShowSecure));
        }
        void ShowSecure()
        {
            vaultTimeout?.Stop();vaultTimeout=null;
            vault?.Dispose();vault=null;
            contents.Children.Clear();
            var top=new DockPanel{Margin=new Thickness(0,0,0,6)};
            var back=Btn("← Saved",ShowOrdinary);
            back.Margin=new Thickness(0,0,5,0);
            DockPanel.SetDock(back,Dock.Right);
            top.Children.Add(back);
            top.Children.Add(Label("Security Vault",16));
            contents.Children.Add(top);
            BuildPassword(contents);
        }
        ShowOrdinary();
    }

'@
ReplaceMethod '    private void BuildInformation(StackPanel body)' '    private void BuildPassword(StackPanel body)' $info
# Remove the overlong header and repeated explanation from Settings.
$settings=@'
    private void BuildSettings(StackPanel body)
    {
        body.Margin=new Thickness(12,8,12,8);
        var weather=new CheckBox{Content="Weather on circle",
            IsChecked=prefs.ShowWeather,Foreground=Ink,
            Margin=new Thickness(2,0,0,9)};
        body.Children.Add(weather);
        var line=Row();
        body.Children.Add(line);
        line.Children.Add(Label("City",12));
        var city=new ComboBox{
            ItemsSource=DeskSpaceWeather.Cities.Select(x=>x.Name).ToList(),
            SelectedItem=prefs.WeatherCity,MinHeight=30,MinWidth=210,
            Margin=new Thickness(5,0,0,2)};
        line.Children.Add(city);
        var state=Label(DeskSpaceWeather.WeatherStatus,11);
        state.TextTrimming=TextTrimming.CharacterEllipsis;
        body.Children.Add(state);
        var actions=Row();body.Children.Add(actions);
        actions.Children.Add(Btn("Save",()=>{
            prefs.ShowWeather=weather.IsChecked==true;
            if(city.SelectedItem is string selected)prefs.WeatherCity=selected;
            prefs.Save();
            state.Text=DeskSpaceWeather.WeatherStatus;
        }));
        actions.Children.Add(Btn("Refresh",async ()=>{
            await DeskSpaceWeather.RefreshAsync(city.SelectedItem as string??prefs.WeatherCity);
            state.Text=DeskSpaceWeather.WeatherStatus;
        }));
        body.Children.Add(Btn("Advanced ⋯",()=>{
            new WidgetOptionsWindow(prefs,()=>prefs.Save()).Show();
        }));
    }

'@
ReplaceMethod '    private void BuildSettings(StackPanel body)' '    private void BuildInformation(StackPanel body)' $settings
Save $p $s

$p="$root/WidgetHost.cs"
$s=Load $p
# Keep a single Date/Calendar entry. Preserve Windows Date mode itself when
# previously selected (old settings remain compatible), but repurpose the
# duplicate donut slot for a Files shortcut to existing multi-category search.
Swap '"Clock", "Date", "Tasks", "Weather", "Settings"' '"Clock", "Files", "Tasks", "Weather", "Settings"'
$segment=$s.IndexOf('    private void ClickSegment(string name)',[StringComparison]::Ordinal)
$segmentEnd=if($segment -ge 0){$s.IndexOf('    private void ApplySettings()', $segment,[StringComparison]::Ordinal)}else{-1}
if($segment -lt 0 -or $segmentEnd -le $segment){throw 'R25 click segment method unavailable'}
$clip=$s.Substring($segment,$segmentEnd-$segment)
$clip=$clip.Replace('if(name is "Clock" or "Date" or "Tasks" or "Weather")',
    'if(name is "Clock" or "Tasks" or "Weather")')
$clip=$clip.Replace('if(name is "Search" or','if(name is "Files" or "Search" or')
$clip=$clip.Replace('            OpenInWidget(name);','            OpenInWidget(name=="Files"?"Search":name);')
if(!$clip.Contains('OpenInWidget(name=="Files"')){throw 'R25 Files shortcut route not found'}
$s=$s.Substring(0,$segment)+$clip+$s.Substring($segmentEnd)

# Module dimensions must be content-dependent rather than every module
# opening the same 602 x 686 rectangle.
$open=$s.IndexOf('    private void OpenInWidget(string name)',[StringComparison]::Ordinal)
$end=if($open -ge 0){$s.IndexOf('    private void PositionModuleFromCircle(', $open,[StringComparison]::Ordinal)}else{-1}
if($open -lt 0 -or $end -le $open){throw 'R25 widget opening boundaries unavailable'}
$block=$s.Substring($open,$end-$open)
$before=@'
        else
        {
            Width=602;Height=686;
            surface=activeModule.Build(name);
        }
'@
$after=@'
        else
        {
            var available=ModuleArea();
            double w=name switch {
                "Settings"=>382,
                "Information"=>385,
                "Password"=>385,
                "Calculator"=>340,
                _=>418
            };
            double h=name switch {
                "Settings"=>304,
                "Information"=>385,
                "Password"=>425,
                "Calculator"=>405,
                _=>440
            };
            Width=Math.Min(available.Width,w);
            Height=Math.Min(available.Height,h);
            surface=activeModule.Build(name);
        }
'@
$before=$before.Replace([string][char]13+[char]10,[string][char]10)
$after=$after.Replace([string][char]13+[char]10,[string][char]10)
if(!$block.Contains($before)){
    $pattern='(?s)        else\s*\{\s*Width=602;Height=686;\s*surface=activeModule\.Build\(name\);\s*\}'
    if([regex]::IsMatch($block,$pattern)){$block=[regex]::Replace($block,$pattern,$after,1)}
    else {throw ('R25 default panel sizing not found. Existing opening method: '+$block)}
} else {$block=$block.Replace($before,$after)}
$s=$s.Substring(0,$open)+$block+$s.Substring($end)
Save $p $s
Write-Host 'R25: compact Settings/Information/default panels, UI/PW/custom label-value storage, one Calendar slot plus Files shortcut.'

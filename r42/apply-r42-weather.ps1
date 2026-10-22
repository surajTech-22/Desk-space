$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$moduleSourceFile=Join-Path $root 'DeskSpaceModuleV10.cs'
$destination=Join-Path $root 'DeskSpaceWeather.cs'
if(!(Test-Path $moduleSourceFile) -or !(Test-Path $destination)){
    throw 'R42 weather integration source missing'
}
Copy-Item -LiteralPath 'r42/DeskSpaceWeatherV42.cs' -Destination $destination -Force
$s=[IO.File]::ReadAllText($moduleSourceFile).Replace([string][char]13+[char]10,[string][char]10)
$a=$s.IndexOf('    private void BuildSettings(StackPanel body)',[StringComparison]::Ordinal)
$z=if($a -ge 0){$s.IndexOf('    private void BuildInformation(StackPanel body)',$a,[StringComparison]::Ordinal)}else{-1}
if($a -lt 0 -or $z -le $a){throw 'R42 cannot isolate existing compact Weather Settings'}
$old=$s.Substring($a,$z-$a)
foreach($k in @('prefs.ShowWeather','prefs.WeatherCity','DeskSpaceWeather.RefreshAsync')){
    if(!$old.Contains($k)){throw "R42 previous weather setting missing: $k"}
}
$replacement=@'
    private void BuildSettings(StackPanel body)
    {
        body.Margin=new Thickness(9,4,9,4);
        var weather=new CheckBox{Content="Weather on circle",
            IsChecked=prefs.ShowWeather,Foreground=Ink,
            Margin=new Thickness(2,0,0,3)};
        body.Children.Add(weather);
        body.Children.Add(Label("Place (search worldwide or use Windows location)",11));
        var cityList=DeskSpaceWeather.Cities.Select(x=>x.Name).ToList();
        if(!cityList.Contains(prefs.WeatherCity,StringComparer.OrdinalIgnoreCase))
            cityList.Insert(0,prefs.WeatherCity);
        var city=new ComboBox{
            ItemsSource=cityList,SelectedItem=prefs.WeatherCity,
            MinHeight=27,Margin=new Thickness(0,0,0,3)
        };
        body.Children.Add(city);
        var query=new TextBox{
            MinHeight=27,Margin=new Thickness(0,1,0,2),
            ToolTip="Type a city or town (at least two letters)"
        };
        body.Children.Add(query);
        var result=Label("Manual place selection stays available offline.",10);
        result.TextTrimming=TextTrimming.CharacterEllipsis;
        body.Children.Add(result);
        var choices=new System.Collections.Generic.Dictionary<string,DeskSpaceWeather.Place>(
            StringComparer.OrdinalIgnoreCase);
        var buttons=Row();
        body.Children.Add(buttons);
        buttons.Children.Add(Btn("Find places",async ()=>{
            try{
                result.Text="Searching locations…";
                var matches=await DeskSpaceWeather.SearchCitiesAsync(query.Text);
                choices.Clear();
                foreach(var place in matches)choices[place.Label]=place;
                var options=matches.Select(x=>x.Label).ToArray();
                if(options.Length==0){result.Text="No matches found. Try city and state.";return;}
                city.ItemsSource=options;
                city.SelectedIndex=0;
                result.Text=options.Length+" places found. Select the intended one.";
            }catch(Exception ex){result.Text="Place search unavailable: "+ex.Message;}
        }));
        buttons.Children.Add(Btn("Use my location",async ()=>{
            result.Text="Requesting Windows location permission…";
            if(await DeskSpaceWeather.EnableCurrentLocationAsync()){
                city.ItemsSource=new[]{DeskSpaceWeather.CurrentLocationChoice};
                city.SelectedIndex=0;
                prefs.WeatherCity=DeskSpaceWeather.CurrentLocationChoice;
                prefs.ShowWeather=true;weather.IsChecked=true;prefs.Save();
                await DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
                result.Text="Current location · "+DeskSpaceWeather.WeatherStatus;
            }else result.Text=DeskSpaceWeather.LocationError;
        }));
        var commands=Row();body.Children.Add(commands);
        commands.Children.Add(Btn("Save",()=>{
            prefs.ShowWeather=weather.IsChecked==true;
            if(city.SelectedItem is string selection){
                if(choices.TryGetValue(selection,out var place))
                    DeskSpaceWeather.SelectCity(place);
                prefs.WeatherCity=selection;
            }
            prefs.Save();
            result.Text="Location selected · "+prefs.WeatherCity;
        }));
        commands.Children.Add(Btn("Refresh",async ()=>{
            if(city.SelectedItem is string selection){
                if(choices.TryGetValue(selection,out var place))
                    DeskSpaceWeather.SelectCity(place);
                prefs.WeatherCity=selection;
                prefs.Save();
            }
            await DeskSpaceWeather.RefreshAsync(prefs.WeatherCity);
            result.Text=DeskSpaceWeather.IsOffline
                ?DeskSpaceWeather.LocationError+" · "+DeskSpaceWeather.WeatherStatus
                :DeskSpaceWeather.WeatherStatus;
        }));
        body.Children.Add(Btn("Advanced ⋯",()=>{
            new WidgetOptionsWindow(prefs,()=>prefs.Save()).Show();
        }));
    }

'@
$replacement=$replacement.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Substring(0,$a)+$replacement+$s.Substring($z)
[IO.File]::WriteAllText($moduleSourceFile,$s,[Text.UTF8Encoding]::new($false))
$service=[IO.File]::ReadAllText($destination)
foreach($key in @('EnableCurrentLocationAsync','SearchCitiesAsync','CurrentLocationChoice',
                  'WeatherReading','Geolocator.RequestAccessAsync')){
    if(!$service.Contains($key)){throw "R42 required weather service missing: $key"}
}
# Additional location search controls must remain visible in compact mode.
# Increase only Settings' module height; all other module sizes are unchanged.
$widget=Join-Path $root 'WidgetHost.cs'
$widgetSource=[IO.File]::ReadAllText($widget)
$sizeKey='"Settings"=>304,'
if($widgetSource.Contains($sizeKey)){
    $widgetSource=$widgetSource.Replace($sizeKey,'"Settings"=>414,')
    [IO.File]::WriteAllText($widget,$widgetSource,[Text.UTF8Encoding]::new($false))
}
Write-Host 'R42 Weather: current Windows location with consent, worldwide place search, original manual/offline modes.'

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
./r42/apply-r42-weather.ps1
$hostSource=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceModuleV10.cs')
$weather=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceWeather.cs')
$oldFallback='if (string.IsNullOrEmpty(location.Name)) location = Cities[0];'
if($weather.Contains($oldFallback)){
    throw 'R42 weather silently falls back to a different city'
}
foreach($require in @('Geolocator.RequestAccessAsync()',
    'GetGeopositionAsync(','CurrentLocationChoice',
    'SearchCitiesAsync(','geocoding-api.open-meteo.com',
    'api.open-meteo.com','selectedPlaces','LocationError',
    'IsOffline=true','LoadCache()')){
    if(!$weather.Contains($require)){
        throw "R42 weather feature or offline gate missing: $require"
    }
}
$a=$hostSource.IndexOf('    private void BuildSettings(StackPanel body)')
$b=if($a -ge 0){
    $hostSource.IndexOf('    private void BuildInformation(StackPanel body)',$a)
}else{-1}
if($a -lt 0 -or $b -le $a){throw 'R42 weather settings not found'}
$settings=$hostSource.Substring($a,$b-$a)
foreach($require in @('Use my location','Find places','prefs.WeatherCity',
    'DeskSpaceWeather.EnableCurrentLocationAsync()',
    'DeskSpaceWeather.SearchCitiesAsync(query.Text)',
    'DeskSpaceWeather.SelectCity(place)','prefs.Save()',
    'CurrentLocationChoice')){
    if(!$settings.Contains($require)){
        throw "R42 weather settings control missing: $require"
    }
}
# The original 7-city presets must remain available for old saved preferences.
foreach($name in @('Bengaluru','Mumbai','Delhi','Chennai','Hyderabad','Pune','Kolkata')){
    if(!$weather.Contains('"'+$name+'"')){
        throw "R42 historic preset disappeared: $name"
    }
}
# Preview animation must remain the existing real-weather rendering
# path; do not replace clock or unrelated modules.
if(!(Test-Path 'DeskSpace_Windows_R1/DeskSpace/DeskSpaceWeatherSceneV11.cs')){
    throw 'R42 native weather scene not preserved'
}
Write-Host 'R42 weather safety checks PASS: opt-in current location, worldwide search, offline cache, historic presets.'

using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeskSpace;

// Opt-in Windows location and Open-Meteo worldwide city lookup.
// Never use IP geolocation without consent. Never persist device coordinates.
// Manual city and cached weather continue functioning while offline.
public static class DeskSpaceWeather
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime nextAttempt = DateTime.MinValue;
    public const string CurrentLocationChoice="Current location";
    private static (double Lat,double Lon)? sessionLocation;
    private static readonly Dictionary<string,(double Lat,double Lon)> selectedPlaces=
        new(StringComparer.OrdinalIgnoreCase);
    public static string LocationError {get;private set;}="";
    public sealed record Place(string Label,double Latitude,double Longitude);
    public static async Task<IReadOnlyList<Place>> SearchCitiesAsync(string query)
    {
        if(string.IsNullOrWhiteSpace(query) || query.Trim().Length<2)
            return Array.Empty<Place>();
        var url="https://geocoding-api.open-meteo.com/v1/search?name="+
            Uri.EscapeDataString(query.Trim())+"&count=12&language=en&format=json";
        using var response=await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if(!json.RootElement.TryGetProperty("results",out var results))
            return Array.Empty<Place>();
        var locations=new List<Place>();
        foreach(var loc in results.EnumerateArray())
        {
            if(!loc.TryGetProperty("latitude",out var latJson) ||
               !loc.TryGetProperty("longitude",out var lonJson))continue;
            var lat=latJson.GetDouble();var lon=lonJson.GetDouble();
            if(!double.IsFinite(lat)||!double.IsFinite(lon)||
               Math.Abs(lat)>90||Math.Abs(lon)>180)continue;
            string Val(string key)=>loc.TryGetProperty(key,out var v)
                && v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
            var display=string.Join(", ",new[]{Val("name"),Val("admin1"),Val("country")}
                .Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct());
            locations.Add(new Place(display,lat,lon));
        }
        return locations;
    }
    public static void SelectCity(Place place)
    {
        // The selection is a normal manual preference. Its coordinates are
        // lookup hints held in memory for this session, not device location.
        selectedPlaces[place.Label]=(place.Latitude,place.Longitude);
    }
    public static async Task<bool> EnableCurrentLocationAsync()
    {
        // Call only from an explicit foreground Settings button on UI thread.
        try
        {
            var permission=await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();
            if(permission!=Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed)
            {
                LocationError="Windows location access denied. Choose a city instead.";
                return false;
            }
            var geo=new Windows.Devices.Geolocation.Geolocator{
                DesiredAccuracy=Windows.Devices.Geolocation.PositionAccuracy.Default
            };
            var position=await geo.GetGeopositionAsync(
                TimeSpan.FromMinutes(15),TimeSpan.FromSeconds(15));
            var coordinate=position.Coordinate.Point.Position;
            // About 1 km precision is adequate for general weather.
            sessionLocation=(Math.Round(coordinate.Latitude,2),
                             Math.Round(coordinate.Longitude,2));
            LocationError="";
            return true;
        }
        catch(Exception ex)
        {
            LocationError="Windows current location unavailable ("+
                ex.GetType().Name+"). Enable Location services or choose a city.";
            return false;
        }
    }
    private static readonly string CachePath=Path.Combine(WorkspaceStore.Root,"weather-last-good.json");
    public static WeatherReading? Latest { get; private set; }=LoadCache();
    public static bool IsOffline {get;private set;}
    public static bool HasCachedReading => Latest!=null;
    public static string WeatherStatus
    {
        get
        {
            if(Latest==null)return "No cached weather · connect to the internet to get conditions";
            var age=DateTime.UtcNow-Latest.FetchedUtc;
            var label=IsOffline||age>TimeSpan.FromMinutes(35)?"Last synced · offline/cached":"Online · updated";
            return label+" "+Latest.FetchedUtc.ToLocalTime().ToString("dd MMM HH:mm");
        }
    }
    private static WeatherReading? LoadCache()
    {
        try
        {
            if(!File.Exists(CachePath))return null;
            var saved=JsonSerializer.Deserialize<WeatherReading>(File.ReadAllText(CachePath));
            if(saved==null||saved.FetchedUtc>DateTime.UtcNow.AddMinutes(5)||
                saved.FetchedUtc<DateTime.UtcNow.AddDays(-21)||
                saved.Celsius< -100||saved.Celsius>75)return null;
            return saved;
        }
        catch(Exception){return null;}
    }
    private static void Persist(WeatherReading reading)
    {
        try
        {
            Directory.CreateDirectory(WorkspaceStore.Root);
            var temp=CachePath+".pending";
            File.WriteAllText(temp,JsonSerializer.Serialize(reading),new UTF8Encoding(false));
            File.Move(temp,CachePath,true);
        }
        catch(Exception){/* local file failure never blocks clock */ }
    }
    public static event Action? Updated;

    public static readonly (string Name, double Lat, double Lon)[] Cities =
    {
        ("Bengaluru", 12.9716, 77.5946), ("Mumbai", 19.0760, 72.8777),
        ("Delhi", 28.6139, 77.2090), ("Chennai", 13.0827, 80.2707),
        ("Hyderabad", 17.3850, 78.4867), ("Pune", 18.5204, 73.8567),
        ("Kolkata", 22.5726, 88.3639)
    };

    public static async Task RefreshAsync(string city)
    {
        if(DateTime.UtcNow<nextAttempt && Latest?.City==city)return;
        if(!await Gate.WaitAsync(0))return;
        try
        {
            if(DateTime.UtcNow<nextAttempt && Latest?.City==city)return;
            (double Lat,double Lon) coordinates;
            string label=city;
            if(string.Equals(city,CurrentLocationChoice,StringComparison.OrdinalIgnoreCase))
            {
                if(!sessionLocation.HasValue)
                {
                    // Use the existing Windows permission if user previously
                    // chose this mode. Do not show a consent prompt on startup.
                    var locator=new Windows.Devices.Geolocation.Geolocator{
                        DesiredAccuracy=Windows.Devices.Geolocation.PositionAccuracy.Default
                    };
                    var reading=await locator.GetGeopositionAsync(
                        TimeSpan.FromMinutes(15),TimeSpan.FromSeconds(12));
                    var point=reading.Coordinate.Point.Position;
                    sessionLocation=(Math.Round(point.Latitude,2),
                                     Math.Round(point.Longitude,2));
                }
                coordinates=sessionLocation.Value;
                label=CurrentLocationChoice;
            }
            else if(selectedPlaces.TryGetValue(city,out var stored))
                coordinates=stored;
            else
            {
                var location=Array.Find(Cities,
                    x=>x.Name.Equals(city,StringComparison.OrdinalIgnoreCase));
                if(!string.IsNullOrWhiteSpace(location.Name))
                    coordinates=(location.Lat,location.Lon);
                else
                {
                    // Manual city selected in previous session: resolve again.
                    var places=await SearchCitiesAsync(city);
                    var chosen=places.FirstOrDefault(p=>
                        p.Label.Equals(city,StringComparison.OrdinalIgnoreCase))
                        ??places.FirstOrDefault();
                    if(chosen==null)
                        throw new InvalidOperationException("No matching weather city.");
                    coordinates=(chosen.Latitude,chosen.Longitude);
                }
            }
            var lat=coordinates.Lat.ToString(CultureInfo.InvariantCulture);
            var lon=coordinates.Lon.ToString(CultureInfo.InvariantCulture);
            var url=$"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,is_day&timezone=auto";
            using var result=await Client.GetAsync(url);
            result.EnsureSuccessStatusCode();
            using var payload=JsonDocument.Parse(await result.Content.ReadAsStringAsync());
            var now=payload.RootElement.GetProperty("current");
            var temp=now.GetProperty("temperature_2m").GetDouble();
            var code=now.GetProperty("weather_code").GetInt32();
            var day=now.GetProperty("is_day").GetInt32()==1;
            if(temp< -100||temp>75)throw new InvalidOperationException("Invalid weather temperature");
            Latest=new WeatherReading(label,temp,code,day,DateTime.UtcNow);
            Persist(Latest);
            IsOffline=false;
            LocationError="";
            nextAttempt=DateTime.UtcNow.AddMinutes(15);
            Updated?.Invoke();
        }
        catch(Exception ex)
        {
            // Never silently substitute Bengaluru or report cached values
            // as current conditions when location/city lookup fails.
            IsOffline=true;
            if(Latest==null)Latest=LoadCache();
            LocationError=ex is System.UnauthorizedAccessException
                ?"Windows location is disabled. Select another city."
                :"Weather unavailable: "+ex.GetType().Name;
            nextAttempt=DateTime.UtcNow.AddMinutes(5);
            Updated?.Invoke();
        }
        finally{Gate.Release();}
    }

    public static string Icon(int code, bool day) =>
        code <= 1 ? (day ? "☀" : "☾") :
        code <= 3 ? "☁" :
        code is 45 or 48 ? "≋" :
        code >= 95 ? "ϟ" :
        code >= 51 ? "☂" : "☁";

    public static string Description(int code) =>
        code == 0 ? "Clear" : code <= 3 ? "Partly cloudy" :
        code <= 48 ? "Foggy" : code >= 95 ? "Thunderstorm" :
        code >= 71 && code <= 86 ? "Snow" :
        code >= 51 ? "Rain" : "Cloudy";
}
public sealed record WeatherReading(string City, double Celsius, int Code, bool IsDay, DateTime FetchedUtc);

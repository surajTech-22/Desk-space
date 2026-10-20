using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeskSpace;

// Public Open-Meteo conditions, no API key, no precise-device geolocation.
// The city is an explicit widget preference. Weather is optional and never blocks the clock.
public static class DeskSpaceWeather
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime nextAttempt = DateTime.MinValue;
    public static WeatherReading? Latest { get; private set; }
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
        if (DateTime.UtcNow < nextAttempt) return;
        if (!await Gate.WaitAsync(0)) return;
        try
        {
            if (DateTime.UtcNow < nextAttempt) return;
            var location = Array.Find(Cities, c => c.Name.Equals(city, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(location.Name)) location = Cities[0];
            var lat = location.Lat.ToString(CultureInfo.InvariantCulture);
            var lon = location.Lon.ToString(CultureInfo.InvariantCulture);
            var uri = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,is_day&timezone=auto";
            using var result = await Client.GetAsync(uri);
            result.EnsureSuccessStatusCode();
            using var payload = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
            var now = payload.RootElement.GetProperty("current");
            var temp = now.GetProperty("temperature_2m").GetDouble();
            var code = now.GetProperty("weather_code").GetInt32();
            bool day = now.GetProperty("is_day").GetInt32() == 1;
            if (temp < -100 || temp > 75) throw new InvalidOperationException("Invalid weather temperature");
            Latest = new WeatherReading(location.Name, temp, code, day, DateTime.UtcNow);
            nextAttempt = DateTime.UtcNow.AddMinutes(15);
            Updated?.Invoke();
        }
        catch (Exception)
        {
            // Offline/timeout: retain last reading and retry without interrupting the clock.
            nextAttempt = DateTime.UtcNow.AddMinutes(5);
        }
        finally { Gate.Release(); }
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

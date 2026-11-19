using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DockForWindows.Services;

public sealed record WidgetLocation(string Name, string Region, double Latitude, double Longitude)
{
    public override string ToString() => string.IsNullOrEmpty(Region) ? Name : $"{Name} · {Region}";
}
public sealed record WidgetForecastDay(DateOnly Date, int Code, double High, double Low);
public sealed record WidgetWeatherSnapshot(double Temperature, int Code, bool IsDay, DateTime ModelTime,
    DateTimeOffset RetrievedAt, IReadOnlyList<WidgetForecastDay> Days)
{
    public IReadOnlyList<WidgetForecastHour> Hours {get;init;}=Array.Empty<WidgetForecastHour>();
}
public sealed record WidgetForecastHour(DateTime Time,int Code,double Temperature);

public sealed class WidgetWeatherService
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly HttpClient _client;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,(DateTimeOffset At,Task<WidgetWeatherSnapshot> Task)> Cache=new();
    private static readonly object CacheGate=new();
    public WidgetWeatherService(HttpClient? client = null) => _client = client ?? SharedClient;
    public async Task<IReadOnlyList<WidgetLocation>> SearchAsync(string name, CancellationToken cancellation)
    {
        name = name.Trim();
        if (name.Length < 2 || name.Length > 100) throw new ArgumentException(LocalizedText.Get("请输入 2–100 个字符的城市名。"));
        using var stream = await _client.GetStreamAsync("https://geocoding-api.open-meteo.com/v1/search?count=6&language=" + (UiText.IsEnglish?"en":"zh") + "&name=" + Uri.EscapeDataString(name), cancellation).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 24 }, cancellation).ConfigureAwait(false);
        var results = new List<WidgetLocation>();
        if (json.RootElement.TryGetProperty("results", out var locations)) foreach (var item in locations.EnumerateArray()) {
            double lat = item.GetProperty("latitude").GetDouble(), lon = item.GetProperty("longitude").GetDouble();
            ValidateLocation(lat, lon);
            string city = item.GetProperty("name").GetString() ?? "";
            string region = item.TryGetProperty("admin1", out var admin) ? admin.GetString() ?? "" : "";
            if (item.TryGetProperty("country", out var country)) region += " " + country.GetString();
            if (city.Length > 0) results.Add(new(city, region.Trim(), lat, lon));
        }
        return results.AsReadOnly();
    }
    public async Task<WidgetWeatherSnapshot> ReadAsync(double latitude,double longitude,CancellationToken cancellation)
    {
        ValidateLocation(latitude,longitude);
        if(_client!=SharedClient)return await ReadCoreAsync(latitude,longitude,cancellation).ConfigureAwait(false);
        string key=FormattableString.Invariant($"{latitude:F6},{longitude:F6}");Task<WidgetWeatherSnapshot> task;
        lock(CacheGate){
            if(Cache.TryGetValue(key,out var entry)&&DateTimeOffset.UtcNow-entry.At<TimeSpan.FromMinutes(15)&&!entry.Task.IsFaulted&&!entry.Task.IsCanceled)task=entry.Task;
            else{if(Cache.Count>=64)Cache.Clear();task=ReadCoreAsync(latitude,longitude,CancellationToken.None);Cache[key]=(DateTimeOffset.UtcNow,task);}
        }
        return await task.WaitAsync(cancellation).ConfigureAwait(false);
    }
    internal static void Invalidate(double latitude,double longitude){lock(CacheGate)Cache.TryRemove(FormattableString.Invariant($"{latitude:F6},{longitude:F6}"),out _);}
    private async Task<WidgetWeatherSnapshot> ReadCoreAsync(double latitude,double longitude,CancellationToken cancellation)
    {
        ValidateLocation(latitude, longitude);
        string location = FormattableString.Invariant($"latitude={latitude:F6}&longitude={longitude:F6}");
        using var stream = await _client.GetStreamAsync("https://api.open-meteo.com/v1/forecast?" + location +
            "&current=temperature_2m,weather_code,is_day&hourly=temperature_2m,weather_code&daily=weather_code,temperature_2m_max,temperature_2m_min&forecast_days=5&timezone=auto", cancellation).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 24 }, cancellation).ConfigureAwait(false);
        return Parse(json.RootElement);
    }
    private static void ValidateLocation(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
            throw new ArgumentOutOfRangeException(nameof(latitude), LocalizedText.Get("城市坐标无效。"));
    }
    public static WidgetWeatherSnapshot Parse(JsonElement root)
    {
        var current = root.GetProperty("current"); var daily = root.GetProperty("daily");
        var dates = daily.GetProperty("time"); var codes = daily.GetProperty("weather_code");
        var highs = daily.GetProperty("temperature_2m_max"); var lows = daily.GetProperty("temperature_2m_min");
        int count = dates.GetArrayLength();
        if (count is < 1 or > 16 || codes.GetArrayLength() != count || highs.GetArrayLength() != count || lows.GetArrayLength() != count)
            throw new FormatException(LocalizedText.Get("天气预报数据不完整。"));
        double Temperature(JsonElement value) {
            double result = value.GetDouble();
            if (!double.IsFinite(result) || result is < -150 or > 100) throw new FormatException(LocalizedText.Get("天气温度数据无效。"));
            return result;
        }
        var days = new List<WidgetForecastDay>();
        for (int i = 0; i < count; i++) {
            double high = Temperature(highs[i]), low = Temperature(lows[i]);
            if (high < low) throw new FormatException(LocalizedText.Get("天气温度范围无效。"));
            days.Add(new(DateOnly.ParseExact(dates[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture), codes[i].GetInt32(), high, low));
        }
        var hours=new List<WidgetForecastHour>();
        if(root.TryGetProperty("hourly",out var hourly)){
            var times=hourly.GetProperty("time");var temps=hourly.GetProperty("temperature_2m");var hourCodes=hourly.GetProperty("weather_code");
            if(times.GetArrayLength()>400||temps.GetArrayLength()!=times.GetArrayLength()||hourCodes.GetArrayLength()!=times.GetArrayLength())throw new FormatException(LocalizedText.Get("逐小时天气数据不完整。"));
            for(int i=0;i<times.GetArrayLength();i++)hours.Add(new(DateTime.SpecifyKind(DateTime.Parse(times[i].GetString()!,CultureInfo.InvariantCulture),DateTimeKind.Unspecified),hourCodes[i].GetInt32(),Temperature(temps[i])));
        }
        return new(Temperature(current.GetProperty("temperature_2m")), current.GetProperty("weather_code").GetInt32(),
            current.GetProperty("is_day").GetInt32() == 1,
            DateTime.SpecifyKind(DateTime.Parse(current.GetProperty("time").GetString()!, CultureInfo.InvariantCulture), DateTimeKind.Unspecified),
            DateTimeOffset.Now, days.AsReadOnly()){Hours=hours.AsReadOnly()};
    }
    public static string Description(int code) => code switch {
        0 => LocalizedText.Get("晴"), 1 => LocalizedText.Get("大部晴朗"), 2 => LocalizedText.Get("局部多云"), 3 => LocalizedText.Get("阴"), 45 or 48 => LocalizedText.Get("雾"),
        51 or 53 or 55 => LocalizedText.Get("毛毛雨"), 56 or 57 or 66 or 67 => LocalizedText.Get("冻雨"), 61 or 63 or 65 => LocalizedText.Get("雨"),
        71 or 73 or 75 or 77 or 85 or 86 => LocalizedText.Get("雪"), 80 or 81 or 82 => LocalizedText.Get("阵雨"), 95 or 96 or 99 => LocalizedText.Get("雷雨"), _ => LocalizedText.Get("天气代码 ") + code };
}

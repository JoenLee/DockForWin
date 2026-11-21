using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace DockForWindows.Services;

public sealed record WidgetSolarDay(DateOnly Date,DateTime? Sunrise,DateTime? Sunset);
public sealed record WidgetSolarState(DateTimeOffset LocalTime,DateTimeOffset? Sunrise,DateTimeOffset? Sunset,DateTimeOffset? Next,bool IsSunrise,double? DayProgress);
public sealed partial class WidgetWeatherService
{
    private static IReadOnlyList<WidgetSolarDay> ParseSolar(JsonElement daily,int count)
    {
        bool hasRise=daily.TryGetProperty("sunrise",out var rises),hasSet=daily.TryGetProperty("sunset",out var sets);
        if(!hasRise&&!hasSet)return Array.Empty<WidgetSolarDay>();
        if(!hasRise||!hasSet||rises.ValueKind!=JsonValueKind.Array||sets.ValueKind!=JsonValueKind.Array||rises.GetArrayLength()!=count||sets.GetArrayLength()!=count)throw new FormatException("日出日落数据不完整。");
        DateTime? Parse(JsonElement item)=>item.ValueKind==JsonValueKind.Null?null:DateTime.SpecifyKind(DateTime.ParseExact(item.GetString()!,"yyyy-MM-dd'T'HH:mm",CultureInfo.InvariantCulture),DateTimeKind.Unspecified);
        var days=new List<WidgetSolarDay>();
        for(int i=0;i<count;i++){
            var date=DateOnly.ParseExact(daily.GetProperty("time")[i].GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture);
            DateTime? rise=Parse(rises[i]),set=Parse(sets[i]);
            if(rise!=null&&DateOnly.FromDateTime(rise.Value)!=date||set!=null&&DateOnly.FromDateTime(set.Value)!=date||rise!=null&&set!=null&&rise>=set)throw new FormatException("日出日落时间无效。");
            days.Add(new(date,rise,set));
        }
        return days.AsReadOnly();
    }
    public static WidgetSolarState SolarAt(WidgetWeatherSnapshot weather,DateTimeOffset utc)
    {
        TimeZoneInfo? zone=null;
        try{if(!string.IsNullOrWhiteSpace(weather.TimeZone))zone=TimeZoneInfo.FindSystemTimeZoneById(weather.TimeZone);}catch(TimeZoneNotFoundException){}catch(InvalidTimeZoneException){}
        var now=zone==null?utc.ToOffset(TimeSpan.FromSeconds(weather.UtcOffsetSeconds)):TimeZoneInfo.ConvertTime(utc,zone);
        DateTimeOffset? At(DateTime? local)=>local is DateTime value?new DateTimeOffset(value,zone?.GetUtcOffset(value)??now.Offset):null;
        var today=weather.SolarDays.FirstOrDefault(d=>d.Date==DateOnly.FromDateTime(now.DateTime));
        var rise=At(today?.Sunrise);var set=At(today?.Sunset);
        var next=weather.SolarDays.SelectMany(d=>new[]{(At:At(d.Sunrise),Rise:true),(At:At(d.Sunset),Rise:false)}).Where(e=>e.At>now).OrderBy(e=>e.At).FirstOrDefault();
        double? progress=rise!=null&&set>rise?Math.Clamp((now-rise.Value).TotalSeconds/(set.Value-rise.Value).TotalSeconds,0,1):null;
        return new(now,rise,set,next.At,next.Rise,progress);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace DockForWindows.Models;

public enum WidgetMaterial { Light, Dark, Glass }
public enum WidgetKind { Weather, Calendar, Clock, Reminders, Notes, Music }
public enum WidgetLayout { Standard, WeatherPoster, WeatherOutlook, CalendarPoster, CalendarWeather, CalendarClock, WeatherHourly, CalendarGiant, CalendarMinimal, CalendarDate, CalendarUpcoming, CalendarTimeline, CalendarGroups, ClockClassic, ClockMinimal, ClockWorld, ClockMonth, ClockDate, ReminderList, ReminderDate, ReminderGroups, ReminderDetail, NotesSmall, NotesWide, NotesList, NotesDetail, MusicSmall, MusicWide, MusicList, MusicLarge, MusicNow }
public enum WidgetNumberStyle { Standard, Rounded, Outline, Art }
public enum WidgetTextWeight { Automatic, Light, Normal, Bold }
public readonly record struct WidgetSize(int Columns, int Rows)
{
    public const double Unit = 84, Gap = 12;
    [System.Text.Json.Serialization.JsonIgnore] public double Width => Columns * Unit + (Columns - 1) * Gap;
    [System.Text.Json.Serialization.JsonIgnore] public double Height => Rows * Unit + (Rows - 1) * Gap;
    public override string ToString() => $"{Columns} × {Rows}";
}
public sealed record WidgetDefinition(string Id, WidgetKind Kind, string Title, WidgetSize Size, WidgetLayout Layout = WidgetLayout.Standard);
public static class WidgetRegistry
{
    private static readonly IReadOnlyList<WidgetDefinition> ChineseDefinitions = Array.AsReadOnly(new[] {
        new WidgetDefinition("weather-small", WidgetKind.Weather, "一眼天气", new(2,2)),
        new WidgetDefinition("weather-square", WidgetKind.Weather, "今日天气", new(2,2)),
        new WidgetDefinition("weather-strip", WidgetKind.Weather, "天气速览", new(6,2)),
        new WidgetDefinition("weather-forecast", WidgetKind.Weather, "五日预报", new(4,2)),
        new WidgetDefinition("weather-poster", WidgetKind.Weather, "温度海报", new(4,2), WidgetLayout.WeatherPoster),
        new WidgetDefinition("weather-outlook", WidgetKind.Weather, "预报面板", new(4,2), WidgetLayout.WeatherOutlook),
        new WidgetDefinition("calendar-day", WidgetKind.Calendar, "今天", new(2,2)),
        new WidgetDefinition("calendar-agenda", WidgetKind.Calendar, "接下来", new(4,2)),
        new WidgetDefinition("calendar-month", WidgetKind.Calendar, "月历", new(2,2)),
        new WidgetDefinition("calendar-planner", WidgetKind.Calendar, "月历与日程", new(4,2)),
        new WidgetDefinition("calendar-poster", WidgetKind.Calendar, "数字月历", new(4,2), WidgetLayout.CalendarPoster),
        new WidgetDefinition("calendar-weather", WidgetKind.Calendar, "日历与天气", new(4,2), WidgetLayout.CalendarWeather),
        new WidgetDefinition("calendar-clock", WidgetKind.Calendar, "时间与日期", new(4,2), WidgetLayout.CalendarClock),
        new("weather-hourly",WidgetKind.Weather,"天空 · 逐小时",new(6,2),WidgetLayout.WeatherHourly),
        new("weather-combined",WidgetKind.Weather,"月历与天气",new(6,2),WidgetLayout.CalendarWeather),
        new("calendar-giant",WidgetKind.Calendar,"巨型数字月历",new(4,2),WidgetLayout.CalendarGiant),
        new("calendar-minimal",WidgetKind.Calendar,"简约数字月历",new(4,2),WidgetLayout.CalendarMinimal),
        new("calendar-date",WidgetKind.Calendar,"日期卡片",new(2,2),WidgetLayout.CalendarDate),
        new("calendar-upcoming",WidgetKind.Calendar,"临近日程",new(4,4),WidgetLayout.CalendarUpcoming),
        new("calendar-timeline",WidgetKind.Calendar,"今天与明天",new(4,4),WidgetLayout.CalendarTimeline),
        new("calendar-groups",WidgetKind.Calendar,"分组日程",new(4,6),WidgetLayout.CalendarGroups),
        new("clock-classic",WidgetKind.Clock,"经典表盘",new(2,2),WidgetLayout.ClockClassic),
        new("clock-minimal",WidgetKind.Clock,"极简表盘",new(2,2),WidgetLayout.ClockMinimal),
        new("clock-world",WidgetKind.Clock,"世界时钟",new(4,2),WidgetLayout.ClockWorld),
        new("clock-month",WidgetKind.Clock,"时钟与月历",new(4,2),WidgetLayout.ClockMonth),
        new("clock-date",WidgetKind.Clock,"日期与时钟",new(4,2),WidgetLayout.ClockDate),
        new("reminder-list",WidgetKind.Reminders,"待办清单",new(2,2),WidgetLayout.ReminderList),
        new("reminder-date",WidgetKind.Reminders,"日期与待办",new(4,2),WidgetLayout.ReminderDate),
        new("reminder-groups",WidgetKind.Reminders,"分组列表",new(4,4),WidgetLayout.ReminderGroups),
        new("reminder-detail",WidgetKind.Reminders,"详细待办",new(4,6),WidgetLayout.ReminderDetail),
        new("notes-small",WidgetKind.Notes,"随手记",new(2,2),WidgetLayout.NotesSmall),
        new("notes-wide",WidgetKind.Notes,"横向备忘录",new(4,2),WidgetLayout.NotesWide),
        new("notes-list",WidgetKind.Notes,"备忘录列表",new(4,4),WidgetLayout.NotesList),
        new("notes-detail",WidgetKind.Notes,"完整备忘录",new(4,6),WidgetLayout.NotesDetail),
        new("music-small",WidgetKind.Music,"最近播放",new(2,2),WidgetLayout.MusicSmall),
        new("music-wide",WidgetKind.Music,"横向播放",new(4,2),WidgetLayout.MusicWide),
        new("music-list",WidgetKind.Music,"播放记录",new(4,4),WidgetLayout.MusicList),
        new("music-large",WidgetKind.Music,"完整播放记录",new(4,6),WidgetLayout.MusicLarge),
        new("music-now",WidgetKind.Music,"当前播放",new(4,4),WidgetLayout.MusicNow) });
    private static readonly Lazy<IReadOnlyList<WidgetDefinition>> EnglishDefinitions=new(()=>Array.AsReadOnly(ChineseDefinitions.Select(d=>d with{Title=LocalizedText.Get(d.Title)}).ToArray()));
    public static IReadOnlyList<WidgetDefinition> Definitions=>LocalizedText.IsEnglish?EnglishDefinitions.Value:ChineseDefinitions;
    public static WidgetDefinition Get(string id) => Definitions.FirstOrDefault(d => d.Id == id)
        ?? throw new ArgumentException(LocalizedText.Get("未知小组件。"), nameof(id));
    public static WidgetInstance CreateInstance(string id, WidgetMaterial material, WidgetOptions? configuration = null)
    {
        var copy = configuration?.Copy() ?? new(); copy.Material = material; copy.Normalize();
        return new WidgetInstance { DefinitionId=Get(id).Id, Material=material, Appearance=copy.CardColors.GetValueOrDefault(id)?.Copy()??new(), Source=copy.Sources.TryGetValue(id,out var source)?source.Copy():new(){City=copy.City,Latitude=copy.Latitude,Longitude=copy.Longitude,UseCurrentLocation=!copy.Latitude.HasValue}, Size=Get(id).Size, Exponent=copy.Exponent };
    }
}


public sealed record WidgetPlacementRequest(WidgetInstance Instance);
public sealed record WidgetDragData(WidgetInstance Instance, double GrabX, double GrabY)
{
    public const string Format = "DockForWindows.Widget.v1";
}
public interface IWidgetPlacementService
{
    bool IsAvailable { get; }
    bool TryRequest(WidgetPlacementRequest request, out string message);
}

public sealed class WidgetCalendarEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Start { get; set; } = DateTime.Today;
    public string Title { get; set; } = "";
    public WidgetCalendarEvent Copy() => (WidgetCalendarEvent)MemberwiseClone();
}
public sealed class WidgetCardColors
{
    public string Background { get; set; } = "";
    public string Foreground { get; set; } = "";
    public string Accent { get; set; } = "";
    public bool? Gradient { get; set; }
    public WidgetNumberStyle NumberStyle { get; set; }
    public WidgetTextWeight TextWeight { get; set; }
    public double TextScale { get; set; } = 1;
    public double PanelScale { get; set; } = 1;
    public double Shadow {get;set;}=1;
    public double Opacity {get;set;}=1;
    public double? Curvature {get;set;}
    public string GradientEnd {get;set;}="";
    internal bool HasColors => Background.Length > 0 || Foreground.Length > 0 || Accent.Length > 0 || Gradient != null;
    internal bool IsDefault => Shadow==1 && Opacity==1 && Curvature==null && GradientEnd.Length==0 && !HasColors && NumberStyle==WidgetNumberStyle.Standard && TextWeight==WidgetTextWeight.Automatic && TextScale==1 && PanelScale==1;
    public void Normalize()
    {
        Shadow=double.IsFinite(Shadow)?Math.Clamp(Shadow,0,2):1;
        Opacity=double.IsFinite(Opacity)?Math.Clamp(Opacity,.1,1):1;
        if(Curvature!=null)Curvature=Curvature==3?3:2.5;
        GradientEnd=ThemeAppearance.TryColor(GradientEnd,out var end)?end:"";
        Background = ThemeAppearance.TryColor(Background, out var background) ? background : "";
        Foreground = ThemeAppearance.TryColor(Foreground, out var foreground) ? foreground : "";
        Accent = ThemeAppearance.TryColor(Accent, out var accent) ? accent : "";
        if(!Enum.IsDefined(NumberStyle))NumberStyle=WidgetNumberStyle.Standard;
        if(!Enum.IsDefined(TextWeight))TextWeight=WidgetTextWeight.Automatic;
        TextScale=double.IsFinite(TextScale)?Math.Clamp(TextScale,.75,1.35):1;
        PanelScale=double.IsFinite(PanelScale)?Math.Clamp(PanelScale,.75,1.25):1;
    }
    public WidgetCardColors Copy() => (WidgetCardColors)MemberwiseClone();
}
public sealed partial class WidgetOptions
{
    public WidgetMaterial Material { get; set; }
    public double Exponent { get; set; } = 2.5;
    public int WeatherSelection { get; set; } = 6;
    public int CalendarSelection { get; set; } = 7;
    public string City { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<WidgetCalendarEvent> Events { get; set; } = new();
    public Dictionary<string, WidgetCardColors> CardColors { get; set; } = new(StringComparer.Ordinal);
    public void Normalize()
    {
        NormalizeSystem();
        if (!Enum.IsDefined(Material)) Material = WidgetMaterial.Light;
        Exponent = Exponent == 3 ? 3 : 2.5;
        WeatherSelection = Math.Clamp(WeatherSelection, 0, WidgetRegistry.Definitions.Count(d => d.Kind == WidgetKind.Weather) - 1);
        CalendarSelection = Math.Clamp(CalendarSelection, 0, WidgetRegistry.Definitions.Count(d => d.Kind == WidgetKind.Calendar) - 1);
        CardColors ??= new();
        CardColors = CardColors.Where(p => p.Value != null && WidgetRegistry.Definitions.Any(d => d.Id == p.Key))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var pair in CardColors.ToArray()) { pair.Value.Normalize(); if (pair.Value.IsDefault) CardColors.Remove(pair.Key); }
        City = (City ?? "").Trim(); if (City.Length > 120) City = City[..120];
        if (Latitude is not double lat || Longitude is not double lon || !double.IsFinite(lat) || !double.IsFinite(lon)
            || Math.Abs(lat) > 90 || Math.Abs(lon) > 180 || City.Length == 0) { Latitude = Longitude = null; City = ""; }
        Events ??= new();
        Events = Events.Where(e => e != null && !string.IsNullOrWhiteSpace(e.Title)).Take(200).ToList();
        var ids = new HashSet<Guid>();
        foreach (var entry in Events) {
            entry.Title = entry.Title.Trim(); if (entry.Title.Length > 120) entry.Title = entry.Title[..120];
            if (entry.Id == Guid.Empty || !ids.Add(entry.Id)) { entry.Id = Guid.NewGuid(); ids.Add(entry.Id); }
            entry.Start = DateTime.SpecifyKind(entry.Start, DateTimeKind.Unspecified);
        }
    }
    public WidgetOptions Copy()
    {
        var copy = (WidgetOptions)MemberwiseClone();
        copy.CopySystem(this);
        copy.Events = Events.Select(e => e.Copy()).ToList();
        copy.CardColors = CardColors.ToDictionary(p => p.Key, p => p.Value.Copy(), StringComparer.Ordinal);
        return copy;
    }
}

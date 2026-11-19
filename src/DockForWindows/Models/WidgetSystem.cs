using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DockForWindows.Models;

public sealed class WidgetSource
{
    public bool UseCurrentLocation {get;set;}
    public bool MicrosoftAccount {get;set;}
    public string City {get;set;}="";
    public double? Latitude {get;set;}
    public double? Longitude {get;set;}
    public string TimeZone {get;set;}=TimeZoneInfo.Local.Id;
    public List<string> WorldZones {get;set;}=new(){"China Standard Time","GMT Standard Time","Eastern Standard Time"};
    public string List {get;set;}="";
    public string NotesFolder {get;set;}="";
    public WidgetSource Copy(){var c=(WidgetSource)MemberwiseClone();c.WorldZones=new(WorldZones);return c;}
    public void Normalize()
    {
        City=(City??"").Trim();if(City.Length>120)City=City[..120];
        if(Latitude is not double lat||Longitude is not double lon||!double.IsFinite(lat)||!double.IsFinite(lon)||Math.Abs(lat)>90||Math.Abs(lon)>180)Latitude=Longitude=null;
        try{TimeZoneInfo.FindSystemTimeZoneById(TimeZone);}catch{TimeZone=TimeZoneInfo.Local.Id;}
        WorldZones=(WorldZones??new()).Where(id=>{try{TimeZoneInfo.FindSystemTimeZoneById(id);return true;}catch{return false;}}).Distinct().Take(6).ToList();
        List=(List??"").Trim();NotesFolder=(NotesFolder??"").Trim();
    }
}
public sealed class WidgetInstance
{
    public Guid InstanceId {get;set;}=Guid.NewGuid();
    public string DefinitionId {get;set;}="calendar-giant";
    public WidgetSize Size {get;set;}=new(4,2);
    public WidgetMaterial Material {get;set;}
    public double Exponent {get;set;}=2.5;
    public WidgetCardColors Appearance {get;set;}=new();
    public WidgetSource Source {get;set;}=new();
    public string Monitor {get;set;}="";
    public int Column {get;set;}
    public int Row {get;set;}
    public bool Locked {get;set;}
    public WidgetInstance Copy(){var c=(WidgetInstance)MemberwiseClone();c.Appearance=Appearance.Copy();c.Source=Source.Copy();return c;}
}
public sealed class WidgetTask
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Title {get;set;}="";
    [System.Text.Json.Serialization.JsonIgnore] public string RemoteId {get;set;}="";
    [System.Text.Json.Serialization.JsonIgnore] public string RemoteListId {get;set;}="";
    public string Detail {get;set;}="";
    public string List {get;set;}=LocalizedText.Get("提醒事项");
    public DateTime? Due {get;set;}
    public bool Completed {get;set;}
    public WidgetTask Copy()=>(WidgetTask)MemberwiseClone();
}
public sealed class WidgetNote
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Title {get;set;}="";
    public string Body {get;set;}="";
    public string Folder {get;set;}=LocalizedText.Get("备忘录");
    public DateTime Modified {get;set;}=DateTime.Now;
    [System.Text.Json.Serialization.JsonIgnore] public string WebUrl {get;set;}="";
    [System.Text.Json.Serialization.JsonIgnore] public string FilePath {get;set;}="";
    public WidgetNote Copy()=>(WidgetNote)MemberwiseClone();
}
public sealed class WidgetMusicEntry
{
    public string Title {get;set;}="";
    public string Artist {get;set;}="";
    public DateTime Played {get;set;}
    public string ArtworkKey {get;set;}="";
    public WidgetMusicEntry Copy()=>(WidgetMusicEntry)MemberwiseClone();
}
public static class WidgetSizes
{
    public static readonly WidgetSize[] All={new(2,2),new(4,2),new(4,4),new(4,6),new(6,2)};
    public static IReadOnlyList<WidgetSize> For(WidgetDefinition d)=>d.Kind switch {
        _ when d.Layout is WidgetLayout.CalendarWeather or WidgetLayout.CalendarGiant or WidgetLayout.CalendarMinimal or WidgetLayout.CalendarPoster=>new[]{new WidgetSize(4,2),new(4,4),new(6,2)},
        WidgetKind.Clock when d.Layout is WidgetLayout.ClockClassic or WidgetLayout.ClockMinimal=>new[]{new WidgetSize(2,2),new(4,4)},
        WidgetKind.Clock=>new[]{new WidgetSize(4,2),new(4,4),new(6,2)},
        WidgetKind.Weather=>new[]{new WidgetSize(2,2),new(4,2),new(4,4),new(6,2)},
        WidgetKind.Calendar when d.Layout is WidgetLayout.CalendarGroups=>new[]{new WidgetSize(4,4),new(4,6)},
        WidgetKind.Calendar when d.Layout is WidgetLayout.CalendarUpcoming or WidgetLayout.CalendarTimeline=>new[]{new WidgetSize(4,2),new(4,4),new(4,6)},
        WidgetKind.Reminders when d.Layout is WidgetLayout.ReminderGroups or WidgetLayout.ReminderDetail=>new[]{new WidgetSize(4,4),new(4,6)},
        WidgetKind.Notes when d.Layout==WidgetLayout.NotesSmall=>new[]{new WidgetSize(2,2),new(4,2)},
        WidgetKind.Notes=>new[]{new WidgetSize(4,2),new(4,4),new(4,6)},
        WidgetKind.Music when d.Layout==WidgetLayout.MusicSmall=>new[]{new WidgetSize(2,2),new(4,2)},
        WidgetKind.Music when d.Layout==WidgetLayout.MusicWide=>new[]{new WidgetSize(4,2),new(6,2)},
        WidgetKind.Music when d.Layout is WidgetLayout.MusicList or WidgetLayout.MusicLarge=>new[]{new WidgetSize(4,4),new(4,6)},
        _=>All };
    public static string Category(WidgetKind kind)=>kind switch{WidgetKind.Weather=>LocalizedText.Get("天气"),WidgetKind.Calendar=>LocalizedText.Get("日历"),WidgetKind.Clock=>LocalizedText.Get("时钟"),WidgetKind.Reminders=>LocalizedText.Get("提醒事项"),WidgetKind.Notes=>LocalizedText.Get("备忘录"),_=>LocalizedText.Get("音乐")};
    public static string Description(WidgetDefinition d)=>d.Kind switch{
        WidgetKind.Weather=>LocalizedText.Get("当前实况与当地预报 · Open-Meteo"),
        WidgetKind.Calendar=>LocalizedText.Get("日期、农历与本应用日程"),
        WidgetKind.Clock=>LocalizedText.Get("本地与世界时间 · 可选择时区"),
        WidgetKind.Reminders=>LocalizedText.Get("Microsoft To Do 或本应用待办 · 清单与截止日期"),
        WidgetKind.Notes=>LocalizedText.Get("OneNote、应用笔记或本地文件夹"),
        _=>LocalizedText.Get("系统媒体会话与本应用观察到的播放记录")};
}
public sealed partial class WidgetOptions
{
    public List<WidgetInstance> Instances {get;set;}=new();
    public List<WidgetTask> Tasks {get;set;}=new();
    public List<WidgetNote> Notes {get;set;}=new();
    public List<WidgetMusicEntry> MusicHistory {get;set;}=new();
    public Dictionary<string,int> Selections {get;set;}=new();
    public Dictionary<string,WidgetSize> Sizes {get;set;}=new();
    public Dictionary<string,WidgetSource> Sources {get;set;}=new();
    private void NormalizeSystem()
    {
        Instances=(Instances??new()).Where(x=>x!=null&&WidgetRegistry.Definitions.Any(d=>d.Id==x.DefinitionId)).Take(100).ToList();
        var ids=new HashSet<Guid>();
        foreach(var x in Instances){if(x.InstanceId==Guid.Empty||!ids.Add(x.InstanceId)){x.InstanceId=Guid.NewGuid();ids.Add(x.InstanceId);}
            var d=WidgetRegistry.Get(x.DefinitionId);if(!WidgetSizes.For(d).Contains(x.Size))x.Size=WidgetSizes.For(d)[0];
            x.Appearance??=new();x.Appearance.Normalize();x.Source??=new();x.Source.Normalize();x.Monitor??="";
            x.Column=Math.Clamp(x.Column,0,500);x.Row=Math.Clamp(x.Row,0,500);x.Exponent=x.Exponent==3?3:2.5;if(!Enum.IsDefined(x.Material))x.Material=WidgetMaterial.Light;}
        Tasks=(Tasks??new()).Where(x=>x!=null&&!string.IsNullOrWhiteSpace(x.Title)).DistinctBy(x=>x.Id).Take(500).ToList();
        foreach(var t in Tasks){t.Title=t.Title[..Math.Min(300,t.Title.Length)];t.Detail=(t.Detail??"");if(t.Detail.Length>4000)t.Detail=t.Detail[..4000];t.List=string.IsNullOrWhiteSpace(t.List)?LocalizedText.Get("提醒事项"):t.List;}
        Notes=(Notes??new()).Where(x=>x!=null).DistinctBy(x=>x.Id).Take(500).ToList();
        foreach(var n in Notes){n.Title??="";n.Body??="";n.Folder??=LocalizedText.Get("备忘录");if(n.Title.Length>300)n.Title=n.Title[..300];if(n.Body.Length>100000)n.Body=n.Body[..100000];}
        MusicHistory=(MusicHistory??new()).Where(x=>x!=null&&!string.IsNullOrWhiteSpace(x.Title)).Take(30).ToList();
        foreach(var entry in MusicHistory){entry.Title=entry.Title[..Math.Min(entry.Title.Length,300)];entry.Artist??="";if(entry.Artist.Length>300)entry.Artist=entry.Artist[..300];if(entry.ArtworkKey==null||entry.ArtworkKey.Length!=64||!entry.ArtworkKey.All(Uri.IsHexDigit))entry.ArtworkKey="";}
        Selections??=new();Sizes??=new();Sources=(Sources??new()).Where(x=>x.Value!=null&&WidgetRegistry.Definitions.Any(d=>d.Id==x.Key)).ToDictionary(x=>x.Key,x=>x.Value);foreach(var s in Sources.Values)s.Normalize();
        foreach(var key in Sizes.Keys.ToArray())if(!WidgetRegistry.Definitions.Any(d=>d.Id==key&&WidgetSizes.For(d).Contains(Sizes[key])))Sizes.Remove(key);
    }
    private void CopySystem(WidgetOptions from)
    {
        Instances=from.Instances.Select(x=>x.Copy()).ToList();Tasks=from.Tasks.Select(x=>x.Copy()).ToList();Notes=from.Notes.Select(x=>x.Copy()).ToList();
        MusicHistory=from.MusicHistory.Select(x=>x.Copy()).ToList();Selections=new(from.Selections);Sizes=new(from.Sizes);Sources=from.Sources.ToDictionary(x=>x.Key,x=>x.Value.Copy());
    }
    // A stale settings draft must preserve independent desktop changes made since it opened.
    public void MergeLive(WidgetOptions baseline,WidgetOptions live)
    {
        Instances=live.Instances.Select(x=>x.Copy()).ToList();MusicHistory=live.MusicHistory.Select(x=>x.Copy()).ToList();
        Tasks=Merge(Tasks,baseline.Tasks,live.Tasks,x=>x.Id,x=>x.Copy());Notes=Merge(Notes,baseline.Notes,live.Notes,x=>x.Id,x=>x.Copy());Events=Merge(Events,baseline.Events,live.Events,x=>x.Id,x=>x.Copy());
    }
    private static List<T> Merge<T>(List<T> draft,List<T> baseline,List<T> live,Func<T,Guid> id,Func<T,T> copy)
    {
        var original=baseline.ToDictionary(id);var current=live.ToDictionary(id);var result=new List<T>();
        foreach(var item in draft){var key=id(item);if(!original.TryGetValue(key,out var old)||JsonSerializer.Serialize(item)!=JsonSerializer.Serialize(old))result.Add(copy(item));else if(current.TryGetValue(key,out var value))result.Add(copy(value));}
        foreach(var item in live)if(!original.ContainsKey(id(item))&&!draft.Any(x=>id(x)==id(item)))result.Add(copy(item));return result;
    }
}

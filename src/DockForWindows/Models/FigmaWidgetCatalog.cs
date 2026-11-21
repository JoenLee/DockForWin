using System;
using System.Collections.Generic;
using System.Linq;

namespace DockForWindows.Models;

internal sealed record FigmaWidgetStyle(string Id,string Title,string Scene,WidgetKind Kind,WidgetSize Size);
internal static class FigmaWidgetCatalog
{
    internal static readonly IReadOnlyList<FigmaWidgetStyle> All = Array.AsReadOnly(new[] {
        new FigmaWidgetStyle("clock-reference-classic","Figma · Classic clock","clock-a-2-2",WidgetKind.Clock,new(2,2)),
        new FigmaWidgetStyle("clock-reference-midnight","Figma · Midnight clock","clock-a-2-3",WidgetKind.Clock,new(2,2)),
        new FigmaWidgetStyle("clock-reference-digital","Figma · Digital clock","clock-a-2-4",WidgetKind.Clock,new(2,2)),
        new FigmaWidgetStyle("clock-reference-alarm","Figma · Next alarm","clock-a-2-5",WidgetKind.Clock,new(2,2)),
        new FigmaWidgetStyle("clock-reference-world","Figma · World clock","clock-a-2-6",WidgetKind.Clock,new(4,2)),
        new FigmaWidgetStyle("clock-reference-city","Figma · City time","clock-a-2-7",WidgetKind.Clock,new(4,2)),
        new FigmaWidgetStyle("clock-figma-mint","Figma · Mint analog clock","clock-b-2-334",WidgetKind.Clock,new(2,2)),
        new FigmaWidgetStyle("clock-figma-violet","Figma · Violet analog clock","clock-b-2-416",WidgetKind.Clock,new(2,2)),
        new FigmaWidgetStyle("clock-figma-cobalt","Figma · Cobalt digital clock","clock-b-2-499",WidgetKind.Clock,new(2,2)),
        new FigmaWidgetStyle("clock-figma-coral-alarm","Figma · Coral alarm","clock-b-2-508",WidgetKind.Clock,new(2,2)),
        new FigmaWidgetStyle("clock-figma-prism-world","Figma · Prism world clock","clock-b-2-518",WidgetKind.Clock,new(4,2)),
        new FigmaWidgetStyle("clock-figma-lime-city","Figma · Lime city clock","clock-b-2-596",WidgetKind.Clock,new(4,2)),
        new FigmaWidgetStyle("music-figma-ios-small","iOS · 2×2","ios-2-2",WidgetKind.Music,new(2,2)),
        new FigmaWidgetStyle("music-figma-ios-wide","iOS · 4×2","ios-2-3",WidgetKind.Music,new(4,2)),
        new FigmaWidgetStyle("music-figma-ios-square","iOS · 4×4","ios-2-4",WidgetKind.Music,new(4,4)),
        new FigmaWidgetStyle("music-figma-ios-large","iOS · 4×6","ios-2-5",WidgetKind.Music,new(4,6)),
        new FigmaWidgetStyle("music-figma-ipod-graphite-wide","iPod · graphite-wide","ipod-1-52",WidgetKind.Music,new(4,2)),
        new FigmaWidgetStyle("music-figma-ipod-horizontal","iPod · horizontal","ipod-2-2",WidgetKind.Music,new(4,2)),
        new FigmaWidgetStyle("music-figma-ipod-aluminum","iPod · aluminum","ipod-2-3",WidgetKind.Music,new(4,2)),
        new FigmaWidgetStyle("music-figma-ipod-compact","iPod · compact","ipod-2-4",WidgetKind.Music,new(2,2)),
        new FigmaWidgetStyle("music-figma-ipod-minimal","iPod · minimal","ipod-2-5",WidgetKind.Music,new(2,2)),
        new FigmaWidgetStyle("music-figma-record","日落 · 黑胶唱机","record-3-2",WidgetKind.Music,new(4,2)),
        new FigmaWidgetStyle("weather-figma-atmosphere-seafoam","日出日落 · seafoam","weather-atmosphere-2-139",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-atmosphere-rose","日出日落 · rose","weather-atmosphere-2-171",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-atmosphere-dawn","日出日落 · dawn","weather-atmosphere-2-2",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-atmosphere-amber","日出日落 · amber","weather-atmosphere-2-203",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-atmosphere-arctic","日出日落 · arctic","weather-atmosphere-2-235",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-atmosphere-day","日出日落 · day","weather-atmosphere-2-3",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-atmosphere-sunset","日出日落 · sunset","weather-atmosphere-2-4",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-atmosphere-night","日出日落 · night","weather-atmosphere-2-5",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-coral","地平线 · coral","weather-horizon-2-274",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-tangerine","地平线 · tangerine","weather-horizon-2-378",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-gold","地平线 · gold","weather-horizon-2-400",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-mint","地平线 · mint","weather-horizon-2-422",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-teal","地平线 · teal","weather-horizon-2-444",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-azure","地平线 · azure","weather-horizon-2-466",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-lavender","地平线 · lavender","weather-horizon-2-488",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-rose","地平线 · rose","weather-horizon-2-510",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-graphite","地平线 · graphite","weather-horizon-2-532",WidgetKind.Weather,new(4,2)),
        new FigmaWidgetStyle("weather-figma-horizon-custom","地平线 · custom","weather-horizon-2-554",WidgetKind.Weather,new(4,2)),
    });
    internal static FigmaWidgetStyle? Find(string id) => All.FirstOrDefault(s => s.Id == id);
    internal static IEnumerable<WidgetDefinition> Definitions => All.Where(s => !s.Id.StartsWith("clock-reference-",StringComparison.Ordinal)).Select(s => new WidgetDefinition(s.Id,s.Kind,s.Title,s.Size,s.Kind==WidgetKind.Music?WidgetLayout.MusicWide:WidgetLayout.Standard));
    internal static WidgetSource? DefaultSource(string id) => Find(id) is {Kind:WidgetKind.Clock} ? new() {TimeZone=id.Contains("city")?"GMT Standard Time":TimeZoneInfo.Local.Id,WorldZones=new(){"Pacific Standard Time","Eastern Standard Time","GMT Standard Time","Tokyo Standard Time"}} : null;
}

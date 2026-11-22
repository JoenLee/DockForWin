using System;
using System.Collections.Generic;
using System.Linq;

namespace DockForWindows.Models;

internal enum DocumentClockContent { Analog, Digital, Alarm, World, City }
internal sealed record DocumentClockStyle(string Id, string Title, string ReferenceName, bool Wide,
    string Background, string Ink, DocumentClockContent Content)
{
    internal WidgetDefinition Definition => new(Id, WidgetKind.Clock, Title, new(Wide ? 4 : 2, 2),
        Content == DocumentClockContent.Analog ? WidgetLayout.ClockClassic : Content is DocumentClockContent.World or DocumentClockContent.City ? WidgetLayout.ClockWorld : WidgetLayout.ClockDate);
    internal string Accent => "#FF8200";
}

// Only the six designs in the supplied screenshot are exposed. The discarded CSS-only variants are gone.
internal static class DocumentClockStyles
{
    internal static readonly IReadOnlyList<DocumentClockStyle> All = Array.AsReadOnly(new[] {
        new DocumentClockStyle("clock-reference-classic", "经典时钟 · 参考款", "Classic clock", false, "#FFFFFF", "#19191C", DocumentClockContent.Analog),
        new("clock-reference-midnight", "午夜时钟 · 参考款", "Midnight clock", false, "#18181B", "#F5F5F7", DocumentClockContent.Analog),
        new("clock-reference-digital", "数字时钟 · 参考款", "Digital clock", false, "#DDE8FA", "#173251", DocumentClockContent.Digital),
        new("clock-reference-alarm", "下次闹钟 · 参考款", "Next alarm", false, "#FCE8D7", "#522D18", DocumentClockContent.Alarm),
        new("clock-reference-world", "世界时钟 · 四城表盘", "World clock", true, "#19191C", "#F5F5F7", DocumentClockContent.World),
        new("clock-reference-city", "城市时间 · 数字与表盘", "City time", true, "#F4F0E8", "#292D32", DocumentClockContent.City)
    });
    internal static DocumentClockStyle? Find(string id) => All.FirstOrDefault(s => s.Id == id);
    internal static IEnumerable<WidgetDefinition> Definitions => All.Select(s => s.Definition);
    internal static IReadOnlyList<WidgetSize> Sizes(DocumentClockStyle style) => style.Wide
        ? new[] { new WidgetSize(4, 2), new(6, 2) } : new[] { new WidgetSize(2, 2), new(4, 4) };
    internal static WidgetSource? DefaultSource(string id) => Find(id) is {} style ? new() {
        TimeZone = style.Content == DocumentClockContent.City ? "GMT Standard Time" : TimeZoneInfo.Local.Id,
        WorldZones = new() { "Pacific Standard Time", "Eastern Standard Time", "GMT Standard Time", "Tokyo Standard Time" }
    } : null;
}

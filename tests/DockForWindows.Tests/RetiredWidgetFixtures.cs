using System;
using System.Collections.Generic;
using System.Linq;
using DockForWindows.Models;

// Retired gallery templates are kept only as explicit fixtures for the shared native playback/layout engine.
// These definitions are never added to the application registry or to a saved user profile.
internal static class RetiredWidgetFixtures
{
    internal static readonly IReadOnlyList<WidgetDefinition> Definitions = new WidgetDefinition[] {
new("clock-dot",WidgetKind.Clock,"点阵 · 方形时钟",new(2,2),WidgetLayout.ClockDot),
new("clock-dot-wide",WidgetKind.Clock,"点阵 · 横向时钟",new(4,2),WidgetLayout.ClockDotWide),
new("music-small",WidgetKind.Music,"最近播放",new(2,2),WidgetLayout.MusicSmall),
new("music-wide",WidgetKind.Music,"横向播放",new(4,2),WidgetLayout.MusicWide),
new("music-list",WidgetKind.Music,"播放记录",new(4,4),WidgetLayout.MusicList),
new("music-now",WidgetKind.Music,"当前播放",new(4,4),WidgetLayout.MusicNow),
new("music-vinyl",WidgetKind.Music,"黑胶 · 方形唱片",new(2,2),WidgetLayout.MusicVinyl),
new("music-vinyl-wide",WidgetKind.Music,"黑胶 · 横向唱片",new(4,2),WidgetLayout.MusicVinylWide),
new("music-poster",WidgetKind.Music,"封面海报播放器",new(4,4),WidgetLayout.MusicPoster),
new("music-frosted",WidgetKind.Music,"浮层播放器",new(4,2),WidgetLayout.MusicFrosted),
new("music-retro",WidgetKind.Music,"复古桌面播放器",new(4,4),WidgetLayout.MusicRetro),
new("music-cassette",WidgetKind.Music,"磁带播放器",new(4,2),WidgetLayout.MusicCassette),
new("music-form-horizontal",WidgetKind.Music,"FORM · 横向唱机",new(4,2),WidgetLayout.MusicWide),
new("music-form-room",WidgetKind.Music,"FORM · 聆听室",new(4,4),WidgetLayout.MusicLarge),
new("music-form-single",WidgetKind.Music,"FORM · 迷你唱机",new(2,2),WidgetLayout.MusicSmall),
new("music-studio-record",WidgetKind.Music,"录音室 · 大唱片",new(4,4),WidgetLayout.MusicLarge),
new("music-pocket",WidgetKind.Music,"经典随身听",new(4,7),WidgetLayout.MusicLarge),
new("music-mini",WidgetKind.Music,"迷你播放条",new(4,1),WidgetLayout.MusicMini),
    };
    internal static WidgetDefinition Get(string id) => Definitions.FirstOrDefault(d=>d.Id==id) ?? WidgetRegistry.Get(id);
}
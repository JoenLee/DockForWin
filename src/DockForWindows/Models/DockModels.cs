using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace DockForWindows.Models;

public enum DockEdge { Bottom, Left, Right }
public enum DockTheme { System, Light, Dark, Custom, Glass }
public enum InterfaceTheme { System, Light, Dark }
public enum InterfaceStyle { Soft, Violet, Mono, Warm }
public enum ItemKind { Application, Folder, RecycleBin, Document }

public sealed class PinnedItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = LocalizedText.Get("应用");
    public string Target { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public ItemKind Kind { get; set; }
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string SourceShortcut { get; set; } = "";
    public string ManagedShortcut { get; set; } = "";
    public string IconLocation { get; set; } = "";
    public string AppUserModelId { get; set; } = "";
    public PinnedItem Copy() => (PinnedItem)MemberwiseClone();
}

public sealed class DockSettings
{
    public int SchemaVersion { get; set; } = 1;
    public DockEdge Edge { get; set; } = DockEdge.Bottom;
    public DockTheme Theme { get; set; } = DockTheme.System;
    public InterfaceTheme InterfaceTheme { get; set; } = InterfaceTheme.System;
    public string InterfaceAccent { get; set; } = SettingsAccentPresets.Default;
    public string Language { get; set; } = "zh-CN";
    public InterfaceStyle InterfaceStyle { get; set; } = InterfaceStyle.Mono;
    public bool HideWhenFullscreen { get; set; } = true;
    public ThemeAppearance Appearance { get; set; } = new();
    public double GlassIntensity { get; set; } = .55;
    public double GlassDistortion { get; set; } = .65;
    public double GlassHighlight { get; set; } = .55;
    public double GlassHighlightWidth { get; set; } = 2.5;
    public double GlassSideDarkness { get; set; } = .55;
    public double GlassTint { get; set; } = .06;
    public double GlassOpacity { get; set; } = 1;
    public double GlassFrost { get; set; } = .1;
    public double GlassGrain { get; set; } = .45;
    // One switch and one slider for everyone: liquid (clear, refracting) or frosted glass, and how far toward
    // 磨砂 (0) or 水光 (1) the material sits. GlassMaterial.Apply turns them into the detailed values above.
    public bool LiquidGlass { get; set; } = true;
    public double GlassQuality { get; set; } = .55;
    // Tint preset for every glass surface: "" (none) or #RRGGBB.
    public string GlassTintColor { get; set; } = "";
    public double IconSize { get; set; } = 48;
    public double Spacing { get; set; } = 8;
    public double Magnification { get; set; } = 1.45;
    public double HoverLift { get; set; } = 12;
    public double EdgeDistance { get; set; } = 6;
    public bool AutoHide { get; set; }
    public bool HideOnDesktop { get; set; }
    public bool StartWithWindows { get; set; }
    public bool UseGlassControlCenter { get; set; } = true;
    public bool HideTaskbarApplications { get; set; }
    public ControlCenterOptions ControlCenter { get; set; } = new();
    public WidgetOptions Widgets { get; set; } = new();
    public bool Animations { get; set; } = true;
    public bool ShowRunningApps { get; set; } = true;
    public bool ShowRecentApps { get; set; } = true;
    public int RecentLimit { get; set; } = 3;
    public List<PinnedItem> RecentApps { get; set; } = new();
    public double BounceHeight { get; set; } = 24;
    public double BounceDuration { get; set; } = 760;
    public bool Reflections { get; set; } = true;
    public double ReflectionOpacity { get; set; } = .28;
    public double ReflectionLength { get; set; } = .35;
    public double ReflectionGap { get; set; } = 2;
    public double ReflectionBlur { get; set; }
    public string MonitorDevice { get; set; } = "";
    public List<PinnedItem> Pins { get; set; } = new();

    public void Normalize()
    {
        IconSize = double.IsFinite(IconSize) ? Math.Clamp(IconSize, 32, 72) : 48;
        Spacing = double.IsFinite(Spacing) ? Math.Clamp(Spacing, 2, 18) : 8;
        Magnification = double.IsFinite(Magnification) ? Math.Clamp(Magnification, 1, 1.8) : 1.45;
        HoverLift = double.IsFinite(HoverLift) ? Math.Clamp(HoverLift, 0, 64) : 12;
        EdgeDistance = double.IsFinite(EdgeDistance) ? Math.Clamp(EdgeDistance, 0, 160) : 6;
        if (!Enum.IsDefined(Edge)) Edge = DockEdge.Bottom;
        if (!Enum.IsDefined(Theme)) Theme = DockTheme.System;
        if (!Enum.IsDefined(InterfaceTheme)) InterfaceTheme = InterfaceTheme.System;
        InterfaceAccent = SettingsAccentPresets.Normalize(InterfaceAccent);
        Language = Language == "en-US" ? "en-US" : "zh-CN";
        InterfaceStyle = InterfaceStyle.Mono; // Migrate legacy window palettes without changing the Dock material.
        Appearance ??= new();
        Appearance.Normalize();
        ControlCenter ??= new(); ControlCenter.Normalize();
        Widgets ??= new(); Widgets.Normalize();
        GlassIntensity = double.IsFinite(GlassIntensity) ? Math.Clamp(GlassIntensity, 0, 1) : .55;
        GlassDistortion = double.IsFinite(GlassDistortion) ? Math.Clamp(GlassDistortion, 0, 1) : .65;
        GlassHighlight = double.IsFinite(GlassHighlight) ? Math.Clamp(GlassHighlight, 0, 1) : .55;
        GlassHighlightWidth = double.IsFinite(GlassHighlightWidth) ? Math.Clamp(GlassHighlightWidth, .5, 8) : 2.5;
        GlassSideDarkness = double.IsFinite(GlassSideDarkness) ? Math.Clamp(GlassSideDarkness, 0, 1) : .55;
        GlassTint = double.IsFinite(GlassTint) ? Math.Clamp(GlassTint, 0, .25) : .06;
        GlassOpacity = double.IsFinite(GlassOpacity) ? Math.Clamp(GlassOpacity, 0, 1) : 1;
        GlassFrost = double.IsFinite(GlassFrost) ? Math.Clamp(GlassFrost, 0, 1) : .1;
        GlassGrain = double.IsFinite(GlassGrain) ? Math.Clamp(GlassGrain, 0, 1) : .45;
        GlassQuality = double.IsFinite(GlassQuality) ? Math.Clamp(GlassQuality, 0, 1) : .55;
        GlassTintColor = System.Text.RegularExpressions.Regex.IsMatch(GlassTintColor ?? "", "^#[0-9A-Fa-f]{6}$") ? GlassTintColor! : "";
        BounceHeight = double.IsFinite(BounceHeight) ? Math.Clamp(BounceHeight, 8, 64) : 24;
        BounceDuration = double.IsFinite(BounceDuration) ? Math.Clamp(BounceDuration, 350, 1800) : 760;
        ReflectionOpacity = double.IsFinite(ReflectionOpacity) ? Math.Clamp(ReflectionOpacity, 0, 1) : .28;
        ReflectionLength = double.IsFinite(ReflectionLength) ? Math.Clamp(ReflectionLength, .05, 1) : .35;
        ReflectionGap = double.IsFinite(ReflectionGap) ? Math.Clamp(ReflectionGap, 0, 12) : 2;
        ReflectionBlur = double.IsFinite(ReflectionBlur) ? Math.Clamp(ReflectionBlur, 0, 8) : 0;
        RecentLimit = Math.Clamp(RecentLimit, 0, 8);
        RecentApps ??= new();
        RecentApps.RemoveAll(p => p is null || p.Kind != ItemKind.Application || string.IsNullOrWhiteSpace(p.Target));
        if (RecentApps.Count > 8) RecentApps.RemoveRange(8, RecentApps.Count - 8);
        MonitorDevice ??= "";
        Pins ??= new();
        Pins.RemoveAll(p => p is null || string.IsNullOrWhiteSpace(p.Target) || !Enum.IsDefined(p.Kind));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pin in Pins)
        {
            if (string.IsNullOrEmpty(pin.Id) || !ids.Add(pin.Id))
            {
                pin.Id = Guid.NewGuid().ToString("N");
                ids.Add(pin.Id);
            }
            if (string.IsNullOrWhiteSpace(pin.Name)) pin.Name = LocalizedText.Get("应用");
            pin.ExecutablePath ??= "";
            pin.Arguments ??= ""; pin.WorkingDirectory ??= ""; pin.SourceShortcut ??= "";
            pin.ManagedShortcut ??= ""; pin.IconLocation ??= ""; pin.AppUserModelId ??= "";
        }
    }

    public DockSettings Copy()
    {
        var copy = (DockSettings)MemberwiseClone();
        copy.Appearance = Appearance.Copy();
        copy.ControlCenter = ControlCenter.Copy();
        copy.Widgets = Widgets.Copy();
        copy.Pins = new List<PinnedItem>(Pins);
        copy.RecentApps = new List<PinnedItem>(RecentApps);
        return copy;
    }

    public void ApplyPreferences(DockSettings source)
    {
        Edge = source.Edge; Theme = source.Theme; Appearance = source.Appearance.Copy();
        InterfaceTheme = source.InterfaceTheme; InterfaceAccent = source.InterfaceAccent; Language = source.Language;
        InterfaceStyle = source.InterfaceStyle;
        HideWhenFullscreen = source.HideWhenFullscreen;
        GlassIntensity = source.GlassIntensity;
        GlassDistortion = source.GlassDistortion;
        GlassHighlight = source.GlassHighlight; GlassHighlightWidth = source.GlassHighlightWidth; GlassSideDarkness = source.GlassSideDarkness;
        GlassTint = source.GlassTint;
        GlassOpacity = source.GlassOpacity; GlassFrost = source.GlassFrost;
        GlassGrain = source.GlassGrain;
        LiquidGlass = source.LiquidGlass; GlassQuality = source.GlassQuality; GlassTintColor = source.GlassTintColor;
        IconSize = source.IconSize; Spacing = source.Spacing; Magnification = source.Magnification;
        HoverLift = source.HoverLift; EdgeDistance = source.EdgeDistance;
        AutoHide = source.AutoHide; HideOnDesktop = source.HideOnDesktop; StartWithWindows = source.StartWithWindows;
        UseGlassControlCenter = source.UseGlassControlCenter;
        HideTaskbarApplications = source.HideTaskbarApplications;
        ControlCenter = source.ControlCenter.Copy();
        Widgets = source.Widgets.Copy();
        Animations = source.Animations; ShowRunningApps = source.ShowRunningApps;
        ShowRecentApps = source.ShowRecentApps; RecentLimit = source.RecentLimit;
        BounceHeight = source.BounceHeight; BounceDuration = source.BounceDuration;
        Reflections = source.Reflections; ReflectionOpacity = source.ReflectionOpacity; ReflectionLength = source.ReflectionLength;
        ReflectionGap = source.ReflectionGap; ReflectionBlur = source.ReflectionBlur;
        MonitorDevice = source.MonitorDevice;
        Normalize();
    }
}

public sealed record TrackedWindow(nint Handle, uint ProcessId, string Title, string ExecutablePath);

public sealed class DockItem : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _needsAttention;
    public bool NeedsAttention {get=>_needsAttention;set{if(_needsAttention==value)return;_needsAttention=value;OnChanged();}}
    private IReadOnlyList<TrackedWindow> _windows = Array.Empty<TrackedWindow>();
    public PinnedItem Pin { get; }
    public bool IsPinned { get; set; }
    public ImageSource? Icon { get => _icon; set { _icon = value; OnChanged(); } }
    public string Name => Pin.Name;
    public IReadOnlyList<TrackedWindow> Windows
    {
        get => _windows;
        set { _windows = value; OnChanged(); OnChanged(nameof(IsRunning)); }
    }
    public bool IsRunning => Windows.Count > 0;
    public DockItem(PinnedItem pin, bool pinned = true) { Pin = pin; IsPinned = pinned; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}



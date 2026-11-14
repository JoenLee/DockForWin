using System;
using System.Collections.Generic;
using System.Linq;

namespace DockForWindows.Models;

public enum ControlModule { Wifi, Bluetooth, Media, Focus, Projection, Brightness, Volume, Battery, DarkMode, PowerSaver, RefreshRate, Performance, AudioOutput, Hdr, Screenshot, ScreenRecording, KeepAwake, Microphone, VoiceInput, NightLight, Airplane, MobileHotspot }
public enum ControlCardShape { Circle, Capsule, Square, Rectangle, Large, Strip }
public sealed class ControlCenterGeometry
{
    public double PanelWidth {get;set;}=345;
    public double PanelScale {get;set;}=1;
    public double ComponentScale {get;set;}=1;
    public double Gap {get;set;}=12;
    public double FontSize {get;set;}=12;
    public double IconSize {get;set;}=18;
    public double ContentGap {get;set;}=6;
    public double CardPadding {get;set;}=18;
    public double CapsuleRoundness {get;set;}=99;
    public double SquareRoundness {get;set;}=41;
    public double MediaRoundness {get;set;}=41;
    public double RectangleRoundness {get;set;}=28;
    public double DetailRoundness {get;set;}=24;
    public double MediaDetailWidth {get;set;}=345;
    public double MediaDetailHeight {get;set;}=620;
    public string ContentAlignment {get;set;}="Center";
    public ControlCenterGeometry Copy()=>(ControlCenterGeometry)MemberwiseClone();
    public void Normalize()
    {
        double Clamp(double v,double min,double max,double fallback)=>double.IsFinite(v)?Math.Clamp(v,min,max):fallback;
        PanelWidth=Clamp(PanelWidth,300,480,345);Gap=Clamp(Gap,6,24,12);FontSize=Clamp(FontSize,11,16,12);IconSize=Clamp(IconSize,16,30,18);ContentGap=Clamp(ContentGap,4,16,6);CardPadding=Clamp(CardPadding,8,22,18);
        MediaDetailWidth=PanelWidth-36;MediaDetailHeight=620;
        PanelScale=Clamp(PanelScale,.65,1.5,1);ComponentScale=Clamp(ComponentScale,.75,1.5,1);
        CapsuleRoundness=Clamp(CapsuleRoundness,0,100,99);SquareRoundness=Clamp(SquareRoundness,0,100,41);RectangleRoundness=Clamp(RectangleRoundness,0,100,28);DetailRoundness=Clamp(DetailRoundness,0,35,24);MediaRoundness=Clamp(MediaRoundness,0,100,41);ContentAlignment=ContentAlignment=="Left"?"Left":"Center";
    }
}
public sealed class ControlModuleLayout
{
    public ControlModule Module {get;set;}
    public int Span {get;set;}=2;
    public ControlCardShape? Shape {get;set;}
    [System.Text.Json.Serialization.JsonIgnore]
    public ControlCardShape EffectiveShape=>Shape??(Span==1?ControlCardShape.Circle:Span==4?ControlCardShape.Rectangle:Module==ControlModule.Media?ControlCardShape.Square:ControlCardShape.Capsule);
    public void SetShape(ControlCardShape shape){if(Module==ControlModule.Media&&shape is ControlCardShape.Circle or ControlCardShape.Capsule)shape=ControlCardShape.Square;if(Module!=ControlModule.Media&&shape is ControlCardShape.Large or ControlCardShape.Strip)shape=ControlCardShape.Rectangle;Shape=shape;Span=shape==ControlCardShape.Circle?1:shape is ControlCardShape.Rectangle or ControlCardShape.Large or ControlCardShape.Strip?4:2;}
    public string PageId {get;set;}="main";
    public string Group {get;set;}="";
    public string DisplayName {get;set;}="";
    public string Glyph {get;set;}="";
    public bool Visible {get;set;}=true;
    public ControlModuleLayout Copy()=>(ControlModuleLayout)MemberwiseClone();
}
public enum ControlCenterColorMode { System, Light, Dark }
public enum ControlCenterMaterialMode { LiquidGlass, ClassicBlur }
public sealed class ControlCenterLayoutPage
{public string Id{get;set;}="main";public string Name{get;set;}=LocalizedText.Get("控制中心");public ControlCenterLayoutPage Copy()=>(ControlCenterLayoutPage)MemberwiseClone();}
public sealed class ControlCenterOptions
{
    public ControlCenterGeometry Geometry {get;set;}=new();
    public List<ControlModuleLayout> Modules {get;set;}=Defaults();
    public List<ControlCenterLayoutPage> Pages{get;set;}=new(){new()};
    public string ActivePageId{get;set;}="main";
    public ControlCenterColorMode ColorMode{get;set;}
    public ControlCenterMaterialMode MaterialMode{get;set;}
    public bool OverrideMaterial {get;set;}
    public double Intensity {get;set;}=.55;
    public double Distortion {get;set;}=.65;
    public double Frost {get;set;}=.1;
    public double Opacity {get;set;}=1;
    public double Tint {get;set;}=.06;
    public double Highlight {get;set;}=.55;
    public double HighlightWidth {get;set;}=2.5;
    public double Grain {get;set;}=.45;
    public string Hotkey {get;set;}="Ctrl+Win+A";
    public int LayoutVersion {get;set;}
    // 参考图布局：顶部两枚圆形按钮 + 右侧音乐大卡，Wi-Fi 胶囊居左第二行，
    // 然后两条通栏滑块（显示器/声音），底部专注与电池并列，深色/省电收尾。
    public static List<ControlModuleLayout> Defaults()=>new(){new(){Module=ControlModule.Projection,Span=1},new(){Module=ControlModule.Bluetooth,Span=1},new(){Module=ControlModule.Media},new(){Module=ControlModule.Wifi},new(){Module=ControlModule.Brightness,Span=4},new(){Module=ControlModule.Volume,Span=4},new(){Module=ControlModule.Focus},new(){Module=ControlModule.Battery},new(){Module=ControlModule.DarkMode},new(){Module=ControlModule.PowerSaver}};
    public void Normalize()
    {
        Geometry??=new();Geometry.Normalize();
        if(!Enum.IsDefined(MaterialMode))MaterialMode=ControlCenterMaterialMode.LiquidGlass;
        if(LayoutVersion<2){if(Modules is null||Modules.Count==0)Modules=Defaults();LayoutVersion=2;}
        if(LayoutVersion<3){Geometry.MediaRoundness=Geometry.SquareRoundness;Geometry.DetailRoundness=Math.Clamp(Geometry.DetailRoundness/Math.Max(1,(Geometry.PanelWidth-36)/2)*100,0,100);LayoutVersion=3;}
        Pages??=new();Pages=Pages.Where(p=>p is not null&&!string.IsNullOrWhiteSpace(p.Id)).DistinctBy(p=>p.Id).Take(12).ToList();if(!Pages.Any(p=>p.Id=="main"))Pages.Insert(0,new());foreach(var p in Pages)p.Name=Clean(p.Name,LocalizedText.Get("控制中心"));if(!Pages.Any(p=>p.Id==ActivePageId))ActivePageId="main";if(!Enum.IsDefined(ColorMode))ColorMode=ControlCenterColorMode.System;
        Modules??=Defaults();var seen=new HashSet<ControlModule>();
        Modules=Modules.Where(m=>m is not null&&Enum.IsDefined(m.Module)&&seen.Add(m.Module)).ToList();
        foreach(var kind in Enum.GetValues<ControlModule>())if(seen.Add(kind))Modules.Add(new(){Module=kind,Visible=false});
        foreach(var m in Modules){if(!Pages.Any(p=>p.Id==m.PageId))m.PageId="main";m.DisplayName=Clean(m.DisplayName,"");m.Group=Clean(m.Group,"");m.Glyph=Clean(m.Glyph,"");m.Span=m.Span is 1 or 2 or 4?m.Span:2;if(m.Shape is ControlCardShape shape&&!Enum.IsDefined(shape))m.Shape=null;m.SetShape(m.EffectiveShape);}
        double Unit(double v,double fallback)=>double.IsFinite(v)?Math.Clamp(v,0,1):fallback;
        Intensity=Unit(Intensity,.55);Distortion=Unit(Distortion,.65);Frost=Unit(Frost,.1);Opacity=Unit(Opacity,1);Tint=Math.Min(.25,Unit(Tint,.06));Highlight=Unit(Highlight,.55);Grain=Unit(Grain,.45);
        HighlightWidth=double.IsFinite(HighlightWidth)?Math.Clamp(HighlightWidth,.5,8):2.5;
        Hotkey=string.IsNullOrWhiteSpace(Hotkey)?"Ctrl+Win+A":Hotkey;
    }
    private static string Clean(string? text,string fallback){string value=new((text??"").Where(c=>!char.IsControl(c)).Take(40).ToArray());return string.IsNullOrWhiteSpace(value)?fallback:value.Trim();}
    public ControlCenterOptions Copy(){var copy=(ControlCenterOptions)MemberwiseClone();copy.Modules=Modules.Select(m=>m.Copy()).ToList();copy.Geometry=Geometry.Copy();copy.Pages=Pages.Select(p=>p.Copy()).ToList();return copy;}
    public DockSettings Material(DockSettings global)
    {
        var s=global.Copy();if(!OverrideMaterial)return s;
        s.GlassIntensity=Intensity;s.GlassDistortion=Distortion;s.GlassFrost=Frost;s.GlassOpacity=Opacity;s.GlassTint=Tint;s.GlassHighlight=Highlight;s.GlassHighlightWidth=HighlightWidth;s.GlassGrain=Grain;return s;
    }
}


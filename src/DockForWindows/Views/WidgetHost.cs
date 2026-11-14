using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using DockForWindows.Models;
using Path = System.Windows.Shapes.Path;

namespace DockForWindows.Views;

internal static class WidgetBrushes
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new();
    private static readonly Dictionary<string, Brush> Gradients = new();
    internal static SolidColorBrush Get(string color)
    {
        if (Cache.TryGetValue(color, out var cached)) return cached;
        var brush = Palette.Brush(color); brush.Freeze(); if (Cache.Count >= 128) Cache.Clear(); Cache.Add(color, brush); return brush;
    }
    internal static Brush Surface(string color, bool gradient)
    {
        if (!gradient) return Get(color);
        if (Gradients.TryGetValue(color, out var cached)) return cached;
        var basis = Get(color).Color;
        byte Blend(byte value, double factor) => (byte)Math.Clamp(value * factor, 0, 255);
        var brush = new LinearGradientBrush(Color.FromArgb(basis.A,Blend(basis.R,1.15),Blend(basis.G,1.15),Blend(basis.B,1.15)),
            Color.FromArgb(basis.A,Blend(basis.R,.78),Blend(basis.G,.78),Blend(basis.B,.78)), new Point(0,0), new Point(1,1));
        brush.Freeze(); if(Gradients.Count>=64) Gradients.Clear(); Gradients.Add(color,brush); return brush;
    }
}

internal static class WidgetGeometry
{
    private static readonly Dictionary<(double Width, double Height, double N), Geometry> Cache = new();
    internal static int CachedCount => Cache.Count;
    internal static Geometry Get(double width, double height, double exponent)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0 || exponent is not (2.5 or 3))
            throw new ArgumentOutOfRangeException(nameof(width));
        var key = (width, height, exponent);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        // The user's visual reference uses short continuous corners, with long straight card edges.
        double radius = Math.Min(20, Math.Min(width, height) * .19);
        var shape = DockSurface.CreateGeometry(new Size(width, height), radius / (Math.Min(width, height) / 2) * 100, exponent: exponent);
        if (Cache.Count >= 48) Cache.Clear(); // Only discrete widget sizes are cached; cap unexpected future size combinations.
        Cache.Add(key, shape); return shape;
    }
}

internal sealed class WidgetHost : Grid
{
    private readonly Path _plate, _shadow, _tint;
    private readonly Grid _content;
    private readonly Grid _visual;
    private readonly ScaleTransform _visualScale=new(1,1);
    private bool _gestureActive;
    private static readonly DependencyProperty HoverScaleProperty=DependencyProperty.Register(nameof(HoverScale),typeof(double),typeof(WidgetHost),
        new FrameworkPropertyMetadata(1d,(owner,_)=>((WidgetHost)owner).UpdateVisualScale()));
    internal double HoverScale=>(double)GetValue(HoverScaleProperty);
    internal double PanelScale {get;private set;}=1;
    internal double VisualScale=>PanelScale*HoverScale;
    internal event Action? VisualScaleChanged;
    private static Brush? _glass;
    private static readonly DropShadowEffect Shadow = CreateShadow();
    private double _shadowStrength=1,_shadowBaseOpacity=.14;
    internal void UseGalleryShadow(){_shadowBaseOpacity=.19;_shadow.Effect=new DropShadowEffect{BlurRadius=24,ShadowDepth=8,Direction=270,Opacity=.19,Color=Color.FromRgb(39,69,74),RenderingBias=RenderingBias.Performance};}
    internal WidgetDefinition Definition { get; private set; }
    internal WidgetMaterial Material { get; private set; }
    internal Geometry Outline { get; private set; } = null!;
    private Services.WidgetWeatherSnapshot? _weather;
    internal void SetWeather(Services.WidgetWeatherSnapshot? weather)=>_weather=weather;
    internal WidgetHost(WidgetDefinition definition, double exponent)
    {
        Definition = definition; Width = definition.Size.Width; Height = definition.Size.Height;Background=Brushes.Transparent;
        _shadow = new Path { Fill = Brushes.Black, Effect = Shadow, IsHitTestVisible = false, CacheMode = new BitmapCache() };
        _plate = new Path { IsHitTestVisible = false }; _tint = new Path { IsHitTestVisible = false };
        _content = new Grid();
        _visual=new Grid{Width=Width,Height=Height,RenderTransformOrigin=new Point(.5,.5),RenderTransform=_visualScale};
        _visual.Children.Add(_shadow); _visual.Children.Add(_plate); _visual.Children.Add(_tint); _visual.Children.Add(_content);Children.Add(_visual);
        SetExponent(exponent); SetMaterial(WidgetMaterial.Light);
        MouseEnter+=(_,_)=>{if(!_gestureActive)ControlMotion.To(this,HoverScaleProperty,1.025,speed:2.4);};
        MouseLeave+=(_,_)=>{if(!_gestureActive)ControlMotion.To(this,HoverScaleProperty,1,speed:2.4);};
        Unloaded+=(_,_)=>{_gestureActive=false;ControlMotion.Stop(this,HoverScaleProperty,1);};
    }
    private void UpdateVisualScale(){_visualScale.ScaleX=_visualScale.ScaleY=VisualScale;VisualScaleChanged?.Invoke();}
    internal Rect VisualBounds(Visual ancestor)=>_visual.TransformToAncestor(ancestor).TransformBounds(new Rect(_visual.RenderSize));
    internal void SetGestureActive(bool active){if(_gestureActive==active)return;_gestureActive=active;ControlMotion.Stop(this,HoverScaleProperty,1);if(!active&&IsMouseOver)ControlMotion.To(this,HoverScaleProperty,1.025,speed:2.4);}
    internal void Resize(WidgetSize size){Definition=Definition with {Size=size};Width=size.Width;Height=size.Height;_visual.Width=Width;_visual.Height=Height;SetExponent(_exponent);VisualScaleChanged?.Invoke();}
    private double _exponent=2.5;
    private bool _active=true;
    internal bool SetActive(bool active){if(_active==active)return false;_active=active;_content.Visibility=active?Visibility.Visible:Visibility.Hidden;return true;}
    internal void SetContent(FrameworkElement content)
    {
        _content.Children.Clear(); _content.Children.Add(content);
    }
    internal void SetExponent(double exponent)
    {
        _exponent=exponent;Outline = WidgetGeometry.Get(Width, Height, exponent);
        _plate.Data = _shadow.Data = _tint.Data = Outline;
        _content.Clip = Outline;
    }
    internal void SetMaterial(WidgetMaterial material, WidgetCardColors? colors = null)
    {
        Material = material; bool dark = material == WidgetMaterial.Dark;
        var defaults = Definition.Layout switch {
            WidgetLayout.WeatherHourly => (Background:dark?"#122949":"#327FBB",Ink:"#F8FCFF",Accent:"#DBF3FF",Gradient:true),
            WidgetLayout.CalendarGiant or WidgetLayout.CalendarMinimal => (Background:dark?"#10233B":"#EEF1F6",Ink:dark?"#A7C5FF":"#125BCE",Accent:"#EB786D",Gradient:false),
            WidgetLayout.CalendarDate => (Background:dark?"#242325":"#FFFDFC",Ink:dark?"#FFF9F1":"#252228",Accent:"#D05C51",Gradient:false),
            _ when Definition.Kind==WidgetKind.Music => (Background:"#202124",Ink:"#FFFFFF",Accent:"#FF708B",Gradient:false),
            _ when Definition.Kind==WidgetKind.Notes => (Background:dark?"#282724":"#FFFDF8",Ink:dark?"#FFF9EC":"#25251F",Accent:"#B68113",Gradient:false),
            _ when Definition.Kind==WidgetKind.Reminders => (Background:dark?"#24262A":"#FEFDFC",Ink:dark?"#FCFAF5":"#25262A",Accent:"#E27D40",Gradient:false),
            WidgetLayout.WeatherPoster => (Background:dark?"#123B55":"#237AA1",Ink:"#F5FCFF",Accent:"#FFE29B",Gradient:true),
            WidgetLayout.WeatherOutlook => (Background:dark?"#243C49":"#E8F2F6",Ink:dark?"#F0F8FB":"#274856",Accent:dark?"#97D8EE":"#237D9B",Gradient:false),
            WidgetLayout.CalendarPoster => (Background:dark?"#10263D":"#183C61",Ink:"#F5F7FC",Accent:"#FFB5A2",Gradient:false),
            WidgetLayout.CalendarWeather => (Background:dark?"#1B2429":"#EAF0EC",Ink:dark?"#F3F8F5":"#253C35",Accent:dark?"#A7D9C9":"#387E69",Gradient:false),
            WidgetLayout.CalendarClock => (Background:dark?"#102939":"#174565",Ink:"#FFE06B",Accent:"#FFE06B",Gradient:true),
            _ => (Background:dark?"#253135":"#FCFEFE",Ink:dark?"#F3F7F7":"#24383B",Accent:dark?"#7FD4CD":"#167F79",Gradient:false) };
        if(Definition.Layout is WidgetLayout.WeatherHourly or WidgetLayout.WeatherPoster&&_weather!=null){
            defaults=(!_weather.IsDay?"#192C52":_weather.Code>=51?"#536C84":_weather.Code>=3?"#7A92A3":"#327FBB","#F8FCFF","#DBF3FF",true);
        }
        bool customBackground = ThemeAppearance.TryColor(colors?.Background,out var background);
        if(!customBackground) background=defaults.Background;
        var basis=WidgetBrushes.Get(background).Color;
        _shadow.Fill=WidgetBrushes.Get(background);
        dark=AdaptiveGlassAppearance.LightIndicator((.2126*basis.R+.7152*basis.G+.0722*basis.B)/255,null);
        string ink=ThemeAppearance.TryColor(colors?.Foreground,out var customInk)?customInk:customBackground?(dark?"#F3F7F7":"#24383B"):defaults.Ink;
        string accent=ThemeAppearance.TryColor(colors?.Accent,out var customAccent)?customAccent:customBackground?(dark?"#7FD4CD":"#167F79"):defaults.Accent;
        Resources["WidgetInk"] = WidgetBrushes.Get(ink);
        Resources["WidgetMuted"] = WidgetBrushes.Get("#B8"+ink[1..]);
        Resources["WidgetAccent"] = WidgetBrushes.Get(accent);
        Resources["WidgetTrack"] = WidgetBrushes.Get(dark ? "#334B4C" : "#E5EFED");
        Resources["WidgetBackground"] = WidgetBrushes.Get(background);
        Resources["WidgetGradient"] = colors?.Gradient ?? defaults.Gradient;
        Resources["WidgetNumberStyle"] = colors?.NumberStyle??WidgetNumberStyle.Standard;
        Resources["WidgetTextWeight"] = colors?.TextWeight??WidgetTextWeight.Automatic;
        Resources["WidgetTextScale"] = colors?.TextScale??1d;
        double strength=Math.Clamp(colors?.Shadow??1,0,2);
        if(Math.Abs(strength-_shadowStrength)>.000001&&_shadow.Effect is DropShadowEffect shadow){_shadowStrength=strength;if(shadow.IsFrozen){shadow=shadow.Clone();_shadow.Effect=shadow;}shadow.Opacity=_shadowBaseOpacity*strength;}_plate.Opacity=_tint.Opacity=colors?.Opacity??1;
        if(colors?.Curvature is double curve)SetExponent(curve);
        double panelScale=colors?.PanelScale??1;
        if(Math.Abs(PanelScale-panelScale)>.000001){PanelScale=panelScale;UpdateVisualScale();}
        _plate.Fill = material == WidgetMaterial.Glass ? GlassBrush() : WidgetBrushes.Surface(background,colors?.Gradient??defaults.Gradient);
        _tint.Fill = material == WidgetMaterial.Glass ? WidgetBrushes.Surface("#BF"+background[1..],colors?.Gradient??defaults.Gradient) : Brushes.Transparent;
        if(ThemeAppearance.TryColor(colors?.GradientEnd,out var gradientEnd)){
            var fill=new LinearGradientBrush(WidgetBrushes.Get(background).Color,WidgetBrushes.Get(gradientEnd).Color,45);fill.Freeze();
            if(material==WidgetMaterial.Glass){fill=fill.Clone();fill.Opacity=.75;fill.Freeze();_tint.Fill=fill;}else _plate.Fill=fill;
        }
        if (SystemParameters.HighContrast) {
            _plate.Fill = SystemColors.WindowBrush; _tint.Fill = Brushes.Transparent;
            Resources["WidgetInk"] = Resources["WidgetMuted"] = SystemColors.WindowTextBrush;
            Resources["WidgetAccent"] = SystemColors.HighlightBrush;
        }
        VisualScaleChanged?.Invoke();
    }
    private static DropShadowEffect CreateShadow()
    {
        var effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 7, Direction = 270, Opacity = .14, Color = Color.FromRgb(39, 69, 74), RenderingBias = RenderingBias.Performance };
        effect.Freeze(); return effect;
    }
    private static Brush GlassBrush()
    {
        if (_glass != null) return _glass;
        var image = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/DockForWindows;component/Assets/PreviewWallpaper.png")),
            Stretch = Stretch.UniformToFill, Width = 512, Height = 256, Effect = new BlurEffect { Radius = 24 } };
        image.Measure(new Size(512, 256)); image.Arrange(new Rect(0, 0, 512, 256));
        var snapshot = new RenderTargetBitmap(512, 256, 96, 96, PixelFormats.Pbgra32); snapshot.Render(image); snapshot.Freeze();
        var brush = new ImageBrush(snapshot) { Stretch = Stretch.UniformToFill }; brush.Freeze();
        return _glass = brush;
    }
}

internal static class WidgetIcons
{
    private static readonly Dictionary<string, Geometry> Cache = new();
    internal static FrameworkElement Weather(int code, bool isDay, double size = 24)
    {
        if (code is 0 or 1) {
            var icon = new ControlCenterIcon(isDay ? "Brightness" : "Focus") { Width = size, Height = size };
            icon.SetResourceReference(ControlCenterIcon.BrushProperty, "WidgetAccent"); return icon;
        }
        return Create(code switch { 2 => "partly-sunny", 71 or 73 or 75 or 77 or 85 or 86 => "snow", >= 51 => "rainy", _ => "cloud" }, size);
    }
    internal static Path Create(string name, double size)
    {
        if (!Cache.TryGetValue(name, out var geometry)) {
            var resource = Application.GetResourceStream(new Uri("/DockForWindows;component/Assets/Ionicons/" + name + ".svg", UriKind.Relative))!;
            using var stream = resource.Stream;
            var drawing = new GeometryGroup { FillRule = FillRule.Nonzero };
            // These official Filled assets contain path elements only; the existing control-center SVG loader stays untouched.
            foreach (var path in XDocument.Load(stream).Descendants().Where(e => e.Name.LocalName == "path")) drawing.Children.Add(Geometry.Parse("F1 " + path.Attribute("d")!.Value));
            var bounds = drawing.Bounds; double cx = 0, cy = 0, count = 0;
            for (int y = 0; y < 40; y++) for (int x = 0; x < 40; x++) {
                var p = new Point(bounds.X + (x + .5) * bounds.Width / 40, bounds.Y + (y + .5) * bounds.Height / 40);
                if (drawing.FillContains(p)) { cx += p.X; cy += p.Y; count++; }
            }
            double extent = Math.Max(bounds.Width, bounds.Height), centerX = count > 0 ? cx / count : bounds.X + bounds.Width / 2,
                centerY = count > 0 ? cy / count : bounds.Y + bounds.Height / 2;
            drawing.Transform = new MatrixTransform(1 / extent, 0, 0, 1 / extent, .5 - centerX / extent, .5 - centerY / extent);
            drawing.Freeze(); geometry = drawing; Cache.Add(name, geometry);
        }
        var icon = new Path { Data = geometry, Width = size, Height = size, Stretch = Stretch.Uniform, IsHitTestVisible = false };
        icon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "WidgetAccent"); return icon;
    }
}

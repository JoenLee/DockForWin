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
    private static readonly Dictionary<(double Width, double Height, double N, double Radius), Geometry> Cache = new();
    internal static int CachedCount => Cache.Count;
    internal static Geometry Get(double width, double height, double exponent, double? cornerRadius = null)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0 || exponent is not (2.5 or 3))
            throw new ArgumentOutOfRangeException(nameof(width));
        double radius = cornerRadius ?? Math.Min(20, Math.Min(width, height) * .19);
        var key = (width, height, exponent, radius);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        // The user's visual reference uses short continuous corners, with long straight card edges.
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
    private DockSurface? _glass;
    private WidgetGlassBackdrop? _capture;
    private readonly AdaptiveGlassAppearance _contrast=new();
    private bool? _lightInk;
    private bool _customInk;
    private Rect _glassPane=Rect.Empty;
    private WidgetDesktopWindow? _glassOwner;
    private readonly ScaleTransform _visualScale=new(1,1);
    private bool _gestureActive;
    private static readonly DependencyProperty HoverScaleProperty=DependencyProperty.Register(nameof(HoverScale),typeof(double),typeof(WidgetHost),
        new FrameworkPropertyMetadata(1d,(owner,_)=>((WidgetHost)owner).UpdateVisualScale()));
    internal double HoverScale=>(double)GetValue(HoverScaleProperty);
    internal double PanelScale {get;private set;}=1;
    internal double VisualScale=>PanelScale*HoverScale;
    internal event Action? VisualScaleChanged;
    private static readonly DropShadowEffect Shadow = CreateShadow();
    private bool _galleryShadow;
    private double _shadowStrength=1,_shadowBaseOpacity=.14;
    internal void UseGalleryShadow(){_galleryShadow=true;_shadowBaseOpacity=.19;_shadow.Effect=new DropShadowEffect{BlurRadius=24,ShadowDepth=8,Direction=270,Opacity=.19,Color=Color.FromRgb(39,69,74),RenderingBias=RenderingBias.Performance};}
    internal WidgetDefinition Definition { get; private set; }
    internal WidgetMaterial Material { get; private set; }
    internal WidgetCardColors CardAppearance {get;private set;}=new();
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
        Loaded+=(_,_)=>UpdateGlassCapture();
        Unloaded+=(_,_)=>{_gestureActive=false;ControlMotion.Stop(this,HoverScaleProperty,1);ReleaseGlassCapture();};
    }
    private void UpdateVisualScale(){_visualScale.ScaleX=_visualScale.ScaleY=VisualScale;VisualScaleChanged?.Invoke();}
    internal Rect VisualBounds(Visual ancestor)=>_visual.TransformToAncestor(ancestor).TransformBounds(new Rect(_visual.RenderSize));
    internal void SetGestureActive(bool active){if(_gestureActive==active)return;_gestureActive=active;ControlMotion.Stop(this,HoverScaleProperty,1);}
    internal void Resize(WidgetSize size){Definition=Definition with {Size=size};Width=size.Width;Height=size.Height;_visual.Width=Width;_visual.Height=Height;SetExponent(_exponent);VisualScaleChanged?.Invoke();}
    private double _exponent=2.5;
    internal double CornerExponent=>_exponent;
    private bool _active=true;
    internal bool SetActive(bool active){if(_active==active)return false;_active=active;_content.Visibility=active?Visibility.Visible:Visibility.Hidden;SetMotionActive(active);UpdateGlassCapture();return true;}
    internal void SetContent(FrameworkElement content)
    {
        if(_content.Children.Count==1&&_content.Children[0] is FigmaWidgetView existing&&content is FigmaWidgetView incomingFigma&&existing.Definition==incomingFigma.Definition){existing.Update(incomingFigma.Data);return;}
        if(_content.Children.Count==1&&_content.Children[0] is WidgetMusicView current&&content is WidgetMusicView incoming&&current.Definition==incoming.Definition){current.Update(incoming.Data);return;}
        _content.Children.Clear(); _content.Children.Add(content);
    }
    internal void SetData(WidgetRenderData data)
    {
        if(_content.Children.Count==1&&_content.Children[0] is FigmaWidgetView figma&&figma.Definition==Definition){figma.Update(data);return;}
        if(_content.Children.Count==1&&_content.Children[0] is WidgetMusicView player&&player.Definition==Definition)player.Update(data);
        else SetContent(WidgetContent.Render(Definition,data));
    }
    internal void SetMotionActive(bool active){if(_content.Children.Count==1&&_content.Children[0] is FigmaWidgetView figma)figma.SetActive(active);if(_content.Children.Count==1&&_content.Children[0] is WidgetMusicView player)player.SetActive(active);}
    private double OutlineRadius => Math.Min(20,Math.Min(Width,Height)*.19);
    internal void SetExponent(double exponent)
    {
        _exponent=exponent;Outline = WidgetGeometry.Get(Width, Height, exponent, OutlineRadius);
        _plate.Data = _shadow.Data = _tint.Data = Outline;
        _content.Clip = Outline;
        if(_glass is not null){_glass.Radius=OutlineRadius/(Math.Min(Width,Height)/2)*100;_glass.CornerExponent=exponent;_glass.Clip=Outline;_glass.InvalidateVisual();}
    }
    internal void SetMaterial(WidgetMaterial material, WidgetCardColors? colors = null)
    {
        CardAppearance=colors?.Copy()??new(); Material = material; bool dark = material == WidgetMaterial.Dark;
        _customInk=ThemeAppearance.TryColor(colors?.Foreground,out _);
        _lightInk=null;
        var documentClock=DocumentClockStyles.Find(Definition.Id);
        var defaults = Definition.Layout switch {
            _ when documentClock is {} css => (Background:css.Background,Ink:css.Ink,Accent:css.Accent,Gradient:false),
            _ when WidgetSampleTokens.Applies(Definition.Layout) => (Background:dark?"#253135":"#FCFEFE",Ink:dark?"#F3F7F7":"#24383B",Accent:dark?"#7FD4CD":"#167F79",Gradient:false),
            WidgetLayout.WeatherHourly => (Background:dark?"#122949":"#327FBB",Ink:"#F8FCFF",Accent:"#DBF3FF",Gradient:true),
            WidgetLayout.CalendarGiant or WidgetLayout.CalendarMinimal => (Background:dark?"#10233B":"#EEF1F6",Ink:dark?"#A7C5FF":"#125BCE",Accent:"#EB786D",Gradient:false),
            WidgetLayout.CalendarDate => (Background:dark?"#242325":"#FFFDFC",Ink:dark?"#FFF9F1":"#252228",Accent:"#D05C51",Gradient:false),
            _ when Definition.Layout is WidgetLayout.ClockDot or WidgetLayout.ClockDotWide or WidgetLayout.ClockOrbit or WidgetLayout.CalendarDot or WidgetLayout.CalendarMiniGrid or WidgetLayout.WeatherDot or WidgetLayout.WeatherSplit or WidgetLayout.ProgressRings or WidgetLayout.CountdownDot or WidgetLayout.CountdownWide => (Background:dark?"#1D1F23":"#F7F5F1",Ink:dark?"#F6F6F5":"#21232A",Accent:dark?"#FF9B45":"#DE6038",Gradient:false),
            WidgetLayout.MusicFrosted => (Background:dark?"#25304D":"#D6D6EE",Ink:dark?"#F7F7FF":"#202337",Accent:dark?"#B6B2FF":"#6455B6",Gradient:true),
            WidgetLayout.MusicRetro or WidgetLayout.MusicCassette => (Background:dark?"#2B2C2C":"#E9E9E6",Ink:dark?"#F7F7F5":"#222326",Accent:dark?"#C4CEC9":"#53625B",Gradient:false),
            _ when Definition.Kind==WidgetKind.AiUsage => (Background:"#101216",Ink:"#F8F9FC",Accent:Definition.Layout==WidgetLayout.AiClaude?"#FFAC65":"#99EA71",Gradient:false),
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
        double strength=_galleryShadow?1:Math.Clamp(colors?.Shadow??1,0,2);
        if(Math.Abs(strength-_shadowStrength)>.000001&&_shadow.Effect is DropShadowEffect shadow){_shadowStrength=strength;if(shadow.IsFrozen){shadow=shadow.Clone();_shadow.Effect=shadow;}shadow.Opacity=_shadowBaseOpacity*strength;}_plate.Opacity=_tint.Opacity=colors?.Opacity??1;
        if(colors?.Curvature is double curve)SetExponent(curve);
        double panelScale=colors?.PanelScale??1;
        if(Math.Abs(PanelScale-panelScale)>.000001){PanelScale=panelScale;UpdateVisualScale();}
        _plate.Fill = WidgetBrushes.Surface(background,colors?.Gradient??defaults.Gradient);
        _tint.Fill = material == WidgetMaterial.Glass ? WidgetBrushes.Surface($"#{(byte)Math.Round((colors?.GlassTint??.035)*255):X2}"+background[1..],colors?.Gradient??defaults.Gradient) : Brushes.Transparent;
        if(ThemeAppearance.TryColor(colors?.GradientEnd,out var gradientEnd)){
            var fill=new LinearGradientBrush(WidgetBrushes.Get(background).Color,WidgetBrushes.Get(gradientEnd).Color,45);fill.Freeze();
            if(material==WidgetMaterial.Glass){fill=fill.Clone();fill.Opacity=colors?.GlassTint??.035;fill.Freeze();_tint.Fill=fill;}else _plate.Fill=fill;
        }
        if (SystemParameters.HighContrast) {
            _plate.Fill = SystemColors.WindowBrush; _tint.Fill = Brushes.Transparent;
            Resources["WidgetInk"] = Resources["WidgetMuted"] = SystemColors.WindowTextBrush;
            Resources["WidgetAccent"] = SystemColors.HighlightBrush;
        }
        ConfigureGlass(colors);
        VisualScaleChanged?.Invoke();
    }
    private static DropShadowEffect CreateShadow()
    {
        var effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 7, Direction = 270, Opacity = .14, Color = Color.FromRgb(39, 69, 74), RenderingBias = RenderingBias.Performance };
        effect.Freeze(); return effect;
    }
    private void ConfigureGlass(WidgetCardColors? colors)
    {
        bool enabled=Material==WidgetMaterial.Glass&&!SystemParameters.HighContrast;
        if(enabled){
            if(_glass is null){_glass=new DockSurface{UseGlass=true,FusedOptics=true,ConstrainFrostToOutline=true,Fill=Brushes.White,StrokeWidth=0,IsHitTestVisible=false};_glass.UseControlCenterLighting();_visual.Children.Insert(2,_glass);SetExponent(_exponent);}
            var settings=WidgetDesktopService.Current?.GlassMaterial??new DockSettings();
            _glass.GlassIntensity=settings.GlassIntensity;_glass.GlassDistortion=settings.GlassDistortion;_glass.GlassFrost=colors?.GlassFrost??.03;_glass.GlassHighlight=settings.GlassHighlight;_glass.GlassHighlightWidth=settings.GlassHighlightWidth;_glass.GlassTint=colors?.GlassTint??.035;_glass.GlassTintColor=GlassTint.Resolve(settings,colors?.GlassTint??.035);_glass.GlassGrain=settings.GlassGrain;_glass.GlassOpacity=settings.GlassOpacity*(colors?.Opacity??1);_glass.Visibility=Visibility.Visible;_glass.InvalidateVisual();
            _plate.Visibility=_glass.Backdrop is null?Visibility.Visible:Visibility.Hidden;
        }else{if(_glass is not null){_glass.Visibility=Visibility.Collapsed;_glass.Backdrop=null;}_plate.Visibility=Visibility.Visible;}
        UpdateGlassCapture();
    }
    private void UpdateGlassCapture()
    {
        if(!IsLoaded||!_active||Material!=WidgetMaterial.Glass||SystemParameters.HighContrast){ReleaseGlassCapture();return;}
        if(_capture is not null&&!_capture.Matches(this))ReleaseGlassCapture();
        if(_capture is null){_capture=WidgetGlassBackdrop.Attach(this);if(_capture is not null&&Window.GetWindow(this) is WidgetDesktopWindow owner){_glassOwner=owner;owner.LocationChanged+=GlassOwnerMoved;owner.DpiChanged+=GlassOwnerDpiChanged;}}
    }
    private void GlassOwnerMoved(object? sender,EventArgs e)=>UpdateGlassCapture();
    private void GlassOwnerDpiChanged(object sender,DpiChangedEventArgs e)=>UpdateGlassCapture();
    private void ReleaseGlassCapture(){if(_glassOwner is not null){_glassOwner.LocationChanged-=GlassOwnerMoved;_glassOwner.DpiChanged-=GlassOwnerDpiChanged;_glassOwner=null;}_capture?.Detach(this);_capture=null;if(_glass is not null)_glass.Backdrop=null;_plate.Visibility=Visibility.Visible;}
    internal bool TryGlassBounds(out Rect bounds)
    {
        bounds=Rect.Empty;if(_glass is null||!IsLoaded||!IsVisible||!_active||_glass.ActualWidth<=0||_glass.ActualHeight<=0)return false;
        if(Window.GetWindow(this) is WidgetDesktopWindow desktop&&!desktop.ContentActive)return false;
        bounds=new Rect(_glass.PointToScreen(new Point()),_glass.PointToScreen(new Point(_glass.ActualWidth,_glass.ActualHeight)));return !bounds.IsEmpty&&bounds.Width>0&&bounds.Height>0;
    }
    internal void SetGlassBackdrop(BitmapSource frame,Rect bounds,Rect pane,bool changed)
    {
        if(_glass is null||Material!=WidgetMaterial.Glass)return;
        var dpi=new DpiScale(pane.Width/_glass.ActualWidth,pane.Height/_glass.ActualHeight);
        bool mapping=_glass.BackdropScreenBounds!=bounds||_glassPane!=pane||!Equals(_glass.CapturePixelScale,dpi)||!ReferenceEquals(_glass.Backdrop,frame);
        _glassPane=pane;
        _glass.CapturePixelScale=dpi;_glass.BackdropScreenBounds=bounds;
        if(!ReferenceEquals(_glass.Backdrop,frame))_glass.Backdrop=frame;else if(changed||mapping)_glass.InvalidateVisual();
        _plate.Visibility=Visibility.Hidden;
        if(!_customInk){_contrast.Observe(frame,new Int32Rect((int)(pane.X-bounds.X),(int)(pane.Y-bounds.Y),Math.Max(1,(int)pane.Width),Math.Max(1,(int)pane.Height)),System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency);
            bool light=AdaptiveGlassAppearance.LightIndicator(_contrast.Luminance,_lightInk);if(_lightInk!=light){_lightInk=light;Resources["WidgetInk"]=WidgetBrushes.Get(light?"#F3F7F7":"#24383B");Resources["WidgetMuted"]=WidgetBrushes.Get(light?"#B8F3F7F7":"#B824383B");}}
        ToolTip=(RenderCapability.Tier>>16)<2?(Services.UiText.IsEnglish?"Hardware refraction is unavailable on this device; showing the supported glass effects.":"当前设备不支持硬件折射，显示可用的玻璃效果。"):null;
    }
    internal void GlassCaptureFailed(Exception error)=>ToolTip=(Services.UiText.IsEnglish?"Glass background is unavailable: ":"玻璃背景暂不可用：")+error.Message;
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

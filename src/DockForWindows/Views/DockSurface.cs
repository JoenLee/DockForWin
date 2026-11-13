using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;

namespace DockForWindows.Views;

// Continuous-curvature corners, capped at a capsule rather than an ellipse.
public sealed class DockSurface : FrameworkElement
{
    private readonly Image _backdropImage = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly Grid _backdropHost = new() { ClipToBounds = true, IsHitTestVisible = false };
    private readonly BlurEffect _frost = new() { Radius = 0, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };
    private readonly SurfaceOverlay _overlay;
    private readonly TintLayer _tintLayer;
    // Colour cast laid through the glass ("色调"): transparent means none. Drawn between the refracted backdrop
    // and the light, at low alpha, so it reads as tinted glass rather than paint.
    public Color GlassTintColor { get; set; } = Colors.Transparent;
    private readonly BackdropViewport _frostClip=new();private bool _constrainFrost;
    public bool ConstrainFrostToOutline
    {
        get=>_constrainFrost;
        set{if(value==_constrainFrost)return;
            if(value){RemoveVisualChild(_backdropHost);_frostClip.Attach(_backdropHost);AddVisualChild(_frostClip);}
            else{RemoveVisualChild(_frostClip);_frostClip.Detach();AddVisualChild(_backdropHost);}
            _constrainFrost=value;InvalidateMeasure();InvalidateVisual();}
    }
    // This viewport clips after blur and never repositions the manually mapped capture layer.
    private sealed class BackdropViewport:FrameworkElement
    {
        private Visual? _child;
        public BackdropViewport(){IsHitTestVisible=false;}
        public void Attach(Visual child){_child=child;AddVisualChild(child);}
        public void Detach(){if(_child is not null)RemoveVisualChild(_child);_child=null;}
        protected override int VisualChildrenCount=>_child is null?0:1;
        protected override Visual GetVisualChild(int index)=>index==0&&_child is not null?_child:throw new ArgumentOutOfRangeException(nameof(index));
    }
    private readonly LiquidGlassEffect? _refraction;
    private readonly LiquidGlassEffect? _lighting;
    internal void UseControlCenterLighting()=>_lighting?.SetControlCenterLighting(true);
    private ImageSource? _backdrop;
    private readonly AdaptiveGlassAppearance _adaptiveAppearance = new();
    public double EffectiveGlassTint => AdaptiveGlassAppearance.TintOpacity(GlassTint, _adaptiveAppearance.Luminance);
    public Rect BackdropScreenBounds { get; set; } = Rect.Empty;
    public double GlassOpacity { get; set; } = 1;
    public double GlassFrost { get; set; }
    public double GlassGrain { get; set; } = .45;
    public double GlassDistortion { get; set; } = .65;
    public bool UseGlass { get; set; }
    internal bool ClassicMaterial { get; set; }
    public double GlassHighlight { get; set; } = .55;
    public double GlassHighlightWidth { get; set; } = 2.5;
    public double GlassSideDarkness { get; set; } = .55;
    public double GlassTint { get; set; } = .06;
    public DockSurface()
    {
        _overlay = new SurfaceOverlay(this); _tintLayer = new TintLayer(this);
        _backdropHost.Children.Add(_backdropImage);
        if ((RenderCapability.Tier >> 16) >= 2)
        {
            _refraction = new LiquidGlassEffect(); _backdropImage.Effect = _refraction;
            _lighting = new LiquidGlassEffect { LightingOnly = 1 };
        }
        AddVisualChild(_backdropHost); AddVisualChild(_tintLayer); AddVisualChild(_overlay);
    }
    protected override int VisualChildrenCount => 3;
    protected override Visual GetVisualChild(int index) => index switch { 0 => ConstrainFrostToOutline?_frostClip:_backdropHost, 1 => _tintLayer, 2 => _overlay, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    protected override Size MeasureOverride(Size available) { _overlay.Measure(available); return new Size(); }
    protected override Size ArrangeOverride(Size finalSize) { if(ConstrainFrostToOutline){_frostClip.Measure(finalSize);_frostClip.Arrange(new Rect(finalSize));}_tintLayer.Arrange(new Rect(finalSize)); _overlay.Arrange(new Rect(finalSize)); ConfigureBackdrop(finalSize); return finalSize; }
    private sealed class TintLayer : FrameworkElement
    {
        private readonly DockSurface _owner;
        public TintLayer(DockSurface owner) { _owner = owner; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext drawing)
        {
            var color = _owner.GlassTintColor;
            if (_owner._geometry is null || !_owner.UseGlass || _owner.ClassicMaterial || color.A == 0) return;
            var brush = new SolidColorBrush(color); brush.Freeze(); drawing.DrawGeometry(brush, null, _owner._geometry);
        }
    }
    private sealed class SurfaceOverlay : FrameworkElement
    {
        private readonly DockSurface _owner;
        public SurfaceOverlay(DockSurface owner) { _owner = owner; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext drawing) => _owner.DrawOverlay(drawing);
    }
    // Set by transformed control-center cards; the Dock uses its usual monitor DPI.
    internal DpiScale? CapturePixelScale { get; set; }
    private void ConfigureBackdrop(Size size)
    {
        if(ClassicMaterial){_backdropHost.Visibility=Visibility.Collapsed;_overlay.Visibility=Visibility.Visible;_overlay.Effect=null;_overlay.Opacity=1;return;}
        var dpi = CapturePixelScale ?? VisualTreeHelper.GetDpi(this);
        if (UseGlass && Backdrop is BitmapSource sample && size.Width > 0 && size.Height > 0)
        {
            var adaptiveOrigin = BackdropScreenBounds.IsEmpty || PresentationSource.FromVisual(this) is null ? new Point() : PointToScreen(new Point());
            var adaptiveBounds = BackdropScreenBounds.IsEmpty ? new Rect(0, 0, sample.PixelWidth, sample.PixelHeight) : BackdropScreenBounds;
            // Exclude the source padding and rounded ends: only the pane's background.
            var region = new Int32Rect((int)Math.Floor(adaptiveOrigin.X - adaptiveBounds.X + size.Width * dpi.DpiScaleX * .1),
                (int)Math.Floor(adaptiveOrigin.Y - adaptiveBounds.Y + size.Height * dpi.DpiScaleY * .2),
                Math.Max(1, (int)Math.Ceiling(size.Width * dpi.DpiScaleX * .8)), Math.Max(1, (int)Math.Ceiling(size.Height * dpi.DpiScaleY * .6)));
            _adaptiveAppearance.Observe(sample, region, System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
        }
        bool materialVisible = UseGlass && GlassOpacity > 0 && Fill is SolidColorBrush materialFill && materialFill.Color.A > 0;
        _overlay.Visibility = !UseGlass || materialVisible ? Visibility.Visible : Visibility.Hidden;
        _overlay.Effect = UseGlass ? _lighting : null; _overlay.Opacity = UseGlass ? GlassOpacity : 1;
        if (_lighting is not null)
        {
            var physicalSize = new Point(Math.Max(1, size.Width * dpi.DpiScaleX), Math.Max(1, size.Height * dpi.DpiScaleY));
            _lighting.ControlProfile=new Point(0,CornerExponent);
            _lighting.SourceSize = physicalSize; _lighting.PlateSize = physicalSize; _lighting.PlateOrigin = new Point();
            _lighting.Shape = new Point(Radius, 0); _lighting.Material = new Point(dpi.DpiScaleX, 0);
            _lighting.Lighting = new Point(GlassHighlight, GlassHighlightWidth); _lighting.Sides = new Point(GlassSideDarkness, 0);
            _lighting.Tint = new Point(EffectiveGlassTint, 0);
        }
        bool visible = Backdrop is BitmapSource && UseGlass && GlassOpacity > 0 && Fill is SolidColorBrush fill && fill.Color.A > 0;
        _backdropHost.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        if (!visible) return;
        var image = (BitmapSource)Backdrop!;
        var bounds = BackdropScreenBounds;
        Point origin;
        if (bounds.IsEmpty || PresentationSource.FromVisual(this) is null)
        {
            origin = new Point(); bounds = new Rect(0, 0, image.PixelWidth, image.PixelHeight);
        }
        else origin = PointToScreen(new Point());
        var imageRectangle = new Rect((bounds.X - origin.X) / dpi.DpiScaleX, (bounds.Y - origin.Y) / dpi.DpiScaleY, image.PixelWidth / dpi.DpiScaleX, image.PixelHeight / dpi.DpiScaleY);
        if (_geometry is not null)
        {
            var sourceClip = _geometry.Clone(); sourceClip.Transform = new TranslateTransform(-imageRectangle.X, -imageRectangle.Y);
            sourceClip.Freeze(); _backdropHost.Clip = sourceClip;
        }
        _backdropHost.Opacity = Math.Clamp(GlassOpacity, 0, 1);
        _frost.Radius = 24 * Math.Pow(Math.Clamp(GlassFrost, 0, 1), 1.6);
        _backdropHost.Effect = _frost.Radius > 0 ? _frost : null;
        _backdropHost.Measure(imageRectangle.Size); _backdropHost.Arrange(imageRectangle);
        if (_refraction is not null)
        {
            _refraction.ControlProfile=new Point(0,CornerExponent);
            _refraction.SourceSize = new Point(image.PixelWidth, image.PixelHeight);
            _refraction.PlateOrigin = new Point(origin.X - bounds.X, origin.Y - bounds.Y);
            _refraction.PlateSize = new Point(size.Width * dpi.DpiScaleX, size.Height * dpi.DpiScaleY);
            _refraction.Shape = new Point(Radius, GlassIntensity);
            _refraction.Material = new Point(dpi.DpiScaleX, GlassFrost);
            _refraction.Texture = new Point(GlassGrain, GlassDistortion); _refraction.WorldOrigin = bounds.TopLeft;
        }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new SurfacePeer(this);
    private sealed class SurfacePeer : FrameworkElementAutomationPeer
    {
        public SurfacePeer(DockSurface owner) : base(owner) { }
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => false;
    }
    public Brush? Fill { get; set; }
    public Brush? Stroke { get; set; }
    public double StrokeWidth { get; set; } = 1;
    public double Radius { get; set; } = 22;
    public double CornerExponent {get;set;}
    public double GlassIntensity { get; set; }
    public ImageSource? Backdrop
    {
        get => _backdrop;
        set
        {
            _backdrop = value; _backdropImage.Source = value;
            InvalidateArrange(); InvalidateVisual();
        }
    }
    private Size _lastSize;
    private double _lastRadius = -1, _lastStroke = -1, _lastExponent=-1;
    private Geometry? _geometry;
    public static Geometry CreateGeometry(Size size, double radius, double stroke = 0, double exponent = 0)
    {
        double inset = Math.Max(0, stroke) / 2;
        var rect = new Rect(inset, inset, Math.Max(0, size.Width - inset * 2), Math.Max(0, size.Height - inset * 2));
        double maximum = Math.Min(rect.Width, rect.Height) / 2;
        double fraction = Math.Clamp(radius / 100, 0, 1);
        double r = maximum * fraction;
        if ((fraction >= 1 && exponent <= 0) || fraction <= 0 || maximum <= 0)
        {
            var capsule = new RectangleGeometry(rect, r, r); capsule.Freeze(); return capsule;
        }
        var profile = exponent==1.5?CapsuleProfile():Profile(exponent>=2?exponent:3 - Math.Pow(fraction, 8));
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            double left = rect.Left, top = rect.Top, right = rect.Right, bottom = rect.Bottom;
            path.BeginFigure(new Point(left + r, top), true, true);
            path.LineTo(new Point(right - r, top), true, false);
            Corner(path, profile, (x, y) => new Point(right - r + x * r, top + y * r));
            path.LineTo(new Point(right, bottom - r), true, false);
            Corner(path, profile, (x, y) => new Point(right - y * r, bottom - r + x * r));
            path.LineTo(new Point(left + r, bottom), true, false);
            Corner(path, profile, (x, y) => new Point(left + r - x * r, bottom - y * r));
            path.LineTo(new Point(left, top + r), true, false);
            Corner(path, profile, (x, y) => new Point(left + y * r, top + r - x * r));
        }
        geometry.Freeze(); return geometry;
    }
    private static List<(Point Start, Point A, Point B, Point End)> CapsuleProfile()
    {
        const double angle=.055;double slope=Math.Tan(angle),r=slope/(3*(1-Math.Cos(angle))+slope*(1-Math.Sin(angle)));
        double x=1-r+r*Math.Sin(angle),y=r*(1-Math.Cos(angle));
        var start=new Point();var join=new Point(x,y);double end=Math.PI/4,k=4d/3*Math.Tan((end-angle)/4);
        var finish=new Point(1-r+r*Math.Sin(end),r-r*Math.Cos(end));
        return new(){(start,new Point(x/3,0),new Point(2*x/3,0),join),
            (join,new Point(x+k*r*Math.Cos(angle),y+k*r*Math.Sin(angle)),new Point(finish.X-k*r*Math.Cos(end),finish.Y-k*r*Math.Sin(end)),finish)};
    }
    private static List<(Point Start, Point A, Point B, Point End)> Profile(double exponent)
    {
        var segments = new List<(Point, Point, Point, Point)>();
        const int count = 12;
        double middle = Math.Pow(.5, 1 / exponent);
        double Y(double x) => 1 - Math.Pow(Math.Max(0, 1 - Math.Pow(x, exponent)), 1 / exponent);
        double Slope(double x) => x == 0 ? 0 : Math.Pow(x, exponent - 1) * Math.Pow(1 - Math.Pow(x, exponent), 1 / exponent - 1);
        for (int i = 0; i < count; i++)
        {
            double x0 = middle * i / count, x1 = middle * (i + 1) / count, step = (x1 - x0) / 3;
            var start = new Point(x0, Y(x0)); var end = new Point(x1, Y(x1));
            var a = new Point(x0 + step, start.Y + Slope(x0) * step);
            var b = new Point(x1 - step, Math.Max(0, end.Y - Slope(x1) * step));
            segments.Add((start, a, b, end));
        }
        return segments;
    }
    private static void Corner(StreamGeometryContext path, List<(Point Start, Point A, Point B, Point End)> profile, Func<double, double, Point> convert)
    {
        foreach (var segment in profile) path.BezierTo(convert(segment.A.X, segment.A.Y), convert(segment.B.X, segment.B.Y), convert(segment.End.X, segment.End.Y), true, false);
        for (int i = profile.Count - 1; i >= 0; i--)
        {
            var segment = profile[i];
            path.BezierTo(convert(1 - segment.B.Y, 1 - segment.B.X), convert(1 - segment.A.Y, 1 - segment.A.X), convert(1 - segment.Start.Y, 1 - segment.Start.X), true, false);
        }
    }
    protected override void OnRender(DrawingContext drawing)
    {
        if (_geometry is null || _lastSize != RenderSize || _lastRadius != Radius || _lastStroke != StrokeWidth || _lastExponent!=CornerExponent)
        {
            _geometry = CreateGeometry(RenderSize, Radius, StrokeWidth,CornerExponent);
            _lastSize = RenderSize; _lastRadius = Radius; _lastStroke = StrokeWidth;_lastExponent=CornerExponent;
        }
        // Clip the material children, not the outer surface: its ambient shadow
        // must be able to extend beyond the rounded outline.
        _overlay.Clip = _geometry;_frostClip.Clip=ConstrainFrostToOutline?_geometry:null;
        ConfigureBackdrop(RenderSize); _overlay.InvalidateVisual(); _tintLayer.InvalidateVisual();
    }
    private void DrawOverlay(DrawingContext drawing)
    {
        if (_geometry is null) return;
        if (!ClassicMaterial && UseGlass && _lighting is not null) drawing.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        else drawing.DrawGeometry(Fill, StrokeWidth > 0 && Stroke is not null ? new Pen(Stroke, StrokeWidth) : null, _geometry);
    }
}

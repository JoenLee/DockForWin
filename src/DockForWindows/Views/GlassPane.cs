using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DockForWindows.Views;

// A pane of glass over the desktop picture, for the settings window.
//
// Two layers of the same picture, screen-aligned so the pane really looks through to what lies behind the window:
// a frosted copy underneath and the clear picture on top, bent at the rim by the Dock's own lens shader. Clarity
// cross-fades them: 0 is milk glass (磨砂), 1 is clear water-light glass (水光). A veil carries the pane's colour,
// and is thickened automatically over dark or busy pictures so text on the pane keeps its contrast. The edge is
// not a line: a band of light, brightest along the lit upper edge, fades into the glass, the way light catches
// the thickness of a real slab.
internal sealed class GlassPane : FrameworkElement
{
    public static readonly DependencyProperty CornerRadiusProperty = Register(nameof(CornerRadius), 18d);
    public static readonly DependencyProperty ClarityProperty = Register(nameof(Clarity), 0d);
    public static readonly DependencyProperty RefractionProperty = Register(nameof(Refraction), .7);
    public static readonly DependencyProperty BevelProperty = Register(nameof(Bevel), 1d);
    public static readonly DependencyProperty VeilProperty = DependencyProperty.Register(nameof(Veil), typeof(Color), typeof(GlassPane), new FrameworkPropertyMetadata(Color.FromArgb(120, 250, 250, 252), FrameworkPropertyMetadataOptions.AffectsRender, (o, _) => ((GlassPane)o).Refresh()));
    // The luminance text on this pane needs underneath it (0..1); the veil is thickened until the pane reaches it.
    public static readonly DependencyProperty TargetLuminanceProperty = Register(nameof(TargetLuminance), -1d);
    public static readonly DependencyProperty DarkProperty = DependencyProperty.Register(nameof(Dark), typeof(bool), typeof(GlassPane), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, (o, _) => ((GlassPane)o).Refresh()));
    private static DependencyProperty Register(string name, double value) => DependencyProperty.Register(name, typeof(double), typeof(GlassPane), new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender, (o, _) => ((GlassPane)o).Refresh()));
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public double Clarity { get => (double)GetValue(ClarityProperty); set => SetValue(ClarityProperty, value); }
    public double Refraction { get => (double)GetValue(RefractionProperty); set => SetValue(RefractionProperty, value); }
    public double Bevel { get => (double)GetValue(BevelProperty); set => SetValue(BevelProperty, value); }
    public Color Veil { get => (Color)GetValue(VeilProperty); set => SetValue(VeilProperty, value); }
    public double TargetLuminance { get => (double)GetValue(TargetLuminanceProperty); set => SetValue(TargetLuminanceProperty, value); }
    public bool Dark { get => (bool)GetValue(DarkProperty); set => SetValue(DarkProperty, value); }

    private readonly Rectangle _frost = new() { IsHitTestVisible = false }, _clear = new() { IsHitTestVisible = false }, _veil = new() { IsHitTestVisible = false };
    private readonly ImageBrush _frostBrush = new() { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
    private readonly ImageBrush _clearBrush = new() { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
    private readonly LiquidGlassEffect? _lens;
    private readonly Edge _edge;
    private Window? _window;
    private Point _origin = new(double.NaN, double.NaN);
    private Size _clipSize; private double _clipRadius = -1;
    public GlassPane()
    {
        IsHitTestVisible = false; _edge = new Edge(this);
        RenderOptions.SetBitmapScalingMode(_frostBrush, BitmapScalingMode.Linear); RenderOptions.SetBitmapScalingMode(_clearBrush, BitmapScalingMode.HighQuality);
        _frost.Fill = _frostBrush; _clear.Fill = _clearBrush;
        if ((RenderCapability.Tier >> 16) >= 2) { _lens = new LiquidGlassEffect(); _clear.Effect = _lens; }
        foreach (var child in new UIElement[] { _frost, _clear, _veil, _edge }) AddVisualChild(child);
        Loaded += (_, _) => Attach(); Unloaded += (_, _) => Detach();
    }
    protected override int VisualChildrenCount => 4;
    protected override Visual GetVisualChild(int index) => index switch { 0 => _frost, 1 => _clear, 2 => _veil, 3 => _edge, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    protected override Size MeasureOverride(Size available) => new();
    protected override Size ArrangeOverride(Size size)
    {
        foreach (var child in new UIElement[] { _frost, _clear, _veil, _edge }) { child.Measure(size); child.Arrange(new Rect(size)); }
        if (_clipSize != size || _clipRadius != CornerRadius)
        {
            _clipSize = size; _clipRadius = CornerRadius;
            var clip = new RectangleGeometry(new Rect(size), Math.Min(CornerRadius, Math.Min(size.Width, size.Height) / 2), Math.Min(CornerRadius, Math.Min(size.Width, size.Height) / 2)); clip.Freeze(); Clip = clip;
        }
        Refresh(); return size;
    }
    private void Attach()
    {
        Detach(); _window = Window.GetWindow(this);
        if (_window is not null) { _window.LocationChanged += Moved; _window.DpiChanged += Moved; }
        WallpaperFrost.Changed += Refresh; LayoutUpdated += Relaid; Refresh();
    }
    private void Detach()
    {
        if (_window is not null) { _window.LocationChanged -= Moved; _window.DpiChanged -= Moved; }
        _window = null; WallpaperFrost.Changed -= Refresh; LayoutUpdated -= Relaid;
    }
    private void Moved(object? sender, EventArgs args) => Refresh();
    private void Relaid(object? sender, EventArgs args)
    {
        if (PresentationSource.FromVisual(this) is not null && PointToScreen(new Point()) != _origin) Refresh();
    }
    // Maps the pane's screen rectangle into each picture and configures the lens; nothing here allocates per move
    // except the lens constants, so dragging the window only moves two viewboxes.
    private void Refresh()
    {
        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0 || PresentationSource.FromVisual(this) is not HwndSource source) return;
        var frost = WallpaperFrost.Get(source.Handle);
        _origin = PointToScreen(new Point()); var far = PointToScreen(new Point(size.Width, size.Height));
        double clarity = Math.Clamp(Clarity, 0, 1);
        _clear.Opacity = clarity; _clear.Visibility = clarity > .001 && frost is not null ? Visibility.Visible : Visibility.Collapsed;
        double luminance = .5;
        if (frost is null) { _frost.Fill = WallpaperFrost.Placeholder(Dark); }
        else
        {
            var cover = frost.Cover; var picture = Dark ? frost.Dark : frost.Light;
            if (!ReferenceEquals(_frostBrush.ImageSource, picture)) _frostBrush.ImageSource = picture;
            if (!ReferenceEquals(_clearBrush.ImageSource, frost.Clear)) _clearBrush.ImageSource = frost.Clear;
            _frost.Fill = _frostBrush;
            Rect Window(BitmapSource image) => new((_origin.X - cover.X) / cover.Width * image.PixelWidth, (_origin.Y - cover.Y) / cover.Height * image.PixelHeight,
                Math.Max(1, (far.X - _origin.X) / cover.Width * image.PixelWidth), Math.Max(1, (far.Y - _origin.Y) / cover.Height * image.PixelHeight));
            _frostBrush.Viewbox = Window(picture); _clearBrush.Viewbox = Window(frost.Clear);
            luminance = WallpaperFrost.Luminance(picture, _frostBrush.Viewbox) * (1 - clarity) + WallpaperFrost.Luminance(frost.Clear, _clearBrush.Viewbox) * clarity;
            if (_lens is not null)
            {
                // The lens works in the pane's physical pixels; radius is a percentage of the half short side.
                double sx = (far.X - _origin.X) / size.Width, w = far.X - _origin.X, h = far.Y - _origin.Y;
                _lens.SourceSize = new Point(Math.Max(1, w), Math.Max(1, h)); _lens.PlateOrigin = new Point(); _lens.PlateSize = new Point(Math.Max(1, w), Math.Max(1, h));
                _lens.Shape = new Point(Math.Clamp(CornerRadius * sx / (Math.Min(w, h) / 2) * 100, 0, 100), Math.Clamp(Refraction, 0, 1));
                _lens.Material = new Point(sx, 0); _lens.Texture = new Point(0, .65 * Math.Clamp(Refraction, 0, 1.4)); _lens.ControlProfile = new Point();
            }
        }
        // Veil: thicken until the pane's luminance reaches the target for its text (light: at least, dark: at most).
        var veil = Veil; double target = TargetLuminance;
        if (target >= 0)
        {
            double veilLum = (veil.R * .2126 + veil.G * .7152 + veil.B * .0722) / 255, a = veil.A / 255d;
            double now = luminance * (1 - a) + veilLum * a;
            if (!Dark && now < target && veilLum > luminance) a = Math.Clamp((target - luminance) / (veilLum - luminance), a, .94);
            if (Dark && now > target && veilLum < luminance) a = Math.Clamp((luminance - target) / (luminance - veilLum), a, .94);
            veil = Color.FromArgb((byte)Math.Round(a * 255), veil.R, veil.G, veil.B);
        }
        if (_veil.Fill is not SolidColorBrush current || current.Color != veil) { var brush = new SolidColorBrush(veil); brush.Freeze(); _veil.Fill = brush; }
        _edge.InvalidateVisual();
    }

    // The rim: a crisp line of light along the top, a soft inner band that fades into the glass (lighter at the top
    // and left where the light comes from), and a breath of shade at the bottom. Drawn once per size.
    private sealed class Edge : FrameworkElement
    {
        private readonly GlassPane _owner;
        public Edge(GlassPane owner) { _owner = owner; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext dc)
        {
            double w = RenderSize.Width, h = RenderSize.Height, r = Math.Min(_owner.CornerRadius, Math.Min(w, h) / 2), bevel = Math.Clamp(_owner.Bevel, 0, 2);
            if (w <= 0 || h <= 0 || bevel <= 0) return;
            bool dark = _owner.Dark; double clear = Math.Clamp(_owner.Clarity, 0, 1);
            double band = (dark ? .07 : .26 + .16 * clear) * bevel;
            const int steps = 14;
            for (int i = 0; i < steps; i++)
            {
                double t = i / (double)steps, alpha = band * (1 - t) * (1 - t) * 1.6 / steps * 2.2;
                var light = new LinearGradientBrush(new GradientStopCollection {
                    new(Color.FromArgb(A(alpha), 255, 255, 255), 0), new(Color.FromArgb(A(alpha * .55), 255, 255, 255), .5), new(Color.FromArgb(A(alpha * (.35 + .4 * clear)), 255, 255, 255), 1) }, new Point(0, 0), new Point(.35, 1));
                light.Freeze();
                double inset = i + .5;
                dc.DrawRoundedRectangle(null, new Pen(light, 1), new Rect(inset, inset, Math.Max(0, w - 2 * inset), Math.Max(0, h - 2 * inset)), Math.Max(0, r - inset), Math.Max(0, r - inset));
            }
            // The crisp catch-light along the lit edge.
            var line = new LinearGradientBrush(new GradientStopCollection {
                new(Color.FromArgb(A((dark ? .26 : .85) * bevel), 255, 255, 255), 0), new(Color.FromArgb(A((dark ? .08 : .3 + .3 * clear) * bevel), 255, 255, 255), .55), new(Color.FromArgb(A((dark ? .05 : .2 + .35 * clear) * bevel), 255, 255, 255), 1) }, new Point(0, 0), new Point(.25, 1));
            line.Freeze();
            dc.DrawRoundedRectangle(null, new Pen(line, 1.2), new Rect(.6, .6, w - 1.2, h - 1.2), Math.Max(0, r - .6), Math.Max(0, r - .6));
            // Shade gathers at the bottom, under the light.
            var shade = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(A(dark ? .22 : .06), dark ? (byte)0 : (byte)70, dark ? (byte)0 : (byte)55, dark ? (byte)0 : (byte)40), 90); shade.Freeze();
            dc.PushClip(new RectangleGeometry(new Rect(0, h - 22, w, 22)));
            dc.DrawRoundedRectangle(null, new Pen(shade, 6), new Rect(3, 3, w - 6, h - 6), Math.Max(0, r - 3), Math.Max(0, r - 3));
            dc.Pop();
        }
        private static byte A(double alpha) => (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
    }
}

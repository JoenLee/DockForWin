using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DockForWindows.Views;

// The settings window is one slab of glass: its frame looks through to the desktop, and the sidebar and content
// float on it as two inset panes. Liquid glass on: frame and sidebar are clear, refracting water-light glass,
// the content pane milky for legibility. Off: all three are quiet frosted milk glass. "质感" moves the clear
// panes between the two. The material follows the draft settings live, so the window previews its own choice.
internal partial class SettingsShellWindow
{
    // Transparency effects on (Windows setting), no high contrast, not a remote session: the glass is drawn.
    protected bool GlassBackdrop { get; } = !SystemParameters.HighContrast && !SystemParameters.IsRemoteSession && TransparencyEnabled();
    internal const double ShadowMargin = 30, FrameRadius = 24, PaneRadius = 18, FrameInset = 14;
    private readonly GlassPane _frame = new() { CornerRadius = FrameRadius, Refraction = .38, Bevel = 1.15 };
    private readonly GlassPane _sidebarGlass = new() { CornerRadius = PaneRadius, Refraction = .55 };
    private readonly GlassPane _contentGlass = new() { CornerRadius = PaneRadius, Refraction = .5 };
    private readonly Border _windowShadow = Caster(FrameRadius), _sidebarShadow = Caster(PaneRadius), _contentShadow = Caster(PaneRadius);
    private readonly DropShadowEffect _windowShadowEffect = new() { BlurRadius = 64, ShadowDepth = 20, Direction = 270, Color = Colors.Black, RenderingBias = RenderingBias.Quality };
    private readonly DropShadowEffect _paneShadowEffect = new() { BlurRadius = 40, ShadowDepth = 12, Direction = 270, Color = Colors.Black, RenderingBias = RenderingBias.Quality };
    private readonly DropShadowEffect _contentShadowEffect = new() { BlurRadius = 40, ShadowDepth = 12, Direction = 270, Color = Colors.Black, RenderingBias = RenderingBias.Quality };
    private Border _windowBody = null!;

    // An opaque shape that only casts: its own area is clipped away so it never shows through the glass.
    private static Border Caster(double radius)
    {
        var caster = new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(radius), IsHitTestVisible = false };
        caster.SizeChanged += (_, args) =>
        {
            var size = args.NewSize; var inner = new RectangleGeometry(new Rect(size), radius, radius);
            var ring = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(-120, -120, size.Width + 240, size.Height + 240)), inner); ring.Freeze(); caster.Clip = ring;
        };
        return caster;
    }
    // Window layers inside the two-column grid: shadow and frame span both columns, the panes sit in their columns.
    private void InstallWindowMaterial(Grid root)
    {
        _windowShadow.Effect = _windowShadowEffect; _sidebarShadow.Effect = _paneShadowEffect; _contentShadow.Effect = _contentShadowEffect;
        foreach (var layer in new FrameworkElement[] { _windowShadow, _frame }) { Grid.SetColumnSpan(layer, 2); root.Children.Insert(0, layer); }
        root.Children.Remove(_windowShadow); root.Children.Insert(0, _windowShadow);
        StateChanged += (_, _) => ApplyWindowShape();
    }
    // Maximized, the window fills the work area: no shadow margin, no outer radius.
    private void ApplyWindowShape()
    {
        bool max = WindowState == WindowState.Maximized;
        _windowBody.Padding = new Thickness(max ? 0 : ShadowMargin);
        _frame.CornerRadius = max ? 0 : FrameRadius; _windowShadow.Visibility = max ? Visibility.Collapsed : Visibility.Visible;
    }
    private Grid SidebarGlass(Grid content) => Floating(_sidebarShadow, _sidebarGlass, content, new Thickness(FrameInset, FrameInset, 6, FrameInset));
    private Grid ContentGlass(Grid content) => Floating(_contentShadow, _contentGlass, content, new Thickness(6, FrameInset, FrameInset, FrameInset));
    private static Grid Floating(Border shadow, GlassPane glass, UIElement content, Thickness margin)
    {
        var pane = new Grid { Margin = margin };
        pane.Children.Add(shadow); pane.Children.Add(glass); pane.Children.Add(content);
        return pane;
    }
    // Pages fade under the toolbar and above the bottom edge (the macOS 26 scroll edge): the content itself goes
    // transparent there, so the pane's glass shows through exactly.
    private void InstallScrollEdges(FrameworkElement pages)
    {
        void Update()
        {
            double h = Math.Max(1, pages.ActualHeight);
            var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            mask.GradientStops.Add(new(Colors.Transparent, 0)); mask.GradientStops.Add(new(Colors.Black, Math.Min(.45, 16 / h)));
            mask.GradientStops.Add(new(Colors.Black, Math.Max(.55, 1 - 20 / h))); mask.GradientStops.Add(new(Colors.Transparent, 1)); mask.Freeze();
            pages.OpacityMask = mask;
        }
        pages.SizeChanged += (_, _) => Update();
    }
    private void ApplyGlassPalette(DesignPalette p)
    {
        bool glass = GlassBackdrop, liquid = glass && SharedDraft.LiquidGlass, dark = p.Dark;
        double quality = Math.Clamp(SharedDraft.GlassQuality, 0, 1);
        foreach (var pane in new[] { _frame, _sidebarGlass, _contentGlass }) { pane.Dark = dark; pane.Visibility = Visibility.Visible; }
        // Clarity: liquid glass follows 质感 (a floor keeps it glassy); frosted glass stays milk glass.
        _frame.Clarity = liquid ? .25 + .75 * quality : 0; _sidebarGlass.Clarity = liquid ? .3 + .7 * quality : 0; _contentGlass.Clarity = 0;
        _frame.Veil = !glass ? p.Canvas : dark ? (liquid ? Color.FromArgb(92, 12, 13, 18) : Color.FromArgb(107, 28, 28, 32)) : (liquid ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(107, 244, 241, 237));
        _sidebarGlass.Veil = !glass ? p.Sidebar : dark ? (liquid ? Color.FromArgb(110, 20, 22, 28) : Color.FromArgb(92, 48, 48, 54)) : (liquid ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(87, 252, 250, 248));
        _contentGlass.Veil = !glass ? p.Canvas : dark ? Color.FromArgb(184, 30, 31, 35) : Color.FromArgb(214, 250, 251, 253);
        // Text sits on the sidebar and content panes: keep their luminance on the right side of the ink.
        _sidebarGlass.TargetLuminance = dark ? .22 : .7; _contentGlass.TargetLuminance = dark ? .2 : .86; _frame.TargetLuminance = -1;
        _windowShadowEffect.Opacity = dark ? .5 : .2; _paneShadowEffect.Opacity = _contentShadowEffect.Opacity = dark ? .3 : .08;
        // Controls floating on the panes share the glass rim.
        var rim = new LinearGradientBrush(new GradientStopCollection { new(p.GlassRimLight, 0), new(p.GlassRimShade, .42), new(p.GlassRimShade, .7), new(DesignSystem.WithAlpha(p.GlassRimLight, p.GlassRimLight.A / 255d * .55), 1) }, new Point(0, 0), new Point(1, 1)); rim.Freeze();
        Resources["SettingsGlassRim"] = rim;
        var control = new LinearGradientBrush(new GradientStopCollection { new(p.Control, 0), new(p.ControlBottom, 1) }, 90); control.Freeze();
        Resources["SettingsGlassControl"] = control;
        Resources["SettingsGlassShadowOpacity"] = dark ? .32 : .09;
        // Sidebar row icons sit in small glass chips when the glass is liquid.
        Resources["SettingsNavChip"] = DesignSystem.Brush(liquid ? (dark ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(97, 255, 255, 255)) : Colors.Transparent);
        Resources["SettingsNavChipRim"] = DesignSystem.Brush(liquid ? (dark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(200, 255, 255, 255)) : Colors.Transparent);
        // The selected row: a bright white glass pill on liquid glass, a quiet darker breath of the pane otherwise.
        Resources["SettingsSelection"] = DesignSystem.Brush(liquid ? (dark ? Color.FromArgb(56, 255, 255, 255) : Color.FromArgb(184, 255, 255, 255)) : (dark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(17, 0, 0, 0)));
        _selection.BorderThickness = new Thickness(liquid ? 1 : 0);
        _selection.BorderBrush = new LinearGradientBrush(Color.FromArgb(dark ? (byte)60 : (byte)255, 255, 255, 255), Color.FromArgb(dark ? (byte)10 : (byte)120, 255, 255, 255), 90);
        _selection.Effect = liquid ? new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = dark ? .3 : .12, Color = Color.FromRgb(20, 60, 120) } : null;
        if (!glass) foreach (var pane in new[] { _frame, _sidebarGlass, _contentGlass }) pane.Clarity = 0;
        RefreshNavigationGlyphs();
    }

    // ---- Sidebar line glyphs (drawn to the mockup; Ionicons only ships filled shapes) ----
    private readonly List<(Path Glyph, int Index)> _navigationGlyphs = new();
    private static Geometry LineGlyph(int index)
    {
        static Geometry R(double x, double y, double w, double h, double r) => new RectangleGeometry(new Rect(x, y, w, h), r, r);
        static Geometry E(double x, double y, double r) => new EllipseGeometry(new Point(x, y), r, r);
        static Geometry P(string data) => Geometry.Parse(data);
        var group = new GeometryGroup();
        Geometry[] parts = index switch
        {
            0 => new[] { R(2.5, 2.5, 5.5, 5.5, 1.6), R(10, 2.5, 5.5, 5.5, 1.6), R(2.5, 10, 5.5, 5.5, 1.6), R(10, 10, 5.5, 5.5, 1.6) },
            1 => new[] { P("M3,5.5 H15 M3,12.5 H15"), E(6.5, 5.5, 1.8), E(11.5, 12.5, 1.8) },
            2 => new[] { R(2.5, 2.5, 13, 5.5, 1.8), R(2.5, 10, 5.5, 5.5, 1.8), R(10, 10, 5.5, 5.5, 1.8) },
            3 => new[] { P("M9,2.5 A6.5,6.5 0 1 0 9,15.5 C10,15.5 10.4,14.8 10.1,14 C9.7,13 10.2,12 11.3,12 H12.7 A2.8,2.8 0 0 0 15.5,9.2 C15.5,5.3 12.6,2.5 9,2.5 Z"), E(5.8, 8.2, .6), E(8, 5.4, .6), E(11.4, 5.8, .6) },
            4 => new[] { P("M2,11 C3.6,11 3.6,6 5.2,6 S6.8,13 8.5,13 S10.1,4 11.8,4 S13.4,11 16,11") },
            5 => new[] { P("M3,12.5 A6,6 0 1 1 15,12.5 M9,12.5 L12,8.5") },
            6 => new[] { E(9, 9, 2.4), P("M9,2.2 V4.2 M9,13.8 V15.8 M2.2,9 H4.2 M13.8,9 H15.8 M4.2,4.2 L5.6,5.6 M12.4,12.4 L13.8,13.8 M4.2,13.8 L5.6,12.4 M12.4,5.6 L13.8,4.2") },
            _ => new[] { E(9, 9, 6.5), P("M9,8.2 V12.6 M9,5.7 V5.8") }
        };
        foreach (var part in parts) group.Children.Add(part);
        group.Freeze(); return group;
    }
    // A row's icon: a 26 px chip (glass on liquid glass, invisible otherwise) holding an 18 px line glyph.
    private FrameworkElement NavigationGlyph(int index)
    {
        var glyph = new Path { Data = LineGlyph(index), StrokeThickness = 1.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Width = 18, Height = 18, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = false };
        _navigationGlyphs.Add((glyph, index));
        var chip = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(.75), Child = glyph };
        chip.SetResourceReference(Border.BackgroundProperty, "SettingsNavChip"); chip.SetResourceReference(Border.BorderBrushProperty, "SettingsNavChipRim");
        return chip;
    }
    private void RefreshNavigationGlyphs()
    {
        var ink = TryFindResource("SettingsSidebarIcon") as Brush ?? Brushes.Gray; var accent = TryFindResource("SettingsAccent") as Brush ?? Brushes.DodgerBlue;
        foreach (var (glyph, index) in _navigationGlyphs) glyph.Stroke = _navigation.SelectedIndex == index ? accent : ink;
    }
}

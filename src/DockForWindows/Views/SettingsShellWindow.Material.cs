using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DockForWindows.Views;

// The settings window is flat: one opaque card, quiet solid rows, no glass of its own. The Dock it configures keeps
// its liquid glass; this window only shows it in the preview.
internal partial class SettingsShellWindow
{
    private void ApplyGlassPalette(DesignPalette p)
    {
        bool dark = p.Dark;
        // Flat controls: one solid face, a hairline, no sheen.
        Resources["SettingsGlassRim"] = DesignSystem.Brush(dark ? DesignSystem.Hex("#1FFFFFFF") : DesignSystem.Hex("#14000000"));
        Resources["SettingsGlassControl"] = DesignSystem.Brush(dark ? DesignSystem.Hex("#3A3A3E") : DesignSystem.Hex("#FFFFFF"));
        Resources["SettingsGlassShadowOpacity"] = 0d;
        Resources["SettingsNavChip"] = Brushes.Transparent; Resources["SettingsNavChipRim"] = Brushes.Transparent;
        Resources["SettingsSelection"] = DesignSystem.Brush(dark ? DesignSystem.Hex("#1CFFFFFF") : DesignSystem.Hex("#0F000000"));
        _selection.BorderThickness = new Thickness(0); _selection.Effect = null;
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

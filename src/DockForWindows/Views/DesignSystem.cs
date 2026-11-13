using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DockForWindows.Views;

// The app's design language in one place: type, colour, shape, light and shadow.
//
// Vocabulary follows Apple's macOS / iOS system design (system colours, inset grouped lists, a 4-pt
// rhythm, continuous corners), tuned for Windows: Segoe UI Variable carries Latin text and Glow Sans SC
// carries Chinese, the way SF and PingFang pair on a Mac. Light always falls from above: surfaces get a
// faint top highlight and soft shadows fall downward, so every layer reads at a consistent depth.
public static class DesignSystem
{
    private static readonly Uri FontBase = new("pack://application:,,,/DockForWindows;component/");
    // Latin from Segoe UI Variable (Windows 11) or Segoe UI; Chinese from the embedded Glow Sans SC subset;
    // anything outside the subset falls back to Microsoft YaHei UI.
    public static FontFamily TextFont { get; } = new(FontBase, "Segoe UI Variable Text, Segoe UI, ./Assets/Fonts/#Glow Sans SC, Microsoft YaHei UI");
    public static FontFamily DisplayFont { get; } = new(FontBase, "Segoe UI Variable Display, Segoe UI, ./Assets/Fonts/#Glow Sans SC, Microsoft YaHei UI");

    // Type scale (DIP).
    public const double LargeTitle = 26, Title = 20, Headline = 14, Body = 13.5, Callout = 12.5, Footnote = 11.5, Caption = 10.5;
    // Shape and rhythm.
    public const double GroupRadius = 12, ControlRadius = 7, TileRadius = 6.5, RowHeight = 46, RowInset = 16, PageInset = 32;

    public static DesignPalette Palette(bool dark) => dark ? Graphite : Porcelain;

    // Light: porcelain white groups resting on a soft pearl canvas.
    public static readonly DesignPalette Porcelain = new(
        Dark: false,
        Canvas: Hex("#F5F5F7"), Sidebar: Hex("#ECECEF"), SidebarVeil: Hex("#B8F2F2F5"),
        Group: Hex("#FFFFFF"), GroupStroke: Hex("#0F000000"), GroupHighlight: Hex("#00FFFFFF"), Separator: Hex("#173C3C43"),
        Label: Hex("#1D1D1F"), SecondaryLabel: Hex("#6E6E73"), TertiaryLabel: Hex("#A1A1A6"),
        Fill: Hex("#33787880"), TrackFill: Hex("#24787880"), QuietFill: Hex("#12767680"), Hover: Hex("#0A000000"), Pressed: Hex("#14000000"),
        Accent: Hex("#007AFF"), AccentHover: Hex("#1C88FF"), AccentPressed: Hex("#0062CC"), OnAccent: Hex("#FFFFFF"),
        Control: Hex("#FFFFFF"), ControlBottom: Hex("#FBFBFD"), ControlStroke: Hex("#1F000000"), Field: Hex("#FFFFFF"),
        Knob: Hex("#FFFFFF"), Inactive: Hex("#D9D9DE"), Shadow: Hex("#000000"), ShadowOpacity: .10,
        Red: Hex("#FF3B30"), Orange: Hex("#FF9500"), Green: Hex("#34C759"));

    // Dark: graphite groups lifted off a near-black canvas by a faint top edge of light.
    public static readonly DesignPalette Graphite = new(
        Dark: true,
        Canvas: Hex("#1D1D1F"), Sidebar: Hex("#262628"), SidebarVeil: Hex("#9E1E1E20"),
        Group: Hex("#2C2C2E"), GroupStroke: Hex("#0FFFFFFF"), GroupHighlight: Hex("#14FFFFFF"), Separator: Hex("#17FFFFFF"),
        Label: Hex("#F5F5F7"), SecondaryLabel: Hex("#98989D"), TertiaryLabel: Hex("#636366"),
        Fill: Hex("#5C787880"), TrackFill: Hex("#3D787880"), QuietFill: Hex("#24767680"), Hover: Hex("#0FFFFFFF"), Pressed: Hex("#1FFFFFFF"),
        Accent: Hex("#0A84FF"), AccentHover: Hex("#2994FF"), AccentPressed: Hex("#0071E3"), OnAccent: Hex("#FFFFFF"),
        Control: Hex("#48484B"), ControlBottom: Hex("#3E3E41"), ControlStroke: Hex("#1AFFFFFF"), Field: Hex("#1C1C1E"),
        Knob: Hex("#F5F5F7"), Inactive: Hex("#4A4A4D"), Shadow: Hex("#000000"), ShadowOpacity: .45,
        Red: Hex("#FF453A"), Orange: Hex("#FF9F0A"), Green: Hex("#30D158"));

    // Category colours for icon tiles (top → bottom: light from above). Saturated but never neon.
    public static readonly (Color Top, Color Bottom)[] TileColors =
    {
        (Hex("#45A1FF"), Hex("#0A6CF0")),   // 0 blue     概览
        (Hex("#A1A1A7"), Hex("#6C6C71")),   // 1 graphite 控制中心
        (Hex("#FFB43B"), Hex("#FF8A00")),   // 2 orange   桌面小组件
        (Hex("#8E8BFF"), Hex("#5E5BE6")),   // 3 indigo   外观与主题
        (Hex("#FF6F8E"), Hex("#FF2D55")),   // 4 pink     动画与交互
        (Hex("#55DA73"), Hex("#25B14A")),   // 5 green    性能与渲染
        (Hex("#ADADB2"), Hex("#7C7C81")),   // 6 gray     通用设置
        (Hex("#62CDF8"), Hex("#2A9FDB")),   // 7 teal     关于
    };

    public static Color Hex(string value) => (Color)ColorConverter.ConvertFromString(value);
    public static SolidColorBrush Brush(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }
    public static Color WithAlpha(Color color, double alpha) => Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);

    // A squircle tile with a vertical gradient, a hairline of light along the top and a white glyph, the way
    // macOS draws settings icons. Size is the tile's edge in DIP.
    public static FrameworkElement IconTile(int colorIndex, string glyph, double size = 24)
    {
        var (top, bottom) = TileColors[Math.Clamp(colorIndex, 0, TileColors.Length - 1)];
        var fill = new LinearGradientBrush(top, bottom, 90); fill.Freeze();
        // SquircleBorder's radius is the extent of a cubic superellipse corner, which reads smaller than a
        // circular radius of the same value; 36 % of the edge matches the iOS settings-icon silhouette.
        double radius = size * .36;
        var tile = new SquircleBorder { Width = size, Height = size, CornerRadius = new CornerRadius(radius), Background = fill };
        var sheen = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(70, 255, 255, 255), 0), new(Color.FromArgb(0, 255, 255, 255), .5) }, 90); sheen.Freeze();
        var layers = new Grid();
        layers.Children.Add(new SquircleBorder { CornerRadius = new CornerRadius(radius), Background = sheen, IsHitTestVisible = false });
        layers.Children.Add(new ControlCenterIcon(glyph) { Width = size * .62, Height = size * .62, Brush = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        tile.Child = layers;
        tile.Effect = new DropShadowEffect { BlurRadius = Math.Max(2, size / 10), ShadowDepth = Math.Max(.5, size / 40), Direction = 270, Opacity = .22, Color = Color.FromRgb(20, 20, 40) };
        return tile;
    }

    // Soft shadow for a raised control (buttons, knobs, segment thumbs).
    public static DropShadowEffect ControlShadow(DesignPalette palette, double depth = 1) =>
        new() { BlurRadius = 3 * depth, ShadowDepth = .6 * depth, Direction = 270, Color = palette.Shadow, Opacity = palette.Dark ? .5 : .14 };
    // Floating surfaces: menus, popovers, tooltips.
    public static DropShadowEffect FloatingShadow(DesignPalette palette) =>
        new() { BlurRadius = 26, ShadowDepth = 9, Direction = 270, Color = palette.Shadow, Opacity = palette.Dark ? .55 : .17 };
}

public sealed record DesignPalette(
    bool Dark,
    Color Canvas, Color Sidebar, Color SidebarVeil,
    Color Group, Color GroupStroke, Color GroupHighlight, Color Separator,
    Color Label, Color SecondaryLabel, Color TertiaryLabel,
    Color Fill, Color TrackFill, Color QuietFill, Color Hover, Color Pressed,
    Color Accent, Color AccentHover, Color AccentPressed, Color OnAccent,
    Color Control, Color ControlBottom, Color ControlStroke, Color Field,
    Color Knob, Color Inactive, Color Shadow, double ShadowOpacity,
    Color Red, Color Orange, Color Green)
{
    public static string ToHex(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
}

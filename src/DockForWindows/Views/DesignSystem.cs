using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DockForWindows.Views;

// The app's design language in one place: type, colour, shape, light and shadow.
//
// Values come from Apple's macOS 26 design kit (system colours, label/fill opacities, Liquid Glass recipe,
// control metrics), adapted to Windows: Segoe UI Variable carries Latin text and Glow Sans SC carries Chinese,
// the way SF and PingFang pair on a Mac.
//
// Colour is never painted flat. Windows sit on a frosted copy of the desktop picture (WallpaperBackdrop), and
// every surface above it is a veil, a fill or a label with alpha, so the desktop's hue runs through the whole
// window the way macOS tints windows and sidebars. Light falls from the upper left: glass carries a bright
// rim on the lit edges and a soft shadow below.
public static class DesignSystem
{
    private static readonly Uri FontBase = new("pack://application:,,,/DockForWindows;component/");
    // Text (body, labels, small sizes): Segoe UI Variable for Latin and Microsoft YaHei UI for Chinese — both
    // TrueType fonts hinted for screens, so 11-14 px stays crisp even on the transparent (grayscale-AA) window.
    // Glow Sans SC is a CFF font without hinting: beautiful from 15 px up, soft below, so it is reserved for titles.
    public static FontFamily TextFont { get; } = new(FontBase, "Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI, ./Assets/Fonts/#Glow Sans SC");
    public static FontFamily DisplayFont { get; } = new(FontBase, "Segoe UI Variable Display, Segoe UI, ./Assets/Fonts/#Glow Sans SC, Microsoft YaHei UI");

    // Type scale (DIP): macOS 26 Large Title 26, Title1 22, Title3 15, Body 13 — Chinese reads a half step larger.
    public const double LargeTitle = 26, Title = 22, Headline = 13.5, Body = 13.5, Callout = 12.5, Footnote = 11.5, Caption = 10.5;
    // Shape and rhythm: groups r12 with 10 between them, 20 page inset, sidebar floating 8 from the frame.
    public const double GroupRadius = 12, ControlRadius = 7, TileRadius = 6.5, RowHeight = 44, RowInset = 14, PageInset = 28, SidebarInset = 8, SidebarRadius = 14;

    public static DesignPalette Palette(bool dark,string? accent=null)
    {
        var palette=dark?Graphite:Porcelain;if(accent is null)return palette;
        var color=Hex(DockForWindows.Models.SettingsAccentPresets.Normalize(accent));if(dark&&color.R==0&&color.G==0&&color.B==0)color=Hex("#E6E6E8");
        Color Blend(byte toward,double amount)=>Color.FromRgb((byte)(color.R+(toward-color.R)*amount),(byte)(color.G+(toward-color.G)*amount),(byte)(color.B+(toward-color.B)*amount));
        bool monochromeDark=dark&&string.Equals(accent,"#000000",StringComparison.OrdinalIgnoreCase);
        return palette with{Accent=color,AccentHover=Blend(255,.12),AccentPressed=Blend(0,.12),OnAccent=monochromeDark?Hex("#171719"):palette.OnAccent,Knob=monochromeDark?Hex("#171719"):palette.Knob};
    }

    // Light: a porcelain veil over the frosted desktop; sidebar glass lets three times as much colour through.
    public static readonly DesignPalette Porcelain = new(
        Dark: false,
        Canvas: Hex("#F3F4F7"), Sidebar: Hex("#E8EAEE"), SidebarVeil: Hex("#66FFFFFF"),
        WindowVeil: Hex("#CCF8F8FA"), GlassSheen: Hex("#47FFFFFF"), GlassRimLight: Hex("#E6FFFFFF"), GlassRimShade: Hex("#33FFFFFF"), GlassEdge: Hex("#14000000"), GlassShadowOpacity: .11,
        Group: Hex("#A6FFFFFF"), GroupStroke: Hex("#0D000000"), GroupHighlight: Hex("#CCFFFFFF"), Separator: Hex("#14000000"),
        Label: Hex("#1D1D1F"), SecondaryLabel: Hex("#6E6E73"), TertiaryLabel: Hex("#AEAEB2"),
        Fill: Hex("#21000000"), TrackFill: Hex("#17000000"), QuietFill: Hex("#0F000000"), Hover: Hex("#0B000000"), Pressed: Hex("#17000000"),
        Accent: Hex("#2F8FFF"), AccentHover: Hex("#4A9CFF"), AccentPressed: Hex("#1C7FF0"), OnAccent: Hex("#FFFFFF"),
        Control: Hex("#D9FFFFFF"), ControlBottom: Hex("#B3FFFFFF"), ControlStroke: Hex("#14000000"), Field: Hex("#C7FFFFFF"),
        Knob: Hex("#FFFFFF"), Inactive: Hex("#24000000"), Shadow: Hex("#000000"), ShadowOpacity: .10,
        Red: Hex("#FF383C"), Orange: Hex("#FF8D28"), Green: Hex("#34C759"));

    // Dark: a graphite veil; the glass deepens bright pictures and keeps a faint cool rim of light.
    public static readonly DesignPalette Graphite = new(
        Dark: true,
        Canvas: Hex("#1B1C1F"), Sidebar: Hex("#242528"), SidebarVeil: Hex("#6B26272A"),
        WindowVeil: Hex("#D118191C"), GlassSheen: Hex("#14FFFFFF"), GlassRimLight: Hex("#4DFFFFFF"), GlassRimShade: Hex("#0FFFFFFF"), GlassEdge: Hex("#80000000"), GlassShadowOpacity: .42,
        Group: Hex("#10FFFFFF"), GroupStroke: Hex("#0FFFFFFF"), GroupHighlight: Hex("#1AFFFFFF"), Separator: Hex("#17FFFFFF"),
        Label: Hex("#F5F5F7"), SecondaryLabel: Hex("#A1A1A6"), TertiaryLabel: Hex("#6C6C70"),
        Fill: Hex("#33FFFFFF"), TrackFill: Hex("#24FFFFFF"), QuietFill: Hex("#14FFFFFF"), Hover: Hex("#0FFFFFFF"), Pressed: Hex("#1FFFFFFF"),
        Accent: Hex("#4C9DFF"), AccentHover: Hex("#66ABFF"), AccentPressed: Hex("#3A8CF0"), OnAccent: Hex("#FFFFFF"),
        Control: Hex("#26FFFFFF"), ControlBottom: Hex("#17FFFFFF"), ControlStroke: Hex("#14FFFFFF"), Field: Hex("#33000000"),
        Knob: Hex("#F5F5F7"), Inactive: Hex("#33FFFFFF"), Shadow: Hex("#000000"), ShadowOpacity: .45,
        Red: Hex("#FF4245"), Orange: Hex("#FF9230"), Green: Hex("#30D158"));

    // Category colours for icon tiles, from the macOS 26 accents (top → bottom: lit from above).
    public static readonly (Color Top, Color Bottom)[] TileColors =
    {
        (Hex("#3DA5FF"), Hex("#0071F0")),   // 0 blue     概览
        (Hex("#A9A9B0"), Hex("#6A6A71")),   // 1 graphite 控制中心
        (Hex("#FFB24A"), Hex("#FF7F14")),   // 2 orange   桌面小组件
        (Hex("#8F86FF"), Hex("#5446EB")),   // 3 indigo   外观与主题
        (Hex("#FF6E8C"), Hex("#FF2650")),   // 4 pink     动画与交互
        (Hex("#5FDB7E"), Hex("#22B347")),   // 5 green    性能与渲染
        (Hex("#B4B4BA"), Hex("#7D7D84")),   // 6 gray     通用设置
        (Hex("#4CD8E6"), Hex("#00A9C2")),   // 7 teal     关于
    };

    public static Color Hex(string value) => (Color)ColorConverter.ConvertFromString(value);
    public static SolidColorBrush Brush(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }
    public static Color WithAlpha(Color color, double alpha) => Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);

    // A macOS 26 settings icon: a squircle with a vertical gradient, a glass rim (bright on the lit upper edge,
    // a breath of shade along the bottom), a soft inner glow at the top and a white glyph with a faint drop.
    public static FrameworkElement IconTile(int colorIndex, string glyph, double size = 24)
    {
        var (top, bottom) = TileColors[Math.Clamp(colorIndex, 0, TileColors.Length - 1)];
        var fill = new LinearGradientBrush(top, bottom, 90); fill.Freeze();
        // SquircleBorder's radius is the extent of a cubic superellipse corner, which reads smaller than a
        // circular radius of the same value; 36 % of the edge matches the macOS icon silhouette.
        double radius = size * .36;
        var tile = new SquircleBorder { Width = size, Height = size, CornerRadius = new CornerRadius(radius), Background = fill };
        var glow = new RadialGradientBrush(Color.FromArgb(64, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)) { Center = new Point(.5, 0), GradientOrigin = new Point(.5, 0), RadiusX = .9, RadiusY = .75 }; glow.Freeze();
        var rim = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(150, 255, 255, 255), 0), new(Color.FromArgb(20, 255, 255, 255), .45), new(Color.FromArgb(34, 0, 0, 0), 1) }, 90); rim.Freeze();
        var layers = new Grid();
        layers.Children.Add(new SquircleBorder { CornerRadius = new CornerRadius(radius), Background = glow, IsHitTestVisible = false });
        layers.Children.Add(new SquircleBorder { CornerRadius = new CornerRadius(radius), BorderBrush = rim, BorderThickness = new Thickness(Math.Max(.6, size / 40)), IsHitTestVisible = false });
        var icon = new ControlCenterIcon(glyph) { Width = size * .6, Height = size * .6, Brush = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        icon.Effect = new DropShadowEffect { BlurRadius = Math.Max(1.5, size / 14), ShadowDepth = Math.Max(.4, size / 60), Direction = 270, Opacity = .28, Color = Color.FromRgb(0, 0, 30) };
        layers.Children.Add(icon);
        tile.Child = layers;
        tile.Effect = new DropShadowEffect { BlurRadius = Math.Max(2, size / 8), ShadowDepth = Math.Max(.5, size / 36), Direction = 270, Opacity = .2, Color = Color.FromRgb(16, 18, 40) };
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
    Color WindowVeil, Color GlassSheen, Color GlassRimLight, Color GlassRimShade, Color GlassEdge, double GlassShadowOpacity,
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

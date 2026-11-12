using System;
using System.Windows.Media;
using DockForWindows.Models;

namespace DockForWindows.Services;

public sealed record ThemeVisuals(bool Dark, Color Background, Color Border, Color Indicator,
    double CornerRadius, double BorderThickness, bool ShadowEnabled, double ShadowOpacity, double ShadowBlur);

public static class ThemeService
{
    public static ThemeVisuals Resolve(DockSettings settings, bool systemDark, bool transparencyEnabled)
    {
        var appearance = settings.Appearance.Copy(); appearance.Normalize();
        bool glass = settings.Theme == DockTheme.Glass;
        bool custom = settings.Theme is DockTheme.Custom or DockTheme.Glass;
        bool baseDark = settings.Theme == DockTheme.Dark || (settings.Theme == DockTheme.System && systemDark);
        Color background = Parse(custom ? appearance.BackgroundColor : baseDark ? "#242B38" : "#F2F5FA");
        bool dark = custom ? .2126 * background.R + .7152 * background.G + .0722 * background.B < 140 : baseDark;
        double opacity = custom ? appearance.Opacity : Math.Clamp((dark ? 237d / 255 : 233d / 255) * appearance.Opacity / .93,0,1);
        if (glass) opacity *= .035 * settings.GlassOpacity;
        bool noBase = !AppearancePolicy.HasPlate(settings);
        background.A = noBase ? (byte)0 : transparencyEnabled ? (glass ? Math.Max((byte)1, Alpha(opacity)) : Alpha(opacity)) : (byte)255;
        var border = Parse(custom ? appearance.BorderColor : dark ? "#65748A" : "#FFFFFF");
        border.A = noBase ? (byte)0 : Alpha(custom ? appearance.BorderOpacity : dark ? 80d / 255 : 184d / 255);
        if (!glass) { border=Parse(appearance.BorderColor); border.A=noBase?(byte)0:Alpha(appearance.BorderOpacity); }
        return new(dark, background, border, Parse(custom ? appearance.IndicatorColor : dark ? "#F2F4F8" : "#243047"),
            appearance.CornerRadius, appearance.BorderThickness,
            !noBase && appearance.ShadowEnabled, appearance.ShadowOpacity, appearance.ShadowBlur);
    }
    private static byte Alpha(double opacity) => (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
    private static Color Parse(string color) => (Color)ColorConverter.ConvertFromString(color);
}

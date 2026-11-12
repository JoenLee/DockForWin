using System;
using System.Globalization;

namespace DockForWindows.Models;

public sealed class ThemeAppearance
{
    public string BackgroundColor { get; set; } = "#242B38";
    public double Opacity { get; set; } = .93;
    public double CornerRadius { get; set; } = 22;
    public string BorderColor { get; set; } = "#FFFFFF";
    public double BorderOpacity { get; set; } = .35;
    public double BorderThickness { get; set; } = 1;
    public string IndicatorColor { get; set; } = "#F2F4F8";
    public bool ShadowEnabled { get; set; } = true;
    public double ShadowOpacity { get; set; } = .20;
    public double ShadowBlur { get; set; } = 22;
    public ThemeAppearance Copy() => (ThemeAppearance)MemberwiseClone();
    public void Normalize()
    {
        BackgroundColor = NormalizeColor(BackgroundColor, "#242B38");
        BorderColor = NormalizeColor(BorderColor, "#FFFFFF");
        IndicatorColor = NormalizeColor(IndicatorColor, "#F2F4F8");
        Opacity = Finite(Opacity, 0, 1, .93);
        CornerRadius = Finite(CornerRadius, 0, 100, 22);
        BorderOpacity = Finite(BorderOpacity, 0, 1, .35);
        BorderThickness = Finite(BorderThickness, 0, 3, 1);
        ShadowOpacity = Finite(ShadowOpacity, 0, .65, .20);
        ShadowBlur = Finite(ShadowBlur, 0, 40, 22);
    }
    private static double Finite(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    private static string NormalizeColor(string? value, string fallback) => TryColor(value, out string normalized) ? normalized : fallback;
    public static bool TryColor(string? value, out string normalized)
    {
        string text = (value ?? "").Trim();
        if (text.StartsWith('#')) text = text[1..];
        if (text.Length == 3) text = $"{text[0]}{text[0]}{text[1]}{text[1]}{text[2]}{text[2]}";
        bool valid = text.Length == 6 && uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);
        normalized = valid ? "#" + text.ToUpperInvariant() : "";
        return valid;
    }
}

public static class ThemePresets
{
    public static readonly string[] Names = { "自由调整", "雾白", "深空", "海湾蓝", "暖沙" };
    public static ThemeAppearance Create(string name) => name switch
    {
        "雾白" => new() { BackgroundColor = "#F2F5FA", Opacity = .88, BorderOpacity = .70, IndicatorColor = "#243047", ShadowOpacity = .17 },
        "海湾蓝" => new() { BackgroundColor = "#173B57", Opacity = .88, BorderColor = "#91CBED", BorderOpacity = .55, IndicatorColor = "#A8E4FF", CornerRadius = 26, ShadowOpacity = .26 },
        "暖沙" => new() { BackgroundColor = "#E9D8BD", Opacity = .90, BorderColor = "#FFF5E6", BorderOpacity = .75, IndicatorColor = "#675139", CornerRadius = 18, ShadowOpacity = .16 },
        _ => new()
    };
}

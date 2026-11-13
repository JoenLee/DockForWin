using System.Windows.Media;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal static class GlassTint
{
    // The tint preset as a translucent colour for glass: strong enough to read, weak enough to stay glass.
    public static Color Resolve(DockSettings settings, double alpha = .2)
    {
        if (string.IsNullOrEmpty(settings.GlassTintColor)) return Colors.Transparent;
        try { var c = (Color)ColorConverter.ConvertFromString(settings.GlassTintColor); return Color.FromArgb((byte)(alpha * 255), c.R, c.G, c.B); }
        catch { return Colors.Transparent; }
    }
}

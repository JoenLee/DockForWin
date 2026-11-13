using System;
using DockForWindows.Models;

namespace DockForWindows.Views;

// The two choices people actually make about glass — liquid or frosted, and how far toward 磨砂 or 水光 — turned
// into the detailed material values the Dock and Control Center renderers already understand.
internal static class GlassMaterial
{
    public static void Apply(DockSettings s)
    {
        double q = Math.Clamp(s.GlassQuality, 0, 1);
        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        s.Theme = DockTheme.Glass;
        if (s.LiquidGlass)
        {
            // Liquid: refraction and light grow toward 水光 while frost melts away.
            s.GlassFrost = Lerp(.5, 0, q); s.GlassDistortion = Lerp(.4, .85, q); s.GlassIntensity = Lerp(.35, .8, q);
            s.GlassHighlight = Lerp(.4, .8, q); s.GlassTint = Lerp(.1, .03, q);
            s.ControlCenter.MaterialMode = ControlCenterMaterialMode.LiquidGlass;
        }
        else
        {
            // Frosted milk glass: no lens, a soft even frost, a quiet rim of light; Control Center uses its cheaper blur.
            s.GlassFrost = Lerp(.85, .55, q); s.GlassDistortion = 0; s.GlassIntensity = 0;
            s.GlassHighlight = .45; s.GlassTint = .12;
            s.ControlCenter.MaterialMode = ControlCenterMaterialMode.ClassicBlur;
        }
    }
    // Tint presets offered beside the material: none, ice, lavender, blossom, mint.
    public static readonly (string Name, string Hex)[] Tints = { ("无色", ""), ("冰蓝", "#BFDDFF"), ("薰衣草", "#D9CCFF"), ("樱粉", "#FFCCDB"), ("薄荷", "#C4EEDC") };
}

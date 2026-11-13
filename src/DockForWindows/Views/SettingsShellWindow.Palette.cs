using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace DockForWindows.Views;
internal partial class SettingsShellWindow
{
    protected DesignPalette Design { get; private set; } = DesignSystem.Porcelain;
    // Publishes the design system's palette (Porcelain / Graphite) as the dynamic resources every template uses.
    protected void ApplySettingsPalette(Palette palette)
    {
        var p = Design = DesignSystem.Palette(palette.Dark);
        void Set(string key, Color color) => Resources[key] = DesignSystem.Brush(color);
        void Gradient(string key, Color top, Color bottom, double end = 1) { var brush = new LinearGradientBrush(new GradientStopCollection { new(top, 0), new(bottom, end) }, 90); brush.Freeze(); Resources[key] = brush; }
        Set("SettingsInk", p.Label); Set("SettingsMuted", p.SecondaryLabel); Set("SettingsTertiary", p.TertiaryLabel);
        Set("SettingsLine", p.Separator); Set("SettingsCard", p.Group); Set("SettingsField", p.Field); Set("SettingsCanvas", p.Canvas);
        Set("SettingsSidebar", MicaBackdrop ? p.SidebarVeil : p.Sidebar); Set("SettingsHero", p.QuietFill); Set("SettingsQuiet", p.QuietFill);
        Set("SettingsTrack", p.TrackFill); Set("SettingsSwitchOff", p.Fill); Set("SettingsHover", p.Hover); Set("SettingsPressed", p.Pressed);
        Set("SettingsSidebarHover", p.Hover); Set("SettingsIndicator", p.Dark ? DesignSystem.Hex("#636366") : p.Control);
        Set("SettingsSidebarInk", p.Label); Set("SettingsSidebarIcon", p.SecondaryLabel); Set("SettingsSidebarSelectionInk", p.OnAccent);
        Set("SettingsSelection", p.Accent); Set("SettingsAccent", p.Accent); Set("SettingsAccentHover", p.AccentHover); Set("SettingsAccentPressed", p.AccentPressed); Set("SettingsOnAccent", p.OnAccent);
        Set("SettingsControlStroke", p.ControlStroke); Set("SettingsKnob", p.Knob); Set("SettingsFocusRing", DesignSystem.WithAlpha(p.Accent, .45));
        Set("SettingsScrollThumb", p.Dark ? DesignSystem.Hex("#4DFFFFFF") : DesignSystem.Hex("#47000000"));
        Set("SettingsMenu", p.Dark ? DesignSystem.Hex("#F22C2C2E") : DesignSystem.Hex("#FAFAFAFC")); Set("SettingsMenuStroke", p.Dark ? DesignSystem.Hex("#1FFFFFFF") : DesignSystem.Hex("#1A000000"));
        // Light from above: the group edge is brighter along the top in dark mode; controls carry a sheen.
        Gradient("SettingsGroupEdge", p.Dark ? p.GroupHighlight : p.GroupStroke, p.GroupStroke, .35);
        Gradient("SettingsControlFill", p.Control, p.ControlBottom);
        Gradient("SettingsControlSheen", p.Dark ? DesignSystem.Hex("#1FFFFFFF") : DesignSystem.Hex("#B3FFFFFF"), DesignSystem.Hex("#00FFFFFF"), .5);
        Gradient("SettingsPrimaryFill", p.AccentHover, p.Accent);
        Gradient("SettingsPrimarySheen", DesignSystem.Hex("#40FFFFFF"), DesignSystem.Hex("#00FFFFFF"), .55);
        Resources["SettingsShadowColor"] = p.Shadow; Resources["SettingsAccentColor"] = p.Accent;
        Resources["SettingsControlShadowOpacity"] = p.Dark ? .5 : .14; Resources["SettingsFloatingShadowOpacity"] = p.Dark ? .55 : .17;
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "SettingsInk", "SettingsMuted", "SettingsTertiary", "SettingsSidebarInk" }) Resources[key] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "SettingsCard", "SettingsField", "SettingsCanvas", "SettingsSidebar" }) Resources[key] = SystemColors.WindowBrush;
            Resources["SettingsSidebarSelectionInk"] = SystemColors.HighlightTextBrush; Resources["SettingsSelection"] = SystemColors.HighlightBrush; Resources["SettingsAccent"] = SystemColors.HighlightBrush;
        }
        _sidebar.SetResourceReference(Panel.BackgroundProperty, "SettingsSidebar"); _right.SetResourceReference(Panel.BackgroundProperty, "SettingsCanvas");
        _selection.SetResourceReference(Border.BackgroundProperty, "SettingsSelection");
        Foreground = (Brush)Resources["SettingsInk"];
        ApplyBackdrop();
    }
}

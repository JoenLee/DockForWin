using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace DockForWindows.Views;
internal partial class SettingsShellWindow
{
    protected DesignPalette Design { get; private set; } = DesignSystem.Porcelain;
    // Publishes the design system's palette (Porcelain / Graphite) as the dynamic resources every template uses.
    // Labels, fills and lines carry alpha so the desktop's hue reaches through them, as on macOS.
    protected void ApplySettingsPalette(Palette palette)
    {
        var p = Design = DesignSystem.Palette(palette.Dark,_draft.InterfaceAccent);
        void Set(string key, Color color) => Resources[key] = DesignSystem.Brush(color);
        void Gradient(string key, Color top, Color bottom, double end = 1) { var brush = new LinearGradientBrush(new GradientStopCollection { new(top, 0), new(bottom, end) }, 90); brush.Freeze(); Resources[key] = brush; }
        Set("SettingsInk", p.Label); Set("SettingsMuted", p.SecondaryLabel); Set("SettingsTertiary", p.TertiaryLabel);
        Set("SettingsLine", p.Separator); Set("SettingsCard", p.Group); Set("SettingsField", p.Field); Set("SettingsCanvas", Colors.Transparent);
        Set("SettingsSidebar", p.Dark?DesignSystem.Hex("#242427"):DesignSystem.Hex("#E8E9ED")); Set("SettingsHero", p.QuietFill); Set("SettingsQuiet", p.QuietFill);
        Set("SettingsTrack", p.TrackFill); Set("SettingsSwitchOff", p.Fill); Set("SettingsHover", p.Hover); Set("SettingsPressed", p.Pressed);
        Set("SettingsSidebarHover", p.Hover); Set("SettingsIndicator", p.Dark ? DesignSystem.Hex("#5E5E62") : DesignSystem.Hex("#FFFFFF"));
        Set("SettingsSidebarInk", p.Label); Set("SettingsSidebarIcon", p.SecondaryLabel); Set("SettingsSidebarSelectionInk", p.Label);
        // The selected sidebar row is neutral glass, as in macOS 26 (a darker breath of the pane, not an accent bar).
        Set("SettingsSelection", p.Dark ? DesignSystem.Hex("#24FFFFFF") : DesignSystem.Hex("#14000000"));
        Set("SettingsAccent", p.Accent); Set("SettingsAccentHover", p.AccentHover); Set("SettingsAccentPressed", p.AccentPressed); Set("SettingsOnAccent", p.OnAccent);
        Set("SettingsControlStroke", p.ControlStroke); Set("SettingsKnob", p.Knob); Set("SettingsFocusRing", DesignSystem.WithAlpha(p.Accent, .42));
        Set("SettingsScrollThumb", p.Dark ? DesignSystem.Hex("#4DFFFFFF") : DesignSystem.Hex("#40000000"));
        Set("SettingsMenu", p.Dark ? DesignSystem.Hex("#F2323236") : DesignSystem.Hex("#F7FBFBFD")); Set("SettingsMenuStroke", p.Dark ? DesignSystem.Hex("#1FFFFFFF") : DesignSystem.Hex("#14000000"));
        Set("SettingsGroupHighlight", p.GroupHighlight);
        // Groups: a hairline that is lighter along the top (light from above) in both appearances.
        Gradient("SettingsGroupEdge", p.GroupHighlight, p.GroupStroke, .3);
        Gradient("SettingsControlFill", p.Control, p.ControlBottom);
        Gradient("SettingsControlSheen", p.Dark ? DesignSystem.Hex("#1AFFFFFF") : DesignSystem.Hex("#99FFFFFF"), DesignSystem.Hex("#00FFFFFF"), .5);
        Gradient("SettingsPrimaryFill", p.AccentHover, p.Accent);
        Gradient("SettingsPrimarySheen", DesignSystem.Hex("#47FFFFFF"), DesignSystem.Hex("#00FFFFFF"), .55);
        Resources["SettingsShadowColor"] = p.Shadow; Resources["SettingsAccentColor"] = p.Accent;
        Resources["SettingsControlShadowOpacity"] = p.Dark ? .45 : .12; Resources["SettingsFloatingShadowOpacity"] = p.Dark ? .55 : .16;
        Set("SettingsPrimaryTint", DesignSystem.WithAlpha(p.Accent, p.Dark ? .2 : .14)); Set("SettingsPrimaryRim", DesignSystem.WithAlpha(p.Accent, p.Dark ? .34 : .28));
        Set("SettingsAccentInk", p.Accent);
        // Flat: an opaque off-white (graphite) card, rows a step darker (lighter), no edges, no sheen.
        Set("SettingsCanvas", p.Dark ? DesignSystem.Hex("#1C1C1E") : DesignSystem.Hex("#F7F7F8"));
        Set("SettingsCard", p.Dark ? DesignSystem.Hex("#2A2A2D") : DesignSystem.Hex("#EDEEF1"));
        Set("SettingsLine", p.Dark ? DesignSystem.Hex("#17FFFFFF") : DesignSystem.Hex("#12000000"));
        // Flat cards still need an edge to read against the canvas: a 1 px line, a touch lighter along the top in dark.
        Gradient("SettingsGroupEdge", p.Dark ? DesignSystem.Hex("#14FFFFFF") : DesignSystem.Hex("#0D000000"), p.Dark ? DesignSystem.Hex("#0AFFFFFF") : DesignSystem.Hex("#0D000000"), .3);
        var canvas = p.Dark ? DesignSystem.Hex("#1C1C1E") : DesignSystem.Hex("#F7F7F8");
        Gradient("SettingsCanvasFade", canvas, DesignSystem.WithAlpha(canvas, 0));
        Set("SettingsScrollThumb", p.Dark ? DesignSystem.Hex("#47FFFFFF") : DesignSystem.Hex("#40000000"));
        Set("SettingsTertiary", p.Dark ? DesignSystem.Hex("#7C7C82") : p.TertiaryLabel);
        if (p.Dark) { Set("SettingsTrack", DesignSystem.Hex("#29FFFFFF")); Set("SettingsSwitchOff", DesignSystem.Hex("#38FFFFFF")); Set("SettingsHover", DesignSystem.Hex("#14FFFFFF")); Set("SettingsPressed", DesignSystem.Hex("#24FFFFFF")); }
        Set("SettingsControlFill", p.Dark ? DesignSystem.Hex("#3A3A3E") : DesignSystem.Hex("#FFFFFF"));
        Resources["SettingsControlSheen"] = Brushes.Transparent;
        Resources["SettingsControlShadowOpacity"] = p.Dark ? .3 : .07;
        Set("SettingsRaised", p.Dark ? DesignSystem.Hex("#30353E") : Colors.White);
        Set("SettingsRaisedEdge", p.Dark ? DesignSystem.Hex("#14FFFFFF") : DesignSystem.Hex("#0F0A1A33"));
        Set("SettingsDanger", p.Dark ? DesignSystem.Hex("#FF6B66") : DesignSystem.Hex("#C9302C"));
        ApplyGlassPalette(p);
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "SettingsInk", "SettingsMuted", "SettingsTertiary", "SettingsSidebarInk", "SettingsSidebarSelectionInk" }) Resources[key] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "SettingsCard", "SettingsField", "SettingsCanvas", "SettingsSidebar" }) Resources[key] = SystemColors.WindowBrush;
            Resources["SettingsSelection"] = SystemColors.HighlightBrush; Resources["SettingsAccent"] = SystemColors.HighlightBrush; Resources["SettingsSidebarSelectionInk"] = SystemColors.HighlightTextBrush;
        }
        RefreshFloatingSidebar(); _right.SetResourceReference(Panel.BackgroundProperty, "SettingsCanvas");
        _selection.SetResourceReference(Border.BackgroundProperty, "SettingsSelection");
        Foreground = (Brush)Resources["SettingsInk"];
        ApplyBackdrop();
    }
}

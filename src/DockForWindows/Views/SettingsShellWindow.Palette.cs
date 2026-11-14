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
        var p = Design = DesignSystem.Palette(palette.Dark);
        void Set(string key, Color color) => Resources[key] = DesignSystem.Brush(color);
        void Gradient(string key, Color top, Color bottom, double end = 1) { var brush = new LinearGradientBrush(new GradientStopCollection { new(top, 0), new(bottom, end) }, 90); brush.Freeze(); Resources[key] = brush; }
        Set("SettingsInk", p.Label); Set("SettingsMuted", p.SecondaryLabel); Set("SettingsTertiary", p.TertiaryLabel);
        Set("SettingsLine", p.Separator); Set("SettingsCard", p.Group); Set("SettingsField", p.Field); Set("SettingsCanvas", Colors.Transparent);
        Set("SettingsSidebar", Colors.Transparent); Set("SettingsHero", p.QuietFill); Set("SettingsQuiet", p.QuietFill);
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
        Set("SettingsAccentInk", p.Dark ? DesignSystem.Hex("#7CB8FF") : DesignSystem.Hex("#0A6AD6"));
        // Flat, cool and calm (UI_UX_Audit_And_Elevation.md, blueprint §2): white surfaces float on a mist-blue canvas;
        // in dark, ink-blue graphite with the same hue. Every grey carries the same ~218° tint, so nothing reads as
        // dead plastic grey. Blue only means "on / value / primary / focus"; "selected" is a raised white pill.
        var c = (string light, string dark) => DesignSystem.Hex(p.Dark ? dark : light);
        var canvas = c("#EEF1F5", "#15171B");
        Set("SettingsCanvas", canvas);
        Set("SettingsCard", c("#FFFFFF", "#1F2228"));
        Set("SettingsLine", c("#EBEEF3", "#2C3038"));
        // Flat cards keep a 1 px edge; in dark the top edge is the lit one.
        Gradient("SettingsGroupEdge", c("#E2E7EE", "#12FFFFFF"), c("#E2E7EE", "#08FFFFFF"), .3);
        Gradient("SettingsCanvasFade", canvas, DesignSystem.WithAlpha(canvas, 0));
        Gradient("SettingsCanvasFadeUp", DesignSystem.WithAlpha(canvas, 0), canvas);
        Set("SettingsInk", c("#18202D", "#E8EBF0")); Set("SettingsSidebarInk", c("#18202D", "#E8EBF0")); Set("SettingsSidebarSelectionInk", c("#18202D", "#E8EBF0"));
        Set("SettingsMuted", c("#5A6576", "#A3ABB8")); Set("SettingsTertiary", c("#97A1AF", "#6E7684"));
        Set("SettingsTrack", c("#E3E8EF", "#343944")); Set("SettingsSwitchOff", c("#D5DCE5", "#3A404B"));
        Set("SettingsHover", c("#0A18202D", "#0FFFFFFF")); Set("SettingsPressed", c("#1418202D", "#1AFFFFFF"));
        Set("SettingsAccent", c("#2F8FFF", "#3D8EF0")); Set("SettingsAccentHover", c("#4A9CFF", "#5A9EF5")); Set("SettingsAccentPressed", c("#1C7FF0", "#2F7FE0"));
        Set("SettingsAccentInk", c("#1F6FE0", "#8CC0FF"));
        Set("SettingsFocusRing", c("#6B2F8FFF", "#D98CC0FF"));
        // Raised: the selected pill of tabs, segments and lists; Quiet: the trough it sits in.
        Set("SettingsRaised", c("#FFFFFF", "#30353E")); Set("SettingsRaisedEdge", c("#0F0A1A33", "#14FFFFFF"));
        Set("SettingsQuiet", c("#E9EDF2", "#191B20")); Set("SettingsHero", c("#E9EDF2", "#191B20"));
        Set("SettingsField", c("#F3F5F8", "#191B20"));
        Set("SettingsControlFill", c("#F3F5F8", "#2A2E35")); Set("SettingsControlStroke", c("#E2E7EE", "#14FFFFFF"));
        Set("SettingsKnob", c("#FFFFFF", "#E9ECF1"));
        Set("SettingsMenu", c("#FFFFFF", "#2A2E35")); Set("SettingsMenuStroke", c("#E2E7EE", "#1AFFFFFF"));
        Set("SettingsScrollThumb", c("#380A1A33", "#42FFFFFF"));
        Set("SettingsPrimaryTint", DesignSystem.WithAlpha(DesignSystem.Hex(p.Dark ? "#3D8EF0" : "#2F8FFF"), p.Dark ? .22 : .13));
        Set("SettingsPrimaryRim", DesignSystem.WithAlpha(DesignSystem.Hex(p.Dark ? "#3D8EF0" : "#2F8FFF"), p.Dark ? .4 : .28));
        Set("SettingsDanger", c("#C9302C", "#FF6B66"));
        Resources["SettingsShadowColor"] = p.Dark ? Colors.Black : DesignSystem.Hex("#0B1730");
        Resources["SettingsControlSheen"] = Brushes.Transparent;
        Resources["SettingsControlShadowOpacity"] = p.Dark ? .3 : .06;
        Resources["SettingsRaisedShadowOpacity"] = p.Dark ? .35 : .1;
        Resources["SettingsFloatingShadowOpacity"] = p.Dark ? .55 : .14;
        _tabMarker.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Direction = 270, Color = p.Dark ? Colors.Black : DesignSystem.Hex("#0B1730"), Opacity = p.Dark ? .35 : .1 };
        ApplyGlassPalette(p);
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "SettingsInk", "SettingsMuted", "SettingsTertiary", "SettingsSidebarInk", "SettingsSidebarSelectionInk" }) Resources[key] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "SettingsCard", "SettingsField", "SettingsCanvas", "SettingsSidebar" }) Resources[key] = SystemColors.WindowBrush;
            Resources["SettingsSelection"] = SystemColors.HighlightBrush; Resources["SettingsAccent"] = SystemColors.HighlightBrush; Resources["SettingsSidebarSelectionInk"] = SystemColors.HighlightTextBrush;
        }
        _sidebar.SetResourceReference(Panel.BackgroundProperty, "SettingsSidebar"); _right.SetResourceReference(Panel.BackgroundProperty, "SettingsCanvas");
        _selection.SetResourceReference(Border.BackgroundProperty, "SettingsSelection");
        Foreground = (Brush)Resources["SettingsInk"];
        ApplyBackdrop();
    }
}

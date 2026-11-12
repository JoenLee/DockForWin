using DockForWindows.Models;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private bool ResolveLight(double luminance,bool? previous)=>UsesClassicBlur?true:_options.ColorMode switch
    {
        ControlCenterColorMode.Light=>false,
        ControlCenterColorMode.Dark=>true,
        _=>AdaptiveGlassAppearance.LightIndicator(luminance,previous)
    };
}

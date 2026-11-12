using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Models;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private ClassicBlurBackdrop? _classicBackdrop;
    internal bool UsesClassicBlur=>_options.MaterialMode==ControlCenterMaterialMode.ClassicBlur&&!SystemParameters.HighContrast;
    private static readonly Brush ClassicLightCard=Palette.Brush("#A84A4D52");
    private static readonly Brush ClassicDarkCard=Palette.Brush("#A8383B40");
    private void ConfigureSurfaceMaterial(DockSurface surface)
    {
        surface.ClassicMaterial=UsesClassicBlur;surface.UseControlCenterLighting();
        surface.Fill=UsesClassicBlur?(_options.ColorMode==ControlCenterColorMode.Light?ClassicLightCard:ClassicDarkCard):SystemParameters.HighContrast?_palette.SettingsCard:Brushes.White;
        if(UsesClassicBlur){surface.Backdrop=null;surface.Opacity=1;}
        surface.InvalidateVisual();
    }
    private void UpdateMaterialMask()
    {
        if(Content is not Grid host)return;
        if(UsesClassicBlur){host.OpacityMask=null;return;}
        double feather=System.Math.Min(.12,(SideGutter-20)/System.Math.Max(1,host.ActualWidth));
        host.OpacityMask=new LinearGradientBrush(new GradientStopCollection{new(Colors.Transparent,0),new(Colors.White,feather),new(Colors.White,1-feather),new(Colors.Transparent,1)},new Point(0,0),new Point(1,0));
    }
    private void ApplyMaterialMode()
    {
        UpdateMaterialMask();
        if(!SystemParameters.HighContrast){
            if(_classicBackdrop is null){_classicBackdrop=new();Panel.SetZIndex(_classicBackdrop,-100);((Grid)Content).Children.Add(_classicBackdrop);}
            _classicBackdrop.Visibility=Visibility.Visible;
        }else if(_classicBackdrop is not null){_classicBackdrop.Visibility=Visibility.Collapsed;_classicBackdrop.Release();}
        if(UsesClassicBlur){
            if(_classicCards is null){_classicCards=new(false,40){Opacity=.83};Panel.SetZIndex(_classicCards,-90);((Grid)Content).Children.Add(_classicCards);}
            _classicCards.Visibility=Visibility.Visible;
        }else if(_classicCards is not null){_classicCards.Visibility=Visibility.Collapsed;_classicCards.Release();}
        UpdateClassicBackdrop();
        _surfaceFrames.Clear();
        foreach(var surface in _surfaces)ConfigureSurfaceMaterial(surface);
        RefreshChrome();
        if(!UsesClassicBlur)MapGlassFrame(true);
    }
    private bool? _classicHeaderLight;
    private void RefreshHeaderForeground(bool force=false)
    {
        if(_modules.Count==0)return;
        var first=_modules[0];bool light=UsesClassicBlur?AdaptiveGlassAppearance.LightIndicator(first.Contrast.Luminance,_classicHeaderLight):first.Light==true;
        if(UsesClassicBlur)_classicHeaderLight=light;
        var foreground=light?Brushes.White:DarkInk;if(!force&&Foreground==foreground)return;
        Foreground=foreground;TextForeground(_root.Children[0],foreground);TextForeground(_edit,foreground);TextForeground(_add,foreground);_message.Foreground=foreground;
    }
    private void UpdateClassicBackdrop()
    {
        if(SystemParameters.HighContrast||_wholeFrame is not {} frame)return;
        _classicBackdrop?.Update(frame,GlassBackdropBounds);
        if(Content is Grid host&&_root.RenderSize.Width>0&&_root.RenderSize.Height>0){
            double luminance=0;foreach(var module in _modules)luminance+=module.Contrast.Luminance;
            _classicBackdrop?.SetFalloff(_root.TransformToAncestor(host).TransformBounds(new Rect(_root.RenderSize)),_modules.Count>0?luminance/_modules.Count:.5);
        }
        if(UsesClassicBlur){_classicCards?.Update(frame,GlassBackdropBounds);UpdateCardFrost();}
    }
}

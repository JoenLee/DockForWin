using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
namespace DockForWindows.Views;
public sealed partial class DockWindow
{
    private sealed class SeparatorState
    {internal Border Border=null!;internal readonly AdaptiveGlassAppearance Contrast=new();internal bool? Light;}
    private Border? _separator;
    private readonly List<SeparatorState> _separators=new();
    private void RegisterSeparator(Border border){_separator??=border;_separators.Add(new(){Border=border});}
    private void RestoreSeparatorColor()
    {
        foreach(var state in _separators){if(state.Light is null||state.Border.Background is not SolidColorBrush brush)continue;state.Light=null;brush.BeginAnimation(SolidColorBrush.ColorProperty,null);brush.Color=((SolidColorBrush)_palette.Text).Color;}
    }
    private void UpdateSeparatorBackdrop(BitmapSource frame,Rect bounds,double tint,double seconds,bool animate)
    {
        foreach(var state in _separators){var border=state.Border;if(!border.IsLoaded||PresentationSource.FromVisual(border) is null||border.Background is not SolidColorBrush brush)continue;
            var center=border.PointToScreen(new Point(border.ActualWidth/2,border.ActualHeight/2));int pad=(int)Math.Ceiling(6*VisualTreeHelper.GetDpi(border).DpiScaleX);
            state.Contrast.Observe(frame,new Int32Rect((int)(center.X-bounds.X)-pad,(int)(center.Y-bounds.Y)-pad,pad*2,pad*2),seconds);
            bool light=AdaptiveGlassAppearance.LightIndicator(state.Contrast.Luminance*(1-tint),state.Light);if(state.Light==light)continue;state.Light=light;
            var target=light?Color.FromRgb(245,246,248):Color.FromRgb(24,26,30);
            if(animate)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(target,TimeSpan.FromMilliseconds(240)){EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}});
            else{brush.BeginAnimation(SolidColorBrush.ColorProperty,null);brush.Color=target;}
        }
    }
}

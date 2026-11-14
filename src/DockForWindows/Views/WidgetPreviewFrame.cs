using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DockForWindows.Views;

// Static gallery lighting, separate from the widget's own (desktop) appearance.
internal sealed class WidgetPreviewFrame : Grid
{
    internal WidgetPreviewFrame(FrameworkElement content)
    {
        var light=new RadialGradientBrush();
        light.GradientStops.Add(new GradientStop(Color.FromArgb(135,255,255,255),0));
        light.GradientStops.Add(new GradientStop(Color.FromArgb(45,255,255,255),.5));
        light.GradientStops.Add(new GradientStop(Colors.Transparent,1));light.Freeze();
        Children.Add(new Border{Background=light,IsHitTestVisible=false});
        var shade=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(0,1)};
        shade.GradientStops.Add(new GradientStop(Color.FromArgb(18,0,0,0),0));
        shade.GradientStops.Add(new GradientStop(Colors.Transparent,.22));
        shade.GradientStops.Add(new GradientStop(Colors.Transparent,.76));
        shade.GradientStops.Add(new GradientStop(Color.FromArgb(22,0,0,0),1));shade.Freeze();
        Children.Add(new Border{Background=shade,IsHitTestVisible=false});
        content.Margin=new Thickness(12);Children.Add(content);
        SizeChanged+=(_,_)=>Clip=new RectangleGeometry(new Rect(RenderSize),14,14);
    }
}

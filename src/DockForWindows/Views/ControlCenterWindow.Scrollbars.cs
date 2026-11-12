using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private static void InstallShortScrollbars(ScrollViewer viewer)
    {
        var style=new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(FrameworkElement.WidthProperty,8d));
        style.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty,76d));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center));
        var track=new FrameworkElementFactory(typeof(ControlCenterScrollTrack),"PART_Track");
        track.SetValue(Track.OrientationProperty,Orientation.Vertical);track.SetValue(Track.IsDirectionReversedProperty,true);
        foreach(var property in new[]{Track.MinimumProperty,Track.MaximumProperty,Track.ValueProperty,Track.ViewportSizeProperty})track.SetBinding(property,new Binding(property.Name){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});
        style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ScrollBar)){VisualTree=track}));viewer.Resources[typeof(ScrollBar)]=style;
    }
}

internal sealed class ControlCenterScrollTrack:Track
{
    public ControlCenterScrollTrack()
    {
        var thumb=new Thumb{MinHeight=18,Background=new SolidColorBrush(Color.FromArgb(130,160,160,160))};
        var capsule=new FrameworkElementFactory(typeof(Border));capsule.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));capsule.SetValue(FrameworkElement.WidthProperty,4d);capsule.SetBinding(Border.BackgroundProperty,new Binding("Background"){RelativeSource=new RelativeSource(RelativeSourceMode.TemplatedParent)});thumb.Template=new ControlTemplate(typeof(Thumb)){VisualTree=capsule};
        Thumb=thumb;
        DecreaseRepeatButton=new RepeatButton{Command=ScrollBar.PageUpCommand,Opacity=0,Focusable=false};
        IncreaseRepeatButton=new RepeatButton{Command=ScrollBar.PageDownCommand,Opacity=0,Focusable=false};

    }
}

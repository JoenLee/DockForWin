using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DockForWindows.Views;

internal static class NavigationGlyphs
{
    private static readonly string[] Paths={
        "M12,3 A9,9 0 1 1 11.99,3 M12,3 L12,21 M12,3 A9,9 0 0 0 12,21",
        "M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z",
        "M2,14 C5,14 5,5 9,5 S13,19 17,19 S20,10 22,10 M18,7 L22,10 L19,13",
        "M3,6 H10 L12,9 H21 V20 H3 Z M3,6 V4 H10 L12,6 H20 V9",
        "M3,4 H21 V17 H3 Z M8,21 H16 M12,17 V21 M6,10 H18 M6,10 L9,7 M6,10 L9,13 M18,10 L15,7 M18,10 L15,13",
        "M9,3 H15 L16,6 L19,7 L21,10 V14 L18,16 L17,19 L14,21 H10 L8,18 L5,17 L3,14 V10 L6,8 L7,5 Z M12,8 A4,4 0 1 1 11.99,8",
        "M21,12 A9,9 0 1 0 12,21 H14 C17,21 17,17 15,16 C13,15 15,12 18,13 C20,14 21,14 21,12 Z M7,8 H7.01 M11,6 H11.01 M16,7 H16.01 M6,13 H6.01"
    };
    public static FrameworkElement Create(int index)
    {
        var path=new Path{Data=Geometry.Parse(Paths[index%Paths.Length]),StrokeThickness=1.65,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Stretch=Stretch.Uniform,Width=18,Height=18,VerticalAlignment=VerticalAlignment.Center};
        path.SetBinding(Shape.StrokeProperty,new Binding("Foreground"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(ListBoxItem),1)});
        System.Windows.Automation.AutomationProperties.SetAutomationId(path,"navigation-glyph-"+index);
        return path;
    }
}


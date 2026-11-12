using System.Windows;
using System.Windows.Media;
namespace DockForWindows.Views;
internal sealed class WindowControlGlyph:FrameworkElement
{
    private readonly Geometry _shape;private readonly Brush? _fill;private readonly Pen? _pen;
    internal WindowControlGlyph(string label,Brush ink)
    {
        Width=Height=10;IsHitTestVisible=false;HorizontalAlignment=HorizontalAlignment.Center;VerticalAlignment=VerticalAlignment.Center;
        _shape=Geometry.Parse(label=="×"?"M-2.7,-2.7 L2.7,2.7 M2.7,-2.7 L-2.7,2.7":label=="−"?"M-2.7,0 L2.7,0":"M-4,-4 L1,-3 L-3,1 Z M4,4 L-1,3 L3,-1 Z");_shape.Freeze();
        if(label is "×" or "−"){_pen=new Pen(ink,1.8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};_pen.Freeze();}else _fill=ink;
    }
    protected override void OnRender(DrawingContext dc){dc.PushTransform(new TranslateTransform(ActualWidth/2,ActualHeight/2));dc.DrawGeometry(_fill,_pen,_shape);dc.Pop();}
}

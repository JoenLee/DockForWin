using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DockForWindows.Views;

public class SquircleBorder : Border
{
    private Size _clipSize;
    private double _clipRadius=-1;
    private Geometry? _outline;
    public static Geometry Outline(Size size, double radius)
    {
        var g = new StreamGeometry(); double w = Math.Max(0, size.Width), h = Math.Max(0, size.Height);
        double r = Math.Clamp(radius, 0, Math.Min(w, h) / 2);
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(r, 0), true, true); c.LineTo(new Point(w-r, 0), true, false);
            Corner((x,y) => new Point(w-r+x*r,y*r)); c.LineTo(new Point(w,h-r),true,false);
            Corner((x,y) => new Point(w-y*r,h-r+x*r)); c.LineTo(new Point(r,h),true,false);
            Corner((x,y) => new Point(r-x*r,h-y*r)); c.LineTo(new Point(0,r),true,false);
            Corner((x,y) => new Point(y*r,r-x*r));
            void Corner(Func<double,double,Point> point)
            {
                const int segments = 32;
                // Fixed cubic superellipse. Symmetric halves avoid endpoint singularities.
                double m = Math.Pow(.5, 1d/3);
                for (int i=1;i<=segments;i++) { double x=m*i/segments; c.LineTo(point(x,1-Math.Pow(1-x*x*x,1d/3)),true,false); }
                for (int i=segments-1;i>=0;i--) { double x=m*i/segments; c.LineTo(point(Math.Pow(1-x*x*x,1d/3),1-x),true,false); }
            }
        }
        g.Freeze(); return g;
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(_outline is null || _clipSize!=RenderSize || _clipRadius!=CornerRadius.TopLeft)
        { _clipSize=RenderSize;_clipRadius=CornerRadius.TopLeft;_outline=Outline(RenderSize,_clipRadius);Clip=_outline; }
        var geometry = _outline;
        dc.DrawGeometry(Background, BorderBrush is not null && BorderThickness.Left > 0 ? new Pen(BorderBrush,BorderThickness.Left) : null, geometry);
    }
}

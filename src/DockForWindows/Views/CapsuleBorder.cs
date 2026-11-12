using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DockForWindows.Views;

// The same fixed cubic profile on buttons, switches and slider rails.
public sealed class CapsuleBorder : Border
{
    // Close to a pill, with curvature easing to zero where the ends join
    // the straight sides. n > 2 avoids the curvature jump of a semicircle.
    public const double PillExponent=2.2;
    public static Geometry SmoothOutline(Size size)
    {
        double w=System.Math.Max(0,size.Width),h=System.Math.Max(0,size.Height),r=System.Math.Min(w,h)/2;
        var geometry=new StreamGeometry();
        using(var c=geometry.Open())
        {
            c.BeginFigure(new Point(r,0),true,true);c.LineTo(new Point(w-r,0),true,false);
            Corner((x,y)=>new Point(w-r+x*r,y*r));c.LineTo(new Point(w,h-r),true,false);
            Corner((x,y)=>new Point(w-y*r,h-r+x*r));c.LineTo(new Point(r,h),true,false);
            Corner((x,y)=>new Point(r-x*r,h-y*r));c.LineTo(new Point(0,r),true,false);
            Corner((x,y)=>new Point(y*r,r-x*r));
            void Corner(System.Func<double,double,Point> point)
            {
                const int segments=48;double m=System.Math.Pow(.5,1/PillExponent);
                for(int i=1;i<=segments;i++){double x=m*i/segments;c.LineTo(point(x,1-System.Math.Pow(1-System.Math.Pow(x,PillExponent),1/PillExponent)),true,false);}
                for(int i=segments-1;i>=0;i--){double x=m*i/segments;c.LineTo(point(System.Math.Pow(1-System.Math.Pow(x,PillExponent),1/PillExponent),1-x),true,false);}
            }
        }
        geometry.Freeze();return geometry;
    }
    protected override void OnRender(DrawingContext dc)
    {
        double inset=BorderThickness.Left/2;
        var rect=new Rect(inset,inset,System.Math.Max(0,ActualWidth-2*inset),System.Math.Max(0,ActualHeight-2*inset));
        var outline=SquircleBorder.Outline(rect.Size,System.Math.Min(rect.Width,rect.Height)/2);
        dc.PushTransform(new TranslateTransform(inset,inset));
        dc.DrawGeometry(Background,BorderBrush is not null&&inset>0?new Pen(BorderBrush,inset*2):null,outline);
        dc.Pop();
    }
}

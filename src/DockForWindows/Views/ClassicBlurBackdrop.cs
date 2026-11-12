using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DockForWindows.Views;

// One live blur below all controls; neither text nor individual cards enter the blur pass.
internal sealed class ClassicBlurBackdrop : FrameworkElement
{
    private readonly Image _image=new(){Stretch=Stretch.Fill,IsHitTestVisible=false,
        Effect=new BlurEffect{Radius=22,KernelType=KernelType.Gaussian,RenderingBias=RenderingBias.Quality}};
    private readonly bool _feather;
    private Rect _imageRect=Rect.Empty;
    private Size _clipSize;
    private readonly Rectangle _tint=new(){Fill=Brushes.Black,Opacity=0,IsHitTestVisible=false};
    private DrawingBrush? _falloff;
    public ClassicBlurBackdrop(bool feather=true,double radius=22){_feather=feather;((BlurEffect)_image.Effect).Radius=radius;IsHitTestVisible=false;AddVisualChild(_image);if(feather)AddVisualChild(_tint);}
    protected override int VisualChildrenCount=>_feather?2:1;
    protected override Visual GetVisualChild(int index)=>index==0?_image:index==1&&_feather?_tint:throw new ArgumentOutOfRangeException(nameof(index));
    protected override Size MeasureOverride(Size available)=>new();
    protected override Size ArrangeOverride(Size finalSize)
    {
        if(_clipSize!=finalSize){_clipSize=finalSize;var clip=new RectangleGeometry(new Rect(finalSize));clip.Freeze();Clip=clip;}
        if(!_imageRect.IsEmpty){_image.Measure(_imageRect.Size);_image.Arrange(_imageRect);}
        if(_feather){_tint.Measure(finalSize);_tint.Arrange(new Rect(finalSize));}
        return finalSize;
    }
    public void Update(BitmapSource frame,Rect bounds)
    {
        if(!ReferenceEquals(_image.Source,frame))_image.Source=frame;
        if(!IsVisible||RenderSize.Width<=0||RenderSize.Height<=0||PresentationSource.FromVisual(this) is null)return;
        // Capture coordinates are physical pixels, including the current entrance/panel scale.
        var origin=PointToScreen(new Point());var far=PointToScreen(new Point(RenderSize.Width,RenderSize.Height));
        double sx=Math.Max(.01,(far.X-origin.X)/RenderSize.Width),sy=Math.Max(.01,(far.Y-origin.Y)/RenderSize.Height);
        var rect=new Rect((bounds.X-origin.X)/sx,(bounds.Y-origin.Y)/sy,frame.PixelWidth/sx,frame.PixelHeight/sy);
        if(rect==_imageRect)return;
        _imageRect=rect;_image.Measure(rect.Size);_image.Arrange(rect);
    }
    public void SetFalloff(Rect panel,double luminance)
    {
        if(!_feather||panel.IsEmpty||panel.Width<=0||panel.Height<=0)return;
        // 57 DIP approximates 1.5 cm at the standard logical DPI.
        panel.Inflate(57,57);
        if(_falloff is null||_falloff.Viewbox.Size!=panel.Size){_falloff=Feather(panel.Size);OpacityMask=_falloff;}
        if(_falloff.Viewport!=panel)_falloff.Viewport=panel;
        double t=Math.Clamp((luminance-.55)/.35,0,1);
        _tint.Opacity=.07*t*t*(3-2*t);
    }
    private static DrawingBrush Feather(Size size)
    {
        double w=size.Width,h=size.Height;
        double r=Math.Min(88,Math.Min(w,h)/2);
        double bottomFade=r;var drawing=new DrawingGroup();using(var dc=drawing.Open()){
            var direction=new RadialGradientBrush{Center=new Point(.12,.1),GradientOrigin=new Point(.12,.1),RadiusX=1.15,RadiusY=1.15};
            direction.GradientStops.Add(new(Colors.White,0));direction.GradientStops.Add(new(Color.FromArgb(205,255,255,255),1));direction.Freeze();
            dc.PushOpacityMask(direction);
            dc.DrawRectangle(Brushes.White,null,new Rect(r,r,Math.Max(0,w-2*r),Math.Max(0,h-r-bottomFade)));
            GradientStopCollection Stops(bool reverse){var stops=new GradientStopCollection();for(int i=0;i<=16;i++){double t=i/16d,v=reverse?1-t:t;double a=v*v*v*(v*(v*6-15)+10);stops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(a*255),255,255,255),t));}return stops;}
            Brush Ramp(bool reverse,bool vertical)=>new LinearGradientBrush(Stops(reverse),new Point(0,0),vertical?new Point(0,1):new Point(1,0));
            dc.DrawRectangle(Ramp(false,true),null,new Rect(r,0,Math.Max(0,w-2*r),r));dc.DrawRectangle(Ramp(true,true),null,new Rect(r,h-bottomFade,Math.Max(0,w-2*r),bottomFade));
            dc.DrawRectangle(Ramp(false,false),null,new Rect(0,r,r,Math.Max(0,h-r-bottomFade)));dc.DrawRectangle(Ramp(true,false),null,new Rect(w-r,r,r,Math.Max(0,h-r-bottomFade)));
            foreach(bool right in new[]{false,true})foreach(bool bottom in new[]{false,true}){
                var center=new Point(right?w-r:r,bottom?h-bottomFade:r);var radial=new RadialGradientBrush{MappingMode=BrushMappingMode.Absolute,Center=center,GradientOrigin=center,RadiusX=r,RadiusY=bottom?bottomFade:r,GradientStops=Stops(true)};
                dc.DrawRectangle(radial,null,new Rect(right?w-r:0,bottom?h-bottomFade:0,r,bottom?bottomFade:r));
            }
        }
        drawing.Freeze();var brush=new DrawingBrush(drawing){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(size),ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(size),Stretch=Stretch.Fill};return brush;
    }
    public void Release(){_image.Source=null;_imageRect=Rect.Empty;_tint.Opacity=0;}
}

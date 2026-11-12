using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DockForWindows.Views;

internal sealed class ControlCenterElasticSlider
{
    private readonly Slider _slider;private readonly Func<bool> _animate;private readonly Action<double,bool> _deform;
    private readonly ScaleTransform _scale=new(1,1);private readonly TranslateTransform _shift=new();
    private bool _dragging;private FrameworkElement? _parent;private double _origin,_grab;
    internal static double Rubber(double distance)=>Math.Sign(distance)*22*(1-Math.Exp(-Math.Abs(distance)/55));
    internal ControlCenterElasticSlider(Slider slider,Func<bool> animate,Action<double,bool> deform)
    {
        _slider=slider;_animate=animate;_deform=deform;slider.IsSnapToTickEnabled=false;slider.SmallChange=1;
        var transforms=new TransformGroup();transforms.Children.Add(_scale);transforms.Children.Add(_shift);slider.RenderTransform=transforms;slider.RenderTransformOrigin=new Point(.5,.5);
        slider.PreviewMouseLeftButtonDown+=(_,e)=>{if(!slider.IsEnabled)return;slider.ApplyTemplate();_parent=VisualTreeHelper.GetParent(slider) as FrameworkElement;if(_parent is null)return;
            _origin=VisualTreeHelper.GetOffset(slider).X;_dragging=true;
            // Re-grabbing a recovering thumb preserves its current displayed position.
            double current=_shift.X;ControlMotion.Stop(_scale,ScaleTransform.ScaleXProperty,_scale.ScaleX);ControlMotion.Stop(_scale,ScaleTransform.ScaleYProperty,_scale.ScaleY);ControlMotion.Stop(_shift,TranslateTransform.XProperty,current);
            _grab=0;if(e.OriginalSource is DependencyObject source){for(var node=source;node is not null&&node!=slider;node=VisualTreeHelper.GetParent(node))if(node is Thumb){double x=e.GetPosition(_parent).X-_origin;double retained=Math.Clamp(current*2,-21.999,21.999);double beyond=-55*Math.Log(1-Math.Abs(retained)/22)*Math.Sign(retained);_grab=x-ValuePosition()-beyond;break;}}
            slider.CaptureMouse();slider.Focus();Update(e.GetPosition(_parent).X-_origin);e.Handled=true;};
        slider.PreviewMouseMove+=(_,e)=>{if(!_dragging||_parent is null)return;if(e.LeftButton!=MouseButtonState.Pressed){Release();return;}Update(e.GetPosition(_parent).X-_origin);e.Handled=true;};
        slider.PreviewMouseLeftButtonUp+=(_,e)=>{if(!_dragging)return;Release();e.Handled=true;};slider.LostMouseCapture+=(_,_)=>Release();slider.Unloaded+=(_,_)=>Release();
    }
    private double ThumbWidth=>(_slider.Template?.FindName("PART_Track",_slider) as Track)?.Thumb?.ActualWidth??22;
    private double ValuePosition()=>ThumbWidth/2+(_slider.ActualWidth-ThumbWidth)*(_slider.Value-_slider.Minimum)/Math.Max(.001,_slider.Maximum-_slider.Minimum);
    private void Update(double x)
    {
        double width=Math.Max(1,_slider.ActualWidth-ThumbWidth),raw=(x-_grab-ThumbWidth/2)/width;
        _slider.Value=_slider.Minimum+Math.Clamp(raw,0,1)*(_slider.Maximum-_slider.Minimum);
        double beyond=raw<0?raw*width:raw>1?(raw-1)*width:0,stretch=_animate()?Rubber(beyond):0;
        ControlMotion.Stop(_scale,ScaleTransform.ScaleXProperty,1+Math.Abs(stretch)/Math.Max(1,_slider.ActualWidth));
        ControlMotion.Stop(_scale,ScaleTransform.ScaleYProperty,1-Math.Abs(stretch)/220);
        ControlMotion.Stop(_shift,TranslateTransform.XProperty,stretch/2);_deform(stretch,false);
    }
    private void Release()
    {
        if(!_dragging)return;_dragging=false;_slider.ReleaseMouseCapture();
        if(_animate()){ControlMotion.To(_scale,ScaleTransform.ScaleXProperty,1,speed:1,damping:.66);ControlMotion.To(_scale,ScaleTransform.ScaleYProperty,1,speed:1,damping:.66);ControlMotion.To(_shift,TranslateTransform.XProperty,0,speed:1,damping:.66);}
        else{ControlMotion.Stop(_scale,ScaleTransform.ScaleXProperty,1);ControlMotion.Stop(_scale,ScaleTransform.ScaleYProperty,1);ControlMotion.Stop(_shift,TranslateTransform.XProperty,0);}
        _deform(0,true);
    }
}

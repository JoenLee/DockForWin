using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DockForWindows.Views;

// Fade only the scrolling viewport. The pinned header and inset scrollbar stay crisp.
internal sealed class ScrollEdgeFade : Grid
{
    private readonly ScrollViewer _scroll;
    private readonly Rectangle _top=new(){Height=18,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false};
    private readonly Rectangle _bottom=new(){Height=18,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};
    private readonly LinearGradientBrush _fade=new(new GradientStopCollection{new(Colors.White,0),new(Colors.White,.1),new(Colors.White,.9),new(Colors.White,1)},new Point(0,0),new Point(0,1));
    private readonly TranslateTransform _elastic=new();private ScrollContentPresenter? _viewport;private VisualBrush? _topBrush,_bottomBrush;
    private double _target,_velocity,_edge,_edgeVelocity,_requestedOffset=double.NaN;private TimeSpan _last;private bool _moving;
    internal long ScrollFrames{get;private set;}
    public ScrollEdgeFade(ScrollViewer scroll)
    {
        _scroll=scroll;ClipToBounds=true;Children.Add(scroll);Children.Add(_top);Children.Add(_bottom);
        foreach(var edge in new[]{_top,_bottom}){edge.Effect=new BlurEffect{Radius=3,KernelType=KernelType.Gaussian,RenderingBias=RenderingBias.Performance};edge.HorizontalAlignment=HorizontalAlignment.Left;edge.Opacity=.5;}
        _top.OpacityMask=new LinearGradientBrush(Colors.White,Colors.Transparent,new Point(0,0),new Point(0,1));_bottom.OpacityMask=new LinearGradientBrush(Colors.Transparent,Colors.White,new Point(0,0),new Point(0,1));
        Loaded+=(_,_)=>{_scroll.ApplyTemplate();_viewport=_scroll.Template.FindName("PART_ScrollContentPresenter",_scroll) as ScrollContentPresenter;if(_viewport is not null){_viewport.OpacityMask=_fade;_viewport.RenderTransform=_elastic;_topBrush=MakeBrush(_viewport);_bottomBrush=MakeBrush(_viewport);_top.Fill=_topBrush;_bottom.Fill=_bottomBrush;}_target=_scroll.VerticalOffset;_last=TimeSpan.Zero;CompositionTarget.Rendering+=Frame;Update();};
        Unloaded+=(_,_)=>{CompositionTarget.Rendering-=Frame;_moving=false;_velocity=_edge=_edgeVelocity=_elastic.Y=0;};
        scroll.PreviewMouseWheel+=(_,e)=>{if(_scroll.ScrollableHeight<=0)return;ScrollBy(-e.Delta/120d*48*Math.Max(1,SystemParameters.WheelScrollLines));e.Handled=true;};
        scroll.ScrollChanged+=(_,_)=>{bool ownRequest=_moving&&double.IsFinite(_requestedOffset)&&Math.Abs(_scroll.VerticalOffset-_requestedOffset)<.5;if(!ownRequest||_scroll.IsMouseCaptureWithin){_target=_scroll.VerticalOffset;_velocity=0;}_target=Math.Clamp(_target,0,_scroll.ScrollableHeight);Update();};SizeChanged+=(_,_)=>Update();
    }
    private static VisualBrush MakeBrush(Visual visual)=>new(visual){ViewboxUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill};
    internal void ScrollBy(double delta)
    {
        double next=_target+delta,limit=_scroll.ScrollableHeight;_target=Math.Clamp(next,0,limit);
        if(next!=_target){_edge=Math.Clamp(_edge-(next-_target)*.13,-24,24);_edgeVelocity=0;}_moving=true;
    }
    private static void Spring(ref double x,ref double velocity,double target,double dt,double frequency) => x=ControlMotion.Step(x,target,ref velocity,dt);
    private void Frame(object? sender,EventArgs e)
    {
        if(e is not RenderingEventArgs frame||frame.RenderingTime==_last)return;
        double dt=_last==TimeSpan.Zero?1d/60:Math.Max(0,(frame.RenderingTime-_last).TotalSeconds);_last=frame.RenderingTime;
        if(!_moving||_viewport is null)return;ScrollFrames++;
        if(_scroll.IsMouseCaptureWithin){_target=_scroll.VerticalOffset;_velocity=0;}
        double position=_scroll.VerticalOffset;Spring(ref position,ref _velocity,_target,dt,18);_requestedOffset=Math.Clamp(position,0,_scroll.ScrollableHeight);_scroll.ScrollToVerticalOffset(_requestedOffset);
        Spring(ref _edge,ref _edgeVelocity,0,dt,17);_elastic.Y=_edge;
        if(Math.Abs(position-_target)<.15&&Math.Abs(_velocity)<.4&&Math.Abs(_edge)<.1&&Math.Abs(_edgeVelocity)<.4){_scroll.ScrollToVerticalOffset(_target);_elastic.Y=0;_moving=false;}
    }
    private void Update()
    {
        if(_viewport is null||_viewport.ActualHeight<=0)return;
        double height=_viewport.ActualHeight,fade=Math.Min(.25,18/height);bool above=_scroll.VerticalOffset>.5,below=_scroll.ScrollableHeight-_scroll.VerticalOffset>.5;
        _fade.GradientStops[0].Color=above?Colors.Transparent:Colors.White;_fade.GradientStops[1].Offset=fade;_fade.GradientStops[2].Offset=1-fade;_fade.GradientStops[3].Color=below?Colors.Transparent:Colors.White;
        _top.Visibility=above?Visibility.Visible:Visibility.Collapsed;_bottom.Visibility=below?Visibility.Visible:Visibility.Collapsed;
        double width=Math.Max(1,_viewport.ActualWidth);_top.Width=_bottom.Width=width;
        if(_topBrush is not null)_topBrush.Viewbox=new Rect(0,0,width,18);if(_bottomBrush is not null)_bottomBrush.Viewbox=new Rect(0,Math.Max(0,height-18),width,18);
    }
}

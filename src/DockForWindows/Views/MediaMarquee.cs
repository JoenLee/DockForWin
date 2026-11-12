using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DockForWindows.Views;

internal sealed class MediaMarquee:Grid
{
    private readonly TextBlock _label;private readonly TranslateTransform _offset=new();
    private string? _text;private double _width=-1,_started,_overflow;
    public MediaMarquee(TextBlock label)
    {
        _label=label;ClipToBounds=true;MinWidth=0;Children.Add(label);
        label.TextTrimming=TextTrimming.None;label.HorizontalAlignment=HorizontalAlignment.Left;label.RenderTransform=_offset;
    }
    protected override Size MeasureOverride(Size available)
    {
        _label.Measure(new Size(double.PositiveInfinity,available.Height));
        // The title's full width must never become the minimum width of its parent star column.
        return new Size(0,_label.DesiredSize.Height);
    }
    protected override Size ArrangeOverride(Size final)
    {_label.Arrange(new Rect(0,0,Math.Max(final.Width,_label.DesiredSize.Width),final.Height));return final;}
    internal void Tick(double now,bool animate)
    {
        if(!IsVisible||ActualWidth<=0)return;
        if(_text!=_label.Text||Math.Abs(_width-ActualWidth)>.1){
            _text=_label.Text;_width=ActualWidth;_started=now;
            _label.Width=double.NaN;_label.Measure(new Size(double.PositiveInfinity,ActualHeight));
            _label.Width=Math.Ceiling(_label.DesiredSize.Width);_overflow=Math.Max(0,_label.Width-ActualWidth);_offset.X=0;
        }
        if(_overflow<1||!animate){_offset.X=0;return;}
        // Pause at each end, then ease into a constant readable speed without a wrap jump.
        double travel=_overflow/28,leg=travel+1.4,t=(now-_started)%(leg*2);
        bool reverse=t>=leg;if(reverse)t-=leg;
        double move=Math.Clamp(t-1.1,0,travel),u=travel<=0?0:move/travel;
        double eased=u*u*u*(u*(u*6-15)+10);
        _offset.X=-_overflow*(reverse?1-eased:eased);
    }
}

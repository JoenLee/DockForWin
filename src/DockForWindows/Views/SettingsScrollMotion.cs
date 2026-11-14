using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DockForWindows.Views;

internal static class SettingsScrollMotion
{
    private sealed class State
    {
        internal required Func<bool> Enabled;
        internal double Target,LastInput;
        internal readonly TranslateTransform Bounce=new();
    }
    private static readonly ConditionalWeakTable<ScrollViewer,State> States=new();
    private static readonly DependencyProperty OffsetProperty=DependencyProperty.RegisterAttached("Offset",typeof(double),typeof(SettingsScrollMotion),new PropertyMetadata(0d,(o,e)=>((ScrollViewer)o).ScrollToVerticalOffset((double)e.NewValue)));
    private static readonly DependencyProperty BounceProperty=DependencyProperty.RegisterAttached("Bounce",typeof(double),typeof(SettingsScrollMotion),new PropertyMetadata(0d,(o,e)=>{if(States.TryGetValue((ScrollViewer)o,out var state))state.Bounce.Y=(double)e.NewValue;}));
    internal static void Attach(ScrollViewer viewer,Func<bool> enabled)
    {
        if(States.TryGetValue(viewer,out _))return;
        var state=new State{Enabled=enabled};States.Add(viewer,state);viewer.ClipToBounds=true;
        viewer.PreviewMouseWheel+=(_,e)=>{
            if(e.Handled)return;
            for(DependencyObject? child=e.OriginalSource as DependencyObject;child!=null&&child!=viewer;child=child is FrameworkContentElement content?content.Parent:VisualTreeHelper.GetParent(child)){
                if(child is WidgetCarousel or ComboBox||child is TextBox {AcceptsReturn:true}||child is ScrollViewer)return;
            }
            if(viewer.ScrollableHeight<=0)return;
            if(!state.Enabled()){Stop(viewer,state);viewer.ScrollToVerticalOffset(viewer.VerticalOffset-e.Delta*.65);e.Handled=true;return;}
            if(ControlMotion.Now-state.LastInput>.7){state.Target=viewer.VerticalOffset;ControlMotion.Stop(viewer,OffsetProperty,viewer.VerticalOffset);}state.LastInput=ControlMotion.Now;
            double requested=state.Target-e.Delta*.65;state.Target=Math.Clamp(requested,0,viewer.ScrollableHeight);
            bool boundary=e.Delta>0&&viewer.VerticalOffset<.5||e.Delta<0&&viewer.VerticalOffset>=viewer.ScrollableHeight-.5;
            if(boundary){
                viewer.ApplyTemplate();if(viewer.Template.FindName("PART_ScrollContentPresenter",viewer) is ScrollContentPresenter presenter)presenter.RenderTransform=state.Bounce;
                ControlMotion.Stop(viewer,BounceProperty,e.Delta>0?9:-9);ControlMotion.To(viewer,BounceProperty,0,speed:1.5,damping:ControlMotion.PageDamping);
            }
            ControlMotion.To(viewer,OffsetProperty,state.Target,speed:1.12);e.Handled=true;
        };
        viewer.PreviewMouseDown+=(_,_)=>Stop(viewer,state);viewer.PreviewKeyDown+=(_,_)=>Stop(viewer,state);
        viewer.IsVisibleChanged+=(_,_)=>{if(!viewer.IsVisible)Stop(viewer,state);};viewer.Unloaded+=(_,_)=>Stop(viewer,state);
    }
    private static void Stop(ScrollViewer viewer,State state){state.Target=viewer.VerticalOffset;state.LastInput=0;ControlMotion.Stop(viewer,OffsetProperty,viewer.VerticalOffset);ControlMotion.Stop(viewer,BounceProperty,0);}
}

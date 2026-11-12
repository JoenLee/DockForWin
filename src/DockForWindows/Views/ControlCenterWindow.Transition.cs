using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private TaskCompletionSource<bool>? _dismissCompletion;
    private const double RevealDistance=96;
    private bool _dismissRequested,_revealDragging,_revealPressed;
    private double _revealPointer,_revealStart,_revealLast,_revealTime,_revealVelocity;
    private static readonly DependencyProperty RevealProperty=DependencyProperty.Register("Reveal",typeof(double),typeof(ControlCenterWindow),new PropertyMetadata(0d,(owner,_)=>((ControlCenterWindow)owner).ApplyReveal()));
    private double Reveal=>(double)GetValue(RevealProperty);
    private void ApplyReveal()
    {
        double progress=Reveal;_lastMotion=_runtime.Elapsed.TotalSeconds;_chromeDirty=true;
        _entrance.Y=(progress-1)*RevealDistance;
        // Fade the live material and content together; sampling and blur quality remain unchanged.
        double alpha=Math.Clamp(progress/.85,0,1);Opacity=alpha*alpha*(3-2*alpha);
    }
    private void SettleReveal(bool open,double? velocity=null)
    {
        if(_closing)return;_revealPressed=_revealDragging=false;if(Mouse.Captured==_root.Children[0])Mouse.Capture(null);
        if(open){_dismissCompletion?.TrySetResult(false);_dismissCompletion=null;}else _dismissCompletion??=new(TaskCreationOptions.RunContinuationsAsynchronously);
        _dismissRequested=!open;PresentationChanged?.Invoke(open);
        if(!Animate){ControlMotion.Stop(this,RevealProperty,open?1:0);if(!open)FinishDismiss();return;}
        _openingUntil=_runtime.Elapsed.TotalSeconds+.8;
        ControlMotion.To(this,RevealProperty,open?1:0,speed:.82,damping:.82,initialVelocity:velocity,completed:()=>{if(!open&&_dismissRequested)FinishDismiss();});
    }
    public void TogglePresentation(){if(_closing)return;SettleReveal(_dismissRequested);}
    private void FinishDismiss()
    {
        if(_closing)return;_closing=true;EndDrag();_mediaPoll.Stop();_lifetime.Cancel();_connectivity.StopBluetoothDiscovery();_poll.Stop();_stateRefresh.Stop();_brightnessDelay.Stop();_allowClose=true;Close();_dismissCompletion?.TrySetResult(true);
    }
    private async Task<bool> DismissForTool(){Dismiss();return _dismissCompletion is {} pending?await pending.Task:!IsVisible;}
    private void InstallRevealGesture(Grid header)
    {
        // Transparent pixels in an AllowsTransparency window otherwise pass input through to the desktop.
        header.Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));
        header.PreviewMouseLeftButtonDown+=(_,e)=>{
            if(_editing||_closing||InInteractive(e.OriginalSource as DependencyObject))return;
            _revealPressed=true;_revealDragging=false;_revealPointer=e.GetPosition((UIElement)Content).Y;
        };
        header.PreviewMouseMove+=(_,e)=>{
            if(!_revealPressed||e.LeftButton!=MouseButtonState.Pressed)return;
            double y=e.GetPosition((UIElement)Content).Y;
            if(!_revealDragging){if(Math.Abs(y-_revealPointer)<3)return;_revealDragging=true;_revealStart=_revealLast=Reveal;_revealTime=ControlMotion.Now;_revealVelocity=0;ControlMotion.Stop(this,RevealProperty,Reveal);header.CaptureMouse();_dismissRequested=false;}
            double progress=_revealStart+(y-_revealPointer)/RevealDistance;
            if(progress>1)progress=1+.12*(1-Math.Exp(-(progress-1)/.12));
            else if(progress<0)progress=-.12*(1-Math.Exp(progress/.12));
            double now=ControlMotion.Now,dt=now-_revealTime;
            if(dt>.001)_revealVelocity=Math.Clamp((progress-_revealLast)/dt,-8,8);
            _revealLast=progress;_revealTime=now;ControlMotion.Stop(this,RevealProperty,progress);e.Handled=true;
        };
        void Release(){if(!_revealPressed)return;_revealPressed=false;if(!_revealDragging)return;_revealDragging=false;double velocity=ControlMotion.Now-_revealTime>.1?0:_revealVelocity;header.ReleaseMouseCapture();SettleReveal(Reveal+velocity*.15>.5,velocity);}
        header.PreviewMouseLeftButtonUp+=(_,e)=>{bool dragged=_revealDragging;Release();if(dragged)e.Handled=true;};
        header.LostMouseCapture+=(_,_)=>Release();
    }
}

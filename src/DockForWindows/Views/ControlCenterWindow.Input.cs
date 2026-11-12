using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DockForWindows.Views;
public sealed partial class ControlCenterWindow
{
    private bool _returnPress;
    private void ReturnFromBackdrop()
    {
        if(_returnPress)return;_returnPress=true;_pointerPressed=true;
        BackDetails();CaptureMouse();
    }
    private void HandleCenterDown(object sender,MouseButtonEventArgs e)
    {
        if(e.ChangedButton!=MouseButton.Left)return;
        if(_returnPress){e.Handled=true;return;}
        var source=e.OriginalSource as DependencyObject;
        bool detailControl=source is not null&&_details.IsAncestorOf(source)&&InInteractive(source);
        if(_detailVisible&&!detailControl){ReturnFromBackdrop();e.Handled=true;return;}
        if(_transitioning&&!_detailVisible){_returnPress=true;CaptureMouse();e.Handled=true;return;}
        if(_detailVisible&&detailControl){_pointerPressed=true;return;}ObserveBackgroundPress(true);
    }
    private void HandleCenterUp(object sender,MouseButtonEventArgs e)
    {
        if(!_returnPress)return;e.Handled=true;_returnPress=false;_pointerPressed=false;
        if(Mouse.Captured==this)ReleaseMouseCapture();
    }
}

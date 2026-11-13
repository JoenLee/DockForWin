using System;
using System.Windows;
using System.Windows.Threading;
namespace DockForWindows.Services;
// Installed only while a Dock menu or window preview is open. Never consumes clicks; the hook runs on its
// own thread and only forwards button-down positions to the UI thread.
internal sealed class OutsideClickWatcher:IDisposable
{
    private readonly LowLevelMouseHook _hook;private volatile bool _disposed;
    internal bool Active=>_hook.Active;
    internal OutsideClickWatcher(Dispatcher dispatcher,Action<Point> outside)
    {
        _hook=new LowLevelMouseHook((message,x,y)=>{
            if(message is 0x0201 or 0x0204 or 0x0207 or 0x020B)dispatcher.BeginInvoke(()=>{if(!_disposed)outside(new Point(x,y));},DispatcherPriority.Input);
            return false;
        },"DockForWindows.OutsideClicks");
    }
    public void Dispose(){_disposed=true;_hook.Dispose();}
}

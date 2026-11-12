using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
namespace DockForWindows.Services;
// Installed only while a Dock menu or window preview is open. Never consumes clicks.
internal sealed class OutsideClickWatcher:IDisposable
{
    private delegate nint Callback(int code,nint message,nint data);
    [StructLayout(LayoutKind.Sequential)]private struct MouseData{public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [DllImport("user32.dll")]private static extern nint SetWindowsHookEx(int kind,Callback callback,nint module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]private static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    private nint _hook;private readonly Callback _callback;private bool _disposed;
    internal bool Active=>_hook!=0;
    internal OutsideClickWatcher(Dispatcher dispatcher,Action<Point> outside)
    {
        _callback=(code,message,data)=>{if(code>=0&&message is 0x0201 or 0x0204 or 0x0207 or 0x020B){var p=Marshal.PtrToStructure<MouseData>(data);dispatcher.BeginInvoke(()=>{if(!_disposed)outside(new Point(p.X,p.Y));},DispatcherPriority.Input);}return CallNextHookEx(_hook,code,message,data);};
        _hook=SetWindowsHookEx(14,_callback,0,0);
    }
    public void Dispose(){_disposed=true;if(_hook!=0){UnhookWindowsHookEx(_hook);_hook=0;}}
}

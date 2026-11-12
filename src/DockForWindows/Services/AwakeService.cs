using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace DockForWindows.Services;

public sealed class AwakeService:IDisposable
{
    public static AwakeService Shared{get;}=new();private readonly object _sync=new();private SafeFileHandle? _request;private Timer? _timer;private bool _display;private long _generation;public DateTimeOffset? Until{get;private set;}
    [StructLayout(LayoutKind.Explicit,Size=40)]private struct Reason{[FieldOffset(0)]public uint Version;[FieldOffset(4)]public uint Flags;[FieldOffset(8)]public nint Text;}
    [DllImport("kernel32.dll",SetLastError=true)]private static extern SafeFileHandle PowerCreateRequest(ref Reason reason);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PowerSetRequest(SafeFileHandle request,int type);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PowerClearRequest(SafeFileHandle request,int type);
    public bool Active{get{lock(_sync)return _request is {IsInvalid:false,IsClosed:false};}}
    public bool DisplayOn{get{lock(_sync)return Active&&_display;}}
    public void Start(TimeSpan? duration,bool displayOn=false)
    {
        if(duration is {} t&&(t<=TimeSpan.Zero||t>TimeSpan.FromDays(1)))throw new ArgumentOutOfRangeException(nameof(duration));
        lock(_sync){Stop();nint text=Marshal.StringToHGlobalUni("DockForWindows 临时保持唤醒");SafeFileHandle? request=null;
            try{var reason=new Reason{Flags=1,Text=text};request=PowerCreateRequest(ref reason);if(request.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());if(!PowerSetRequest(request,1))throw new Win32Exception(Marshal.GetLastWin32Error());if(displayOn&&!PowerSetRequest(request,0))throw new Win32Exception(Marshal.GetLastWin32Error());_request=request;request=null;_display=displayOn;Until=duration is {} interval?DateTimeOffset.UtcNow+interval:null;long generation=_generation;if(duration.HasValue)_timer=new Timer(_=>{lock(_sync){if(generation==_generation)Stop();}},null,duration.Value,Timeout.InfiniteTimeSpan);}
            finally{request?.Dispose();Marshal.FreeHGlobal(text);}}
    }
    public void Stop(){lock(_sync){_generation++;_timer?.Dispose();_timer=null;if(_request is {IsInvalid:false,IsClosed:false} request){if(_display)PowerClearRequest(request,0);PowerClearRequest(request,1);} _request?.Dispose();_request=null;_display=false;Until=null;}}
    public void Dispose()=>Stop();
}

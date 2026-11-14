using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace DockForWindows.Services;

public sealed record DisplayHdrState(bool Supported,bool Enabled,bool Active,bool PolicyLimited,string? Reason);
public static class DisplayControlService
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool EnumDisplaySettingsEx(string name,int mode,nint data,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int ChangeDisplaySettingsEx(string name,nint mode,nint window,uint flags,nint parameter);
    [DllImport("user32.dll")]private static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
    [DllImport("user32.dll")]private static extern int QueryDisplayConfig(uint flags,ref uint paths,[Out]Path[] pathData,ref uint modes,nint modeData,nint topology);
    [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]private static extern int GetSourceName(ref SourceName data);
    [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]private static extern int GetColor(ref ColorInfo data);
    [DllImport("user32.dll",EntryPoint="DisplayConfigSetDeviceInfo")]private static extern int SetColor(ref ColorState data);
    [StructLayout(LayoutKind.Sequential)]private struct Luid{public uint Low;public int High;}
    [StructLayout(LayoutKind.Sequential)]private struct Source{public Luid Adapter;public uint Id,Mode,Status;}
    [StructLayout(LayoutKind.Sequential)]private struct Target{public Luid Adapter;public uint Id,Mode,Output,Rotation,Scaling,Numerator,Denominator,Scan;public int Available;public uint Status;}
    [StructLayout(LayoutKind.Sequential)]private struct Path{public Source Source;public Target Target;public uint Flags;}
    [StructLayout(LayoutKind.Sequential)]private struct Header{public uint Type,Size;public Luid Adapter;public uint Id;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct SourceName{public Header Header;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;}
    [StructLayout(LayoutKind.Sequential)]private struct ColorInfo{public Header Header;public uint Flags,Encoding,Bits,ActiveMode;}
    [StructLayout(LayoutKind.Sequential)]private struct ColorState{public Header Header;public uint Flags;}
    // DEVMODEW's fixed 220-byte header and offsets come from the Windows SDK.
    private static nint ModeBuffer(){var p=Marshal.AllocHGlobal(220);Marshal.Copy(new byte[220],0,p,220);Marshal.WriteInt16(p,68,220);return p;}
    public static int ReadRefreshRate(string device)
    {var p=ModeBuffer();try{if(!EnumDisplaySettingsEx(device,-1,p,0))throw new Win32Exception(Marshal.GetLastWin32Error());return Marshal.ReadInt32(p,184);}finally{Marshal.FreeHGlobal(p);}}
    public static IReadOnlyList<int> RefreshRates(string device)
    {
        var p=ModeBuffer();try{
            if(!EnumDisplaySettingsEx(device,-1,p,0))throw new Win32Exception(Marshal.GetLastWin32Error());int width=Marshal.ReadInt32(p,172),height=Marshal.ReadInt32(p,176),bits=Marshal.ReadInt32(p,168);var rates=new SortedSet<int>();
            for(int i=0;i<4096&&EnumDisplaySettingsEx(device,i,p,0);i++){if(Marshal.ReadInt32(p,172)==width&&Marshal.ReadInt32(p,176)==height&&Marshal.ReadInt32(p,168)==bits){int rate=Marshal.ReadInt32(p,184);if(rate>1&&rate<=1000)rates.Add(rate);}}
            return rates.ToArray();
        }finally{Marshal.FreeHGlobal(p);}
    }
    private static void SetRate(string device,int rate)
    {
        var p=ModeBuffer();try{
            if(!EnumDisplaySettingsEx(device,-1,p,0))throw new Win32Exception(Marshal.GetLastWin32Error());Marshal.WriteInt32(p,72,Marshal.ReadInt32(p,72)|0x400000);Marshal.WriteInt32(p,184,rate);
            int test=ChangeDisplaySettingsEx(device,p,0,2,0);if(test!=0)throw new InvalidOperationException(LocalizedText.Format($"显示器拒绝此刷新率（{test}）。"));int result=ChangeDisplaySettingsEx(device,p,0,1,0);if(result!=0)throw new InvalidOperationException(LocalizedText.Format($"未能切换刷新率（{result}）。"));
        }finally{Marshal.FreeHGlobal(p);}
    }
    public sealed class RateTrial:IDisposable
    {
        private readonly object _sync=new();private readonly string _device;private readonly int _original;private readonly Timer _timer;private bool _finished;public DateTimeOffset Deadline{get;}=DateTimeOffset.UtcNow.AddSeconds(15);public Exception? RestoreError{get;private set;}
        internal RateTrial(string device,int rate){_device=device;_original=ReadRefreshRate(device);if(!RefreshRates(device).Contains(rate))throw new InvalidOperationException(LocalizedText.Get("该显示器未报告支持此刷新率。"));SetRate(device,rate);_timer=new Timer(_=>Dispose(),null,TimeSpan.FromSeconds(15),Timeout.InfiniteTimeSpan);}
        public void Confirm(){lock(_sync){if(_finished)throw new InvalidOperationException(LocalizedText.Get("显示设置已恢复，请重新选择。"));_finished=true;_timer.Dispose();}}
        public void Dispose(){lock(_sync){if(_finished)return;_finished=true;_timer.Dispose();try{SetRate(_device,_original);}catch(Exception ex){RestoreError=ex;}}}
    }
    public static RateTrial TryRefreshRate(string device,int rate)=>new(device,rate);
    private static Target FindTarget(string device)
    {
        for(int attempt=0;attempt<3;attempt++){
            int error=GetDisplayConfigBufferSizes(2,out uint count,out uint modeCount);if(error!=0)throw new Win32Exception(error);if(count>128||modeCount>512)throw new InvalidOperationException(LocalizedText.Get("显示器配置过大。"));var paths=new Path[Math.Max(1,count)];nint modes=Marshal.AllocHGlobal(checked((int)Math.Max(1,modeCount)*64));
            try{error=QueryDisplayConfig(2,ref count,paths,ref modeCount,modes,0);if(error==122)continue;if(error!=0)throw new Win32Exception(error);
                for(int i=0;i<count;i++){var item=paths[i];var source=new SourceName{Header=new Header{Type=1,Size=(uint)Marshal.SizeOf<SourceName>(),Adapter=item.Source.Adapter,Id=item.Source.Id},Name=""};if(GetSourceName(ref source)==0&&source.Name.Equals(device,StringComparison.OrdinalIgnoreCase))return item.Target;}
            }finally{Marshal.FreeHGlobal(modes);}
        }
        throw new InvalidOperationException(LocalizedText.Get("未找到活动显示器。"));
    }
    private static ColorInfo ReadColor(Target target)
    {var info=new ColorInfo{Header=new Header{Type=15,Size=(uint)Marshal.SizeOf<ColorInfo>(),Adapter=target.Adapter,Id=target.Id}};int error=GetColor(ref info);if(error!=0)throw new Win32Exception(error,LocalizedText.Get("此系统未提供可区分 HDR 与广色域的控制接口。"));return info;}
    public static DisplayHdrState ReadHdr(string device)
    {try{var color=ReadColor(FindTarget(device));bool supported=(color.Flags&16)!=0,limited=(color.Flags&8)!=0;return new(supported,(color.Flags&32)!=0,color.ActiveMode==2,limited,!supported?LocalizedText.Get("此显示器不支持 HDR。"):limited?LocalizedText.Get("HDR 受系统策略限制。"):null);}catch(Exception ex){return new(false,false,false,false,ex.Message);}}
    public static void SetHdr(string device,bool enabled)
    {var target=FindTarget(device);var info=ReadColor(target);if((info.Flags&16)==0||(info.Flags&8)!=0)throw new InvalidOperationException(LocalizedText.Get("显示器不支持 HDR，或被系统策略限制。"));var state=new ColorState{Header=new Header{Type=16,Size=(uint)Marshal.SizeOf<ColorState>(),Adapter=target.Adapter,Id=target.Id},Flags=enabled?1u:0u};int error=SetColor(ref state);if(error!=0)throw new Win32Exception(error);}
}

using System;
using System.Collections;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace DockForWindows.Services;

public sealed record AudioState(double Volume,bool Muted);
public sealed record PowerState(int? Percent,bool PluggedIn);

public static class SystemControlService
{
    [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDevices
    {
        void EnumAudioEndpoints(int flow,uint mask,out nint devices);
        void GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
    }
    [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDevice
    {void Activate(ref Guid iid,uint context,nint parameters,[MarshalAs(UnmanagedType.IUnknown)]out object result);}
    [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IVolume
    {
        void RegisterControlChangeNotify(nint callback);void UnregisterControlChangeNotify(nint callback);void GetChannelCount(out uint count);
        void SetMasterVolumeLevel(float level,ref Guid context);void SetMasterVolumeLevelScalar(float level,ref Guid context);
        void GetMasterVolumeLevel(out float level);void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint channel,float level,ref Guid context);void SetChannelVolumeLevelScalar(uint channel,float level,ref Guid context);
        void GetChannelVolumeLevel(uint channel,out float level);void GetChannelVolumeLevelScalar(uint channel,out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)]bool muted,ref Guid context);void GetMute([MarshalAs(UnmanagedType.Bool)]out bool muted);
    }
    // The shared enumerator is serialised with AudioDeviceService; see CoreAudio.
    private static T Audio<T>(Func<IVolume,T> action,int flow=0)=>CoreAudio.Use(enumerator=>
    {
        object? activated=null;IDevice? device=null;
        try{((IDevices)enumerator).GetDefaultAudioEndpoint(flow,1,out device);var iid=typeof(IVolume).GUID;device.Activate(ref iid,23,0,out activated);return action((IVolume)activated);}
        finally{Release(activated);Release(device);}
    });
    public static AudioState ReadAudio()=>Audio(v=>{v.GetMasterVolumeLevelScalar(out float level);v.GetMute(out bool muted);return new AudioState(level*100,muted);});
    public static void SetVolume(double percent)=>Audio(v=>{var context=Guid.Empty;v.SetMasterVolumeLevelScalar((float)Math.Clamp(percent/100,0,1),ref context);return true;});
    public static void SetMute(bool muted)=>Audio(v=>{var context=Guid.Empty;v.SetMute(muted,ref context);return true;});
    public static AudioState ReadMicrophone()=>Audio(v=>{v.GetMasterVolumeLevelScalar(out float level);v.GetMute(out bool muted);return new AudioState(level*100,muted);},1);
    public static void SetMicrophoneVolume(double percent)=>Audio(v=>{var context=Guid.Empty;v.SetMasterVolumeLevelScalar((float)Math.Clamp(percent/100,0,1),ref context);return true;},1);
    public static void SetMicrophoneMute(bool muted)=>Audio(v=>{var context=Guid.Empty;v.SetMute(muted,ref context);return true;},1);
    private static void Release(object? value){if(value is not null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}

    public static byte? ReadBrightness()
    {
        using var search=new System.Management.ManagementObjectSearcher(@"root\wmi","SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True");
        using var results=search.Get();
        foreach(System.Management.ManagementObject item in results){using(item)return Convert.ToByte(item["CurrentBrightness"]);}
        return null;
    }
    public static bool SetBrightness(double percent)
    {
        using var search=new System.Management.ManagementObjectSearcher(@"root\wmi","SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True");
        using var results=search.Get();bool changed=false;
        foreach(System.Management.ManagementObject item in results)
        {using(item){var result=item.InvokeMethod("WmiSetBrightness",new object[]{0U,(byte)Math.Clamp(Math.Round(percent),0,100)});changed|=Convert.ToUInt32(result)==0;}}
        return changed;
    }
    [StructLayout(LayoutKind.Sequential)]private struct NativePower{public byte Ac,Battery,Percent,Reserved;public uint Life,FullLife;}
    [DllImport("kernel32.dll")]private static extern bool GetSystemPowerStatus(out NativePower status);
    public static PowerState ReadPower()=>GetSystemPowerStatus(out var p)?new PowerState(p.Percent<=100?p.Percent:null,p.Ac==1):new(null,false);
    [DllImport("powrprof.dll")]private static extern uint PowerGetEffectiveOverlayScheme(out Guid scheme);
    [DllImport("powrprof.dll")]private static extern uint PowerSetActiveOverlayScheme(Guid scheme);
    private static readonly Guid OverlayPowerSave=new("961cc777-2547-4f9d-8174-7d86181b8a7a"),OverlayBalanced=Guid.Empty;
    public static bool? ReadPowerSaver(){try{return PowerGetEffectiveOverlayScheme(out var s)==0?s==OverlayPowerSave:null;}catch{return null;}}
    public static bool SetPowerSaver(bool on){uint result=PowerSetActiveOverlayScheme(on?OverlayPowerSave:OverlayBalanced);if(result!=0)throw new System.ComponentModel.Win32Exception((int)result);if(ReadPowerSaver()!=on)throw new InvalidOperationException(LocalizedText.Get("Windows 未确认电源模式切换。"));return true;}
    public static Guid? ReadPerformanceMode(){try{return PowerGetEffectiveOverlayScheme(out var mode)==0?mode:null;}catch{return null;}}
    public static void SetPerformanceMode(Guid mode){if(mode!=OverlayPowerSave&&mode!=Guid.Empty&&mode!=new Guid("ded574b5-45a0-4f42-8737-46345c09c238"))throw new ArgumentException(LocalizedText.Get("未知电源模式。"));uint result=PowerSetActiveOverlayScheme(mode);if(result!=0)throw new System.ComponentModel.Win32Exception((int)result);if(ReadPerformanceMode()!=mode)throw new InvalidOperationException(LocalizedText.Get("Windows 未确认电源模式切换。"));}
    private const string PersonalizeKey=@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public static bool? ReadDarkMode(){try{return Microsoft.Win32.Registry.GetValue(PersonalizeKey,"AppsUseLightTheme",null) is int light?(light==0):null;}catch{return null;}}
    public static bool SetDarkMode(bool dark)
    {
        try
        {
            Microsoft.Win32.Registry.SetValue(PersonalizeKey,"AppsUseLightTheme",dark?0:1);
            // 广播让资源管理器与正在运行的应用立即切换主题。
            nint text=Marshal.StringToHGlobalUni("ImmersiveColorSet");try{SendMessageTimeout(0xffff,0x001A,0,text,2,1000,out _);}finally{Marshal.FreeHGlobal(text);}if(ReadDarkMode()!=dark)throw new InvalidOperationException(LocalizedText.Get("Windows 未确认深色模式切换。"));
            return true;
        }
        catch(Exception ex){throw new InvalidOperationException(LocalizedText.Get("系统未能应用此设置：")+ex.Message,ex);}
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint SendMessageTimeout(nint window,uint message,nint wParam,nint lParam,uint flags,uint timeout,out nint result);
    public static string ReadNetwork()
    {
        try{return NetworkInterface.GetAllNetworkInterfaces().Any(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))?LocalizedText.Get("已连接"):LocalizedText.Get("未连接");}catch{return LocalizedText.Get("网络设置");}
    }
}


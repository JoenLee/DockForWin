using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Management.Deployment;

namespace DockForWindows.Services;

public static class SystemToolService
{
    [StructLayout(LayoutKind.Sequential)]private struct Keyboard{public ushort Key,Scan;public uint Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct Mouse{public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Explicit)]private struct Union{[FieldOffset(0)]public Keyboard Keyboard;[FieldOffset(0)]public Mouse Mouse;}
    [StructLayout(LayoutKind.Sequential)]private struct Input{public uint Type;public Union Data;}
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] input,int size);
    private static readonly Lazy<(bool Capture,bool Record)> Features=new(()=>{
        try{foreach(var p in new PackageManager().FindPackagesForUser(""))if(p.Id.Name=="Microsoft.ScreenSketch"){var v=p.Id.Version;return(true,v.Major>11||v.Major==11&&v.Minor>=2307);}}catch{}
        return(false,false);
    });
    public static bool CanCapture=>Features.Value.Capture;
    public static bool CanRecord=>Features.Value.Record&&OperatingSystem.IsWindowsVersionAtLeast(10,0,22621);
    public static bool CanDictate=>OperatingSystem.IsWindowsVersionAtLeast(10,0,19041);
    public static void OpenScreenshot(){if(!CanCapture)throw new InvalidOperationException(LocalizedText.Get("未检测到 Windows 截图工具，请先安装或修复该应用。"));Chord(0x5B,0x10,0x53);}
    public static void OpenRecording(){if(!CanRecord)throw new InvalidOperationException(LocalizedText.Get("此版本截图工具不支持系统录屏快捷入口，请更新 Windows 截图工具。"));Chord(0x5B,0x10,0x52);}
    public static void OpenDictation(){if(!CanDictate)throw new InvalidOperationException(LocalizedText.Get("此 Windows 版本不支持听写入口。"));Chord(0x5B,0x48);}
    private static void Chord(params ushort[] keys)
    {
        var inputs=new Input[keys.Length*2];for(int i=0;i<keys.Length;i++){inputs[i]=new Input{Type=1,Data=new Union{Keyboard=new Keyboard{Key=keys[i]}}};inputs[keys.Length+i]=new Input{Type=1,Data=new Union{Keyboard=new Keyboard{Key=keys[keys.Length-1-i],Flags=2}}};}
        if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Input>())!=inputs.Length)throw new Win32Exception(Marshal.GetLastWin32Error(),LocalizedText.Get("Windows 未接受快捷键；请检查目标应用是否以更高权限运行。"));
    }
}

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DockForWindows.Services;

public static class ProjectionService
{
    [DllImport("user32.dll")]private static extern int SetDisplayConfig(uint pathCount,nint paths,uint modeCount,nint modes,uint flags);
    [DllImport("user32.dll")]private static extern int GetDisplayConfigBufferSizes(uint flags,out uint pathCount,out uint modeCount);
    [DllImport("user32.dll")]private static extern int QueryDisplayConfig(uint flags,ref uint pathCount,nint paths,ref uint modeCount,nint modes,out uint topology);
    public static string ReadTopology()
    {
        const uint current=4;int error=GetDisplayConfigBufferSizes(current,out var paths,out var modes);if(error!=0)throw new Win32Exception(error);
        // Oversized element storage avoids assumptions about SDK struct versions;
        // the OS writes its own native path/mode structures into these buffers.
        nint pathBuffer=Marshal.AllocHGlobal(checked((int)Math.Max(1,paths)*256)),modeBuffer=Marshal.AllocHGlobal(checked((int)Math.Max(1,modes)*256));
        try{error=QueryDisplayConfig(current,ref paths,pathBuffer,ref modes,modeBuffer,out var topology);if(error!=0)throw new Win32Exception(error);return topology switch{1=>LocalizedText.Get("仅电脑屏幕"),2=>LocalizedText.Get("复制显示"),4=>LocalizedText.Get("扩展显示"),8=>LocalizedText.Get("仅第二屏幕"),_=>LocalizedText.Get("投影与无线显示")};}
        finally{Marshal.FreeHGlobal(pathBuffer);Marshal.FreeHGlobal(modeBuffer);}
    }
    public static void SetTopology(uint topology)
    {
        if(topology is not (1 or 2 or 4 or 8))throw new ArgumentOutOfRangeException(nameof(topology));
        int error=SetDisplayConfig(0,0,0,0,0x80|topology);if(error!=0)throw new Win32Exception(error,LocalizedText.Get("Windows 无法应用此投影模式，请检查已连接的显示器。"));
    }
}

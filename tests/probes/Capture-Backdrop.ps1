$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Drawing; using System.Runtime.InteropServices;
public static class CaptureProbe {
 [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
 [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr w,IntPtr d,uint flags);
 public static void Shell(string path) {SetThreadDpiAwarenessContext(new IntPtr(-4));using(var b=new Bitmap(2560,1600)){using(var g=Graphics.FromImage(b)){var d=g.GetHdc();try{PrintWindow(GetShellWindow(),d,2);}finally{g.ReleaseHdc(d);}}b.Save(path);}}
 [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr w);
 [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr w,IntPtr d);
 [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr d,int x,int y,int w,int h,IntPtr s,int sx,int sy,uint op);
 public static void Save(string path,uint op) {
 SetThreadDpiAwarenessContext(new IntPtr(-4));
 using(var b=new Bitmap(2560,250)) { using(var g=Graphics.FromImage(b)) {var d=g.GetHdc();var s=GetDC(IntPtr.Zero);try{BitBlt(d,0,0,2560,250,s,0,1350,op);}finally{ReleaseDC(IntPtr.Zero,s);g.ReleaseHdc(d);}}b.Save(path);}}
}
'@ -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Runtime,System.Private.Windows.GdiPlus,System.Private.Windows.Core
[CaptureProbe]::Save((Join-Path $PSScriptRoot 'no-layer.png'),0x00CC0020)
[CaptureProbe]::Save((Join-Path $PSScriptRoot 'with-layer.png'),0x40CC0020)
[CaptureProbe]::Shell((Join-Path $PSScriptRoot 'shell.png'))



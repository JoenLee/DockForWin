using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
internal static class Program {
 [ComImport,Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface CaptureInterop { nint CreateForWindow(nint window,ref Guid iid); nint CreateForMonitor(nint monitor,ref Guid iid); }
 [DllImport("combase.dll")] static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)]string text,int length,out nint value);
 [DllImport("combase.dll")] static extern int WindowsDeleteString(nint value);
 [DllImport("combase.dll")] static extern int RoGetActivationFactory(nint name,ref Guid iid,out nint factory);
 [DllImport("d3d11.dll")] static extern int D3D11CreateDevice(nint adapter,int type,nint software,uint flags,nint levels,uint levelCount,uint version,out nint device,out int selected,out nint context);
 [DllImport("d3d11.dll")] static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgi,out nint inspectable);
 static GraphicsCaptureItem Item(nint window) {
  string name="Windows.Graphics.Capture.GraphicsCaptureItem";WindowsCreateString(name,name.Length,out var str);nint factory=0,item=0;
  try {var iid=typeof(CaptureInterop).GUID;Marshal.ThrowExceptionForHR(RoGetActivationFactory(str,ref iid,out factory)); var interop=(CaptureInterop)Marshal.GetObjectForIUnknown(factory);var itemGuid=new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");item=interop.CreateForWindow(window,ref itemGuid);return MarshalInspectable<GraphicsCaptureItem>.FromAbi(item);}
  finally {if(item!=0)Marshal.Release(item);if(factory!=0)Marshal.Release(factory);WindowsDeleteString(str);}
 }
 [STAThread] static void Main(){
  var app=new Application();var window=new Window{Title="Dock WGC prototype",Width=600,Height=300,Background=Brushes.DarkBlue};
  window.Loaded+=async(_,_)=>{ nint d3d=0,ctx=0,dxgi=0,inspectable=0;Direct3D11CaptureFramePool? pool=null;GraphicsCaptureSession? session=null;
   try {Marshal.ThrowExceptionForHR(D3D11CreateDevice(0,1,0,0x20,0,0,7,out d3d,out _,out ctx));var iid=new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3d,ref iid,out dxgi));Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi,out inspectable));var device=MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
    var item=Item(new WindowInteropHelper(window).Handle);pool=Direct3D11CaptureFramePool.CreateFreeThreaded(device,DirectXPixelFormat.B8G8R8A8UIntNormalized,2,item.Size);session=pool.CreateCaptureSession(item);session.IsCursorCaptureEnabled=false;session.IsBorderRequired=false;
    int frames=0;pool.FrameArrived+=(p,a)=>{using var frame=p.TryGetNextFrame();System.Threading.Interlocked.Increment(ref frames);};session.StartCapture();await Task.Delay(2000);window.Background=Brushes.OrangeRed;await Task.Delay(2000);Console.WriteLine($"frames={frames}, border={session.IsBorderRequired}, size={item.Size.Width}x{item.Size.Height}");
   }catch(Exception ex){Console.WriteLine(ex);}finally{session?.Dispose();pool?.Dispose();if(inspectable!=0)Marshal.Release(inspectable);if(dxgi!=0)Marshal.Release(dxgi);if(ctx!=0)Marshal.Release(ctx);if(d3d!=0)Marshal.Release(d3d);window.Close();}
  };app.Run(window);
 }
}

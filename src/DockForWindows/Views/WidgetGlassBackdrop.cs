using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DockForWindows.Services;
using Forms=System.Windows.Forms;

namespace DockForWindows.Views;

// Desktop cards on one monitor share a capture; gallery/editor cards share their owning window's capture.
internal sealed class WidgetGlassBackdrop
{
    private static readonly Dictionary<object,WidgetGlassBackdrop> Groups=new();
    private readonly object _key;
    private readonly HashSet<WidgetHost> _hosts=new();
    private readonly GlassBackdropService _capture=new();
    private readonly DispatcherTimer _timer;
    private readonly FramePerformance _performance=new();
    private readonly Forms.Screen? _screen;
    private WriteableBitmap? _frame;
    private bool _busy,_disposed;
    private long _lastError;
    private long _lastChange;
    internal static int ActiveGroups=>Groups.Count;
    internal long UploadedFrames {get;private set;}
    private WidgetGlassBackdrop(object key,Dispatcher dispatcher,nint handle)
    {
        _key=key;_screen=key is string device?Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==device):null;_performance.Detect(handle);_timer=new DispatcherTimer(TimeSpan.FromSeconds(1d/30),DispatcherPriority.Background,(_,_)=>Tick(),dispatcher);_timer.Start();
    }
    private static object Key(Window window)=>window is WidgetDesktopWindow?Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).DeviceName:window;
    internal bool Matches(WidgetHost host)=>Window.GetWindow(host) is {} window&&Equals(_key,Key(window));
    internal static WidgetGlassBackdrop? Attach(WidgetHost host)
    {
        var window=Window.GetWindow(host);if(window is null)return null;
        object key=Key(window);
        if(!Groups.TryGetValue(key,out var group)){group=new(key,host.Dispatcher,new WindowInteropHelper(window).Handle);Groups.Add(key,group);}
        group._hosts.Add(host);return group;
    }
    internal void Detach(WidgetHost host)
    {
        _hosts.Remove(host);if(_hosts.Count!=0)return;
        _disposed=true;_timer.Stop();Groups.Remove(_key);_frame=null;
        // The worker owns the native capture until its current read has completed.
        if(!_busy)_capture.Dispose();
    }
    private async void Tick()
    {
        if(_disposed||_busy)return;
        var panes=new List<(WidgetHost Host,Rect Bounds)>();nint handle=0;double dpi=1;
        try{
            foreach(var host in _hosts){if(!host.TryGlassBounds(out var bounds))continue;
                panes.Add((host,bounds));handle=new WindowInteropHelper(Window.GetWindow(host)).Handle;dpi=Math.Max(dpi,VisualTreeHelper.GetDpi(host).DpiScaleX);}
            if(panes.Count==0)return;
            // A stable window/monitor extent avoids reallocating textures on each carousel or drag frame.
            System.Drawing.Rectangle request;
            if(_screen is not null)request=_screen.Bounds;
            else if(NativeMethods.GetWindowRect(handle,out var rect))request=System.Drawing.Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
            else return;
            _capture.ExcludedWindows=_screen is null?null:WidgetDesktopService.Current?.WindowHandles;
            _busy=true;
            using var sample=await Task.Run(()=>Read(handle,request,dpi));
            if(_disposed)return;
            if(sample is null)throw new InvalidOperationException(UiText.IsEnglish?"No background frame was returned.":"未取得背景画面。");
            bool fresh=_frame is null||_frame.PixelWidth!=sample.Width||_frame.PixelHeight!=sample.Height;
            if(fresh&&!sample.Changed){_capture.ForgetComposite();return;}
            if(fresh)_frame=new WriteableBitmap(sample.Width,sample.Height,96,96,PixelFormats.Bgra32,null);
            if(sample.Changed){_frame!.WritePixels(new Int32Rect(0,0,sample.Width,sample.Height),sample.Pixels,sample.Width*4,0);UploadedFrames++;_lastChange=Environment.TickCount64;}
            _timer.Interval=TimeSpan.FromSeconds(1/(Environment.TickCount64-_lastChange>500?30:Math.Clamp(_performance.RefreshRate,30,360)));
            foreach(var (host,bounds) in panes)if(_hosts.Contains(host))host.SetGlassBackdrop(_frame!,sample.Bounds,bounds,sample.Changed);
        }catch(Exception error){foreach(var host in _hosts)host.GlassCaptureFailed(error);if(Environment.TickCount64-_lastError>5000){_lastError=Environment.TickCount64;WidgetDesktopService.Current?.LogGlassError(error);}}
        finally{_busy=false;if(_disposed)_capture.Dispose();}
    }
    private Sample? Read(nint handle,System.Drawing.Rectangle request,double dpi)
    {
        if(_capture.ReadPixels(handle,request,dpi) is not {} raw)return null;
        int pad=(int)Math.Ceiling(24*dpi);
        var sample=new Sample{Width=raw.Width,Height=raw.Height,Bounds=new Rect(request.X-pad,request.Y-pad,raw.Width,raw.Height),Changed=raw.Changed};
        // ReadPixels lends its buffer. Copy on this serial worker before any later read can reuse it.
        if(raw.Changed){int bytes=checked(raw.Width*raw.Height*4);sample.Pixels=ArrayPool<byte>.Shared.Rent(bytes);raw.Pixels.AsSpan(0,bytes).CopyTo(sample.Pixels);}
        return sample;
    }
    private sealed class Sample:IDisposable
    {
        internal byte[] Pixels=Array.Empty<byte>();internal int Width,Height;internal Rect Bounds;internal bool Changed;
        public void Dispose(){if(Pixels.Length>0){ArrayPool<byte>.Shared.Return(Pixels);Pixels=Array.Empty<byte>();}}
    }
}

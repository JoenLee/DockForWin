using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Services;
using Forms=System.Windows.Forms;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private sealed class SurfaceFrame
    {
        public Point Origin;public Size Size;public Rect Logical;public double LastTint,Moved;
    }
    private sealed class SampleResult:IDisposable
    {
        public byte[] Pixels {get;private set;}
        public readonly int Width,Height;
        public readonly Rect BackdropBounds;
        private int ByteCount=>checked(Width*Height*4);
        // False when the capture service found nothing new under the panel; such a result carries no pixels.
        public bool Changed {get;private init;}=true;
        public SampleResult(byte[] pixels,int width,int height,Rect bounds)
        {
            Width=width;Height=height;BackdropBounds=bounds;
            var source=pixels.AsSpan(0,ByteCount);
            Pixels=ArrayPool<byte>.Shared.Rent(source.Length);
            source.CopyTo(Pixels);
        }
        private SampleResult(int width,int height,Rect bounds){Width=width;Height=height;BackdropBounds=bounds;Pixels=Array.Empty<byte>();Changed=false;}
        public static SampleResult Unchanged(int width,int height,Rect bounds)=>new(width,height,bounds);
        public bool SamePixels(SampleResult? previous)=>previous is not null&&Width==previous.Width&&Height==previous.Height&&Pixels.AsSpan(0,ByteCount).SequenceEqual(previous.Pixels.AsSpan(0,ByteCount));
        public void Dispose(){var pixels=Pixels;Pixels=Array.Empty<byte>();if(pixels.Length>0)ArrayPool<byte>.Shared.Return(pixels);}
    }
    private static readonly Brush DarkInk=Palette.Brush("#202124");
    private static readonly BitmapCache StillPlate=Frozen(new BitmapCache{RenderAtScale=1,SnapsToDevicePixels=false,EnableClearType=false});
    private static T Frozen<T>(T value)where T:Freezable{value.Freeze();return value;}
    private readonly Dictionary<DockSurface,SurfaceFrame> _surfaceFrames=new();
    private WriteableBitmap? _wholeFrame;
    private bool _backgroundPixelsChanged;
    private SampleResult? _displayedSample;
    internal long UploadedFrames {get;private set;}
    private double _lastChrome,_lastMotion;
    private readonly FramePerformance _perf=new();private TimeSpan _renderTime;
    private void OnRendering(object? sender,EventArgs args)
    {
        if(!_ready)return;if(_closing){MapGlassFrame(false);return;}ObserveBackgroundPress((NativeMethods.GetAsyncKeyState(1)&0x8000)!=0);
        if(args is RenderingEventArgs rendering){if(rendering.RenderingTime==_renderTime)return;_renderTime=rendering.RenderingTime;}
        double now=_runtime.Elapsed.TotalSeconds;
        _perf.OnFrame(now);
        double dt=_lastFrame==0?1d/60:now-_lastFrame;_lastFrame=now;
        AnimationFrames++;if(AnimationFrames>6)LongestAnimationGap=Math.Max(LongestAnimationGap,dt);
        foreach(var c in _modules)
        {
            if(c==_drag){TrackDragPointer(c,System.Windows.Input.Mouse.GetPosition(_cards));continue;}
            double x=c.Position.X,y=c.Position.Y;
            if(c.MoveStarted<0){x=ControlMotion.Step(x,c.Target.X,ref c.VX,dt,ControlMotion.LayoutSpeed);y=ControlMotion.Step(y,c.Target.Y,ref c.VY,dt,ControlMotion.LayoutSpeed);}
            double w=ControlMotion.Step(c.Root.Width,c.Target.Width,ref c.VW,dt,ControlMotion.LayoutSpeed),h=ControlMotion.Step(c.Root.Height,c.Target.Height,ref c.VH,dt,ControlMotion.LayoutSpeed);
            if(c.MoveStarted>=0){double elapsed=now-c.MoveStarted,ease=c.MovePath.Value(elapsed),arc=Math.Sin(Math.Clamp(ease,0,1)*Math.PI);x=c.MoveFrom.X+(c.Target.X-c.MoveFrom.X)*ease+c.Avoidance.X*arc;y=c.MoveFrom.Y+(c.Target.Y-c.MoveFrom.Y)*ease+c.Avoidance.Y*arc;double v=c.MovePath.Velocity(elapsed),av=ease>0&&ease<1?Math.Cos(ease*Math.PI)*Math.PI*v:0;c.VX=(c.Target.X-c.MoveFrom.X)*v+c.Avoidance.X*av;c.VY=(c.Target.Y-c.MoveFrom.Y)*v+c.Avoidance.Y*av;if(elapsed>=c.MovePath.Duration)c.MoveStarted=-1;}
            double delta=Math.Abs(x-c.Position.X)+Math.Abs(y-c.Position.Y)+Math.Abs(w-c.Root.Width)+Math.Abs(h-c.Root.Height);
            if(delta>.02){_lastMotion=now;_chromeDirty=true;}
            if(delta>.002){c.Position.X=x;c.Position.Y=y;c.Root.Width=w;c.Root.Height=h;}
        }
        StepNavigationGlass();MapGlassFrame(_backgroundPixelsChanged);_backgroundPixelsChanged=false;UpdateSmoothMediaTimeline();UpdateSafetyFade();UpdateInputRegion();
        // No dispatcher timer limits the animation frame rate. Acquisition runs independently,
        // with one request in flight and no accumulating queue when a window is slow to capture.
        // The independent serial pump acquires the next sample without waiting an extra composition frame.
        if(!UsesClassicBlur&&_chromeDirty&&GlassBackdrop is not null&&now-_lastChrome>.05){_lastChrome=now;_chromeDirty=false;foreach(var chrome in _chrome)chrome.RefreshMaterial();}
    }
    // A changing backdrop is followed at the display's rate. Once nothing behind the panel has changed for half a
    // second, it is looked at 30 times a second instead; the first change found brings the full rate straight back,
    // and moving or resizing the window asks for a sample at once (RequestSample).
    private readonly System.Threading.SemaphoreSlim _sampleRequested=new(0,1);
    private double _lastBackdropChange=double.NegativeInfinity;
    private void RequestSample(){if(_sampleRequested.CurrentCount==0)try{_sampleRequested.Release();}catch(System.Threading.SemaphoreFullException){}}
    private async Task CaptureLoop()
    {
        try{
            while(!_closing){double started=_runtime.Elapsed.TotalMilliseconds;await CaptureFrame();
                bool settled=_runtime.Elapsed.TotalSeconds-_lastBackdropChange>.5&&_entranceStarted;
                double period=1000/(settled?30:Math.Clamp(_perf.RefreshRate,30,360));
                await _sampleRequested.WaitAsync(TimeSpan.FromMilliseconds(Math.Max(1,period-(_runtime.Elapsed.TotalMilliseconds-started))),_lifetime.Token);
            }
        }catch(OperationCanceledException)when(_closing){}
    }
    private void PositionAtTray(bool initial=false)
    {
        if(_closing||_positioning||!_ready||(!initial&&(_drag is not null||_resizing)))return;_positioning=true;
        try
        {
            nint handle=new WindowInteropHelper(this).Handle;_screen=Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==_screen?.DeviceName)??Forms.Screen.FromPoint(Forms.Cursor.Position);var area=_screen.WorkingArea;
            _perf.Detect(handle);ControlMotion.RefreshRate=_perf.RefreshRate; // 跨屏 / DPI 变化时重评估刷新率与呈现策略
            if(initial)NativeMethods.SetWindowPos(handle,0,area.Right-400,area.Top+16,0,0,0x0015);
            // 混合 DPI：若窗口中心尚未落在目标屏，先移到目标屏工作区内，确保 GetDpiForWindow 读到目标屏 DPI 再计算缩放。
            else if(NativeMethods.GetWindowRect(handle,out var pre)&&!_screen.Bounds.Contains((int)((pre.Left+pre.Right)/2d),(int)((pre.Top+pre.Bottom)/2d)))NativeMethods.SetWindowPos(handle,0,area.Left+16,area.Top+16,0,0,0x0015);
            double dpi=NativeMethods.GetDpiForWindow(handle)/96d;double logicalHeight=Math.Max(_mainHeight,_stage.Height)+90;
            double fit=ControlCenterLayout.FitPanel(_options.Geometry.PanelScale,new Size(_stage.Width+SideGutter*2,logicalHeight),new Size(Math.Max(1,area.Width/dpi-16),Math.Max(1,area.Height/dpi-20)));if(Math.Abs(fit-_fit.ScaleX)>.002){_fit.ScaleX=_fit.ScaleY=fit;Width=(_stage.Width+SideGutter*2)*fit;}
            _fromBelow=!(area.Top>_screen.Bounds.Top);
            int x=area.Right-(int)Math.Ceiling((Width-(SideGutter-40)*_fit.ScaleX)*dpi)-8;int y=area.Top>_screen.Bounds.Top?area.Top+4-(int)Math.Round((VerticalGutter-18)*_fit.ScaleY*dpi):area.Bottom-(int)Math.Ceiling(ActualHeight*dpi)-4+(int)Math.Round((VerticalGutter-18)*_fit.ScaleY*dpi);
            if(NativeMethods.GetWindowRect(handle,out var current)&&(Math.Abs(current.Left-x)>1||Math.Abs(current.Top-y)>1)){NativeMethods.SetWindowPos(handle,0,x,y,0,0,0x0015);_lastMotion=_runtime.Elapsed.TotalSeconds;}
        }
        finally{if(Content is FrameworkElement host&&_screen is not null&&PresentationSource.FromVisual(host) is not null){var area=_screen.WorkingArea;var a=host.PointFromScreen(new Point(area.Left,area.Top));var b=host.PointFromScreen(new Point(area.Right,area.Bottom));var rect=Rect.Intersect(new Rect(a,b),new Rect(host.RenderSize));if(host.Clip is not RectangleGeometry existing||existing.Rect!=rect){var clip=new RectangleGeometry(rect);clip.Freeze();host.Clip=clip;}}_positioning=false;}
    }
    // Opens on one copy of the screen under the panel (about 20 ms) instead of waiting 150-250 ms for the first
    // composed sample; the capture loop's first result replaces it, normally within a few frames of the entrance.
    private async Task SeedFromScreen()
    {
        if(_closing||_wholeFrame is not null)return;nint handle=new WindowInteropHelper(this).Handle;if(!NativeMethods.GetWindowRect(handle,out var rect))return;double dpi=VisualTreeHelper.GetDpi(this).DpiScaleX;
        var bounds=System.Drawing.Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
        try
        {
            var raw=await Task.Run(()=>GlassBackdropService.ReadScreen(bounds,dpi));
            if(raw is not {} frame||_closing||_wholeFrame is not null)return;
            int pad=(int)Math.Ceiling(24*dpi);
            using var outcome=new SampleResult(frame.Pixels,frame.Width,frame.Height,new Rect(bounds.X-pad,bounds.Y-pad,frame.Width,frame.Height));
            ApplySample(outcome);_capture.ForgetComposite();
        }
        catch(Exception ex){_log?.Invoke(LocalizedText.Get("控制中心背景采样"),ex);}
    }
    private async Task CaptureFrame()
    {
        if(_busy||_closing||!IsVisible)return;nint handle=new WindowInteropHelper(this).Handle;if(!NativeMethods.GetWindowRect(handle,out var rect))return;double dpi=VisualTreeHelper.GetDpi(this).DpiScaleX;_busy=true;
        try
        {
            var bounds=System.Drawing.Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
            var outcome=await Task.Run(()=>Sample(handle,bounds,dpi));
            if(_closing){outcome?.Dispose();return;}
            if(outcome is null)return;
            // Upload on the UI continuation: retaining a completed snapshot until Rendering adds stale-frame latency.
            try{ApplySample(outcome);if(outcome.Changed){_displayedSample?.Dispose();_displayedSample=outcome;}else outcome.Dispose();}
            catch{outcome.Dispose();throw;}
        }
        catch(Exception ex){if(_runtime.Elapsed.TotalSeconds-_lastCaptureError>5){_lastCaptureError=_runtime.Elapsed.TotalSeconds;_log?.Invoke(LocalizedText.Get("控制中心背景采样"),ex);}}
        finally{_busy=false;if(_closing)_capture.Dispose();}
    }
    // One full-resolution live texture is shared by all plates. Their existing shader maps
    // screen coordinates locally; changing a plate's size never creates a new capture bitmap.
    private SampleResult? Sample(nint handle,System.Drawing.Rectangle bounds,double dpi)
    {
        if(_capture.ReadPixels(handle,bounds,dpi) is not {} raw)return null;
        int pad=(int)Math.Ceiling(24*dpi);var backdrop=new Rect(bounds.X-pad,bounds.Y-pad,raw.Width,raw.Height);
        // Nothing new under the panel: no copy here, no upload on the UI thread.
        return raw.Changed?new SampleResult(raw.Pixels,raw.Width,raw.Height,backdrop):SampleResult.Unchanged(raw.Width,raw.Height,backdrop);
    }    private void ApplySample(SampleResult outcome)
    {
        bool fresh=_wholeFrame is null||_wholeFrame.PixelWidth!=outcome.Width||_wholeFrame.PixelHeight!=outcome.Height;
        // An unchanged result cannot fill a new bitmap; have the next read compose a whole frame.
        if(fresh&&!outcome.Changed){_capture.ForgetComposite();return;}
        // The capture service already compared these pixels with the frame it handed over last.
        bool pixelsChanged=fresh||outcome.Changed;
        bool mappingChanged=fresh||GlassBackdropBounds!=outcome.BackdropBounds;
        if(fresh)_wholeFrame=new WriteableBitmap(outcome.Width,outcome.Height,96,96,PixelFormats.Bgra32,null);
        var frame=_wholeFrame!;int rawStride=outcome.Width*4;
        if(pixelsChanged){UploadedFrames++;_lastBackdropChange=_runtime.Elapsed.TotalSeconds;}
        if(pixelsChanged)frame.WritePixels(new Int32Rect(0,0,outcome.Width,outcome.Height),outcome.Pixels,rawStride,0);
        GlassBackdropBounds=outcome.BackdropBounds;GlassBackdrop=frame;CapturedFrames++;UpdateDetailForeground(frame);
        // Pixels are already uploaded. Coalesce only mapping work, never capture frames.
        if(mappingChanged){MapGlassFrame(pixelsChanged);_backgroundPixelsChanged=false;}
        else _backgroundPixelsChanged|=pixelsChanged;
        if(pixelsChanged||_lastFrame-_lastMotion<.15||_modules.Any(c=>c.Light is null))
        {
            foreach(var c in _modules)
            {
                if(!c.Root.IsVisible||_cards.Opacity<.001)continue;
                var origin=c.Root.PointToScreen(new Point());var far=c.Root.PointToScreen(new Point(c.Root.ActualWidth,c.Root.ActualHeight));c.Contrast.Observe(frame,new Int32Rect((int)(origin.X-GlassBackdropBounds.X),(int)(origin.Y-GlassBackdropBounds.Y),Math.Max(1,(int)(far.X-origin.X)),Math.Max(1,(int)(far.Y-origin.Y))),Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency);
                bool light=ResolveLight(c.Contrast.Luminance,c.Light);if(c.Light!=light){c.Light=light;TextForeground(c.Body,light?Brushes.White:Palette.Brush("#202124"));}
            }
        }
        RefreshHeaderForeground();
        AnimateOpen();
    }

    private void MapGlassFrame(bool pixelsChanged)
    {
        if(_wholeFrame is not {} frame)return;
        UpdateClassicBackdrop();
        if(UsesClassicBlur)return;
        foreach(var surface in _surfaces){
            if(!surface.IsVisible||surface.ActualWidth<=0||surface.ActualHeight<=0)continue;
            if(surface==_detailSurface&&_detailGlassHost.Opacity<.001)continue;
            if(surface!=_detailSurface&&surface!=_navigationGlass&&_cards.Opacity<.001)continue;
            if(!_surfaceFrames.TryGetValue(surface,out var cached)){cached=new SurfaceFrame();_surfaceFrames.Add(surface,cached);}
            var origin=surface.PointToScreen(new Point());var logical=surface.TransformToAncestor(this).TransformBounds(new Rect(surface.RenderSize));
            bool geometry=cached.Origin!=origin||cached.Size!=surface.RenderSize||cached.Logical!=logical;
            // A still plate is one cached texture while the page around it repaints. A moving plate re-renders every
            // frame anyway (what lies behind it changes), and its cache would only add a pass to write and one to draw;
            // it renders directly until it has been still for a moment. The morphing navigation plate never settles.
            if(geometry)cached.Moved=_lastFrame;
            var mode=surface==_navigationGlass?null:_lastFrame-cached.Moved>.12?StillPlate:null;
            if(!ReferenceEquals(surface.CacheMode,mode))surface.CacheMode=mode;
            bool source=surface.BackdropScreenBounds!=GlassBackdropBounds||!ReferenceEquals(surface.Backdrop,frame);
            if(geometry||source||(pixelsChanged&&_lastFrame-cached.LastTint>.12)){
                var dpi=VisualTreeHelper.GetDpi(this);
                surface.CapturePixelScale=new DpiScale(Math.Max(.01,logical.Width*dpi.DpiScaleX/surface.ActualWidth),Math.Max(.01,logical.Height*dpi.DpiScaleY/surface.ActualHeight));
                surface.BackdropScreenBounds=GlassBackdropBounds;if(!ReferenceEquals(surface.Backdrop,frame))surface.Backdrop=frame;else surface.InvalidateVisual();
                cached.Origin=origin;cached.Size=surface.RenderSize;cached.Logical=logical;cached.LastTint=_lastFrame;
                if(surface.Opacity==0)surface.Opacity=1;
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using Windows.Graphics.Capture;

// Measures the liquid-glass control center the way a person meets it: opening, expanding a module,
// reversing quickly, dragging a slider, a scrolling page behind the glass, idling and closing.
// Frame intervals come from Windows Graphics Capture observing the control center window, i.e. the
// updates the compositor received; Rendering callbacks are reported separately and never as FPS.
// The page behind the panel runs in its own process (--perf-background), as a real window would.
// Scenes (default: open,idle,expand-return,rapid-reverse,slider,live-background,close):
//   replay        the entrance again on a warm window (DOCK_PERF_REPLAYS times)
//   calibrate[-layered], calibrate-large[-layered]   plain animated windows: the observer's own ceiling
//   shot, classicshot, surfaces   PNGs for before/after pixel comparison (Liquid Glass, Classic Blur, Dock-style plates)
//   flash, fade, hittest          a new window never shows an opaque frame; the reveal fades; clear strips pass clicks
//   blurprofile   WPF's Gaussian BlurEffect kernel, measured on screen (the fused plate shader reproduces it)
// DOCK_PERF_DATA: profile folder with settings.json (default: isolated defaults).
// DOCK_PERF_SCENES: comma-separated subset. DOCK_PERF_LABEL: name for this run. DOCK_PERF_STRESS=1: every glass control at 1.
internal static class ControlCenterPerfChecks
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static object Field(object o,string n)=>o.GetType().GetField(n,F)!.GetValue(o)!;
    private static object? Call(object o,string n,params object?[] args)=>o.GetType().GetMethod(n,F)!.Invoke(o,args);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern int GetCurrentThreadId();
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] private static extern nint FindWindow(string? className,string name);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint WindowFromPoint(System.Drawing.Point point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetAncestor(nint window,int flags);
    // Native thread id -> owning module of its start address, so the WPF render thread (wpfgfx) and the
    // capture worker (coreclr) can be told apart without instrumenting the app.
    private static Dictionary<int,(double Ms,string Module)> Threads(Process process)
    {
        process.Refresh();var modules=new List<(long Start,long End,string Name)>();
        try{foreach(ProcessModule m in process.Modules)modules.Add(((long)m.BaseAddress,(long)m.BaseAddress+m.ModuleMemorySize,System.IO.Path.GetFileNameWithoutExtension(m.ModuleName)));}catch{}
        var result=new Dictionary<int,(double,string)>();
        foreach(ProcessThread t in process.Threads){try{long a=StartOf(t.Id);string name=modules.FirstOrDefault(m=>a>=m.Start&&a<m.End).Name??"?";string? described=Describe(t.Id);if(!string.IsNullOrEmpty(described))name=described.Replace(".NET ","").Replace(' ','_');result[t.Id]=(t.TotalProcessorTime.TotalMilliseconds,name);}catch{}}
        return result;
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern nint OpenThread(int access,bool inherit,int id);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [System.Runtime.InteropServices.DllImport("ntdll.dll")] private static extern int NtQueryInformationThread(nint thread,int type,out nint value,int size,nint returned);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern int GetThreadDescription(nint thread,out nint text);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern nint LocalFree(nint memory);
    private static string? Describe(int id){nint h=OpenThread(0x0800,false,id);if(h==0)return null;try{if(GetThreadDescription(h,out nint text)<0||text==0)return null;try{return System.Runtime.InteropServices.Marshal.PtrToStringUni(text);}finally{LocalFree(text);}}finally{CloseHandle(h);}}
    private static long StartOf(int id){nint h=OpenThread(0x40,false,id);if(h==0)return 0;try{return NtQueryInformationThread(h,9,out nint start,nint.Size,0)==0?(long)start:0;}finally{CloseHandle(h);}}
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmGetCompositionTimingInfo(nint hwnd,nint info);
    private static (long Frames,long Refreshes) DwmFrames(){nint info=System.Runtime.InteropServices.Marshal.AllocHGlobal(320);try{System.Runtime.InteropServices.Marshal.WriteInt32(info,320);if(DwmGetCompositionTimingInfo(0,info)!=0)return (0,0);return (System.Runtime.InteropServices.Marshal.ReadInt64(info,56),System.Runtime.InteropServices.Marshal.ReadInt64(info,36));}finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(info);}}
    private static double P(List<double> a,double p){if(a.Count==0)return 0;var b=a.Order().ToArray();return b[Math.Min(b.Length-1,(int)(b.Length*p))];}

    private sealed class Observer:IDisposable
    {
        private readonly IDisposable _window;private readonly object _gate=new();private List<double>? _samples;private double _last;
        public Observer(WindowFrameCapture device,nint handle)
        {
            var item=(GraphicsCaptureItem)typeof(WindowFrameCapture).GetMethod("CreateItem",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{handle})!;
            var type=typeof(WindowFrameCapture).GetNestedType("CaptureWindow",BindingFlags.NonPublic)!;
            // The platform default spaces capture frames >=16.7 ms apart (every third vsync at 165 Hz); ask for every update.
            _window=(IDisposable)type.GetConstructors(F).Single().Invoke(new object?[]{item,Field(device,"_winrtDevice"),TimeSpan.FromMilliseconds(1)});
            var pool=(Direct3D11CaptureFramePool)Field(_window,"_pool");var frameGate=Field(_window,"Gate");
            pool.FrameArrived+=(_,_)=>{lock(frameGate){if(Field(_window,"Latest") is not Direct3D11CaptureFrame frame)return;double stamp=frame.SystemRelativeTime.TotalMilliseconds;lock(_gate){if(_samples is not null&&stamp>_last){if(_last>0)_samples.Add(stamp-_last);_last=stamp;}}}};
        }
        public void Begin(List<double> target){lock(_gate){_samples=target;_last=0;}}
        public void End(){lock(_gate){_samples=null;}}
        public void Dispose(){End();_window.Dispose();}
    }

    // --perf-background left top width height: the document page behind the panel, in its own process.
    internal static int Background(double left,double top,double width,double height,int parent=0)
    {
        // Leave with the measuring process, even when it fails before signalling.
        if(parent>0)new System.Threading.Thread(()=>{try{Process.GetProcessById(parent).WaitForExit();}catch{}Environment.Exit(0);}){IsBackground=true}.Start();
        var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};
        var page=new Canvas{ClipToBounds=true};var scroll=new TranslateTransform();var content=new Canvas();content.RenderTransform=scroll;page.Children.Add(content);
        var shift=new TranslateTransform();var gradient=new LinearGradientBrush(Color.FromRgb(46,58,120),Color.FromRgb(18,128,120),35){RelativeTransform=shift};
        var window=new Window{Left=left,Top=top,Width=width,Height=height,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Background=gradient,Content=page,Title="dock-perf-background"};
        var random=new Random(7);
        for(int row=0;row<90;row++){
            double y=row*46;
            content.Children.Add(new TextBlock{Text=string.Concat(Enumerable.Repeat("控制中心液态玻璃 Liquid glass refraction ",4)),FontSize=row%7==0?22:15,Foreground=new SolidColorBrush(Color.FromArgb(230,255,255,255)),Margin=new Thickness(24,y,0,0)});
            if(row%3==0){var block=new Rectangle{Width=80+random.Next(240),Height=22,RadiusX=6,RadiusY=6,Fill=new SolidColorBrush(Color.FromRgb((byte)random.Next(80,255),(byte)random.Next(80,255),(byte)random.Next(80,255)))};Canvas.SetLeft(block,40+random.Next(300));Canvas.SetTop(block,y+24);content.Children.Add(block);}
        }
        // Scrolling a cached page is a texture move, so the page itself stays light on a shared integrated GPU.
        content.CacheMode=new BitmapCache{RenderAtScale=1};
        var scrollSignal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,@"Local\DockPerfScroll");
        var quitSignal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset,@"Local\DockPerfQuit");
        var listener=new System.Threading.Thread(()=>{
            var handles=new System.Threading.WaitHandle[]{scrollSignal,quitSignal};
            while(true){int which=System.Threading.WaitHandle.WaitAny(handles);
                if(which==1){window.Dispatcher.Invoke(window.Close);return;}
                window.Dispatcher.Invoke(()=>{scroll.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,-900,TimeSpan.FromSeconds(4)));shift.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,.3,TimeSpan.FromSeconds(2)){AutoReverse=true});});}
        }){IsBackground=true};listener.Start();
        app.Run(window);return 0;
    }
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;
        string label=Environment.GetEnvironmentVariable("DOCK_PERF_LABEL") is {Length:>0} named?named:"run";
        var settings=new SettingsService(Environment.GetEnvironmentVariable("DOCK_PERF_DATA") is {Length:>0} profile?profile:System.IO.Path.Combine(directory,"profile")).Load();settings.Animations=true;
        if(Environment.GetEnvironmentVariable("DOCK_PERF_STRESS")=="1"){settings.GlassIntensity=settings.GlassDistortion=settings.GlassFrost=settings.GlassOpacity=settings.GlassHighlight=settings.GlassSideDarkness=settings.GlassGrain=1;settings.ControlCenter.OverrideMaterial=false;}
        settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.LiquidGlass;
        var wanted=(Environment.GetEnvironmentVariable("DOCK_PERF_SCENES") is {Length:>0} list?list:"open,idle,expand-return,rapid-reverse,slider,live-background,close").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        // Only the right side of the work area: enough to sit under the panel and its padding without covering the whole screen.
        // The page runs in its own process, as a real window behind the panel would: its rendering and scrolling
        // do not share this process's WPF render thread.
        var area=SystemParameters.WorkArea;double width=Math.Min(area.Width,760);
        var driver=new Window{Left=area.Left,Top=area.Top,Width=1,Height=1,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Title="dock-perf-driver"};
        var scrollSignal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,@"Local\DockPerfScroll");
        var quitSignal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset,@"Local\DockPerfQuit");quitSignal.Reset();
        var host=Process.GetCurrentProcess().MainModule!.FileName;var dll=typeof(ControlCenterPerfChecks).Assembly.Location;
        var remote=Process.Start(new ProcessStartInfo(host,$"\"{dll}\" --perf-background {area.Right-width} {area.Top} {width} {area.Height} {Environment.ProcessId}"){UseShellExecute=false,CreateNoWindow=true})!;
        System.Threading.Thread.Sleep(2500);
        var background=driver;
        var results=new List<object>();
        background.Loaded+=async(_,_)=>{
            ControlCenterWindow? center=null;Observer? observer=null;WindowFrameCapture? device=null;
            var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
            var process=Process.GetCurrentProcess();
            try{
                device=new WindowFrameCapture();
                List<double>? current=null;int uiThread=GetCurrentThreadId();
                async Task<(ControlCenterWindow Window,double OpenMs)> Open(){
                    var window=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};var clock=Stopwatch.StartNew();window.Show();
                    var handle=new WindowInteropHelper(window).Handle;observer?.Dispose();observer=new Observer(device,handle);if(current is not null)observer.Begin(current);
                    double opened=-1;while(clock.ElapsedMilliseconds<5000){if((bool)Field(window,"_entranceStarted")){opened=clock.Elapsed.TotalMilliseconds;break;}await Task.Delay(1);}
                    return (window,opened);
                }
                async Task Scene(string scene,Func<Task> act,double? openMs=null){
                    var intervals=new List<double>();var callbacks=new List<double>();double last=0;var timer=Stopwatch.StartNew();
                    process.Refresh();var cpu=process.TotalProcessorTime;long bytes=GC.GetTotalAllocatedBytes();int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
                    var startCenter=center;long captured=center?.CapturedFrames??0,animation=center?.AnimationFrames??0,uploaded=center?.UploadedFrames??0;
                    void Callback(object? s,EventArgs e){double now=timer.Elapsed.TotalMilliseconds;if(last>0)callbacks.Add(now-last);last=now;}
                    // UI-thread busy time: every dispatcher operation, including layout and the UI half of rendering.
                    double busy=0;var opClock=new Stopwatch();int depth=0;
                    void Started(object? s,System.Windows.Threading.DispatcherHookEventArgs e){if(depth++==0)opClock.Restart();}
                    void Completed(object? s,System.Windows.Threading.DispatcherHookEventArgs e){if(depth>0&&--depth==0)busy+=opClock.Elapsed.TotalMilliseconds;}
                    var hooks=System.Windows.Threading.Dispatcher.CurrentDispatcher.Hooks;hooks.OperationStarted+=Started;hooks.OperationCompleted+=Completed;hooks.OperationAborted+=Completed;
                    var before=Threads(process);var dwmBefore=DwmFrames();
                    CompositionTarget.Rendering+=Callback;current=intervals;observer?.Begin(intervals);
                    await act();
                    observer?.End();current=null;CompositionTarget.Rendering-=Callback;hooks.OperationStarted-=Started;hooks.OperationCompleted-=Completed;hooks.OperationAborted-=Completed;process.Refresh();double seconds=timer.Elapsed.TotalSeconds;
                    var after=Threads(process);var dwmAfter=DwmFrames();
                    string top=string.Join(" ",after.Select(t=>(t.Key,t.Value.Module,Ms:t.Value.Ms-(before.TryGetValue(t.Key,out var b)?b.Ms:0))).Where(t=>t.Ms>0).OrderByDescending(t=>t.Ms).Take(7).Select(t=>$"{(t.Key==uiThread?"UI":t.Module)}:{t.Ms/seconds/10:0}%"));
                    double hz=center is not null?((FramePerformance)Field(center,"_perf")).RefreshRate:60,period=1000/hz;
                    var motion=intervals.Where(i=>i<=250).ToList();
                    int B(double lo,double hi)=>intervals.Count(i=>i>lo*period&&i<=hi*period);
                    string histogram=$"1:{B(0,1.5)} 2:{B(1.5,2.5)} 3:{B(2.5,3.5)} 4-6:{B(3.5,6.5)} 7-16:{B(6.5,16.5)} >16:{intervals.Count(i=>i>16.5*period)}";
                    File.WriteAllText(System.IO.Path.Combine(directory,$"intervals-{scene}-{results.Count}.txt"),string.Join(Environment.NewLine,intervals.Select(i=>i.ToString("0.00"))));
                    var row=new{label,scene,histogram,seconds=Math.Round(seconds,2),openMs=openMs is null?(double?)null:Math.Round(openMs.Value,1),frames=intervals.Count,
                        motionFps=motion.Count>0?Math.Round(1000/motion.Average(),1):0,p50=Math.Round(P(motion,.5),2),p95=Math.Round(P(motion,.95),2),p99=Math.Round(P(motion,.99),2),maxMs=Math.Round(intervals.Count>0?intervals.Max():0,1),
                        dropped=motion.Count(i=>i>period*1.5),hitches=motion.Count(i=>i>period*3.5),severe=intervals.Count(i=>i>50&&i<=250),
                        callbackFps=callbacks.Count>0?Math.Round(1000/callbacks.Average(),1):0,cpuCorePercent=Math.Round((process.TotalProcessorTime-cpu).TotalSeconds/seconds*100,1),
                        allocMiB=Math.Round((GC.GetTotalAllocatedBytes()-bytes)/1048576d,2),gc=$"{GC.CollectionCount(0)-g0}/{GC.CollectionCount(1)-g1}/{GC.CollectionCount(2)-g2}",
                        captures=(center?.CapturedFrames??0)-(ReferenceEquals(center,startCenter)?captured:0),uploads=(center?.UploadedFrames??0)-(ReferenceEquals(center,startCenter)?uploaded:0),renders=(center?.AnimationFrames??0)-(ReferenceEquals(center,startCenter)?animation:0),hz=Math.Round(hz,1),privateMiB=Math.Round(process.PrivateMemorySize64/1048576d,1),
                        sources=center?.CaptureSources is {Length:>0} s?s.Split(" gpu")[0].Split(", ").Length:0,uiBusyPercent=Math.Round(busy/seconds/10,1),dwmFps=Math.Round((dwmAfter.Frames-dwmBefore.Frames)/seconds,1),threads=top};
                    results.Add(row);File.WriteAllText(System.IO.Path.Combine(directory,"results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(row));
                    await Task.Delay(250);
                }
                foreach(string scene in wanted){
                    if(scene=="surfaces"){
                        // Dock-style plates (frost bleeding past the outline) over a fixed pattern, for before/after pixel comparison.
                        var probe=new Window{Left=area.Right-width+20,Top=area.Top+20,Width=560,Height=520,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Background=Brushes.DarkSlateGray};
                        var grid=new Grid();probe.Content=grid;var plates=new List<DockSurface>();
                        foreach(var (frost,intensity,row) in new[]{(0.0,1.0,0),(0.3,.6,1),(0.6,1.0,2),(1.0,.5,3)}){
                            var plate=new DockSurface{UseGlass=true,Fill=Brushes.White,GlassOpacity=1,GlassIntensity=intensity,GlassDistortion=.65,GlassFrost=frost,GlassHighlight=.6,GlassHighlightWidth=2.5,GlassTint=.06,Radius=85,Width=420,Height=84,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,30+row*120,0,0)};
                            grid.Children.Add(plate);plates.Add(plate);
                        }
                        probe.Show();await Task.Delay(300);var dpi=VisualTreeHelper.GetDpi(grid);var wo=grid.PointToScreen(new Point());int pw=(int)Math.Ceiling(grid.ActualWidth*dpi.DpiScaleX),ph=(int)Math.Ceiling(grid.ActualHeight*dpi.DpiScaleY);
                        var px=new byte[pw*ph*4];for(int y=0;y<ph;y++)for(int x=0;x<pw;x++){int i=(y*pw+x)*4;px[i]=(byte)((x/9+y/7)%2==0?30:235);px[i+1]=(byte)(y%256);px[i+2]=(byte)(x%256);px[i+3]=255;}
                        var pattern=System.Windows.Media.Imaging.BitmapSource.Create(pw,ph,96,96,PixelFormats.Bgra32,null,px,pw*4);pattern.Freeze();
                        foreach(var plate in plates){plate.BackdropScreenBounds=new Rect(wo.X,wo.Y,pw,ph);plate.Backdrop=pattern;plate.InvalidateVisual();}
                        await Task.Delay(600);var h=new WindowInteropHelper(probe).Handle;NativeMethods.GetWindowRect(h,out var r);
                        using(var shot=new System.Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top)){using(var g=System.Drawing.Graphics.FromImage(shot))g.CopyFromScreen(r.Left,r.Top,0,0,shot.Size);shot.Save(System.IO.Path.Combine(directory,"surfaces.png"),System.Drawing.Imaging.ImageFormat.Png);}
                        probe.Close();continue;
                    }
                    if(scene=="classicshot"){
                        if(center is not null){center.Dismiss();await Task.Delay(900);center=null;}
                        var classic=settings.Copy();classic.ControlCenter.MaterialMode=ControlCenterMaterialMode.ClassicBlur;
                        var w=new ControlCenterWindow(classic,connectivity:fake){DismissOnDeactivate=false};w.Show();await Task.Delay(1800);var h=new WindowInteropHelper(w).Handle;NativeMethods.GetWindowRect(h,out var r);
                        using(var shot=new System.Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top)){using(var g=System.Drawing.Graphics.FromImage(shot))g.CopyFromScreen(r.Left,r.Top,0,0,shot.Size);shot.Save(System.IO.Path.Combine(directory,"classic.png"),System.Drawing.Imaging.ImageFormat.Png);}
                        w.Dismiss();await Task.Delay(900);continue;
                    }
                    if(scene=="hittest"){
                        // Clicks on the clear strips beyond the halo reach the window underneath, as with the layered window.
                        var h=new WindowInteropHelper(center).Handle;NativeMethods.GetWindowRect(h,out var r);var page=FindWindow(null,"dock-perf-background");
                        string Owner(int x,int y){nint at=WindowFromPoint(new System.Drawing.Point(x,y));nint top=GetAncestor(at,2);return top==h?"control-center":top==page?"page":top.ToString();}
                        int midY=(r.Top+r.Bottom)/2;double s0=VisualTreeHelper.GetDpi(center).DpiScaleX;
                        Console.WriteLine($"HIT left strip(10px): {Owner(r.Left+10,midY)}; halo (40 DIP in): {Owner(r.Left+(int)(40*s0),midY)}; card (120 DIP in): {Owner(r.Left+(int)(120*s0),midY)}; bottom strip: {Owner((r.Left+r.Right)/2,r.Bottom-6)}");
                        continue;
                    }
                    if(scene=="fade"){
                        // The reveal fades the whole panel; a composed window must honour Window.Opacity like the layered one did.
                        var reveal=(DependencyProperty)typeof(ControlCenterWindow).GetField("RevealProperty",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                        var h=new WindowInteropHelper(center).Handle;
                        foreach(double value in new[]{0.35,1.0,0.0}){
                            ControlMotion.Stop(center!,reveal,value);await Task.Delay(500);NativeMethods.GetWindowRect(h,out var r);
                            using var shot=new System.Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top);using(var g=System.Drawing.Graphics.FromImage(shot))g.CopyFromScreen(r.Left,r.Top,0,0,shot.Size);
                            shot.Save(System.IO.Path.Combine(directory,$"fade-{value:0.00}.png"),System.Drawing.Imaging.ImageFormat.Png);
                        }
                        ControlMotion.Stop(center!,reveal,1);await Task.Delay(300);continue;
                    }
                    if(scene=="flash"){
                        // A new window must never show an opaque frame before WPF's first transparent one: grab the screen
                        // where the gutters are while it appears and count dark pixels (the page behind them is teal/blue).
                        for(int attempt=0;attempt<3;attempt++){
                            if(center is not null){center.Dismiss();await Task.Delay(900);center=null;}
                            var w=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};w.Show();var h=new WindowInteropHelper(w).Handle;var clock=Stopwatch.StartNew();int worst=0;double worstAt=0;
                            while(clock.ElapsedMilliseconds<600){
                                if(NativeMethods.GetWindowRect(h,out var r)&&r.Right>r.Left){
                                    using var shot=new System.Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top);using(var g=System.Drawing.Graphics.FromImage(shot))g.CopyFromScreen(r.Left,r.Top,0,0,shot.Size);
                                    int dark=0,total=0;for(int y=0;y<shot.Height;y+=4)for(int x=0;x<Math.Min(40,shot.Width);x+=2){var c=shot.GetPixel(x,y);total++;if(c.R+c.G+c.B<40)dark++;}
                                    int share=total>0?dark*100/total:0;if(share>worst){worst=share;worstAt=clock.Elapsed.TotalMilliseconds;}
                                }
                                await Task.Delay(1);
                            }
                            Console.WriteLine($"FLASH attempt {attempt}: darkest gutter frame {worst}% dark at {worstAt:0}ms");center=w;
                        }
                        continue;
                    }
                    if(scene=="blurprofile"){
                        // WPF's Gaussian BlurEffect, measured: a one-pixel white line on black, blurred at several radii.
                        foreach(double radius in new[]{1.0,2.2,4.0,8.0}){foreach(var bias in new[]{System.Windows.Media.Effects.RenderingBias.Performance,System.Windows.Media.Effects.RenderingBias.Quality}){
                            var probe=new Window{Left=area.Right-width+20,Top=area.Top+20,Width=200,Height=120,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Background=Brushes.Black};
                            double dip=1/VisualTreeHelper.GetDpi(background).DpiScaleX;var line=new Rectangle{Width=dip,Height=60,Fill=Brushes.White,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(100,30,0,0)};
                            var host=new Grid{Background=Brushes.Black,Effect=new System.Windows.Media.Effects.BlurEffect{Radius=radius,KernelType=System.Windows.Media.Effects.KernelType.Gaussian,RenderingBias=bias}};host.Children.Add(line);probe.Content=host;probe.Show();await Task.Delay(500);
                            var origin=line.PointToScreen(new Point());using var shot=new System.Drawing.Bitmap(41,1);using(var g=System.Drawing.Graphics.FromImage(shot))g.CopyFromScreen((int)Math.Round(origin.X)-20,(int)Math.Round(origin.Y)+30,0,0,shot.Size);
                            var values=new List<string>();for(int x=0;x<41;x++)values.Add(shot.GetPixel(x,0).G.ToString());
                            Console.WriteLine($"BLUR r={radius} {bias}: "+string.Join(" ",values));probe.Close();}}
                        continue;
                    }
                    if(scene is "calibrate-large" or "calibrate-large-layered"){
                        // A window the size of the control center whose whole content moves every frame.
                        var probe=new Window{Left=area.Right-width+20,Top=area.Top+20,Width=485,Height=726,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,AllowsTransparency=scene=="calibrate-large-layered",Background=scene=="calibrate-large-layered"?Brushes.Transparent:Brushes.Black};
                        var panel=new Grid{Background=new LinearGradientBrush(Color.FromArgb(200,40,90,160),Color.FromArgb(200,20,160,140),35),Margin=new Thickness(60)};
                        for(int r=0;r<14;r++)panel.Children.Add(new TextBlock{Text="控制中心 Liquid glass 液态玻璃 "+r,FontSize=14,Foreground=Brushes.White,Margin=new Thickness(12,12+r*40,0,0)});
                        var shiftY=new TranslateTransform();panel.RenderTransform=shiftY;probe.Content=panel;probe.Show();await Task.Delay(400);
                        var keep=observer;observer=new Observer(device,new WindowInteropHelper(probe).Handle);
                        await Scene(scene,async()=>{shiftY.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(-96,0,TimeSpan.FromSeconds(1)){AutoReverse=true,RepeatBehavior=new RepeatBehavior(2)});await Task.Delay(4000);});
                        observer.Dispose();observer=keep;probe.Close();continue;
                    }
                    if(scene is "calibrate" or "calibrate-layered"){
                        var probe=new Window{Left=area.Right-width+20,Top=area.Top+20,Width=420,Height=320,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,AllowsTransparency=scene=="calibrate-layered",Background=Brushes.Black};
                        var dot=new Rectangle{Width=60,Height=60,Fill=Brushes.Orange,HorizontalAlignment=HorizontalAlignment.Left};var move=new TranslateTransform();dot.RenderTransform=move;probe.Content=new Grid{Children={dot}};probe.Show();await Task.Delay(400);
                        var keep=observer;observer=new Observer(device,new WindowInteropHelper(probe).Handle);
                        await Scene(scene,async()=>{move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,340,TimeSpan.FromSeconds(1)){AutoReverse=true,RepeatBehavior=new RepeatBehavior(2)});await Task.Delay(4000);});
                        observer.Dispose();observer=keep;probe.Close();continue;
                    }
                    if(scene=="open"){
                        for(int i=0;i<3;i++){
                            if(center is not null){center.Dismiss();await Task.Delay(900);}
                            ControlCenterWindow? opened=null;double ms=0;
                            await Scene("open",async()=>{var o=await Open();opened=o.Window;ms=o.OpenMs;center=opened;await Task.Delay(900);},null);
                            // Report the latency of the entrance on the same row set.
                            Console.WriteLine($"{{\"label\":\"{label}\",\"scene\":\"open-latency\",\"openMs\":{ms:0.0}}}");results.Add(new{label,scene="open-latency",openMs=Math.Round(ms,1)});
                        }
                        continue;
                    }
                    if(center is null){var o=await Open();center=o.Window;await Task.Delay(1500);}
                    if(scene=="idle")await Scene(scene,()=>Task.Delay(3000));
                    else if(scene=="replay"){
                        // The entrance again on a window that is already open and warm: reveal from hidden to shown.
                        var reveal=(DependencyProperty)typeof(ControlCenterWindow).GetField("RevealProperty",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                        int replays=int.TryParse(Environment.GetEnvironmentVariable("DOCK_PERF_REPLAYS"),out int n)?n:3;
                        for(int i=0;i<replays;i++){
                            ControlMotion.Stop(center!,reveal,0);await Task.Delay(400);
                            await Scene(scene,async()=>{Call(center,"SettleReveal",true,null);await Task.Delay(900);});
                        }
                    }
                    else if(scene=="shot"){
                        // What the screen shows: the control center composed over the background.
                        await Task.Delay(800);var h=new WindowInteropHelper(center).Handle;NativeMethods.GetWindowRect(h,out var r);
                        using var bitmap=new System.Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top);using(var g=System.Drawing.Graphics.FromImage(bitmap))g.CopyFromScreen(r.Left,r.Top,0,0,bitmap.Size);
                        bitmap.Save(System.IO.Path.Combine(directory,$"shot-{label}.png"),System.Drawing.Imaging.ImageFormat.Png);
                        if(center.GlassBackdrop is System.Windows.Media.Imaging.BitmapSource backdropFrame){var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(backdropFrame));using var file=File.Create(System.IO.Path.Combine(directory,$"backdrop-{label}.png"));encoder.Save(file);Console.WriteLine("BACKDROP "+center.CaptureSources);}
                        Call(center,"OpenModule",ControlModule.Wifi,false);await Task.Delay(1200);
                        using var detail=new System.Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top);using(var g=System.Drawing.Graphics.FromImage(detail))g.CopyFromScreen(r.Left,r.Top,0,0,detail.Size);
                        detail.Save(System.IO.Path.Combine(directory,$"shot-{label}-detail.png"),System.Drawing.Imaging.ImageFormat.Png);
                        if(center.GlassBackdrop is System.Windows.Media.Imaging.BitmapSource detailFrame){var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(detailFrame));using var file=File.Create(System.IO.Path.Combine(directory,$"backdrop-{label}-detail.png"));encoder.Save(file);Console.WriteLine("BACKDROP detail "+center.GlassBackdropBounds+" "+center.CaptureSources);}Call(center,"HideDetails");await Task.Delay(900);
                    }
                    else if(scene=="expand-return")await Scene(scene,async()=>{for(int i=0;i<8;i++){if(i%2==0)Call(center,"OpenModule",ControlModule.Wifi,false);else Call(center,"HideDetails");await Task.Delay(520);}Call(center,"HideDetails");await Task.Delay(500);});
                    else if(scene=="rapid-reverse")await Scene(scene,async()=>{for(int i=0;i<32;i++){if(i%2==0)Call(center,"OpenModule",ControlModule.Wifi,false);else Call(center,"HideDetails");await Task.Delay(120);}Call(center,"HideDetails");await Task.Delay(500);});
                    else if(scene=="slider")await Scene(scene,async()=>{
                        // A drag at the display rate: a new value every composition frame, as a high-rate mouse would give.
                        typeof(ControlCenterWindow).GetField("_updating",F)!.SetValue(center,true);var slider=(Slider)Field(center,"_volume");var drag=Stopwatch.StartNew();
                        void Drag(object? s,EventArgs e)=>slider.Value=50+45*Math.Sin(drag.Elapsed.TotalSeconds*2.2);
                        CompositionTarget.Rendering+=Drag;await Task.Delay(4000);CompositionTarget.Rendering-=Drag;typeof(ControlCenterWindow).GetField("_updating",F)!.SetValue(center,false);});
                    else if(scene=="live-background")await Scene(scene,async()=>{
                        // The page's own update rate is the ceiling for the glass that follows it.
                        nint page=FindWindow(null,"dock-perf-background");var source=page!=0?new Observer(device,page):null;var own=new List<double>();source?.Begin(own);
                        scrollSignal.Set();await Task.Delay(4300);source?.End();source?.Dispose();
                        var moving=own.Where(i=>i<=250).ToList();Console.WriteLine($"{{\"label\":\"{label}\",\"scene\":\"live-background-source\",\"frames\":{own.Count},\"motionFps\":{(moving.Count>0?1000/moving.Average():0):0.0},\"p50\":{P(moving,.5):0.00}}}");});
                    else if(scene=="close")await Scene(scene,async()=>{center.Dismiss();await Task.Delay(1000);center=null;});
                }
                result=0;
            }catch(Exception ex){Console.WriteLine(ex);}finally{observer?.Dispose();device?.Dispose();center?.Dismiss();await Task.Delay(900);quitSignal.Set();if(!remote.WaitForExit(3000))remote.Kill();background.Close();app.Shutdown(result);}
        };app.Run(background);return result;
    }
}

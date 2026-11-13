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
using System.Windows.Media.Effects;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using Windows.Graphics.Capture;

internal static class MaterialPerfChecks
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static object Field(object o,string n)=>o.GetType().GetField(n,F)!.GetValue(o)!;
    private static object? Call(object o,string n,params object?[] args)=>o.GetType().GetMethod(n,F)!.Invoke(o,args);
    private static double P(List<double> a,double p){if(a.Count==0)return 0;var b=a.Order().ToArray();return b[Math.Min(b.Length-1,(int)(b.Length*p))];}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;
        // DOCK_PERF_DATA points at a real profile to measure its layout; otherwise an isolated default profile is used.
        var settings=new SettingsService(Environment.GetEnvironmentVariable("DOCK_PERF_DATA") is {Length:>0} profile?profile:Path.Combine(directory,"profile")).Load();settings.Animations=true;
        settings.GlassIntensity=settings.GlassDistortion=settings.GlassFrost=settings.GlassOpacity=settings.GlassHighlight=settings.GlassSideDarkness=settings.GlassGrain=1;
        var area=SystemParameters.WorkArea;var background=new Window{Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Background=new LinearGradientBrush(Colors.DarkSlateBlue,Colors.Teal,35)};
        background.Loaded+=async(_,_)=>{
            ControlCenterWindow? center=null;IDisposable? observer=null;WindowFrameCapture? device=null;
            var results=new List<object>();var gate=new object();List<double>? samples=null;double lastStamp=0;
            try{
                var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.ClassicBlur;
                center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();await Task.Delay(1800);
                // Observe compositor-produced window frames without copying them back to CPU.
                device=new WindowFrameCapture();var item=(GraphicsCaptureItem)typeof(WindowFrameCapture).GetMethod("CreateItem",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{new WindowInteropHelper(center).Handle})!;
                var type=typeof(WindowFrameCapture).GetNestedType("CaptureWindow",BindingFlags.NonPublic)!;
                observer=(IDisposable)type.GetConstructors(F).Single().Invoke(new object?[]{item,Field(device,"_winrtDevice"),null});
                var pool=(Direct3D11CaptureFramePool)Field(observer,"_pool");var frameGate=Field(observer,"Gate");
                pool.FrameArrived+=(_,_)=>{lock(frameGate){if(Field(observer,"Latest") is not Direct3D11CaptureFrame frame)return;double stamp=frame.SystemRelativeTime.TotalMilliseconds;lock(gate){if(samples is not null&&stamp>lastStamp){if(lastStamp>0)samples.Add(stamp-lastStamp);lastStamp=stamp;}}}};
                var modes=Environment.GetEnvironmentVariable("DOCK_PERF_SINGLE")=="glass"?new[]{ControlCenterMaterialMode.LiquidGlass}:Environment.GetEnvironmentVariable("DOCK_PERF_SINGLE")=="blur"?new[]{ControlCenterMaterialMode.ClassicBlur}:new[]{ControlCenterMaterialMode.ClassicBlur,ControlCenterMaterialMode.LiquidGlass,ControlCenterMaterialMode.LiquidGlass,ControlCenterMaterialMode.ClassicBlur};
                foreach(var mode in modes){
                    settings.ControlCenter.MaterialMode=mode;Call(center,"ApplyPreview",settings);
                    foreach(var surface in ((IEnumerable)Field(center,"_surfaces")).Cast<DockSurface>())((BlurEffect)Field(surface,"_frost")).RenderingBias=RenderingBias.Quality;
                    await Task.Delay(900);
                    foreach(string scene in new[]{"idle-live-background","expand-return","rapid-reverse","slider"}){
                        var intervals=new List<double>();var callbacks=new List<double>();double last=0;var timer=Stopwatch.StartNew();var process=Process.GetCurrentProcess();process.Refresh();var cpu=process.TotalProcessorTime;long bytes=GC.GetTotalAllocatedBytes();int layouts=0;
                        void Callback(object? s,EventArgs e){double now=timer.Elapsed.TotalMilliseconds;if(last>0)callbacks.Add(now-last);last=now;}
                        void Layout(object? s,EventArgs e)=>layouts++;
                        CompositionTarget.Rendering+=Callback;center.LayoutUpdated+=Layout;
                        lock(gate){samples=intervals;lastStamp=0;}
                        if(scene=="idle-live-background"){
                            var brush=(LinearGradientBrush)background.Background;var tr=new TranslateTransform();brush.RelativeTransform=tr;tr.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,.3,TimeSpan.FromSeconds(2)){AutoReverse=true});await Task.Delay(4200);tr.BeginAnimation(TranslateTransform.XProperty,null);
                        }else if(scene=="slider"){
                            typeof(ControlCenterWindow).GetField("_updating",F)!.SetValue(center,true);var slider=(Slider)Field(center,"_volume");
                            for(int i=0;i<160;i++){slider.Value=50+50*Math.Sin(i*.07);await Task.Delay(25);}typeof(ControlCenterWindow).GetField("_updating",F)!.SetValue(center,false);
                        }else{
                            for(int i=0;i<(scene=="rapid-reverse"?32:8);i++){if(i%2==0)Call(center,"OpenModule",ControlModule.Wifi,false);else Call(center,"HideDetails");await Task.Delay(scene=="rapid-reverse"?120:520);}Call(center,"HideDetails");
                        }
                        lock(gate){samples=null;}CompositionTarget.Rendering-=Callback;center.LayoutUpdated-=Layout;process.Refresh();
                        double seconds=timer.Elapsed.TotalSeconds;
                        var row=new{mode=mode.ToString(),scene,seconds,compositorFrames=intervals.Count,compositorFps=intervals.Count>0?1000/intervals.Average():0,compositorMeanMs=intervals.Count>0?intervals.Average():0,compositorP95Ms=P(intervals,.95),compositorP99Ms=P(intervals,.99),callbackFps=callbacks.Count>0?1000/callbacks.Average():0,callbackP95Ms=P(callbacks,.95),cpuMachinePercent=(process.TotalProcessorTime-cpu).TotalSeconds/seconds/Environment.ProcessorCount*100,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,allocatedMiB=(GC.GetTotalAllocatedBytes()-bytes)/1048576d,layouts,refreshHz=((FramePerformance)Field(center,"_perf")).RefreshRate,pid=process.Id};
                        results.Add(row);File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(row));
                        if(intervals.Count<5)throw new Exception("No reliable compositor frame samples; do not substitute Rendering callbacks.");await Task.Delay(300);
                    }
                }
                result=0;
            }catch(Exception ex){Console.WriteLine(ex);}finally{lock(gate){samples=null;}observer?.Dispose();device?.Dispose();center?.Dismiss();await Task.Delay(900);background.Close();app.Shutdown(result);}
        };app.Run(background);return result;
    }
}

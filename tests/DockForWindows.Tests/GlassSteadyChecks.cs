using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class GlassSteadyChecks
{
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object? Field(object owner,string name)=>owner.GetType().GetField(name,Private)!.GetValue(owner);
    private static void Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Private)!.Invoke(owner,args);
    private static double Percentile(IEnumerable<double> values,double p){var a=values.Order().ToArray();return a.Length==0?0:a[Math.Min(a.Length-1,(int)(a.Length*p))];}
    internal static int Run(int seconds,string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var settings=new SettingsService("D:/DockForWindows/data").Load();settings.Theme=DockTheme.Glass;settings.Animations=true;
        settings.GlassFrost=settings.GlassIntensity=settings.GlassDistortion=settings.GlassHighlight=settings.GlassOpacity=settings.GlassSideDarkness=settings.GlassGrain=1;
        settings.GlassHighlightWidth=8;settings.GlassTint=.25;settings.Appearance.ShadowEnabled=true;settings.Appearance.ShadowBlur=40;settings.Appearance.ShadowOpacity=.65;settings.ControlCenter.OverrideMaterial=false;settings.Normalize();
        var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
        var center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};var area=SystemParameters.WorkArea;
        var fixture=new Window{Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Background=new LinearGradientBrush(Colors.DarkSlateBlue,Colors.OrangeRed,40)};
        var frames=new List<double>();var memory=new List<object>();double last=0;var clock=Stopwatch.StartNew();TimeSpan rendered=TimeSpan.MinValue;
        void Quality(){foreach(var surface in (IEnumerable<DockSurface>)Field(center,"_surfaces")!)((BlurEffect)typeof(DockSurface).GetField("_frost",Private)!.GetValue(surface)!).RenderingBias=RenderingBias.Quality;}
        void Frame(object? sender,EventArgs args){if(args is RenderingEventArgs e){if(rendered==e.RenderingTime)return;rendered=e.RenderingTime;}double now=clock.Elapsed.TotalMilliseconds;if((double)Field(center,"_navigationStarted")!>=0){if(last>0)frames.Add(now-last);last=now;}else last=0;}
        fixture.Loaded+=async(_,_)=>{
            try{
                center.Show();Quality();await Task.Delay(2000);CompositionTarget.Rendering+=Frame;using var process=Process.GetCurrentProcess();long before=GC.GetTotalAllocatedBytes(false);var cpu=process.TotalProcessorTime;
                clock.Restart();int tick=0;
                while(clock.Elapsed.TotalSeconds<seconds){
                    if(tick%12==0){if(tick%24==0)Call(center,"OpenModule",ControlModule.Wifi,false);else Call(center,"HideDetails");Quality();}
                    if(tick%8==0){process.Refresh();memory.Add(new{seconds=clock.Elapsed.TotalSeconds,workingSetBytes=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,managedBytes=GC.GetTotalMemory(false),handles=process.HandleCount,threads=process.Threads.Count});if(tick%40==0)Console.WriteLine($"STEADY {clock.Elapsed.TotalSeconds:F0}s private={process.PrivateMemorySize64/1048576d:F1} MiB working={process.WorkingSet64/1048576d:F1} MiB managed={GC.GetTotalMemory(false)/1048576d:F1} MiB handles={process.HandleCount} captures={center.CapturedFrames}");}
                    await Task.Delay(250);tick++;
                }
                CompositionTarget.Rendering-=Frame;process.Refresh();
                var report=new{seconds=clock.Elapsed.TotalSeconds,callbackFps=1000/frames.Average(),callbackP95Ms=Percentile(frames,.95),callbackP99Ms=Percentile(frames,.99),capturedFrames=center.CapturedFrames,totalAllocatedBytes=GC.GetTotalAllocatedBytes(false)-before,cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds,peakWorkingSetBytes=process.PeakWorkingSet64,peakPrivateBytes=process.PeakPagedMemorySize64,memory,notes="Only active navigation callback intervals; full quality, no forced GC/trim; one persistent window; test connectivity only, actual system-state reads remain active."};
                File.WriteAllText(Path.Combine(directory,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine($"PASS {seconds}s highest-quality persistent window; callbacks={report.callbackFps:F2}/s P95={report.callbackP95Ms:F2}ms P99={report.callbackP99Ms:F2}ms");result=0;
            }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}
            finally{CompositionTarget.Rendering-=Frame;center.Dismiss();await Task.Delay(700);fixture.Close();app.Shutdown(result);}
        };
        app.Run(fixture);return result;
    }
}

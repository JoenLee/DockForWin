using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
internal static class SliderAnimationChecks
{
    [StructLayout(LayoutKind.Sequential)]private struct CursorPoint{public int X,Y;}
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint dx,uint dy,uint data,nuint extra);
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object? Field(object owner,string name)=>owner.GetType().GetField(name,Private)!.GetValue(owner);
    private static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject{if(root is T item)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static double Percentile(List<double> values,double fraction){var sorted=values.Order().ToArray();return sorted.Length==0?0:sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*fraction))];}
    internal static int Run(string output)
    {
        int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{
            ControlCenterWindow? center=null;GetCursorPos(out var cursor);bool pressed=false;
            var fixture=new Window{Title="Slider timing background",WindowStyle=WindowStyle.None,Left=0,Top=0,Width=SystemParameters.PrimaryScreenWidth,Height=SystemParameters.PrimaryScreenHeight,Background=Brushes.Blue};
            var intervals=new List<double>();var response=new List<double>();var clock=Stopwatch.StartNew();double last=0,pending=-1;int layouts=0;TimeSpan rendering=TimeSpan.MinValue;
            void Frame(object? sender,EventArgs args){if(args is RenderingEventArgs e){if(e.RenderingTime==rendering)return;rendering=e.RenderingTime;}double now=clock.Elapsed.TotalMilliseconds;if(last>0)intervals.Add(now-last);last=now;if(pending>=0){response.Add(now-pending);pending=-1;}}
            try{
                fixture.Show();var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                var settings=new DockSettings{Theme=DockTheme.Glass,Animations=true,GlassIntensity=1,GlassDistortion=1,GlassFrost=1,GlassHighlight=1,GlassGrain=1,GlassSideDarkness=1,GlassOpacity=1,GlassHighlightWidth=8,GlassTint=.25};settings.Appearance.ShadowEnabled=true;settings.Appearance.ShadowBlur=40;settings.Appearance.ShadowOpacity=.65;settings.ControlCenter.OverrideMaterial=false;settings.Normalize();
                center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();
                // Exercise actual WPF capture/drag without changing the user's system volume.
                typeof(ControlCenterWindow).GetField("_volumeBusy",Private)!.SetValue(center,true);
                foreach(var surface in (IEnumerable<DockSurface>)Field(center,"_surfaces")!)((BlurEffect)Field(surface,"_frost")!).RenderingBias=RenderingBias.Quality;
                await Task.Delay(1600);var slider=(Slider)Field(center,"_volume")!;var thumb=Children<Thumb>(slider).First(t=>t.IsVisible);
                var start=thumb.PointToScreen(new Point(thumb.ActualWidth/2,thumb.ActualHeight/2));var left=slider.PointToScreen(new Point());var right=slider.PointToScreen(new Point(slider.ActualWidth,slider.ActualHeight));
                center.LayoutUpdated+=(_,_)=>layouts++;CompositionTarget.Rendering+=Frame;
                SetCursorPos((int)start.X,(int)start.Y);mouse_event(2,0,0,0,0);pressed=true;await Task.Delay(50);
                if(!slider.IsMouseCaptureWithin)throw new Exception("Slider did not acquire actual mouse capture");
                double minimum=slider.Value,maximum=slider.Value;
                for(int i=0;i<60;i++){
                    pending=clock.Elapsed.TotalMilliseconds;double t=.5+.4*Math.Cos(i*Math.PI/15);SetCursorPos((int)(left.X+(right.X-left.X)*t),(int)start.Y);
                    if(i%15==0)fixture.Background=i%30==0?Brushes.Red:Brushes.Blue;
                    await Task.Delay(16);minimum=Math.Min(minimum,slider.Value);maximum=Math.Max(maximum,slider.Value);
                }
                mouse_event(4,0,0,0,0);pressed=false;await Task.Delay(150);CompositionTarget.Rendering-=Frame;
                if(maximum-minimum<30||intervals.Count<10)throw new Exception("Insufficient slider motion or callbacks");
                var report=new{scenario="actual-volume-thumb-drag",hardwareVolumeWrites=false,callbackCount=intervals.Count,callbackMeanMs=intervals.Average(),callbackP95Ms=Percentile(intervals,.95),callbackP99Ms=Percentile(intervals,.99),injectedInputToCallbackMeanMs=response.Average(),injectedInputToCallbackP95Ms=Percentile(response,.95),layouts,minimum,maximum,notes="Actual mouse capture/drag and dynamic full-quality glass; volume writer held busy in test instance. Callback timings are not presents or input-to-photon latency."};
                File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));result=0;
            }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}
            finally{CompositionTarget.Rendering-=Frame;if(pressed)mouse_event(4,0,0,0,0);center?.Dismiss();SetCursorPos(cursor.X,cursor.Y);await Task.Delay(650);fixture.Close();app.Shutdown(result);}
        };app.Run();return result;
    }
}

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
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class EntranceMotionChecks
{
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(value)!;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private sealed record Sample(double Seconds,double Opacity,double Scale,double Offset,double[] CardOpacity,double[] CardScale,double[] CardOffset);
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{
            ControlCenterWindow? center=null;var traces=new List<object>();
            var area=SystemParameters.WorkArea;var fixture=new Window{Title="Entrance motion isolated source",WindowStyle=WindowStyle.None,ShowInTaskbar=false,Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height,Background=Brushes.DarkSlateGray};
            try{
                var motion=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.ControlMotion")!;var spring=motion.GetNestedType("SpringPath",BindingFlags.NonPublic)!;
                object Path(double speed,double damping)=>Activator.CreateInstance(spring,new object[]{24d,0d,0d,speed,damping,true})!;
                double Duration(object path)=>(double)spring.GetField("Duration")!.GetValue(path)!;
                double Value(object path,double time)=>(double)spring.GetMethod("Value")!.Invoke(path,new object[]{time})!;
                var previous=Path(1.22,.90);var next=Path(.82,.82);
                Check(Duration(next)>.6&&Duration(next)<.8,"interactive spring provides a longer natural settling interval");
                Check(Value(next,.08)>0&&Value(next,Duration(next))==0,"new spring decelerates and settles at its exact target");
                fixture.Show();
                foreach(var mode in new[]{ControlCenterMaterialMode.ClassicBlur,ControlCenterMaterialMode.LiquidGlass}){
                    var settings=new DockSettings{Theme=DockTheme.Glass,Animations=true};settings.ControlCenter.MaterialMode=mode;
                    var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                    center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};
                    var samples=new List<Sample>();var clock=Stopwatch.StartNew();double started=-1;bool beforeBackdrop=false;
                    void Frame(object? sender,EventArgs args){
                        if(!Field<bool>(center,"_entranceStarted"))return;
                        if(center.GlassBackdrop is null)beforeBackdrop=true;
                        if(started<0)started=clock.Elapsed.TotalSeconds;
                        var cards=Field<IEnumerable>(center,"_modules").Cast<object>().ToArray();
                        samples.Add(new Sample(clock.Elapsed.TotalSeconds-started,center.Opacity,Field<ScaleTransform>(center,"_scale").ScaleX,Field<TranslateTransform>(center,"_entrance").Y,cards.Select(c=>Field<Grid>(c,"Root").Opacity).ToArray(),cards.Select(c=>Field<ScaleTransform>(c,"Scale").ScaleX).ToArray(),cards.Select(c=>Field<TranslateTransform>(c,"Enter").Y).ToArray()));
                    }
                    CompositionTarget.Rendering+=Frame;center.Show();
                    try{for(int i=0;i<160&&(started<0||clock.Elapsed.TotalSeconds-started<.75);i++)await Task.Delay(20);}
                    finally{CompositionTarget.Rendering-=Frame;}
                    Check(!beforeBackdrop&&started>=0&&samples.Count>=4,mode+": entrance starts with a real background sample");
                    Check(samples.All(s=>s.CardOpacity.All(o=>Math.Abs(o-1)<.000001)),mode+": one window fade; no staggered card alpha pulses");
                    var active=samples.Where(s=>s.Seconds<.5).ToArray();
                    Check(active.All(s=>s.Scale>=.984&&s.CardScale.All(v=>v>=.989)),mode+": subtle scale keeps slide as the dominant motion");
                    Check(samples.Zip(samples.Skip(1),(a,b)=>b.Opacity-a.Opacity).All(delta=>delta>-.002),mode+": entrance opacity never visibly reverses");
                    Check(center.Opacity>.999&&Math.Abs(Field<TranslateTransform>(center,"_entrance").Y)<.01&&Math.Abs(Field<ScaleTransform>(center,"_scale").ScaleX-1)<.0001,mode+": slide/scale/alpha settle cleanly");
                    traces.Add(new{mode=mode.ToString(),samples});center.Dismiss();await Task.Delay(650);Check(!center.IsVisible,mode+": normal dismiss closes cleanly");
                    // Close while the entrance is still running: the same properties must hand off without resets.
                    fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                    center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();
                    for(int i=0;i<100&&!Field<bool>(center,"_entranceStarted");i++)await Task.Delay(10);
                    await Task.Delay(45);center.Dismiss();for(int i=0;i<100&&center.IsVisible;i++)await Task.Delay(15);
                    Check(!center.IsVisible,mode+": immediate close interrupts the entrance without a stuck window");
                }
                var instant=new DockSettings{Animations=false};instant.ControlCenter.MaterialMode=ControlCenterMaterialMode.ClassicBlur;
                var noWrite=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                center=new ControlCenterWindow(instant,connectivity:noWrite){DismissOnDeactivate=false};center.Show();for(int i=0;i<100&&!Field<bool>(center,"_entranceStarted");i++)await Task.Delay(15);
                Check(center.Opacity==1&&Field<TranslateTransform>(center,"_entrance").Y==0,"animation-disabled setting still shows immediately");center.Dismiss();
                File.WriteAllText(System.IO.Path.Combine(directory,"entrance-traces.json"),JsonSerializer.Serialize(new{oldDuration=Duration(previous),newDuration=Duration(next),note="WPF animated-property traces, not actual screen FPS or input-to-photon measurements",traces},new JsonSerializerOptions{WriteIndented=true}));
                result=0;
            }catch(Exception error){Console.Error.WriteLine(error);}
            finally{center?.Dismiss();await Task.Delay(650);fixture.Close();app.Shutdown(result);}
        };app.Run();return result;
    }
}

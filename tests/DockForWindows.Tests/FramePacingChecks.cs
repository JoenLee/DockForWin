using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DockForWindows.Views;

internal static class FramePacingChecks
{
    internal static int Run(string path)
    {
        int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var lines=new List<string>();
        app.Startup+=async(_,_)=>{
            try{
                foreach(bool transparent in new[]{false,true}){
                    var moving=new Border{Width=80,Height=80,Background=Brushes.CornflowerBlue,RenderTransform=new TranslateTransform()};
                    var window=new Window{Width=400,Height=400,WindowStyle=WindowStyle.None,AllowsTransparency=transparent,Background=transparent?Brushes.Transparent:Brushes.White,Content=moving,Topmost=true,ShowInTaskbar=false};window.Show();
                    var perf=new FramePerformance();perf.Detect(new WindowInteropHelper(window).Handle);
                    var animation=new DoubleAnimation(0,100,TimeSpan.FromSeconds(.7)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever};Timeline.SetDesiredFrameRate(animation,(int)Math.Ceiling(perf.RefreshRate));((TranslateTransform)moving.RenderTransform).BeginAnimation(TranslateTransform.XProperty,animation);
                    await Task.Delay(750);var clock=Stopwatch.StartNew();var times=new List<double>();double last=0;
                    void Frame(object? s,EventArgs e){double now=clock.Elapsed.TotalMilliseconds;if(last>0)times.Add(now-last);last=now;}
                    CompositionTarget.Rendering+=Frame;await Task.Delay(2500);CompositionTarget.Rendering-=Frame;
                    var sorted=times.Order().ToArray();string line=$"transparent={transparent}; display={perf.RefreshRate:F2}Hz; callbacks={1000/times.Average():F2}/s; P95={sorted[(int)(sorted.Length*.95)]:F2}ms; P99={sorted[(int)(sorted.Length*.99)]:F2}ms";lines.Add(line);Console.WriteLine(line);window.Close();await Task.Delay(200);
                }
                File.WriteAllLines(path,lines);result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}finally{app.Shutdown(result);}
        };app.Run();return result;
    }
}

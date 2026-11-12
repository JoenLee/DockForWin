using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DockForWindows.Services;

internal static class CaptureLifetimeChecks
{
    public static int Run()
    {
        var app=new Application();var area=SystemParameters.WorkArea;
        var window=new Window{Title="Capture lifetime probe",Left=area.Right-360,Top=area.Top+20,Width=340,Height=520,Background=Brushes.RoyalBlue,Topmost=true,ShowInTaskbar=false};
        window.Loaded+=async(_,_)=>
        {
            using var capture=new GlassBackdropService();var handle=new WindowInteropHelper(window).Handle;var point=window.PointToScreen(new Point());var far=window.PointToScreen(new Point(340,520));var bounds=System.Drawing.Rectangle.FromLTRB((int)point.X,(int)point.Y,(int)far.X,(int)far.Y);var watch=Stopwatch.StartNew();var process=Process.GetCurrentProcess();int frames=0,last=-1;
            while(watch.Elapsed.TotalSeconds<65)
            {
                var bitmap=await Task.Run(()=>capture.Render(handle,bounds,50,1,VisualTreeDpi,raw:true));frames++;int tick=(int)watch.Elapsed.TotalSeconds;
                if(tick/10!=last){last=tick/10;process.Refresh();Console.WriteLine($"LIFETIME {tick}s frames={frames} privateMB={process.PrivateMemorySize64/1048576d:0.0} managedMB={GC.GetTotalMemory(false)/1048576d:0.0}");}
                if(tick>=30&&tick<31){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
                await Task.Delay(5);
            }
            window.Close();
        };VisualTreeDpi=1.6;app.Run(window);return 0;
    }
    private static double VisualTreeDpi;
}

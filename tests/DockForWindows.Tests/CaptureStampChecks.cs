using System;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DockForWindows.Services;
using Brushes=System.Windows.Media.Brushes;
internal static class CaptureStampChecks
{
    internal static int Run()
    {
        int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{
            var window=new Window{Title="WGC exact frame identity check",Width=320,Height=180,Left=120,Top=120,WindowStyle=WindowStyle.None,Topmost=true,Background=Brushes.Blue};
            try{
                window.Show();await Task.Delay(200);
                using var capture=new WindowFrameCapture();var handle=new WindowInteropHelper(window).Handle;
                var read=typeof(WindowFrameCapture).GetMethod("Read",BindingFlags.Instance|BindingFlags.NonPublic)!;
                var sample=new Rectangle(-32768,-32768,65536,65536);object? stamp=null;
                (Bitmap? Image,bool Same) Read(Rectangle region){object?[] args={handle,region,Rectangle.Empty,stamp,null,false};var image=(Bitmap?)read.Invoke(capture,args);stamp=args[4];return (image,(bool)args[5]!);}
                for(int i=0;i<200&&stamp is null;i++){using var image=Read(sample).Image;await Task.Delay(5);}
                if(stamp is null)throw new Exception("No timestamped WGC frame");
                bool reused=false;
                for(int i=0;i<30&&!reused;i++){long copies=capture.Copies;var frame=Read(sample);using var image=frame.Image;reused=frame.Same;if(reused&&(image is not null||capture.Copies!=copies))throw new Exception("Identical frame performed a GPU readback");}
                if(!reused)throw new Exception("No identical compositor frame observed");
                var p=window.PointToScreen(new System.Windows.Point(20,20));var roi=new Rectangle((int)p.X,(int)p.Y,40,40);
                var cropped=Read(roi);using(var image=cropped.Image){if(cropped.Same||image is null||image.Width!=40||image.Height!=40)throw new Exception("Changed ROI incorrectly reused old pixels");}
                using(var image=Read(sample).Image){}
                window.Left+=24;await Task.Delay(100);var moved=Read(sample);using(var image=moved.Image){if(moved.Same||image is null)throw new Exception("Moved window reused a previous mapping");}
                window.Background=Brushes.Red;bool red=false;
                var watch=System.Diagnostics.Stopwatch.StartNew();
                while(watch.ElapsedMilliseconds<2000&&!red){await Task.Delay(5);var frame=Read(sample);using var image=frame.Image;if(image is not null){var color=image.GetPixel(image.Width/2,image.Height/2);red=color.R>200&&color.B<40;}}
                if(!red)throw new Exception("New source frame was hidden by the reuse check");
                Console.WriteLine($"PASS identical frame avoids readback; ROI, window movement and new red frame invalidate immediately; observed update {watch.Elapsed.TotalMilliseconds:F2}ms; copies={capture.Copies}");result=0;
            }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}
            finally{window.Close();app.Shutdown(result);}
        };app.Run();return result;
    }
}

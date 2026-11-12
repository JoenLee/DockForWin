using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
internal static class GlassResponseChecks
{
    private static void Quality(ControlCenterWindow center)
    {
        var surfaces=(System.Collections.Generic.List<DockSurface>)typeof(ControlCenterWindow).GetField("_surfaces",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(center)!;
        foreach(var surface in surfaces)((System.Windows.Media.Effects.BlurEffect)typeof(DockSurface).GetField("_frost",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(surface)!).RenderingBias=System.Windows.Media.Effects.RenderingBias.Quality;
    }
    internal static int Run()
    {
        int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{
            ControlCenterWindow? center=null;
            var fixture=new Window{Title="Glass response colour source",WindowStyle=WindowStyle.None,Left=0,Top=0,Width=SystemParameters.PrimaryScreenWidth,Height=SystemParameters.PrimaryScreenHeight,Background=Brushes.Blue};
            try{
                fixture.Show();
                var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                var settings=new DockSettings{Theme=DockTheme.Glass,Animations=true,GlassIntensity=1,GlassDistortion=1,GlassFrost=1,GlassHighlight=1,GlassGrain=1,GlassSideDarkness=1,GlassOpacity=1,GlassHighlightWidth=8,GlassTint=.25};settings.Appearance.ShadowEnabled=true;settings.Appearance.ShadowBlur=40;settings.Appearance.ShadowOpacity=.65;settings.ControlCenter.OverrideMaterial=false;settings.Normalize();
                center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();
                Quality(center);
                await Task.Delay(1400);
                for(int n=0;n<12;n++){
                    bool red=n%2==0;fixture.Background=red?Brushes.Red:Brushes.Blue;
                    if(n>=4&&n<8)typeof(ControlCenterWindow).GetMethod(n%2==0?"OpenModule":"HideDetails",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(center,n%2==0?new object[]{ControlModule.Wifi,false}:null);
                    if(n>=8)center.Left+=red?16:-16;
                    Quality(center);
                    var clock=System.Diagnostics.Stopwatch.StartNew();double upload=-1,screen=-1;
                    while(clock.ElapsedMilliseconds<1500&&(upload<0||screen<0)){
                        await Task.Delay(5);
                        if(center.GlassBackdrop is BitmapSource frame){var pixel=new byte[4];frame.CopyPixels(new Int32Rect(frame.PixelWidth/2,frame.PixelHeight/2,1,1),pixel,4,0);if(upload<0&&(red?pixel[2]>pixel[0]+80:pixel[0]>pixel[2]+80))upload=clock.Elapsed.TotalMilliseconds;}
                        var surfaces=(System.Collections.Generic.List<DockSurface>)typeof(ControlCenterWindow).GetField("_surfaces",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(center)!;
                        var surface=surfaces.First(s=>s.IsVisible&&s.Opacity>.5&&s.ActualWidth>80&&s.ActualHeight>40);
                        var p=surface.PointToScreen(new Point(surface.ActualWidth*.4,surface.ActualHeight*.6));using var image=new System.Drawing.Bitmap(1,1);using(var g=System.Drawing.Graphics.FromImage(image))g.CopyFromScreen((int)p.X,(int)p.Y,0,0,image.Size);
                        var color=image.GetPixel(0,0);if(screen<0&&(red?color.R>color.B+20:color.B>color.R+20))screen=clock.Elapsed.TotalMilliseconds;
                    }
                    Console.WriteLine($"response phase={n} scenario={(n<4?"static":n<8?"navigation":"window-move")} upload_ms={upload:F2} screen_ms={screen:F2}");
                    if(upload<0||screen<0)throw new Exception("Background colour failed to reach live glass");
                    await Task.Delay(180);
                }result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{center?.Dismiss();await Task.Delay(650);fixture.Close();app.Shutdown(result);}
        };app.Run();return result;
    }
}


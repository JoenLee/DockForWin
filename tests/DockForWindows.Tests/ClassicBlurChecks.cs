using System;
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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class ClassicBlurChecks
{
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    private static object? Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,args);
    private static IEnumerable<T> Tree<T>(DependencyObject value)where T:DependencyObject
    {if(value is T item)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(value);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(value,i)))yield return child;}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static double Percentile(List<double> values,double percentile){var sorted=values.Order().ToArray();return sorted[Math.Min(sorted.Length-1,(int)Math.Ceiling(sorted.Length*percentile)-1)];}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{
            ControlCenterWindow? center=null;
            var area=SystemParameters.WorkArea;
            var source=new Window{Title="Classic blur isolated colour source",WindowStyle=WindowStyle.None,ShowInTaskbar=false,Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height,Background=Brushes.Blue};
            try{
                var settings=new DockSettings{Theme=DockTheme.Glass,Animations=true,GlassIntensity=1,GlassDistortion=1,GlassFrost=1,GlassOpacity=1,GlassHighlight=1,GlassSideDarkness=1,GlassGrain=1,GlassHighlightWidth=8,GlassTint=.25};
                settings.Appearance.ShadowEnabled=true;settings.Appearance.ShadowBlur=40;settings.Appearance.ShadowOpacity=.65;
                settings.ControlCenter.Modules.Single(m=>m.Module==ControlModule.Wifi).Visible=true;
                Check(settings.ControlCenter.MaterialMode==ControlCenterMaterialMode.LiquidGlass,"legacy/default configuration keeps Liquid Glass");
                settings.ControlCenter.MaterialMode=(ControlCenterMaterialMode)999;settings.Normalize();Check(settings.ControlCenter.MaterialMode==ControlCenterMaterialMode.LiquidGlass,"unknown mode normalizes safely");
                settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.ClassicBlur;
                var store=new SettingsService(Path.Combine(directory,"isolated-data"));store.Save(settings);Check(store.Load().ControlCenter.MaterialMode==ControlCenterMaterialMode.ClassicBlur,"material persists through SettingsService");
                var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                source.Show();center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();await Task.Delay(1600);
                var capture=Field<object>(center,"_capture");var surfaces=Field<List<DockSurface>>(center,"_surfaces");
                Check(surfaces.Count>0&&surfaces.All(s=>Field<bool>(s,"<ClassicMaterial>k__BackingField")),"all cards use flat classic material");
                Check(surfaces.All(s=>s.Backdrop is null),"cards do not bind or blur individual background images");
                var blurs=Tree<FrameworkElement>(center).Where(e=>e.IsVisible&&e.Effect is BlurEffect).ToArray();
                Console.WriteLine("visible blurs="+blurs.Length+" "+string.Join(",",blurs.Select(e=>e.GetType().Name+":"+e.Tag)));
                Check(blurs.Length==2&&((BlurEffect)blurs[0].Effect).KernelType==KernelType.Gaussian,"two shared Gaussian material layers");
                foreach(var element in Tree<FrameworkElement>(center).Where(e=>e.IsVisible&&e.Effect is LiquidGlassEffect or DropShadowEffect))Console.WriteLine("remaining effect="+element.GetType().Name+" "+element.Effect.GetType().Name+" parent="+VisualTreeHelper.GetParent(element).GetType().Name);
                Check(!Tree<FrameworkElement>(center).Any(e=>e.IsVisible&&e.Effect is LiquidGlassEffect or DropShadowEffect),"classic has no visible optical Shader or material shadow");
                async Task<object> Response(bool red,int phase)
                {
                    source.Background=red?Brushes.Red:Brushes.Blue;var clock=Stopwatch.StartNew();double upload=-1,screen=-1;
                    while(clock.ElapsedMilliseconds<2200&&(upload<0||screen<0)){
                        await Task.Delay(5);
                        if(center.GlassBackdrop is {} frame){var pixel=new byte[4];frame.CopyPixels(new Int32Rect(frame.PixelWidth/2,frame.PixelHeight/2,1,1),pixel,4,0);if(upload<0&&(red?pixel[2]>pixel[0]+70:pixel[0]>pixel[2]+70))upload=clock.Elapsed.TotalMilliseconds;}
                        var surface=surfaces.First(s=>s.IsVisible&&s.Opacity>.5&&s.ActualWidth>80&&s.ActualHeight>40);
                        var point=surface.PointToScreen(new Point(surface.ActualWidth*.16,surface.ActualHeight*.75));
                        using var image=new System.Drawing.Bitmap(1,1);using(var graphics=System.Drawing.Graphics.FromImage(image))graphics.CopyFromScreen((int)point.X,(int)point.Y,0,0,image.Size);
                        var color=image.GetPixel(0,0);if(screen<0&&(red?color.R>color.B+15:color.B>color.R+15))screen=clock.Elapsed.TotalMilliseconds;
                    }
                    Check(upload>=0&&screen>=0,"fresh background reaches upload and screen, phase "+phase);
                    return new{phase,uploadMs=upload,screenResponseMs=screen};
                }
                var responses=new List<object>();
                for(int i=0;i<6;i++){
                    if(i==2)Call(center,"OpenModule",ControlModule.Wifi,false);
                    if(i==3)Call(center,"HideDetails");
                    if(i>=4)center.Left+=i%2==0?-18:18;
                    responses.Add(await Response(i%2==0,i));await Task.Delay(220);
                }
                var volume=Field<Slider>(center,"_volume");typeof(ControlCenterWindow).GetField("_updating",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(center,true);
                volume.Value=37;typeof(ControlCenterWindow).GetField("_updating",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(center,false);
                Check(volume.Value==37,"existing slider retained, test suppresses hardware writes");
                Check(((Grid)center.Content).OpacityMask is null,"classic has no inherited edge fade over flat cards");
                var root=Field<Grid>(center,"_root");var background=Field<FrameworkElement>(center,"_classicBackdrop");
                var expected=background.PointToScreen(new Point());var imageRect=Field<Rect>(background,"_imageRect");var origin=background.PointToScreen(imageRect.TopLeft);
                Check(Math.Abs(origin.X-center.GlassBackdropBounds.X)<2&&Math.Abs(origin.Y-center.GlassBackdropBounds.Y)<2,"blur mapping preserves physical capture origin after movement");
                void Screenshot(string name){var p=center.PointToScreen(new Point());var far=center.PointToScreen(new Point(center.ActualWidth,center.ActualHeight));using var image=new System.Drawing.Bitmap((int)(far.X-p.X),(int)(far.Y-p.Y));using(var g=System.Drawing.Graphics.FromImage(image))g.CopyFromScreen((int)p.X,(int)p.Y,0,0,image.Size);image.Save(Path.Combine(directory,name));}
                source.Background=new LinearGradientBrush(Colors.SlateBlue,Colors.Silver,35);await Task.Delay(600);Screenshot("classic-blur.png");
                var measurements=new List<object>();
                async Task Measure(ControlCenterMaterialMode mode,int round)
                {
                    settings.ControlCenter.MaterialMode=mode;Call(center,"ApplyPreview",settings);
                    foreach(var surface in surfaces)Field<BlurEffect>(surface,"_frost").RenderingBias=RenderingBias.Quality;
                    await Task.Delay(650);
                    var process=Process.GetCurrentProcess();process.Refresh();var cpu=process.TotalProcessorTime;var start=Stopwatch.StartNew();var intervals=new List<double>();double last=0;
                    void Frame(object? sender,EventArgs args){double now=start.Elapsed.TotalMilliseconds;if(last>0)intervals.Add(now-last);last=now;}
                    CompositionTarget.Rendering+=Frame;
                    try{for(int n=0;n<5;n++){Call(center,"OpenModule",ControlModule.Wifi,false);await Task.Delay(380);Call(center,"HideDetails");await Task.Delay(380);}for(int n=0;n<8;n++){if(n%2==0)Call(center,"OpenModule",ControlModule.Wifi,false);else Call(center,"HideDetails");await Task.Delay(65);}await Task.Delay(500);}
                    finally{CompositionTarget.Rendering-=Frame;}
                    process.Refresh();measurements.Add(new{mode=mode.ToString(),round,durationMs=start.Elapsed.TotalMilliseconds,callbackMeanMs=intervals.Average(),callbackP95Ms=Percentile(intervals,.95),callbackP99Ms=Percentile(intervals,.99),cpuCorePercent=(process.TotalProcessorTime-cpu).TotalMilliseconds/start.Elapsed.TotalMilliseconds*100,workingSetMiB=process.WorkingSet64/1048576d,privateBytesMiB=process.PrivateMemorySize64/1048576d,actualPresentFps=(double?)null,gpuFrameMs=(double?)null});
                }
                await Measure(ControlCenterMaterialMode.LiquidGlass,1);Screenshot("liquid-glass.png");await Measure(ControlCenterMaterialMode.ClassicBlur,1);
                await Measure(ControlCenterMaterialMode.ClassicBlur,2);await Measure(ControlCenterMaterialMode.LiquidGlass,2);
                for(int n=0;n<16;n++){settings.ControlCenter.MaterialMode=n%2==0?ControlCenterMaterialMode.LiquidGlass:ControlCenterMaterialMode.ClassicBlur;Call(center,"ApplyPreview",settings);await Task.Delay(30);}
                Check(ReferenceEquals(capture,Field<object>(center,"_capture")),"rapid switching retains the same capture service");
                Check(((Grid)center.Content).Children.Cast<object>().Count(o=>o.GetType().Name=="ClassicBlurBackdrop")==2,"rapid switching retains only the two shared blur layers");
                Check(Tree<FrameworkElement>(center).Count(e=>e.IsVisible&&e.Effect is BlurEffect)==2,"two shared Gaussian layers remain after reverse/switch stress");
                settings.ControlCenter.Geometry.PanelScale=1.25;settings.ControlCenter.Geometry.ComponentScale=1.15;settings.ControlCenter.ColorMode=ControlCenterColorMode.Light;
                settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.ClassicBlur;Call(center,"ApplyPreview",settings);await Task.Delay(550);
                responses.Add(await Response(true,6));await Task.Delay(180);Screenshot("classic-scaled-light.png");
                var scaledOrigin=background.PointToScreen(Field<Rect>(background,"_imageRect").TopLeft);
                Check(Math.Abs(scaledOrigin.X-center.GlassBackdropBounds.X)<2&&Math.Abs(scaledOrigin.Y-center.GlassBackdropBounds.Y)<2,"125% panel and 115% controls retain correct live sample mapping");
                settings.ControlCenter.Geometry.PanelScale=1;settings.ControlCenter.Geometry.ComponentScale=1;settings.ControlCenter.ColorMode=ControlCenterColorMode.Dark;Call(center,"ApplyPreview",settings);await Task.Delay(400);Screenshot("classic-dark.png");
                settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.LiquidGlass;Call(center,"ApplyPreview",settings);await Task.Delay(180);
                Check(background.Visibility==Visibility.Collapsed&&Field<Image>(background,"_image").Source is null,"leaving classic releases its background binding");
                Check(surfaces.All(s=>!Field<bool>(s,"<ClassicMaterial>k__BackingField"))&&Tree<FrameworkElement>(center).Any(e=>e.IsVisible&&e.Effect is LiquidGlassEffect),"Liquid Glass restores live Shader branch");
                var dockSurface=new DockSurface{UseGlass=true};Check(!Field<bool>(dockSurface,"<ClassicMaterial>k__BackingField"),"ordinary Dock surfaces keep original material defaults");
                File.WriteAllText(Path.Combine(directory,"results.json"),JsonSerializer.Serialize(new{refreshRate=Field<FramePerformance>(center,"_perf").RefreshRate,dpi=VisualTreeHelper.GetDpi(center).DpiScaleX,measurements,responses,note="Rendering callback intervals are diagnostic only, NOT actual present FPS. CPU core percent is not normalized by logical processor count. Screen response includes source rendering, WGC, blur, DWM and polling; not input-to-photon. No GPU/actual-present measurement available."},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("PASS comparison saved; actual present FPS and GPU frame time remain unmeasured");result=0;
            }catch(Exception error){Console.Error.WriteLine(error);}
            finally{center?.Dismiss();await Task.Delay(650);source.Close();app.Shutdown(result);}
        };app.Run();return result;
    }
}

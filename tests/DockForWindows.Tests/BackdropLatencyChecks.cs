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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
internal static class BackdropLatencyChecks
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static object Field(object o,string n)=>o.GetType().GetField(n,F)!.GetValue(o)!;
    internal static int Run(string path){var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;var area=SystemParameters.WorkArea;
        var source=new Window{Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Background=Brushes.Blue};ControlCenterWindow? center=null;
        source.Loaded+=async(_,_)=>{try{
            var settings=new SettingsService("D:/DockForWindows/data").Load();settings.GlassFrost=settings.GlassIntensity=settings.GlassDistortion=settings.GlassOpacity=settings.GlassHighlight=1;settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.LiquidGlass;
            var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
            center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();await Task.Delay(2000);
            if(Environment.GetEnvironmentVariable("DOCK_RESOURCE_ONLY")=="1"){
                var effects=new HashSet<System.Windows.Media.Effects.Effect>();int surfaces=0;
                void Visit(DependencyObject node){if(node is DockSurface)surfaces++;if(node is UIElement element&&element.Effect is {} e)effects.Add(e);
                    foreach(var f in node.GetType().GetFields(F))if(typeof(System.Windows.Media.Effects.Effect).IsAssignableFrom(f.FieldType)&&f.GetValue(node) is System.Windows.Media.Effects.Effect effect)effects.Add(effect);
                    for(int n=0;n<VisualTreeHelper.GetChildrenCount(node);n++)Visit(VisualTreeHelper.GetChild(node,n));}
                Visit(center);var start=center.PointToScreen(new Point());var end=center.PointToScreen(new Point(center.ActualWidth,center.ActualHeight));var areaRect=System.Drawing.Rectangle.Intersect(new System.Drawing.Rectangle((int)start.X,(int)start.Y,(int)(end.X-start.X),(int)(end.Y-start.Y)),System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(center).Handle).Bounds);using(var shot=new System.Drawing.Bitmap(areaRect.Width,areaRect.Height)){using(var g=System.Drawing.Graphics.FromImage(shot))g.CopyFromScreen(areaRect.Location,System.Drawing.Point.Empty,areaRect.Size);shot.Save(Path.ChangeExtension(path,"png"));}var info=new{surfaces,effects=effects.GroupBy(e=>e.GetType().Name).ToDictionary(g=>g.Key,g=>g.Count())};File.WriteAllText(path,JsonSerializer.Serialize(info));Console.WriteLine(JsonSerializer.Serialize(info));result=0;return;
            }
            var card=((IEnumerable)Field(center,"_modules")).Cast<object>().First(c=>(ControlModule)Field(c,"Kind")==ControlModule.Brightness);var plate=(DockSurface)Field(card,"Surface");var p=plate.PointToScreen(new Point(plate.ActualWidth*.85,plate.ActualHeight*.25));
            var rows=new List<object>();using var screen=new System.Drawing.Bitmap(3,3);using var graphics=System.Drawing.Graphics.FromImage(screen);
            foreach(bool red in new[]{true,false,true,false,true,false,true,false}){
                await Task.Delay(200);var clock=Stopwatch.StartNew();source.Background=red?Brushes.Red:Brushes.Blue;double uploaded=-1,visible=-1;
                while(clock.ElapsedMilliseconds<1800){
                    if(uploaded<0&&center.GlassBackdrop is {} frame){int x=(int)(p.X-center.GlassBackdropBounds.X),y=(int)(p.Y-center.GlassBackdropBounds.Y);var pixel=new byte[4];frame.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);if(red?pixel[2]>pixel[0]+60:pixel[0]>pixel[2]+60)uploaded=clock.Elapsed.TotalMilliseconds;}
                    if(visible<0){graphics.CopyFromScreen((int)p.X-1,(int)p.Y-1,0,0,screen.Size);var pixel=screen.GetPixel(1,1);if(red?pixel.R>pixel.B+40:pixel.B>pixel.R+40)visible=clock.Elapsed.TotalMilliseconds;}
                    if(uploaded>=0&&visible>=0)break;await Task.Delay(2);
                }
                rows.Add(new{red,uploadMs=uploaded,visibleMs=visible,uploadToVisibleMs=visible>=0&&uploaded>=0?visible-uploaded:-1});if(uploaded<0||visible<0)throw new Exception("Background freshness timed out; do not report a synthetic delay.");
            }
            File.WriteAllText(path,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(rows));Console.WriteLine("PASS desktop color changes reach both the live bitmap and visible glass card; times include source WPF presentation and polling overhead, not physical photon latency.");result=0;
        }catch(Exception e){Console.WriteLine(e);}finally{center?.Dismiss();await Task.Delay(1000);source.Close();app.Shutdown(result);}};app.Run(source);return result;}
}

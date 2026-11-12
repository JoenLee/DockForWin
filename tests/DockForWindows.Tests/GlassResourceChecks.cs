using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class GlassResourceChecks
{
    private static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject
    {if(root is T item)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{
            ControlCenterWindow? center=null;
            try{
                // Existing screen-pixel checks cover the standalone fallback used by Dock settings.
                await (Task)typeof(FourThemeChecks).GetMethod("CheckButtonOptics",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{directory})!;
                var paletteType=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.Palette")!;
                var create=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.IosStyles")!.GetMethod("Create",BindingFlags.Static|BindingFlags.Public)!;
                object Palette(DockSettings settings)=>Activator.CreateInstance(paletteType,new object[]{settings})!;
                var options=new DockSettings{InterfaceTheme=InterfaceTheme.Light,InterfaceStyle=InterfaceStyle.Mono};var palette=Palette(options);
                var firstStyles=create.Invoke(null,new[]{palette});var sameStyles=create.Invoke(null,new[]{Palette(options.Copy())});Assert(ReferenceEquals(firstStyles,sameStyles),"Same appearance reparsed the style dictionary");
                long styleBefore=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++)create.Invoke(null,new[]{palette});long styleAllocated=GC.GetAllocatedBytesForCurrentThread()-styleBefore;
                Assert(styleAllocated<65536,"Repeated style access allocated a new dictionary");options.InterfaceStyle=InterfaceStyle.Warm;Assert(!ReferenceEquals(firstStyles,create.Invoke(null,new[]{Palette(options)})),"Appearance change reused the wrong styles");
                Console.WriteLine($"PASS bounded style reuse for the same appearance; theme change invalidates it; 200 accesses allocated {styleAllocated} bytes");
                var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                center=new ControlCenterWindow(new DockSettings(),connectivity:fake){DismissOnDeactivate=false};center.Show();await Task.Delay(1000);
                var chrome=Tree<GlassButtonChrome>(center).First(c=>c.IsLoaded&&c.IsVisible);
                var effects=Tree<FrameworkElement>(chrome).Where(e=>e.Effect is LiquidGlassEffect).ToArray();Assert(effects.Length==2,"Missing glass passes");
                var lens=effects.Single(e=>((LiquidGlassEffect)e.Effect).LightingOnly==0);var lighting=effects.Single(e=>((LiquidGlassEffect)e.Effect).LightingOnly==1);
                var lensEffect=lens.Effect;var lightEffect=lighting.Effect;var clip=lens.Clip;var input=((LiquidGlassEffect)lensEffect).Input;
                for(int i=0;i<200;i++){chrome.Interaction=(i%4)*.1;typeof(GlassButtonChrome).GetMethod("RefreshMaterial",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(chrome,null);}
                Assert(ReferenceEquals(lens.Effect,lensEffect)&&ReferenceEquals(lighting.Effect,lightEffect),"Refresh recreated shader effects");
                Assert(ReferenceEquals(lens.Clip,clip)&&ReferenceEquals(((LiquidGlassEffect)lensEffect).Input,input),"Refresh recreated stable geometry or input brush");
                Assert(input is ImageBrush brush&&ReferenceEquals(brush.ImageSource,center.GlassBackdrop),"Live host frame was not retained");
                Assert(Math.Abs(((LiquidGlassEffect)lensEffect).Lighting.X-(.95+chrome.Interaction))<1e-10,"Interaction lighting stopped updating");
                Console.WriteLine("PASS live host material retains both shaders, input brush and stable outline through 200 refreshes");
                var effect=new LiquidGlassEffect{SourceSize=new Point(400,300),PlateOrigin=new Point(12,24),PlateSize=new Point(120,60)};
                for(int i=0;i<20;i++){effect.SourceSize=new Point(400,300);effect.PlateOrigin=new Point(12,24);effect.PlateSize=new Point(120,60);}
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<10000;i++){effect.SourceSize=new Point(400,300);effect.PlateOrigin=new Point(12,24);effect.PlateSize=new Point(120,60);}
                long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert(allocated<65536,$"Redundant shader writes allocated {allocated} bytes");
                effect.PlateOrigin=new Point(13,25);Assert(effect.PlateOrigin==new Point(13,25),"Shader constants stopped updating");
                Console.WriteLine($"PASS unchanged shader constants avoid dependency updates; 30,000 writes allocated {allocated} bytes");result=0;
            }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}
            finally{center?.Dismiss();await Task.Delay(650);app.Shutdown(result);}
        };
        app.Run();return result;
    }
}

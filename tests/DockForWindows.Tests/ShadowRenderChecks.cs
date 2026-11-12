using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;
internal static class ShadowRenderChecks
{
    private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static object Field(object o,string n)=>o.GetType().GetField(n,F)!.GetValue(o)!;
    private static object? Call(object o,string n,params object?[] a)=>o.GetType().GetMethod(n,F)!.Invoke(o,a);
    private static IEnumerable<T> Tree<T>(DependencyObject o)where T:DependencyObject{if(o is T found)yield return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var x in Tree<T>(VisualTreeHelper.GetChild(o,i)))yield return x;}
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static void NoShadows(ControlCenterWindow c,string name)=>Check(!Tree<UIElement>(c).Any(e=>e.Effect is DropShadowEffect),name+" has zero attached DropShadowEffect instances");
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;
        ControlCenterWindow? center=null;Window? settingsWindow=null;DockWindow? dock=null;WindowTracker? tracker=null;using var icons=new IconService();
        app.Startup+=async(_,_)=>{try{
            var seed=new DockSettings{Theme=DockTheme.Glass,Animations=true,GlassIntensity=1,GlassFrost=1,GlassDistortion=1,GlassHighlight=1,GlassSideDarkness=1};seed.Appearance.ShadowEnabled=true;seed.Appearance.ShadowOpacity=.6;seed.Appearance.ShadowBlur=34;
            var json=JsonNode.Parse(JsonSerializer.Serialize(seed))!;var old=json["ControlCenter"]!;old["ShadowOpacity"]=.6;old["ShadowBlur"]=36;old["SideDarkness"]=1;
            var service=new SettingsService(Path.Combine(directory,"legacy-data"));File.WriteAllText(service.SettingsPath,json.ToJsonString());var loaded=service.Load();
            Check(service.RecoveryNotice is null&&loaded.Appearance.ShadowEnabled&&loaded.Appearance.ShadowOpacity==.6&&loaded.ControlCenter.Hotkey==seed.ControlCenter.Hotkey,"legacy shadow fields load without losing global shadow or control-center settings");
            service.Save(loaded);var saved=JsonNode.Parse(File.ReadAllText(service.SettingsPath))!;
            Check(saved["ControlCenter"]!["ShadowOpacity"] is null&&saved["ControlCenter"]!["ShadowBlur"] is null&&saved["ControlCenter"]!["SideDarkness"] is null&&saved["Appearance"]!["ShadowEnabled"]!.GetValue<bool>(),"saving drops obsolete control-center fields and preserves Dock shadow settings");
            Check(typeof(DockSurface).Assembly.GetType("DockForWindows.Views.ControlPlateShadow") is null&&typeof(ControlCenterWindow).GetField("_plateShadows",F) is null,"dedicated shadow class and cache are removed");
            var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
            center=new ControlCenterWindow(loaded,connectivity:fake){DismissOnDeactivate=false};center.Show();for(int i=0;i<300&&center.CapturedFrames<2;i++)await Task.Delay(20);await Task.Delay(900);
            NoShadows(center,"main cards and slider thumbs");
            var surface=((IEnumerable)Field(center,"_surfaces")).Cast<DockSurface>().First(s=>s.IsVisible&&s.Backdrop is not null&&((BlurEffect)Field(s,"_frost")).Radius>0);var lighting=(ShaderEffect)Field(surface,"_lighting");var shader=(PixelShader)typeof(ShaderEffect).GetProperty("PixelShader",F)!.GetValue(lighting)!;
            Check(shader.UriSource.ToString().Contains("ControlCenterLighting"),"control-center lighting uses the compiled shadow-free shader");
            var lens=(ShaderEffect)Field(surface,"_refraction");var lensShader=(PixelShader)typeof(ShaderEffect).GetProperty("PixelShader",F)!.GetValue(lens)!;
            Check(!lensShader.UriSource.ToString().Contains("ControlCenterLighting")&&((BlurEffect)Field(surface,"_frost")).Radius>0,"original refraction and Gaussian frosting remain active");
            foreach(var kind in new[]{ControlModule.Wifi,ControlModule.Bluetooth,ControlModule.Volume,ControlModule.Brightness,ControlModule.Microphone}){
                Call(center,"OpenModule",kind,false);await Task.Delay(650);NoShadows(center,kind+" details");Call(center,"HideDetails");await Task.Delay(650);
            }
            var capture=Field(center,"_capture");for(int i=0;i<6;i++){loaded.ControlCenter.MaterialMode=i%2==0?ControlCenterMaterialMode.ClassicBlur:ControlCenterMaterialMode.LiquidGlass;Call(center,"ApplyPreview",loaded);await Task.Delay(100);NoShadows(center,"material switch "+i);}
            Check(ReferenceEquals(capture,Field(center,"_capture")),"rapid mode changes reuse the existing capture service");
            var frame=(WriteableBitmap)center.GlassBackdrop!;var bounds=center.GlassBackdropBounds;var pixels=new byte[frame.PixelWidth*frame.PixelHeight*4];for(int i=0;i<pixels.Length;i+=4){pixels[i+2]=231;pixels[i+3]=255;}
            var sampleType=typeof(ControlCenterWindow).GetNestedType("SampleResult",BindingFlags.NonPublic)!;using(var sample=(IDisposable)Activator.CreateInstance(sampleType,F,null,new object[]{pixels,frame.PixelWidth,frame.PixelHeight,bounds},null)!){
                Call(center,"ApplySample",sample);var pixel=new byte[4];frame.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
                Check(pixel[2]==231&&ReferenceEquals(frame,center.GlassBackdrop)&&(bool)Field(center,"_backgroundPixelsChanged"),"existing bitmap uploads immediately; only mapping invalidation is coalesced");
                Call(center,"OnRendering",null,EventArgs.Empty);Check(!(bool)Field(center,"_backgroundPixelsChanged"),"animation callback consumes the mapping flag without retaining captured frames");
            }
            Call(center,"OpenModule",ControlModule.Wifi,false);await Task.Delay(90);Call(center,"HideDetails");await Task.Delay(90);Call(center,"OpenModule",ControlModule.Wifi,false);await Task.Delay(120);Call(center,"HideDetails");await Task.Delay(1000);
            Check(!(bool)Field(center,"_detailVisible")&&(double)((FrameworkElement)Field(center,"_cards")).Opacity>.99,"rapid interrupted navigation restores original main controls");
            center.Dismiss();await Task.Delay(1000);Check(center.GlassBackdrop is null,"closing releases captured image binding");
            var model=new DockViewModel(loaded,service,icons);tracker=new WindowTracker(app.Dispatcher);dock=new DockWindow(model,tracker);var type=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.SettingsWindow")!;var palette=Activator.CreateInstance(typeof(DockSurface).Assembly.GetType("DockForWindows.Views.Palette")!,new object[]{loaded})!;
            settingsWindow=(Window)Activator.CreateInstance(type,new object[]{dock,palette,loaded,0})!;((CheckBox)Field(settingsWindow,"_live")).IsChecked=false;settingsWindow.Topmost=true;settingsWindow.Show();await Task.Delay(300);
            Call(settingsWindow,"ShowSetting","cc-frost");await Task.Delay(350);var ranges=(IDictionary)Field(settingsWindow,"_ranges");
            Check(!ranges.Contains("cc-shadow-opacity")&&!ranges.Contains("cc-shadow-blur")&&!ranges.Contains("cc-side")&&ranges.Contains("shadow-opacity")&&ranges.Contains("shadow-blur"),"control-center shadow UI is removed while Dock shadow UI stays available");
            var draft=(DockSettings)Field(settingsWindow,"_draft");Check(draft.Appearance.ShadowEnabled&&draft.Appearance.ShadowOpacity==.6,"opening settings does not modify Dock shadow preferences");
            var plain=new GlassButtonChrome{Width=100,Height=30};var plainWindow=new Window{Width=180,Height=100,Content=plain,Topmost=true,ShowInTaskbar=false};plainWindow.Show();await Task.Delay(180);Check(Tree<UIElement>(plain).Any(e=>e.Effect is DropShadowEffect),"ordinary GlassButtonChrome retains its original shadow path");plainWindow.Close();
            result=0;
        }catch(Exception e){Console.Error.WriteLine(e);}finally{settingsWindow?.Close();dock?.Close();tracker?.Dispose();center?.Dismiss();await Task.Delay(900);app.Shutdown(result);}};app.Run();return result;
    }
}

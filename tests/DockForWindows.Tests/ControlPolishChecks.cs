using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class ControlPolishChecks
{
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private static object Field(object o,string name)=>o.GetType().GetField(name,Flags)!.GetValue(o)!;
    private static object? Call(object o,string name,params object?[] args)=>o.GetType().GetMethod(name,Flags)!.Invoke(o,args);
    private static System.Collections.Generic.IEnumerable<T> Tree<T>(DependencyObject o)where T:DependencyObject{if(o is T value)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var v in Tree<T>(VisualTreeHelper.GetChild(o,i)))yield return v;}
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint x,uint y,uint data,nuint extra);
    private static async Task ClickAt(Point p){SetCursorPos((int)p.X,(int)p.Y);mouse_event(2,0,0,0,0);await Task.Delay(40);mouse_event(4,0,0,0,0);await Task.Delay(500);}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};ControlCenterWindow? center=null;
        app.Startup+=async(_,_)=>{try{
            foreach(var mode in new[]{ControlCenterMaterialMode.ClassicBlur,ControlCenterMaterialMode.LiquidGlass}){
                var settings=new DockSettings();settings.ControlCenter.MaterialMode=mode;settings.ControlCenter.Normalize();settings.ControlCenter.Modules.Single(m=>m.Module==ControlModule.Wifi).SetShape(ControlCardShape.Capsule);
                var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlCenterInteractionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();for(int wait=0;wait<400;wait++){await Task.Delay(20);if((bool)Field(center,"_entranceStarted")&&center.Opacity==1&&((TranslateTransform)Field(center,"_entrance")).Y==0&&!((bool)Field(center,"_polling")))break;}await Task.Delay(300);center.Activate();
                var cards=((IEnumerable)Field(center,"_modules")).Cast<object>().ToArray();var wifi=cards.Single(c=>(ControlModule)Field(c,"Kind")==ControlModule.Wifi);var root=(Grid)Field(wifi,"Root");
                Check(!Tree<Button>(root).Any(b=>AutomationProperties.GetName(b).StartsWith("展开")),mode+": no triangular detail buttons remain");
                var title=(TextBlock)Field(wifi,"Title");await ClickAt(title.PointToScreen(new Point(title.ActualWidth/2,title.ActualHeight/2)));
                Check((bool)Field(center,"_detailVisible")&&(int)Field(fake,"Toggles")==0,mode+": title enters details without toggling Wi-Fi");
                await Task.Delay(900);Console.WriteLine("kind="+Field(center,"_openKind")+" title="+((TextBlock)Field(center,"_detailTitle")).Text+" rows="+((IList)Field(center,"_deviceRows")).Count+" message="+((TextBlock)Field(center,"_message")).Text+" signals="+string.Join(",",Tree<FrameworkElement>(center).Where(e=>e.GetType().Name=="ControlCenterIcon").Select(e=>e.GetType().GetProperty("SignalStrength",Flags)!.GetValue(e))));
                Check(Tree<FrameworkElement>(center).Any(e=>e.GetType().Name=="ControlCenterIcon"&&(int)e.GetType().GetProperty("SignalStrength",Flags)!.GetValue(e)! ==82),mode+": network rows show real strength through the fan icon");
                Check(!Tree<TextBlock>(center).Any(t=>t.Text.Contains("82%")),mode+": numeric Wi-Fi strength is removed from visible rows");
                var stage=(FrameworkElement)Field(center,"_stage");var outside=stage.PointToScreen(new Point(-15,40));await ClickAt(outside);
                Check(!(bool)Field(center,"_detailVisible")&&(int)Field(fake,"Toggles")==0,mode+": blank return consumes the whole click without toggling underlying controls");
                for(int wait=0;wait<200&&((bool)Field(center,"_transitioning")||(bool)Field(center,"_returnPress"));wait++)await Task.Delay(20);var primary=(Button)Field(wifi,"Primary");await ClickAt(primary.PointToScreen(new Point(primary.ActualWidth/2,primary.ActualHeight/2)));
                Console.WriteLine($"quick toggles={Field(fake,"Toggles")} return={Field(center,"_returnPress")} transitioning={Field(center,"_transitioning")} enabled={primary.IsEnabled} p={primary.PointToScreen(new Point(primary.ActualWidth/2,primary.ActualHeight/2))} clip={((Grid)center.Content).Clip?.Bounds}");Check((int)Field(fake,"Toggles")==1&&!(bool)Field(center,"_detailVisible"),mode+": only the round primary hit area performs the quick action");
                var dark=cards.Single(c=>(ControlModule)Field(c,"Kind")==ControlModule.DarkMode);Call(center,"Active",ControlModule.DarkMode,true);await Task.Delay(220);Check(((SolidColorBrush)((Border)Field(dark,"Holder")).Background).Color==Colors.White,mode+": dark mode uses a white active circle from actual state");if(mode==ControlCenterMaterialMode.ClassicBlur)Check(((SolidColorBrush)((System.Windows.Shapes.Path)Field(dark,"Accent")).Fill).Color.A==0,"classic active cards keep the shared base tint");
                var slider=(Slider)Field(center,"_volume");Check(((SolidColorBrush)slider.Resources["ControlSliderAccent"]).Color==Color.FromRgb(10,132,255),mode+": horizontal volume uses blue track resources");
                Tree<Button>(center).Single(b=>AutomationProperties.GetAutomationId(b)=="control-center-edit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(150);
                Check(cards.All(c=>((Button)Field(c,"EditBadge")).Visibility==Visibility.Visible&&((Thumb)Field(c,"Grip")).Visibility==Visibility.Visible),mode+": editing shows per-card remove badges and curved resize handles");
                var remove=(Button)Field(wifi,"EditBadge");remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(550);
                Check(!((ControlCenterOptions)Field(center,"_options")).Modules.Single(m=>m.Module==ControlModule.Wifi).Visible,mode+": edit badge removes the requested card through existing layout state");
                var a=center.PointToScreen(new Point());var b=center.PointToScreen(new Point(center.ActualWidth,center.ActualHeight));using(var shot=new System.Drawing.Bitmap((int)(b.X-a.X),(int)(b.Y-a.Y))){using(var g=System.Drawing.Graphics.FromImage(shot))g.CopyFromScreen((int)a.X,(int)a.Y,0,0,shot.Size);shot.Save(Path.Combine(directory,mode+"-edit.png"));}
                center.Dismiss();await Task.Delay(1100);
            }
            var entryType=typeof(DockSurface).Assembly.GetType("DockForWindows.Services.ControlCenterTaskbarEntry")!;
            foreach(bool state in new[]{false,true}){var symbol=(DrawingImage)entryType.GetMethod("CreateSymbol",Flags)!.Invoke(null,new object[]{state})!;Check(symbol.IsFrozen&&!symbol.Drawing.Bounds.IsEmpty,"vector accessory symbol is cached for state "+state);}
            int taskbarToggles=0;var entry=(Window)Activator.CreateInstance(entryType,Flags,null,new object[]{(Action)(()=>taskbarToggles++)},null)!;entry.Show();await Task.Delay(80);Call(entry,"Place",new Rect(600,100,40,50));await Task.Delay(80);
            Check(!entry.ShowInTaskbar&&entry.Topmost,"network-side taskbar accessory does not occupy an application slot or tray icon");var button=(Button)Field(entry,"_button");button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(taskbarToggles==2,"accessory forwards two independent toggle actions");((IDisposable)entry).Dispose();
            result=0;
        }catch(Exception e){Console.Error.WriteLine(e);}finally{mouse_event(4,0,0,0,0);center?.Dismiss();await Task.Delay(1100);app.Shutdown(result);}};app.Run();return result;
    }
}

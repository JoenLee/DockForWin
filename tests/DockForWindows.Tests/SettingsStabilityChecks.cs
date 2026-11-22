using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class SettingsStabilityChecks
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
    static object Field(object o,string key){for(Type? t=o.GetType();t!=null;t=t.BaseType)if(t.GetField(key,Flags) is FieldInfo f)return f.GetValue(o)!;throw new Exception(key);}
    static IEnumerable<T> Tree<T>(DependencyObject o)where T:DependencyObject{if(o is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var c in Tree<T>(VisualTreeHelper.GetChild(o,i)))yield return c;}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static void Capture(FrameworkElement target,string path){var bmp=new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth),(int)Math.Ceiling(target.ActualHeight),96,96,PixelFormats.Pbgra32);bmp.Render(target);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(path);encoder.Save(file);}
    internal static int Run(string directory,bool english=false)
    {
        Directory.CreateDirectory(directory);UiText.SetLanguage(english?"en-US":"zh-CN");int result=1;
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{using var icons=new IconService();using var tracker=new WindowTracker(app.Dispatcher);SettingsWindow? settings=null;DockWindow? dock=null;WidgetDesktopService? widgets=null;
            try{
                var seed=new DockSettings{Language=english?"en-US":"zh-CN",InterfaceTheme=InterfaceTheme.Light,StartWithWindows=StartupRegistration.ReadCommand()!=null};
                var store=new SettingsService(Path.Combine(directory,"profile"));var model=new DockViewModel(seed,store,icons);dock=new DockWindow(model,tracker);
                widgets=new WidgetDesktopService(model);widgets.Error+=m=>throw new Exception(m);
                settings=new SettingsWindow(dock,new Palette(seed),seed,0);((CheckBox)Field(settings,"_live")).IsChecked=false;settings.Show();await Task.Delay(600);
                var host=(SettingsPageHost)Field(settings,"_pages");var sizes=new List<object>();
                foreach(var key in new[]{"appearance","general","interface","icons","motion","apps","placement","system","performance","about","widgets"}){
                    host.Navigate(key);await Task.Delay(260);settings.UpdateLayout();
                    var scroll=host.Children.OfType<ScrollViewer>().FirstOrDefault(s=>s.IsVisible);
                    sizes.Add(new{Key=key,Width=settings.ActualWidth,Height=settings.ActualHeight,Extent=scroll?.ExtentHeight,Viewport=scroll?.ViewportHeight,Scrollable=scroll?.ScrollableHeight});
                    if(key=="icons")Capture(settings,Path.Combine(directory,"icons.png"));
                    if(key!="appearance" && settings.ActualHeight>=600)Check(scroll is {ScrollableHeight:0,ComputedVerticalScrollBarVisibility:Visibility.Collapsed},"short page fits without scrolling: "+key+" ("+scroll?.ExtentHeight+" / "+scroll?.ViewportHeight+")");
                    if(key is "general" or "appearance" or "widgets" or "icons")Capture(settings,Path.Combine(directory,key+".png"));
                }
                File.WriteAllText(Path.Combine(directory,"page-sizes.json"),JsonSerializer.Serialize(sizes,new JsonSerializerOptions{WriteIndented=true}));
                host.Navigate("appearance");await Task.Delay(250);var longPage=host.Children.OfType<ScrollViewer>().Single(s=>s.IsVisible);
                Check(longPage.ScrollableHeight>100,"real overflow still scrolls");Check(!Tree<TextBlock>(longPage).Any(t=>t.Text==UiText.Get("高级设置")) && Tree<TextBlock>(longPage).Any(t=>t.Text==UiText.Get("精细调节")),"advanced heading is not duplicated");
                longPage.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});
                Check(((FrameworkElement)longPage.Content).CacheMode is BitmapCache,"inertial scroll caches the static body instead of repainting all shadows");
                await Task.Delay(900);Check(longPage.VerticalOffset>0 && ((FrameworkElement)longPage.Content).CacheMode==null,"scroll moves and releases its temporary cache");
                host.Navigate("widgets");await Task.Delay(250);Tree<Button>(settings).Single(b=>AutomationProperties.GetAutomationId(b)=="widget-home-clock").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(300);var gallery=Tree<WidgetGalleryPage>(settings).Single();gallery.ShowCategory(WidgetKind.Clock);await Task.Delay(250);
                var button=Tree<Button>(gallery).Single(b=>AutomationProperties.GetAutomationId(b)=="widget-add-clock");button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(1300);
                Check(model.Settings.Widgets.Instances.Count==1,"gallery add creates and persists a desktop instance with settings open");
                var windows=(System.Collections.IDictionary)Field(widgets,"_windows");var desktop=(WidgetDesktopWindow)windows.Values.Cast<object>().Single();
                Check(desktop.Attached&&desktop.IsVisible&&desktop.ActualWidth>0,"gallery-created native window is attached, visible and laid out");Capture(desktop,Path.Combine(directory,"desktop-render.png"));
                var bounds=desktop.PointToScreen(new Point());using(var image=new System.Drawing.Bitmap((int)(desktop.ActualWidth*VisualTreeHelper.GetDpi(desktop).DpiScaleX),(int)(desktop.ActualHeight*VisualTreeHelper.GetDpi(desktop).DpiScaleY))){using(var g=System.Drawing.Graphics.FromImage(image))g.CopyFromScreen((int)bounds.X,(int)bounds.Y,0,0,image.Size);image.Save(Path.Combine(directory,"desktop-screen.png"));}
                foreach(var theme in new[]{InterfaceTheme.Dark,InterfaceTheme.Light}){var draft=(DockSettings)Field(settings,"_draft");draft.InterfaceTheme=theme;typeof(SettingsShellWindow).GetMethod("ApplySettingsPalette",Flags)!.Invoke(settings,new object[]{new Palette(draft)});await Task.Delay(100);Check(model.Settings.Widgets.Instances.Count==1,"theme preview preserves live desktop widgets: "+theme);
                    Check(Tree<WidgetPreviewFrame>(gallery).All(f=>f.Children.OfType<Border>().All(b=>b.Opacity==(theme==InterfaceTheme.Dark?0:1))),"preview lighting follows window theme: "+theme);Capture(settings,Path.Combine(directory,"gallery-"+theme+".png"));}
                var shell=(Grid)Field(settings,"_shellContent");
                foreach(var state in new[]{WindowState.Maximized,WindowState.Normal}){settings.WindowState=state;await Task.Delay(200);settings.HideToTray();settings.RestoreFromTray();await Task.Delay(200);var geometry=(RectangleGeometry)((GeometryDrawing)((DrawingBrush)shell.OpacityMask).Drawing).Geometry;Check(geometry.RadiusX==(state==WindowState.Maximized?0:12),"outer radius survives hide/restore: "+state);}
                Check(shell.Clip is RectangleGeometry {RadiusX:12} && ReferenceEquals(((GeometryDrawing)((DrawingBrush)shell.OpacityMask).Drawing).Geometry,shell.Clip),"outer clip and antialias mask share the restored outline");
                settings.Height=520;host.Navigate("general");await Task.Delay(200);Check(host.Children.OfType<ScrollViewer>().Single(s=>s.IsVisible).ScrollableHeight>0,"smaller windows retain access to overflowing settings");
                Capture(settings,Path.Combine(directory,"restored.png"));result=0;
            }catch(Exception e){Console.WriteLine(e);File.WriteAllText(Path.Combine(directory,"exception.txt"),e.ToString());}
            finally{widgets?.Dispose();settings?.ClosePermanently();dock?.Close();app.Shutdown(result);}
        };app.Run();return result;
    }
}

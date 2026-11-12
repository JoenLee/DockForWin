using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class SettingsRefinementChecks
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static object Field(object owner,string key)=>owner.GetType().GetField(key,Flags)!.GetValue(owner)!;
    private static object? Call(object owner,string key,params object[] args)=>owner.GetType().GetMethod(key,Flags)!.Invoke(owner,args);
    private static System.Collections.Generic.IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T found)yield return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static T Find<T>(Window window,string id)where T:DependencyObject=>Tree<T>(window).Single(x=>AutomationProperties.GetAutomationId(x)==id);
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    private static void Capture(Window window,string file){var a=window.PointToScreen(new Point());var b=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));using var bitmap=new System.Drawing.Bitmap((int)(b.X-a.X),(int)(b.Y-a.Y));using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen((int)a.X,(int)a.Y,0,0,bitmap.Size);bitmap.Save(file,System.Drawing.Imaging.ImageFormat.Png);}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using var icons=new IconService();var settings=new DockSettings{Theme=DockTheme.Glass,InterfaceTheme=InterfaceTheme.Light,InterfaceStyle=InterfaceStyle.Warm};var store=new SettingsService(Path.Combine(directory,"isolated-data"));var model=new DockViewModel(settings,store,icons);var tracker=new WindowTracker(app.Dispatcher);var dock=new DockWindow(model,tracker);Window? window=null;
        app.Startup+=async(_,_)=>{try{
            foreach(var legacy in Enum.GetValues<InterfaceStyle>()){var migrated=settings.Copy();migrated.InterfaceStyle=legacy;migrated.Normalize();Check(migrated.InterfaceStyle==InterfaceStyle.Mono&&InterfaceStyles.Resolve(legacy,false)==InterfaceStyles.Resolve(InterfaceStyle.Mono,false),"legacy palette "+legacy+" resolves to monochrome without changing material");}
            var assembly=typeof(DockSurface).Assembly;var type=assembly.GetType("DockForWindows.Views.SettingsWindow")!;object Palette(DockSettings s)=>Activator.CreateInstance(assembly.GetType("DockForWindows.Views.Palette")!,new object[]{s})!;
            window=(Window)Activator.CreateInstance(type,new object[]{dock,Palette(settings),settings,0})!;((CheckBox)Field(window,"_live")).IsChecked=false;window.Topmost=true;window.Show();await Task.Delay(350);
            var green=Find<Button>(window,"settings-shell-window-maximize");var yellow=Find<Button>(window,"settings-shell-window-minimize");var red=Find<Button>(window,"settings-shell-window-close");
            var g=green.TranslatePoint(new Point(),window);var y=yellow.TranslatePoint(new Point(),window);var r=red.TranslatePoint(new Point(),window);
            Check(g.X>window.ActualWidth-120&&g.X<y.X&&y.X<r.X&&Math.Abs(g.Y-r.Y)<1,"window controls sit at top right in green-yellow-red order");
            foreach(var button in new[]{green,yellow,red}){var circle=(Grid)button.Template.FindName("Circle",button)!;var glyph=(FrameworkElement)button.Content;var center=glyph.TranslatePoint(new Point(glyph.ActualWidth/2,glyph.ActualHeight/2),circle);Check(Math.Abs(center.X-circle.ActualWidth/2)<=.51/VisualTreeHelper.GetDpi(circle).DpiScaleX&&Math.Abs(center.Y-circle.ActualHeight/2)<=.51/VisualTreeHelper.GetDpi(circle).DpiScaleY,"window glyph is centered in its 16 DIP circle: "+AutomationProperties.GetName(button));}
            green.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(120);Check(window.WindowState==WindowState.Maximized,"green control maximizes");green.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(120);Check(window.WindowState==WindowState.Normal,"green control restores");yellow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(window.WindowState==WindowState.Minimized,"yellow control minimizes");window.WindowState=WindowState.Normal;await Task.Delay(140);
            Call(window,"ShowSetting","start-with-windows");await Task.Delay(310);var toggle=Find<CheckBox>(window,"start-with-windows");toggle.IsChecked=false;await Task.Delay(270);var knob=(Border)toggle.Template.FindName("SwitchKnob",toggle)!;var rail=(Grid)VisualTreeHelper.GetParent(knob);var shift=(TranslateTransform)((TransformGroup)knob.RenderTransform).Children[1];
            Check(rail.Width==44&&rail.Height==26&&knob.Width==18&&knob.Height==18&&Math.Abs(shift.X)<.01,"new switch uses a 44x26 track and 18 DIP circle");toggle.IsChecked=true;await Task.Delay(290);Check(Math.Abs(shift.X-18)<.01&&((SolidColorBrush)((Border)toggle.Template.FindName("SwitchTrack",toggle)!).Background).Color==Color.FromRgb(8,8,8),"switch moves 18 DIP in 200ms while keeping the black rail");Capture(window,Path.Combine(directory,"02-switch.png"));
            Call(window,"ShowSetting","interface-theme");await Task.Delay(310);Check(!Tree<Button>(window).Any(b=>AutomationProperties.GetAutomationId(b).StartsWith("interface-style-")),"removed colored palette choices");Capture(window,Path.Combine(directory,"03-monochrome.png"));
            Find<ListBox>(window,"settings-navigation").SelectedIndex=1;await Task.Delay(310);Check(!Tree<TextBox>(window).Any(t=>AutomationProperties.GetAutomationId(t)=="control-center-hotkey")&&!Tree<Button>(window).Any(b=>b.Content is string text&&(text.Contains("导入网页设计参数")||text=="打开控制中心")),"removed hotkey, open/import buttons and explanation from the settings page");Capture(window,Path.Combine(directory,"04-control-entry.png"));
            Find<ListBox>(window,"settings-navigation").SelectedIndex=0;await Task.Delay(240);Capture(window,Path.Combine(directory,"01-light.png"));
            var dark=settings.Copy();dark.InterfaceTheme=InterfaceTheme.Dark;var darkWindow=(Window)Activator.CreateInstance(type,new object[]{dock,Palette(dark),dark,5})!;((CheckBox)Field(darkWindow,"_live")).IsChecked=false;darkWindow.Topmost=true;darkWindow.Show();await Task.Delay(300);Capture(darkWindow,Path.Combine(directory,"05-dark.png"));Check(((SolidColorBrush)darkWindow.Background).Color.R<30,"dark monochrome settings remain available");darkWindow.Close();
            dock.Show();await Task.Delay(400);((DispatcherTimer)Field(dock,"_glassTimer")).Stop();((DispatcherTimer)Field(dock,"_edgeTimer")).Stop();tracker.Dispose();await Task.Delay(150);model.Items.Add(new DockItem(new PinnedItem{Name="Recent fixture",Target="fixture.exe"},false));Call(dock,"Rebuild");await Task.Delay(100);
            var separator=(Border)Field(dock,"_separator");var point=separator.PointToScreen(new Point(separator.ActualWidth/2,separator.ActualHeight/2));var bounds=new Rect(point.X-32,point.Y-32,64,64);
            BitmapSource Frame(byte value){var pixels=Enumerable.Repeat(value,64*64*4).ToArray();for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;var frame=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,pixels,256);frame.Freeze();return frame;}
            var black=Frame(0);var white=Frame(255);for(int i=0;i<20;i++)Call(dock,"UpdateSeparatorBackdrop",black,bounds,0d,100d+i,false);Check(((SolidColorBrush)separator.Background).Color.R>220,"recent-app separator becomes light on a dark background");
            for(int i=0;i<20;i++)Call(dock,"UpdateSeparatorBackdrop",white,bounds,0d,200d+i,false);Check(((SolidColorBrush)separator.Background).Color.R<80,"recent-app separator becomes dark on a light background");Call(dock,"RestoreSeparatorColor");Check(separator.Opacity==.35,"separator keeps the existing geometry and opacity");
            Check(!File.Exists(store.SettingsPath),"UI verification leaves preferences unsaved in the isolated profile");result=0;
        }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}finally{window?.Close();dock.Close();tracker.Dispose();app.Shutdown(result);}};app.Run();return result;
    }
}

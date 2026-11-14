using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using Forms=System.Windows.Forms;

internal static class ThemedMenuChecks
{
    [DllImport("user32.dll")]private static extern bool SetProcessDpiAwarenessContext(nint value);
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static object Value(object owner,string field)=>owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Capture(Window window,string path){var p=window.PointToScreen(new System.Windows.Point());using var bitmap=new Bitmap((int)(window.ActualWidth*VisualTreeHelper.GetDpi(window).DpiScaleX),(int)(window.ActualHeight*VisualTreeHelper.GetDpi(window).DpiScaleY));using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen((int)p.X,(int)p.Y,0,0,bitmap.Size);bitmap.Save(path);}
    internal static int Run(string directory)
    {
        SetProcessDpiAwarenessContext(-4);Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var window=new Window{Width=520,Height=360,Title="圆角菜单 · 隔离检查",Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=System.Windows.Media.Brushes.WhiteSmoke};
        app.Startup+=async(_,_)=>{try{
            window.Show();var config=new DockSettings{InterfaceTheme=InterfaceTheme.Light,InterfaceAccent="#B65980"};int invoked=0;
            var menu=new ContextMenu{PlacementTarget=window,Placement=PlacementMode.Center};NativeMenuStyles.Apply(menu,config);var item=new MenuItem{Header="打开设置",IsCheckable=true,IsChecked=true};item.Click+=(_,_)=>invoked++;menu.Items.Add(item);menu.Items.Add(new Separator());menu.Items.Add(new MenuItem{Header="打开控制中心"});
            menu.IsOpen=true;await Task.Delay(200);item.ApplyTemplate();var face=(Border)item.Template.FindName("Choice",item);var mark=(System.Windows.Shapes.Path)item.Template.FindName("Check",item);var point=item.PointToScreen(new System.Windows.Point(item.ActualWidth/2,item.ActualHeight/2));SetCursorPos((int)point.X,(int)point.Y);item.Focus();await Task.Delay(180);
            Check(face.CornerRadius.TopLeft==7&&mark.Visibility==Visibility.Visible,"Dock menu uses rounded rows and a checked-item glyph");Check(item.IsHighlighted&&((SolidColorBrush)face.Background).Color==DesignSystem.Hex(config.InterfaceAccent),"Dock menu hover follows the global accent");Capture(window,Path.Combine(directory,"01-dock-menu.png"));item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Check(invoked==1,"Dock menu action remains callable");menu.IsOpen=false;
            var tray=TrayMenu.Create(()=>invoked++,()=>{},()=>{},()=>config);tray.PlacementTarget=window;tray.Placement=PlacementMode.Center;tray.IsOpen=true;await Task.Delay(150);var trayItem=(MenuItem)tray.Items[0];trayItem.ApplyTemplate();var trayFace=(Border)trayItem.Template.FindName("Choice",trayItem);point=trayItem.PointToScreen(new System.Windows.Point(trayItem.ActualWidth/2,trayItem.ActualHeight/2));SetCursorPos((int)point.X,(int)point.Y);trayItem.Focus();await Task.Delay(180);
            Check(ReferenceEquals(menu.Style,tray.Style)&&ReferenceEquals(item.Style,trayItem.Style),"tray and Dock menus use the exact same menu and row templates");Check(trayItem.IsHighlighted&&((SolidColorBrush)trayFace.Background).Color==DesignSystem.Hex(config.InterfaceAccent),"tray hover uses the same global accent");
            Check(Math.Abs(item.ActualHeight-trayItem.ActualHeight)<1&&trayItem.ActualWidth>0,"tray row height and padding match the reference menu");Capture(window,Path.Combine(directory,"02-tray-menu.png"));trayItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Check(invoked==2&&tray.Items.Count==4,"tray actions and separator are preserved");tray.IsOpen=false;
            config.InterfaceAccent="#168A9C";config.InterfaceTheme=InterfaceTheme.Dark;tray.IsOpen=true;await Task.Delay(160);point=trayItem.PointToScreen(new System.Windows.Point(trayItem.ActualWidth/2,trayItem.ActualHeight/2));SetCursorPos((int)point.X,(int)point.Y);await Task.Delay(120);Check(((SolidColorBrush)tray.Resources["MenuAccent"]).Color==DesignSystem.Hex(config.InterfaceAccent)&&((SolidColorBrush)tray.Resources["MenuBackground"]).Color.R<80,"reopening the tray menu reads the current theme and accent");Capture(window,Path.Combine(directory,"03-tray-menu-dark.png"));tray.IsOpen=false;NativeMenuStyles.Apply(menu,config);Check(((SolidColorBrush)menu.Resources["MenuAccent"]).Color==DesignSystem.Hex(config.InterfaceAccent),"Dock menu refreshes the current theme on reopening");SetCursorPos((int)window.PointToScreen(new System.Windows.Point(150,140)).X,(int)window.PointToScreen(new System.Windows.Point(150,140)).Y);TrayMenu.Show(tray);await Task.Delay(150);Check(tray.IsOpen,"tray mouse-position entry opens without dismissing itself");tray.IsOpen=false;TrayMenu.Show(tray);await Task.Delay(150);Check(tray.IsOpen,"tray menu can be reopened after closing");tray.IsOpen=false;result=0;
        }catch(Exception ex){Console.Error.WriteLine(ex);}finally{window.Close();app.Shutdown(result);}};app.Run();return result;
    }
}

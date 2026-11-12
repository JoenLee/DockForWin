using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class RedesignChecks
{
    [DllImport("user32.dll")]static extern bool SetProcessDpiAwarenessContext(nint c);
    [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]static extern nint GetDC(nint w);
    [DllImport("user32.dll")]static extern int ReleaseDC(nint w,nint dc);
    [DllImport("gdi32.dll")]static extern uint GetPixel(nint dc,int x,int y);
    static double Variance(Point center){var dc=GetDC(0);double sum=0,squares=0;try{for(int i=-40;i<40;i++){double v=GetPixel(dc,(int)center.X+i,(int)center.Y)&255;sum+=v;squares+=v*v;}}finally{ReleaseDC(0,dc);}return squares/80-Math.Pow(sum/80,2);}
    static int RedMinusBlue(Point point){var dc=GetDC(0);try{uint p=GetPixel(dc,(int)point.X,(int)point.Y);return (int)(p&255)-(int)((p>>16)&255);}finally{ReleaseDC(0,dc);}}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static T[] All<T>(DependencyObject parent)where T:DependencyObject{
        var found=new HashSet<DependencyObject>();void Visit(DependencyObject p){if(!found.Add(p))return;foreach(var c in LogicalTreeHelper.GetChildren(p).OfType<DependencyObject>())Visit(c);if(p is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)Visit(VisualTreeHelper.GetChild(p,i));}Visit(parent);return found.OfType<T>().ToArray();
    }
    static T Find<T>(Window w,string id)where T:DependencyObject{var visible=All<T>(w).Where(e=>AutomationProperties.GetAutomationId(e)==id).ToArray();if(visible.Length>0)return visible.Single();var pages=w.GetType().GetField("_settingsPages",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(w) as IDictionary;return pages!.Values.Cast<DependencyObject>().Distinct().SelectMany(All<T>).Distinct().Single(e=>AutomationProperties.GetAutomationId(e)==id);}
    static void Capture(Window window,string path){var a=window.PointToScreen(new Point());var b=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));using var bmp=new System.Drawing.Bitmap(Math.Max(1,(int)(b.X-a.X)),Math.Max(1,(int)(b.Y-a.Y)));using var g=System.Drawing.Graphics.FromImage(bmp);g.CopyFromScreen((int)a.X,(int)a.Y,0,0,bmp.Size);bmp.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
    public static BitmapSource Glyph(Brush brush){var v=new DrawingVisual();using(var dc=v.RenderOpen()){dc.DrawRectangle(brush,null,new Rect(45,18,10,64));dc.DrawRectangle(brush,null,new Rect(18,45,64,10));}var image=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32);image.Render(v);image.Freeze();return image;}
    public static int Run(string directory)
    {
        SetProcessDpiAwarenessContext((nint)(-4));Directory.CreateDirectory(directory);SetCursorPos(100,100);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var settings=new DockSettings{Theme=DockTheme.Glass,IconSize=52,AutoHide=false,HideWhenFullscreen=false,ShowRunningApps=false,ShowRecentApps=false,Reflections=true};
        string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");for(int i=0;i<7;i++)settings.Pins.Add(new PinnedItem{Name="UI fixture "+i,Target=exe,ExecutablePath=exe});
        using var icons=new IconService();var model=new DockViewModel(settings,new SettingsService(Path.Combine(directory,"isolated-data")),icons);var tracker=new WindowTracker(app.Dispatcher);var dock=new DockWindow(model,tracker);int exit=0;
        var checker=new DrawingGroup();using(var dc=checker.Open()){dc.DrawRectangle(Brushes.Black,null,new Rect(0,0,8,8));dc.DrawRectangle(Brushes.White,null,new Rect(0,0,4,4));dc.DrawRectangle(Brushes.White,null,new Rect(4,4,4,4));}
        var background=new Window{Title="Settings blur verification",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Left=0,Top=0,Width=SystemParameters.WorkArea.Width,Height=SystemParameters.WorkArea.Height,Background=new DrawingBrush(checker){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,8,8)}};background.Show();
        dock.Loaded+=async(_,_)=>{try{
            await Task.Delay(1300);dock.OpenSettings();await Task.Delay(500);Window w=app.Windows.Cast<Window>().Single(x=>x.Title.StartsWith("程序坞设置"));
            Check(((Grid)((Border)w.Content).Child).ColumnDefinitions.Count==2,"设置为固定目录与动态内容双栏");
            Find<Button>(w,"open-settings-preview").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(320);
            Check(Find<FrameworkElement>(w,"rounded-preview-frame") is SquircleBorder,"预览外框采用三次超椭圆");
            var scene=Find<Grid>(w,"wallpaper-preview");Check(scene.Background is ImageBrush ib&&ib.ImageSource is BitmapImage bmp&&bmp.UriSource.ToString().Contains("PreviewWallpaper"),"预览嵌入用户壁纸");
            Check(All<DockSurface>(w).Single().Backdrop is BitmapSource,"预览材质实际取样壁纸而不是纯色示意");Capture(w,Path.Combine(directory,"01-settings-light.png"));
            var theme=Find<ComboBox>(w,"theme-mode");theme.SelectedIndex=(int)DockTheme.Light;await Task.Delay(180);
            Check(!Find<Slider>(w,"glass-intensity").IsEnabled&&!Find<Slider>(w,"glass-frost").IsEnabled,"非玻璃主题的玻璃参数置灰停用");
            theme.SelectedIndex=(int)DockTheme.Glass;await Task.Delay(150);Check(Find<Slider>(w,"glass-intensity").IsEnabled,"玻璃主题恢复原参数控件");
            Find<ListBox>(w,"settings-navigation").SelectedIndex=1;await Task.Delay(100);Check(!Find<CheckBox>(w,"reflections").IsEnabled,"有底板时倒影开关停用");
            Find<Slider>(w,"glass-transparency").Value=100;await Task.Delay(180);Check(Find<CheckBox>(w,"reflections").IsEnabled&&Find<Slider>(w,"reflection-opacity").IsEnabled,"完全透明时倒影及参数恢复");
            Check(All<Image>(dock).Any(x=>AutomationProperties.GetAutomationId(x)=="dock-reflection"),"桌面实际仅在无底板时创建倒影");
            Find<Slider>(w,"glass-transparency").Value=0;await Task.Delay(180);Check(!All<Image>(dock).Any(x=>AutomationProperties.GetAutomationId(x)=="dock-reflection"),"恢复底板后实际移除倒影");
            Find<ListBox>(w,"settings-navigation").SelectedIndex=2;await Task.Delay(80);Find<CheckBox>(w,"animations").IsChecked=false;await Task.Delay(150);Check(!Find<Slider>(w,"bounce-height").IsEnabled,"关闭动画后相关滑块停用");
            Find<ListBox>(w,"settings-navigation").SelectedIndex=4;await Task.Delay(80);Check(Find<CheckBox>(w,"desktop-auto-hide").IsChecked==false,"桌面默认固定显示");
            Find<ListBox>(w,"settings-navigation").SelectedIndex=5;await Task.Delay(100);Check(Find<CheckBox>(w,"start-with-windows") is not null,"提供开机自启开关且不在预览中写入");
            Find<ListBox>(w,"settings-navigation").SelectedIndex=6;await Task.Delay(100);Find<ComboBox>(w,"interface-theme").SelectedIndex=2;await Task.Delay(250);w=app.Windows.Cast<Window>().Single(x=>x.Title.StartsWith("程序坞设置"));Capture(w,Path.Combine(directory,"02-settings-dark.png"));
            Check(((SolidColorBrush)w.Background).Color.R<65,"深色双栏设置可正确重建");
            var probe=w.PointToScreen(new Point(w.ActualWidth-140,w.ActualHeight-170));background.Background=Brushes.Blue;await Task.Delay(250);int blue=RedMinusBlue(probe);background.Background=Brushes.OrangeRed;await Task.Delay(250);int red=RedMinusBlue(probe);Check(blue==red&&((SolidColorBrush)w.Background).Color.A==255,"设置窗口恢复不透明底色且不再透出背景");w.Close();await Task.Delay(100);dock.CancelPreview();
            for(int i=7;i<60;i++)model.Items.Add(new DockItem(new PinnedItem{Name="Crowd "+i,Target=exe,ExecutablePath=exe}){Icon=model.Items[0].Icon});
            foreach(var edge in new[]{DockEdge.Left,DockEdge.Right}){
                var draft=settings.Copy();draft.Edge=edge;draft.Reflections=false;draft.Animations=false;dock.PreviewSettings(draft);await Task.Delay(450);
                var strip=All<DockStrip>(dock).Single();var surface=All<DockSurface>(dock).Single();var span=strip.TransformToAncestor(dock).TransformBounds(strip.IconSpan);var center=dock.PointToScreen(new Point(span.X+span.Width/2,span.Y+span.Height/2));var plate=surface.PointToScreen(new Point(surface.ActualWidth/2,surface.ActualHeight/2));
                var screen=System.Windows.Forms.Screen.PrimaryScreen!;Check(((ScaleTransform)strip.LayoutTransform).ScaleX<1,"侧边空间不足时整体缩小："+edge);
                Check(Math.Abs(center.Y-(screen.Bounds.Top+screen.Bounds.Height/2d))<3&&Math.Abs(center.Y-plate.Y)<3,"侧边图标与底板沿屏幕中线居中："+edge);
                Check(Math.Abs(center.X-plate.X)<3,"侧边底板与图标横向中心一致："+edge);
                var top=dock.PointToScreen(new Point(0,span.Top));var bottom=dock.PointToScreen(new Point(0,span.Bottom));Check(top.Y>=screen.WorkingArea.Top&&bottom.Y<=screen.WorkingArea.Bottom,"侧边所有图标均在屏幕内："+edge);Capture(dock,Path.Combine(directory,"03-side-"+edge+".png"));
            }
            Console.WriteLine("REDESIGN RESULT PASS");
        }catch(Exception ex){Console.WriteLine("REDESIGN RESULT FAIL "+ex);exit=1;}finally{dock.Close();background.Close();app.Shutdown(exit);}};
        app.Run(dock);return exit;
    }
}

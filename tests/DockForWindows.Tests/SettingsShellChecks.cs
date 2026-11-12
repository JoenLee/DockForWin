using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Views;
using DockForWindows.Services;
using DockForWindows.ViewModels;

internal static class SettingsShellChecks
{
    [DllImport("user32.dll")]private static extern bool SetProcessDpiAwarenessContext(nint value);
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static readonly Type ShellType=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.SettingsShellWindow")!;
    private static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T item)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static T Find<T>(DependencyObject root,string id)where T:DependencyObject=>Tree<T>(root).Single(x=>AutomationProperties.GetAutomationId(x)==id);
    private static object Field(object owner,string name)=>owner.GetType().GetField(name,Private)!.GetValue(owner)!;
    private static object Property(object owner,string name)=>owner.GetType().GetProperty(name,Private)!.GetValue(owner)!;
    private static void Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Private)!.Invoke(owner,args);
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static void Click(Window window,string id)=>Find<Button>(window,id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Screenshot(Window window,string file)
    {var a=window.PointToScreen(new Point());var b=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));using var image=new System.Drawing.Bitmap((int)(b.X-a.X),(int)(b.Y-a.Y));using(var g=System.Drawing.Graphics.FromImage(image))g.CopyFromScreen((int)a.X,(int)a.Y,0,0,image.Size);image.Save(file,System.Drawing.Imaging.ImageFormat.Png);}
    internal static int Run(string directory,bool preview=false)
    {
        SetProcessDpiAwarenessContext(-4);Directory.CreateDirectory(directory);int result=1;
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Window? window=null;
        var seed=new DockSettings{Theme=DockTheme.Glass,InterfaceTheme=InterfaceTheme.Light,Animations=true};double seedOpacity=seed.Appearance.Opacity;DockSettings? reviewed=null;int reviewPage=-1;
        app.Startup+=async(_,_)=>{
            try{
                Action<DockSettings,int> review=(draft,page)=>{reviewed=draft;reviewPage=page;if(preview)File.WriteAllText(Path.Combine(directory,"preview-draft.json"),JsonSerializer.Serialize(draft,new JsonSerializerOptions{WriteIndented=true}));};
                window=(Window)Activator.CreateInstance(ShellType,Private,null,new object[]{seed,review,0},null)!;window.Topmost=!preview;if(preview){result=0;window.Closed+=(_,_)=>app.Shutdown();}window.Show();await Task.Delay(400);
                if(preview){Find<Button>(window,"settings-shell-review").Content="完成预览";return;}
                using(var icons=new IconService()){
                    var model=new DockViewModel(new DockSettings(),new SettingsService(Path.Combine(directory,"legacy-test-data")),icons);var tracker=new WindowTracker(app.Dispatcher);var dock=new DockWindow(model,tracker);
                    var pending=seed.Copy();pending.StartWithWindows=StartupRegistration.ReadCommand() is null;
                    var paletteType=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.Palette")!;var palette=Activator.CreateInstance(paletteType,new object[]{pending})!;
                    var legacyType=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.SettingsWindow")!;var legacy=(Window)Activator.CreateInstance(legacyType,new object[]{dock,palette,pending,0})!;
                    Assert(((DockSettings)Field(legacy,"_draft")).StartWithWindows==pending.StartWithWindows,"legacy review preserves the supplied startup draft");legacy.Close();dock.Close();tracker.Dispose();
                }
                var host=(Grid)Field(window,"_pages");var nav=Find<ListBox>(window,"settings-shell-navigation");
                Assert(nav.Items.Count==8,"eight original categories remain available");
                Assert(!Tree<DockSurface>(window).Any()&&!Tree<GlassButtonChrome>(window).Any(),"settings shell has no live glass/capture visual");
                Assert(((Grid)Find<FrameworkElement>(window,"settings-shell-two-columns")).ColumnDefinitions[0].Width.Value==240,"sidebar is 240 DIP");
                Screenshot(window,Path.Combine(directory,"01-appearance.png"));
                var theme=Find<ListBox>(window,"shell-dock-theme");theme.SelectedIndex=1;Find<Slider>(window,"shell-transparency").Value=37;
                var draft=(DockSettings)Field(window,"_draft");Assert(seed.Theme==DockTheme.Glass&&Math.Abs(seed.Appearance.Opacity-seedOpacity)<.001&&draft.Theme==DockTheme.Light&&Math.Abs(draft.Appearance.Opacity-.63)<.001,"controls edit an isolated draft");
                var sidebar=nav.PointToScreen(new Point());Click(window,"shell-material-link");await Task.Delay(320);
                Assert((string)Property(host,"CurrentKey")=="material"&&nav.SelectedIndex==3&&nav.PointToScreen(new Point())==sidebar,"secondary navigation keeps the sidebar fixed");
                window.Height=560;await Task.Delay(180);var material=host.Children.OfType<ScrollViewer>().Single();material.ScrollToVerticalOffset(70);await Task.Delay(80);double offset=material.VerticalOffset;Assert(offset>0,"secondary page can scroll in a small window");
                nav.SelectedIndex=5;await Task.Delay(260);Click(window,"settings-shell-back");await Task.Delay(300);
                Assert(ReferenceEquals(material,host.Children.OfType<ScrollViewer>().Single())&&Math.Abs(material.VerticalOffset-offset)<1,"back restores the cached page and scroll offset");
                Click(window,"settings-shell-forward");await Task.Delay(300);Assert(nav.SelectedIndex==5,"forward restores category history");
                window.Height=740;nav.SelectedIndex=4;await Task.Delay(250);var animation=Find<CheckBox>(window,"shell-animations");animation.IsChecked=false;await Task.Delay(230);
                var knob=(Border)animation.Template.FindName("SwitchKnob",animation)!;var transform=(TransformGroup)knob.RenderTransform;
                Assert(!draft.Animations&&Math.Abs(((TranslateTransform)transform.Children[1]).X)<.01,"switch state and knob agree");
                animation.IsChecked=true;await Task.Delay(230);Assert(draft.Animations&&Math.Abs(((TranslateTransform)transform.Children[1]).X-18)<.01,"switch reaches the checked position");
                Screenshot(window,Path.Combine(directory,"02-motion.png"));Click(window,"shell-motion-link");await Task.Delay(320);Screenshot(window,Path.Combine(directory,"03-secondary.png"));
                for(int i=0;i<80;i++){nav.SelectedIndex=i%8;if(i%5==0)await Task.Delay(8);if(host.Children.Count>2||host.Children.OfType<FrameworkElement>().Sum(p=>p.Opacity)<.25)throw new Exception("Transition step="+i+" count="+host.Children.Count+" opacity="+string.Join(",",host.Children.OfType<FrameworkElement>().Select(p=>p.Opacity)));}
                await Task.Delay(330);Assert((int)Property(host,"CachedPageCount")<=16,"page cache stays bounded");
                nav.SelectedIndex=1;await Task.Delay(240);Click(window,"shell-panel-link");await Task.Delay(300);Find<Slider>(window,"shell-panel-scale").Value=140;Assert((string)Property(host,"CurrentKey")=="panel-proportions"&&Math.Abs(draft.ControlCenter.Geometry.PanelScale-1.4)<.001&&Math.Abs(seed.ControlCenter.Geometry.PanelScale-1.4)>.01,"third detail page edits only its control-center draft");Screenshot(window,Path.Combine(directory,"05-panel-proportions.png"));
                var search=Find<TextBox>(window,"settings-shell-search");search.Text="开机";await Task.Delay(100);Assert(nav.SelectedIndex==6&&nav.Items.OfType<ListBoxItem>().Count(i=>i.Visibility==Visibility.Visible)==1,"search finds settings by category keywords");search.Text="不存在的分类";Assert(Find<TextBlock>(window,"settings-shell-search-empty").Visibility==Visibility.Visible,"search explains an empty result");search.Clear();
                nav.SelectedIndex=3;await Task.Delay(260);Assert(Math.Abs(Find<Slider>(window,"shell-transparency").Value-37)<.01,"rapid navigation preserves draft controls");
                ((ListBoxItem)nav.Items[0]).Focus();((ListBoxItem)nav.Items[0]).RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(nav),Environment.TickCount,Key.Down){RoutedEvent=Keyboard.KeyDownEvent});await Task.Delay(230);Assert(nav.SelectedIndex==1,"sidebar supports native arrow-key navigation");nav.SelectedIndex=0;await Task.Delay(230);
                Click(window,"settings-shell-window-minimize");Assert(window.WindowState==WindowState.Minimized,"yellow control minimizes the native window");window.WindowState=WindowState.Normal;await Task.Delay(120);
                Click(window,"settings-shell-window-maximize");await Task.Delay(120);Assert(window.WindowState==WindowState.Maximized,"green control maximizes the native window");Click(window,"settings-shell-window-maximize");await Task.Delay(120);Assert(window.WindowState==WindowState.Normal,"green control restores the native window");
                window.Width=780;window.Height=570;await Task.Delay(200);Screenshot(window,Path.Combine(directory,"04-small-window.png"));Assert(host.ActualWidth>0&&Find<Button>(window,"settings-shell-review").IsVisible,"small window retains content and actions");
                window.Width=980;window.Height=740;await Task.Delay(200);
                using var process=Process.GetCurrentProcess();File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{dpi=VisualTreeHelper.GetDpi(window).DpiScaleX,cachedPages=Property(host,"CachedPageCount"),activePages=host.Children.Count,workingSetBytes=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,notes="Real WPF windows; no forced GC, no configuration writes, no GPU FPS claims."},new JsonSerializerOptions{WriteIndented=true}));
                Click(window,"settings-shell-review");Assert(reviewed is not null&&Math.Abs(reviewed.Appearance.Opacity-.63)<.001&&reviewPage==0,"review callback receives the modified draft and category");result=0;
            }catch(Exception ex){result=1;Console.Error.WriteLine("FAIL "+ex);if(preview)app.Shutdown(1);}
            finally{if(!preview){if(window?.IsVisible==true)window.Close();app.Shutdown(result);}}
        };
        app.Run();return result;
    }
}

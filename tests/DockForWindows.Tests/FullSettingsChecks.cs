using System;
using System.Collections;
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
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class FullSettingsChecks
{
    [DllImport("user32.dll")]private static extern bool SetProcessDpiAwarenessContext(nint value);
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static readonly Type SettingsType=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.SettingsWindow")!;
    private static readonly Type PaletteType=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.Palette")!;
    private static object Field(object owner,string name){for(Type? type=owner.GetType();type!=null;type=type.BaseType){var field=type.GetField(name,Flags|BindingFlags.DeclaredOnly);if(field!=null)return field.GetValue(owner)!;}throw new MissingFieldException(name);}
    private static object Property(object owner,string name)=>owner.GetType().GetProperty(name,Flags)!.GetValue(owner)!;
    private static object? Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags)!.Invoke(owner,args);
    private static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T found)yield return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static T Find<T>(DependencyObject root,string id)where T:DependencyObject=>Tree<T>(root).Single(x=>AutomationProperties.GetAutomationId(x)==id);
    private static void Assert(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static void Click(Window window,string id)=>Find<Button>(window,id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Capture(Window window,string path){var a=window.PointToScreen(new Point());var b=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));using var bitmap=new System.Drawing.Bitmap((int)(b.X-a.X),(int)(b.Y-a.Y));using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen((int)a.X,(int)a.Y,0,0,bitmap.Size);bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
    internal static int Run(string directory,bool preview=false,bool dashboardOnly=false)
    {
        SetProcessDpiAwarenessContext(-4);Directory.CreateDirectory(directory);int result=1;
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Window? window=null;DockWindow? dock=null;WindowTracker? tracker=null;IconService? icons=null;
        app.Startup+=async(_,_)=>{
            try{
                icons=new IconService();var seed=new DockSettings{Theme=DockTheme.Glass,InterfaceTheme=InterfaceTheme.Light,StartWithWindows=StartupRegistration.ReadCommand()!=null};
                var store=new SettingsService(Path.Combine(directory,"isolated-data"));var model=new DockViewModel(seed,store,icons);tracker=new WindowTracker(app.Dispatcher);dock=new DockWindow(model,tracker);
                Window Create(DockSettings settings,int page=0){var palette=Activator.CreateInstance(PaletteType,new object[]{settings})!;var created=(Window)Activator.CreateInstance(SettingsType,new object[]{dock,palette,settings,page})!;((CheckBox)Field(created,"_live")).IsChecked=false;created.Topmost=!preview;return created;}
                window=Create(seed,dashboardOnly?8:0);window.Show();await Task.Delay(450);
                if(preview){window.Title="设置 · DockForWin · 独立预览（隔离配置）";Call(window,"ShowSetting","start-with-windows");await Task.Delay(320);var startup=Find<CheckBox>(window,"start-with-windows");startup.IsEnabled=false;startup.ToolTip="独立预览不修改系统开机启动注册；请在正式设置中调整。";Find<ListBox>(window,"settings-navigation").SelectedIndex=0;window.Closed+=(_,_)=>app.Shutdown();result=0;return;}
                var nav=Find<ListBox>(window,"settings-navigation");var host=(Grid)Field(window,"_pages");var draft=(DockSettings)Field(window,"_draft");
                var ranges=(Dictionary<string,Slider>)Field(window,"_ranges");var colors=(Dictionary<string,TextBox>)Field(window,"_colors");var ids=((IEnumerable<string>)Property(window,"SettingsControlIds")).ToHashSet();
                Assert(nav.Items.Count==8,"new primary navigation registered without losing legacy settings");
                nav.SelectedIndex=0;await Task.Delay(260);
                Assert((string)Property(host,"CurrentKey")=="dashboard"&&Tree<TextBlock>(window).Any(t=>t.Text=="6 类组件"),"dashboard reports six widget categories");
                Assert(Tree<Button>(window).Count(b=>System.Text.RegularExpressions.Regex.IsMatch(AutomationProperties.GetAutomationId(b),@"^dashboard-\d$"))==6,"six overview cards use shared surfaces");
                Capture(window,Path.Combine(directory,"00-dashboard.png"));
                var columns=((Grid)((Border)window.Content).Child).ColumnDefinitions;
                Assert(Math.Abs(columns[0].ActualWidth-240)<1,"sidebar uses 240 DIP");Click(window,"settings-sidebar-collapse");await Task.Delay(80);
                Assert(Math.Abs(columns[0].ActualWidth-76)<1,"sidebar folds while keeping navigation targets");nav.SelectedIndex=1;await Task.Delay(240);Assert((string)Property(host,"CurrentKey")=="control-center","collapsed sidebar navigates correctly");
                Click(window,"settings-sidebar-collapse");await Task.Delay(80);Assert(Math.Abs(columns[0].ActualWidth-240)<1,"sidebar restores its original width");
                Click(window,"settings-about");await Task.Delay(240);Assert((string)Property(host,"CurrentKey")=="about","bottom about action is reachable");Click(window,"settings-shell-back");await Task.Delay(280);
                nav.SelectedIndex=6;await Task.Delay(240);Click(window,"overview-apps");await Task.Delay(280);Assert((string)Property(host,"CurrentKey")=="apps"&&(bool)Property(host,"IsDetail"),"legacy application settings retained under general navigation");
                nav.SelectedIndex=5;await Task.Delay(280);Assert(Tree<TextBlock>(window).Any(t=>t.Text=="私有内存")&&Tree<TextBlock>(window).Any(t=>t.Text.EndsWith(" MB"))&&Tree<TextBlock>(window).Count(t=>t.Text=="尚未接入")>=4,"performance page distinguishes measured memory from unavailable rendering data");
                Capture(window,Path.Combine(directory,"00-performance.png"));
                nav.SelectedIndex=3;await Task.Delay(280);
                if(dashboardOnly){Click(window,"overview-icons");await Task.Delay(280);Assert((string)Property(host,"CurrentKey")=="icons","icon/reflection settings have a visible appearance entry");nav.SelectedIndex=0;await Task.Delay(240);Capture(window,Path.Combine(directory,"00-dashboard.png"));result=0;return;}
                Assert(ranges.Keys.Concat(colors.Keys).All(ids.Contains),"every original slider and color editor has a reachable route");
                Assert(seed.ControlCenter.Modules.All(m=>ids.Contains("module-"+m.Module)),"all existing module toggles remain reachable");
                Assert(ids.Contains("start-with-windows")&&!ids.Contains("control-center-hotkey")&&ids.Contains("dock-monitor"),"startup and monitor controls retained; removed hotkey field absent");
                Assert(!Tree<GlassButtonChrome>(window).Any(),"settings buttons use local native templates without glass sampling");
                Assert(Find<FrameworkElement>(window,"appearance-stage") is not null&&Find<CheckBox>(window,"liquid-glass") is not null&&Find<Slider>(window,"glass-quality") is not null,"appearance opens as an inspector with stage, liquid-glass switch and quality slider");
                await Task.Delay(400);Capture(window,Path.Combine(directory,"01-appearance.png"));
                Call(window,"ShowSetting","theme-mode");await Task.Delay(310);
                var segment=Find<ListBox>(window,"theme-mode-segment");segment.SelectedIndex=(int)DockTheme.Custom;await Task.Delay(160);
                Assert(draft.Theme==DockTheme.Custom&&seed.Theme==DockTheme.Glass,"segmented theme edits only the existing draft");
                var combo=(ComboBox)Field(window,"_preset");combo.IsDropDownOpen=true;await Task.Delay(150);Assert(combo.IsDropDownOpen,"native preset popup opens");Capture(window,Path.Combine(directory,"02-preset.png"));combo.IsDropDownOpen=false;combo.SelectedIndex=1;await Task.Delay(160);Assert(draft.Theme==DockTheme.Custom,"original preset handler remains active");
                segment.SelectedIndex=(int)DockTheme.Glass;
                Call(window,"ShowSetting","glass-frost");await Task.Delay(310);var sidebar=nav.PointToScreen(new Point());ranges["glass-frost"].Value=26.3;await Task.Delay(180);
                Assert(Math.Abs(draft.GlassFrost-.263)<.001&&Math.Abs(seed.GlassFrost-.263)>.001,"glass slider preserves fine precision and deep-copy isolation");
                Assert((bool)Property(host,"IsDetail")&&nav.SelectedIndex==3,"material controls use a secondary page in their original category");
                window.Height=540;await Task.Delay(150);var material=host.Children.OfType<ScrollViewer>().Single();material.ScrollToVerticalOffset(130);await Task.Delay(80);double offset=material.VerticalOffset;Assert(offset>0,"secondary settings scroll at minimum height");
                nav.SelectedIndex=4;await Task.Delay(240);Click(window,"settings-shell-back");await Task.Delay(310);Assert(ReferenceEquals(material,host.Children.OfType<ScrollViewer>().Single())&&Math.Abs(material.VerticalOffset-offset)<1&&nav.PointToScreen(new Point())==sidebar,"return preserves the cached page, scroll and fixed sidebar");
                Capture(window,Path.Combine(directory,"03-material-small.png"));
                Call(window,"ShowSetting","cc-panel-scale");await Task.Delay(310);ranges["cc-panel-scale"].Value=121;await Task.Delay(170);Assert(Math.Abs(draft.ControlCenter.Geometry.PanelScale-1.21)<.001&&Math.Abs(seed.ControlCenter.Geometry.PanelScale-1.21)>.001,"control-center geometry edits original isolated options");
                Call(window,"ShowSetting","control-material-override");await Task.Delay(310);var overrides=Find<CheckBox>(window,"control-material-override");overrides.IsChecked=false;await Task.Delay(170);Assert(!ranges["cc-frost"].IsEnabled,"inherited material disables individual controls");overrides.IsChecked=true;await Task.Delay(170);Assert(ranges["cc-frost"].IsEnabled,"material override re-enables individual controls");
                Call(window,"ShowSetting","module-"+seed.ControlCenter.Modules[0].Module);await Task.Delay(310);window.Height=780;await Task.Delay(100);Capture(window,Path.Combine(directory,"04-modules.png"));
                string moduleId="module-"+seed.ControlCenter.Modules[0].Module;Find<CheckBox>(window,moduleId).IsChecked=!seed.ControlCenter.Modules[0].Visible;await Task.Delay(170);Assert(draft.ControlCenter.Modules[0].Visible!=seed.ControlCenter.Modules[0].Visible,"module visibility keeps original callback and seed isolation");
                Call(window,"ShowSetting","background-color");await Task.Delay(310);colors["background-color"].Text="oops";await Task.Delay(170);Assert(!Find<Button>(window,"save-settings").IsEnabled,"invalid colors still block saving");colors["background-color"].Text="#F4F5F7";await Task.Delay(170);Assert(Find<Button>(window,"save-settings").IsEnabled,"valid colors restore save availability");
                for(int i=0;i<80;i++){nav.SelectedIndex=i%8;if(i%4==0)await Task.Delay(8);AssertFrame(host);}for(int wait=0;wait<20&&host.Children.Count>1;wait++)await Task.Delay(100);Console.WriteLine($"settle children={host.Children.Count}");Assert(host.Children.Count==1,"rapid navigation settles to one active page");
                // A repeated route before/after the current history index must keep forward history.
                nav.SelectedIndex=0;nav.SelectedIndex=1;nav.SelectedIndex=0;Call(host,"Back");Call(host,"Back");var state=Call(window,"CaptureNavigationState")!;string current=(string)Property(host,"CurrentKey");
                var restored=Create(draft);Call(restored,"RestoreNavigationState",state);restored.Show();await Task.Delay(330);var restoredHost=(Grid)Field(restored,"_pages");Assert((string)Property(restoredHost,"CurrentKey")==current&&(bool)Property(restoredHost,"CanGoForward"),"rebuild restores the exact repeated-route history position");restored.Close();
                window.Width=760;window.Height=540;nav.SelectedIndex=7;await Task.Delay(240);Capture(window,Path.Combine(directory,"05-small-window.png"));Assert(Find<Button>(window,"save-settings").IsVisible&&host.ActualWidth>400,"minimum window retains settings and save actions");
                foreach(InterfaceStyle style in Enum.GetValues<InterfaceStyle>()){
                    foreach(var mode in new[]{InterfaceTheme.Light,InterfaceTheme.Dark}){var themed=draft.Copy();themed.InterfaceStyle=style;themed.InterfaceTheme=mode;var sample=Create(themed,6);sample.Show();await Task.Delay(240);Assert(Find<ListBox>(sample,"settings-navigation").SelectedIndex==6,"palette "+style+" / "+mode+" loads full settings");if(style==default||mode==InterfaceTheme.Light)Capture(sample,Path.Combine(directory,$"theme-{style}-{mode}.png"));sample.Close();}
                }
                Assert(!Directory.EnumerateFiles(Path.Combine(directory,"isolated-data"),"settings.json",SearchOption.AllDirectories).Any(),"draft interaction does not write settings");
                window.Width=1040;window.Height=780;nav.SelectedIndex=2;await Task.Delay(240);Click(window,"save-settings");await Task.Delay(240);Assert(Math.Abs(model.Settings.GlassFrost-.263)<.001&&File.Exists(store.SettingsPath),"original save chain persists to isolated profile");
                using var process=Process.GetCurrentProcess();File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{categories=nav.Items.Count,ranges=ranges.Count,colors=colors.Count,reachableControls=ids.Count,cachedPages=Property(host,"CachedPageCount"),activePages=host.Children.Count,dpi=VisualTreeHelper.GetDpi(window).DpiScaleX,workingSetBytes=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,notes="Isolated full settings; no forced GC or GPU FPS claims. No real capture window shown; desktop preview disabled."},new JsonSerializerOptions{WriteIndented=true}));
                result=0;
            }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);result=1;}
            finally{if(!preview||result!=0){window?.Close();dock?.Close();tracker?.Dispose();icons?.Dispose();app.Shutdown(result);}}
        };app.Run();return result;
    }
    internal static int Rebuild(string directory)
    {
        SetProcessDpiAwarenessContext(-4);Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        using var icons=new IconService();var settings=new DockSettings{Theme=DockTheme.Glass,InterfaceTheme=InterfaceTheme.Light,StartWithWindows=StartupRegistration.ReadCommand()!=null};
        var model=new DockViewModel(settings,new SettingsService(Path.Combine(directory,"isolated-data")),icons);var tracker=new WindowTracker(app.Dispatcher);var dock=new DockWindow(model,tracker);
        app.Startup+=async(_,_)=>{try{
            var window=(Window)Activator.CreateInstance(SettingsType,new object[]{dock,Activator.CreateInstance(PaletteType,new object[]{settings})!,settings,0})!;
            void Adopt(Window value){dock.GetType().GetField("_settings",Flags)!.SetValue(dock,value);((CheckBox)Field(value,"_live")).IsChecked=false;}
            Adopt(window);window.Width=820;window.Height=580;window.Show();Call(window,"ShowSetting","glass-frost");await Task.Delay(350);
            var host=(Grid)Field(window,"_pages");var page=host.Children.OfType<ScrollViewer>().Single();page.ScrollToVerticalOffset(120);await Task.Delay(80);double offset=page.VerticalOffset;string route=(string)Property(host,"CurrentKey");
            ((Dictionary<string,Slider>)Field(window,"_ranges"))["glass-frost"].Value=35.2;
            var switched=((DockSettings)Field(window,"_draft")).Copy();switched.InterfaceTheme=InterfaceTheme.Dark;Call(dock,"ReopenSettings",switched,0);await Task.Delay(420);var next=(Window)Field(dock,"_settings");Adopt(next);var nextHost=(Grid)Field(next,"_pages");
            Assert(!window.IsVisible&&next.IsVisible&&(string)Property(nextHost,"CurrentKey")==route,"real theme callback rebuilds on the same secondary page");
            Assert(Math.Abs(next.Width-820)<1&&Math.Abs(next.Height-580)<1&&Math.Abs(nextHost.Children.OfType<ScrollViewer>().Single().VerticalOffset-offset)<1,"real rebuild retains window size and scroll offset");
            Assert(Math.Abs(((DockSettings)Field(next,"_draft")).GlassFrost-.352)<.001&&Math.Abs(model.Settings.GlassFrost-.352)>.01,"theme rebuild retains unsaved changes without committing");
            Click(next,"open-settings-preview");await Task.Delay(350);Assert((string)Property(nextHost,"CurrentKey")=="settings-preview-0"&&Find<FrameworkElement>(next,"wallpaper-preview").ActualWidth>100,"shared preview route renders the original wallpaper example");Capture(next,Path.Combine(directory,"06-full-preview.png"));
            Click(next,"settings-shell-back");await Task.Delay(310);Assert((string)Property(nextHost,"CurrentKey")==route,"return from preview restores the secondary page");
            Click(next,"cancel-settings");await Task.Delay(400);var cancelled=(Window)Field(dock,"_settings");Adopt(cancelled);Assert(!next.IsVisible&&Math.Abs(((DockSettings)Field(cancelled,"_draft")).GlassFrost-model.Settings.GlassFrost)<.001,"cancel rebuild discards the unsaved draft");
            Assert((string)Property(Field(cancelled,"_pages"),"CurrentKey")==route,"cancel keeps the current navigation location");
            Assert(!File.Exists(Path.Combine(directory,"isolated-data","settings.json")),"rebuild and cancel never write a profile");cancelled.Close();result=0;
        }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}finally{dock.Close();tracker.Dispose();app.Shutdown(result);}};app.Run();return result;
    }
    private static void AssertFrame(Grid host){if(host.Children.Count>2||host.Children.OfType<FrameworkElement>().Sum(p=>p.Opacity)<.25)throw new Exception("Rapid navigation drained page opacity or exceeded two active pages");}
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;

internal static class WidgetSystemChecks
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetProcessDpiAwarenessContext(nint value);
    static readonly Assembly App=typeof(WidgetOptions).Assembly;
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static string Output="";static int Passed,Failed;
    static object New(string type,params object?[] args)=>Activator.CreateInstance(App.GetType(type)!,All,null,args,null)!;
    static object? Call(object owner,string name,params object?[] args)=>owner.GetType().GetMethod(name,All)!.Invoke(owner,args);
    static object? Prop(object owner,string name)=>owner.GetType().GetProperty(name,All)!.GetValue(owner);
    static void Check(bool value,string message){Console.WriteLine((value?"PASS ":"FAIL ")+message);if(value)Passed++;else Failed++;}
    internal static int Run(string[] args)
    {
        SetProcessDpiAwarenessContext(-4);Output=args[0];Directory.CreateDirectory(Output);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        if(args.Contains("--preview")){app.Startup+=(_,_)=>Preview(app);return app.Run();}
        Visual=args.Contains("--visual");app.Startup+=async(_,_)=>{try{if(args.Contains("--desktop-only"))await Desktop(new());else await Run(args.Contains("--desktop"));if(args.Contains("--live")){var service=new WidgetWeatherService();var snapshot=await service.ReadAsync(39.9042,116.4074,default);var cached=await service.ReadAsync(39.9042,116.4074,default);Check(snapshot.Hours.Count>12&&snapshot.Days.Count==5,"live Open-Meteo includes real hourly and daily forecasts");Check(ReferenceEquals(snapshot,cached),"weather requests reuse the shared location cache");File.WriteAllText(Path.Combine(Output,"live-weather.json"),JsonSerializer.Serialize(snapshot));}}catch(Exception e){Failed++;Console.WriteLine(e);File.WriteAllText(Path.Combine(Output,"exception.txt"),e.ToString());}finally{File.WriteAllText(Path.Combine(Output,"result.json"),JsonSerializer.Serialize(new{Passed,Failed}));app.Shutdown(Failed>0?1:0);}};
        return app.Run();
    }
    static async void Preview(Application app)
    {
        string profile=Path.Combine(Output,"profile");var store=new SettingsService(profile);var settings=File.Exists(store.SettingsPath)?store.Load():new DockSettings{InterfaceTheme=InterfaceTheme.Light};settings.StartWithWindows=StartupRegistration.ReadCommand()!=null;
        var icons=new IconService();var model=new DockViewModel(settings,store,icons);var tracker=new WindowTracker(app.Dispatcher);var dock=new DockForWindows.Views.DockWindow(model,tracker);var service=New("DockForWindows.Views.WidgetDesktopService",model);
        var palette=New("DockForWindows.Views.Palette",settings);var window=(Window)New("DockForWindows.Views.SettingsWindow",dock,palette,settings,9);((CheckBox)window.GetType().GetField("_live",All)!.GetValue(window)!).IsChecked=false;
        window.Title="桌面小组件 · 新版隔离预览";window.Closed+=(_,_)=>{((IDisposable)service).Dispose();dock.Close();tracker.Dispose();icons.Dispose();app.Shutdown();};
        window.Show();window.Activate();Call(window,"ShowSetting","start-with-windows");await Task.Delay(300);if(!window.IsLoaded)return;var navigation=typeof(DockForWindows.Views.DockWindow).Assembly.GetType("DockForWindows.Views.SettingsShellWindow")!.GetField("_navigation",All)!.GetValue(window) as ListBox;
        var startup=Find<CheckBox>(window,"start-with-windows");if(startup!=null)startup.IsEnabled=false;
        navigation!.SelectedIndex=2;
        Console.WriteLine("PREVIEW "+profile);
    }
    static T? Find<T>(DependencyObject root,string id)where T:DependencyObject=>Tree<T>(root).FirstOrDefault(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)==id);
    static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T value)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    static async Task Run(bool desktop)
    {
        Check(Enum.GetValues<WidgetKind>().All(k=>WidgetRegistry.Definitions.Any(d=>d.Kind==k)),"all six categories have registered templates");
        var options=new WidgetOptions();options.Tasks.Add(new(){Title="整理本周项目",Detail="检查日程与交接记录",List="工作",Due=DateTime.Today.AddHours(18)});options.Tasks.Add(new(){Title="读完正在读的书",List="个人"});options.Notes.Add(new(){Title="今天的想法",Body="留住值得记录的事情。\n让桌面留一点从容。",Modified=DateTime.Now});options.Events.Add(new(){Title="项目评审",Start=DateTime.Today.AddDays(1).AddHours(10)});
        var instance=WidgetRegistry.CreateInstance("calendar-giant",WidgetMaterial.Light,options);options.Instances.Add(instance);var copy=options.Copy();copy.Instances[0].Source.City="厦门";copy.Tasks[0].Completed=true;copy.Notes[0].Body="changed";
        Check(options.Instances[0].Source.City!="厦门"&&!options.Tasks[0].Completed&&options.Notes[0].Body!="changed","instances, sources, tasks and notes copy independently");
        var baseline=options.Copy();var draft=options.Copy();var live=options.Copy();live.Tasks[0].Completed=true;live.Instances[0].Column=3;draft.Notes[0].Body="draft edit";draft.MergeLive(baseline,live);
        Check(draft.Tasks[0].Completed&&draft.Instances[0].Column==3&&draft.Notes[0].Body=="draft edit","stale settings save merges live task state and positions with draft edits");
        string json=JsonSerializer.Serialize(draft);var loaded=JsonSerializer.Deserialize<WidgetOptions>(json)!;loaded.Normalize();Check(loaded.Instances.Count==1&&loaded.Instances[0].Size==new WidgetSize(4,2)&&loaded.Tasks[0].Completed,"full widget layout round-trips without recursive configuration");
        var days=Enumerable.Range(0,5).Select(i=>new WidgetForecastDay(DateOnly.FromDateTime(DateTime.Today.AddDays(i)),i==1?61:0,24+i,16+i)).ToArray();
        var weather=new WidgetWeatherSnapshot(22,0,true,DateTime.Now,DateTimeOffset.Now,days){Hours=Enumerable.Range(0,12).Select(i=>new WidgetForecastHour(DateTime.Now.AddHours(i),i%3==0?2:0,22+i%4)).ToArray()};
        var media=new DockForWindows.Services.MediaState("真实媒体接口布局测试","离线测试替身",true,true,true,true,null,TimeSpan.FromSeconds(34),TimeSpan.FromMinutes(3),true);
        var data=New("DockForWindows.Views.WidgetRenderData",options,new WidgetSource{City="布局测试城市"},weather,media,null,null,true,null,null,null,null,null,null,null,null);
        var renderer=App.GetType("DockForWindows.Views.WidgetContent")!.GetMethod("Render",All)!;
        var window=new Window{Title="小组件隔离视觉检查",Width=1000,Height=760,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Brushes.LightGray,ShowInTaskbar=false};var root=new Grid{Background=Brushes.White};window.Content=root;window.Show();
        int count=0;foreach(var d in WidgetRegistry.Definitions){foreach(var size in WidgetSizes.For(d)){
            var host=(FrameworkElement)New("DockForWindows.Views.WidgetHost",d with {Size=size},2.5);host.HorizontalAlignment=HorizontalAlignment.Left;host.VerticalAlignment=VerticalAlignment.Top;Call(host,"SetMaterial",WidgetMaterial.Light,null);Call(host,"SetContent",renderer.Invoke(null,new[]{(object)(d with{Size=size}),data}));root.Children.Clear();root.Children.Add(host);await Dispatcher.Yield(DispatcherPriority.Background);window.UpdateLayout();
            Check(host.ActualWidth==size.Width&&host.ActualHeight==size.Height&&host.IsMeasureValid,"render "+d.Id+" "+size);
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(size.Width),(int)Math.Ceiling(size.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(host);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(Output,d.Id+"-"+size.Columns+"x"+size.Rows+".png"));encoder.Save(stream);count++;
        }}Check(count>100,"all supported layouts render at their true logical sizes");
        var gallery=(FrameworkElement)New("DockForWindows.Views.WidgetGalleryPage",options,null);root.Children.Clear();root.Children.Add(gallery);await Task.Delay(700);window.UpdateLayout();Check((bool)Prop(gallery,"IsUpdating")!,"gallery starts its shared lifecycle timer");Capture(root,"gallery.png");
        root.Children.Clear();await Task.Delay(100);Check(!(bool)Prop(gallery,"IsUpdating")!,"gallery unload stops updates");window.Close();
        var shell=(Window)New("DockForWindows.Views.SettingsShellWindow",new DockSettings{Widgets=options,InterfaceTheme=InterfaceTheme.Light},new Action<DockSettings,int>((_,_)=>{}),9);shell.Show();await Task.Delay(650);shell.UpdateLayout();Capture((FrameworkElement)shell.Content,"styled-gallery.png");shell.Close();
        if(desktop)await Desktop(options);
    }
    static void Capture(FrameworkElement target,string file){var bitmap=new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth),(int)Math.Ceiling(target.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(target);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(bitmap));using var f=File.Create(Path.Combine(Output,file));e.Save(f);}
    // Glass over the desktop must show the widget, not the wallpaper behind it: the desktop window's own capture leaves
    // its layered child windows out, so the widget's published pixels are drawn over it.
    static bool Visual;
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern nint WindowFromPoint(System.Drawing.Point point);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern nint GetAncestor(nint window,uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool GetWindowRect(nint window,out System.Drawing.Rectangle rect);
    // Hardware check: waits (up to 3 min) for someone to show the desktop, then compares the screen with the widget.
    static async Task OnScreen(object window)
    {
        nint handle=(nint)window.GetType().GetField("_handle",All)!.GetValue(window)!;Console.WriteLine("floating="+window.GetType().GetField("_floating",All)!.GetValue(window));
        GetWindowRect(handle,out var r);var centre=new System.Drawing.Point((r.Left+r.Width)/2,(r.Top+r.Height)/2);Console.WriteLine("SHOW THE DESKTOP NOW");
        var deadline=DateTime.UtcNow.AddMinutes(3);bool uncovered=false;
        while(DateTime.UtcNow<deadline){await Task.Delay(250);var hit=WindowFromPoint(centre);if(hit==handle||GetAncestor(hit,2)==handle||GetAncestor(hit,2)==(nint)GetShellWindow()){uncovered=true;break;}}
        Check(uncovered,"desktop at the widget was shown within 3 minutes");if(!uncovered)return;await Task.Delay(800);
        var e=(FrameworkElement)((Window)window).Content;var dpi=VisualTreeHelper.GetDpi(e);int w=r.Width-r.Left,h=r.Height-r.Top;
        var rtb=new RenderTargetBitmap(w,h,96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);rtb.Render(e);var own=new byte[4];rtb.CopyPixels(new Int32Rect(w/2,h/2,1,1),own,4,0);
        using var screen=new System.Drawing.Bitmap(1,1);using(var g=System.Drawing.Graphics.FromImage(screen))g.CopyFromScreen(centre,System.Drawing.Point.Empty,new System.Drawing.Size(1,1));var s=screen.GetPixel(0,0);
        using var shot=new System.Drawing.Bitmap(w,h);using(var g=System.Drawing.Graphics.FromImage(shot))g.CopyFromScreen(r.Left,r.Top,0,0,new System.Drawing.Size(w,h));shot.Save(Path.Combine(Output,"on-screen.png"));
        Console.WriteLine($"widget centre {own[2]},{own[1]},{own[0]} screen {s.R},{s.G},{s.B}");
        Check(Math.Abs(own[2]-s.R)+Math.Abs(own[1]-s.G)+Math.Abs(own[0]-s.B)<12,"the widget is actually drawn on the screen, not only laid out");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern nint GetShellWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern nint GetWindow(nint window,uint command);
    static async Task BackdropSeesWidget(object window)
    {
        nint handle=(nint)window.GetType().GetField("_handle",All)!.GetValue(window)!;DesktopWidgetLayers.Snapshot? snapshot=null;
        // On a desktop that cannot draw other processes' children the widget is a top-level window just above the
        // desktop, sampled like any window; only the z-order is checked here.
        if((bool)window.GetType().GetField("_floating",All)!.GetValue(window)!){await Task.Delay(300);nint above=handle;while(above!=0&&above!=GetShellWindow())above=GetWindow(above,2);int gap=0;for(nint w=GetWindow(handle,2);w!=0&&w!=GetShellWindow();w=GetWindow(w,2))gap++;Console.WriteLine("windows between widget and desktop: "+gap);Check(above==GetShellWindow()&&GetAncestor(handle,2)==handle,"top-level desktop widget stays above the desktop window");return;}
        for(int i=0;i<40&&snapshot==null;i++){await Task.Delay(50);snapshot=DesktopWidgetLayers.Sources.FirstOrDefault(s=>s.Handle==handle)?.Latest;}
        Check(snapshot!=null,"desktop widget publishes its rendered pixels for glass sampling");if(snapshot==null)return;
        var b=snapshot.Bounds;var area=new System.Drawing.Rectangle(b.Left+b.Width/4,b.Top+b.Height/4,b.Width/2,b.Height/2);
        uint Centre(byte[] pixels,int width,int x,int y)=>BitConverter.ToUInt32(pixels,(y*width+x)*4);
        uint widget=Centre(snapshot.Pixels,b.Width,b.Width/2,b.Height/2);
        async Task<uint?> Sample(){using var glass=new GlassBackdropService();for(int i=0;i<20;i++){if(glass.ReadPixels(0,area,1) is {} frame){int pad=24;return Centre(frame.Pixels,frame.Width,pad+area.Width/2,pad+area.Height/2);}await Task.Delay(50);}return null;}
        var with=await Sample();var source=DesktopWidgetLayers.Sources.First(s=>s.Handle==handle);DesktopWidgetLayers.Remove(source);var without=await Sample();DesktopWidgetLayers.Add(source);
        Console.WriteLine($"widget centre {widget:X8}, sampled with widget {with:X8}, desktop only {without:X8}");
        Check(widget>>24==255&&with==widget,"glass sampling the desktop shows the widget's own pixels");
        Check(without!=null&&without!=widget,"the desktop capture alone does not contain the widget (the case this layer fixes)");
    }
    static async Task Desktop(WidgetOptions seed)
    {
        string? startup=StartupRegistration.ReadCommand();var store=new SettingsService(Path.Combine(Output,"profile"));var settings=new DockSettings{StartWithWindows=startup!=null};settings.Widgets.Tasks=seed.Tasks.Select(t=>t.Copy()).ToList();settings.Widgets.Notes=seed.Notes.Select(n=>n.Copy()).ToList();using var icons=new IconService();var model=new DockViewModel(settings,store,icons);
        var service=New("DockForWindows.Views.WidgetDesktopService",model);
        service.GetType().GetEvent("Error",All)!.GetAddMethod(true)!.Invoke(service,new object[]{new Action<string>(message=>Console.WriteLine("DESKTOP ERROR "+message))});
        try{
            var layout=App.GetType("DockForWindows.Services.WidgetDesktopLayout")!;var bounds=await(Task<IReadOnlyList<Rect>>)layout.GetMethod("Icons",All)!.Invoke(null,null)!;Check(bounds.Count>=0,"actual Explorer desktop icon bounds queried");
            var widget=WidgetRegistry.CreateInstance("clock-classic",WidgetMaterial.Light);bool added=await(Task<bool>)Call(service,"AddAsync",widget)!;Check(added&&model.Settings.Widgets.Instances.Count==1,"actual desktop child widget added and saved");if(!added)return;
            var windows=(System.Collections.IDictionary)service.GetType().GetField("_windows",All)!.GetValue(service)!;var first=windows[widget.InstanceId]!;Check((bool)Prop(first,"Attached")!,"widget HWND parent belongs to Explorer desktop");
            if(Visual)await OnScreen(first);else await BackdropSeesWidget(first);
            Call(service,"Duplicate",model.Settings.Widgets.Instances[0]);await Task.Delay(500);Check(model.Settings.Widgets.Instances.Count==2,"duplicate creates an independent desktop instance");
            var current=model.Settings.Widgets.Copy();current.Instances[0].Locked=true;current.Instances[0].Source.TimeZone="GMT Standard Time";model.TrySaveWidgets(current);Check(((WidgetInstance)Prop(first,"Instance")!).Locked,"instance settings propagate without replacing Dock");
            var beforeRestore=model.Settings.Widgets.Copy();((IDisposable)service).Dispose();var restoredStore=new SettingsService(Path.Combine(Output,"profile"));var restored=restoredStore.Load();Check(restored.Widgets.Instances.Count==2&&restored.Widgets.Instances[0].Locked&&restored.Widgets.Instances[0].Source.TimeZone=="GMT Standard Time","independent instance settings and positions survive profile reload");
            service=New("DockForWindows.Views.WidgetDesktopService",model);service.GetType().GetEvent("Error",All)!.GetAddMethod(true)!.Invoke(service,new object[]{new Action<string>(m=>Console.WriteLine("DESKTOP ERROR "+m))});
            windows=(System.Collections.IDictionary)service.GetType().GetField("_windows",All)!.GetValue(service)!;Check(windows.Count==2&&windows.Values.Cast<object>().All(w=>(bool)Prop(w,"Attached")!),"desktop service restart restores both Explorer child windows");
            var changed=model.Settings.Widgets.Instances[0];Call(service,"Change",changed.InstanceId,new Action<WidgetInstance>(item=>item.Size=new(4,4)));await Task.Delay(500);Check(model.Settings.Widgets.Instances[0].Size==new WidgetSize(4,4),"desktop resizing validates the larger footprint and saves");
            var screen=System.Windows.Forms.Screen.AllScreens.First(s=>s.DeviceName==model.Settings.Widgets.Instances[0].Monitor);double dpi=(double)layout.GetMethod("Dpi",All)!.Invoke(null,new object[]{screen})!;
            await(Task)Call(service,"Place",windows[changed.InstanceId],new Point(screen.WorkingArea.Right-450*dpi/96,screen.WorkingArea.Top+40*dpi/96))!;Check(model.Settings.Widgets.Instances[0].Column>=0&&model.Settings.Widgets.Instances[0].Row>=0,"desktop movement snaps to a valid saved grid cell");
            var rectangles=model.Settings.Widgets.Instances.Select(item=>(Rect)layout.GetMethod("Bounds",All)!.Invoke(null,new object[]{item,screen,dpi})!).ToArray();Check(!rectangles[0].IntersectsWith(rectangles[1])&&rectangles.All(r=>bounds.All(icon=>!r.IntersectsWith(icon))),"actual desktop footprints avoid each other and current Explorer icons");
            foreach(var item in model.Settings.Widgets.Instances.ToArray())Call(service,"Delete",item.InstanceId);Check(model.Settings.Widgets.Instances.Count==0&&windows.Count==0,"delete closes desktop windows and persists layout");
            Check(StartupRegistration.ReadCommand()==startup,"desktop operations preserve exact startup registration");
        }finally{((IDisposable)service).Dispose();}
    }
}


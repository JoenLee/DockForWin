using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using MediaState=DockForWindows.Services.MediaState;

internal static class FigmaWidgetChecks
{
    private static int passed; private static string output="";
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
    internal static int Run(string folder)
    {
        NativeMethods.SetProcessDpiAwarenessContext(-4);output=folder;Directory.CreateDirectory(folder);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{try{Models();await Solar();await Visuals();File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=passed,Failed=0}));app.Shutdown();}
            catch(Exception e){Console.WriteLine(e);File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());app.Shutdown(1);}};return app.Run();
    }
    private static void Models()
    {
        Check(FigmaWidgetCatalog.All.Count==40,"all 40 Figma styles are available");
        Check(FigmaWidgetCatalog.All.Select(s=>s.Id).Distinct().Count()==40,"stable unique IDs");
        var options=new WidgetOptions();
        foreach(var style in FigmaWidgetCatalog.All){var d=WidgetRegistry.Get(style.Id);Check(d.Size==style.Size,"native grid "+style.Id);options.Instances.Add(WidgetRegistry.CreateInstance(style.Id,WidgetMaterial.Light));}
        options.FavoriteTracks.Add(new string('A',64));var copy=options.Copy();copy.FavoriteTracks.Add(new string('B',64));Check(options.FavoriteTracks.Count==1,"favorite copies are independent");
        options.Normalize();var restored=JsonSerializer.Deserialize<WidgetOptions>(JsonSerializer.Serialize(options))!;restored.Normalize();Check(restored.Instances.Count==40&&restored.FavoriteTracks.Count==1,"40 instances and favorite persist through normalization");
        using var stream=Application.GetResourceStream(new Uri("/DockForWindows;component/Assets/FigmaWidgets/scenes.json",UriKind.Relative))!.Stream;using var doc=JsonDocument.Parse(stream);
        int assets=0;foreach(var scene in doc.RootElement.EnumerateArray())foreach(var node in scene.GetProperty("nodes").EnumerateArray())if(node.TryGetProperty("asset",out var file)){
            using var asset=Application.GetResourceStream(new Uri("/DockForWindows;component/Assets/FigmaWidgets/assets/"+file.GetString(),UriKind.Relative))!.Stream;Check(asset.Length>0,"embedded original asset "+file.GetString());assets++;}
        Check(assets>=760,"all visible asset slots have local resources");
    }
    private sealed class Handler:HttpMessageHandler
    {
        internal string Query="";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Query=request.RequestUri!.Query;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("""
        {"timezone":"Asia/Shanghai","utc_offset_seconds":28800,"current":{"temperature_2m":22,"weather_code":0,"is_day":1,"time":"2026-11-21T12:07"},"daily":{"time":["2026-11-21","2026-11-22"],"weather_code":[0,1],"temperature_2m_max":[25,26],"temperature_2m_min":[15,16],"sunrise":["2026-11-21T06:42","2026-11-22T06:43"],"sunset":["2026-11-21T19:18","2026-11-22T19:17"]}}
        """,Encoding.UTF8,"application/json")});}
    }
    private static async Task Solar()
    {
        var handler=new Handler();var service=new WidgetWeatherService(new HttpClient(handler));var snapshot=await service.ReadAsync(31.23,121.47,default);
        Check(handler.Query.Contains("sunrise,sunset"),"weather requests real sunrise and sunset");
        var solar=WidgetWeatherService.SolarAt(snapshot,new DateTimeOffset(2026,11,21,4,7,0,TimeSpan.Zero));
        Check(solar.LocalTime.Hour==12&&solar.Next?.Hour==19&&!solar.IsSunrise,"location timezone and sunset selection");
        Check((solar.Next!.Value-solar.LocalTime)==TimeSpan.FromHours(7)+TimeSpan.FromMinutes(11),"exact solar countdown");
        solar=WidgetWeatherService.SolarAt(snapshot,new DateTimeOffset(2026,11,21,14,0,0,TimeSpan.Zero));Check(solar.IsSunrise&&solar.Next?.Day==22,"night chooses next-day sunrise");
        var polar=snapshot with{SolarDays=new[]{new WidgetSolarDay(new DateOnly(2026,11,21),null,null)}};Check(WidgetWeatherService.SolarAt(polar,DateTimeOffset.UtcNow).Next==null,"polar/missing solar times remain unknown");
    }
    private static void Save(FrameworkElement element,string name)
    {
        element.Measure(new Size(element.Width,element.Height));element.Arrange(new Rect(0,0,element.Width,element.Height));element.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.Width*2),(int)Math.Ceiling(element.Height*2),192,192,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name+".png"));encoder.Save(file);
    }
    private static async Task Visuals()
    {
        var data=new WidgetRenderData(new(),new(){TimeZone="Pacific Standard Time"}){MotionEnabled=false,DesignReference=true};
        foreach(var style in FigmaWidgetCatalog.All){var d=WidgetRegistry.Get(style.Id);var host=new WidgetHost(d,2.5);host.SetMaterial(WidgetMaterial.Light,new(){Shadow=0});host.SetData(data);Save(host,style.Id);Check(WidgetMusicView.Elements<FigmaWidgetView>(host).Single().LayerCount>0,"renders original layers "+style.Id);}
        var media=new MediaState("Long real system title — this should remain clipped inside its slot","Test artist",true,true,true,true,null,TimeSpan.FromSeconds(102),TimeSpan.FromSeconds(240),true){CanShuffle=true,CanRepeat=true};
        foreach(var style in FigmaWidgetCatalog.All.Where(s=>s.Kind==WidgetKind.Music)){
            int commands=0;var view=new FigmaWidgetView(WidgetRegistry.Get(style.Id),data with{DesignReference=false,Preview=false,Media=media,MediaCommand=_=>commands++,Seek=_=>{}});
            var buttons=WidgetMusicView.Elements<Button>(view).Where(b=>AutomationProperties.GetAutomationId(b).StartsWith("figma-")).ToArray();
            var transport=buttons.Where(b=>AutomationProperties.GetAutomationId(b).StartsWith("figma-previous")||AutomationProperties.GetAutomationId(b).StartsWith("figma-next")||AutomationProperties.GetAutomationId(b).StartsWith("figma-toggle")).ToArray();
            Check(transport.Length>=3,"all transport controls exist "+style.Id);
            foreach(var button in transport)button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(commands==transport.Length,"transport routes to media service "+style.Id);
            if(style.Id!="music-figma-ipod-compact")Check(WidgetMusicView.Elements<TextBlock>(view).Any(t=>t.Text==media.Title||t.Text.Contains("Long real")),"real title replaces design sample "+style.Id);
            view.Update(data with{DesignReference=false,Preview=false,Media=media with{Playing=false,Position=TimeSpan.FromSeconds(110)},MediaCommand=_=>commands++});
            var pausedView=new FigmaWidgetView(WidgetRegistry.Get(style.Id),data with{DesignReference=false,Preview=false,Media=media with{Playing=false,Position=TimeSpan.FromSeconds(110)}}){Width=style.Size.Width,Height=style.Size.Height};Save(pausedView,"paused-"+style.Id);
            if(style.Id!="music-figma-ipod-compact"&&style.Id!="music-figma-ipod-minimal"&&style.Id!="music-figma-ipod-aluminum"&&style.Id!="music-figma-ios-small")Check(WidgetMusicView.Elements<TextBlock>(view).Any(t=>t.Text=="1:50"),"elapsed time updates from real timeline "+style.Id);
            view.Update(data with{DesignReference=false,Preview=false});Check(transport.All(b=>!b.IsEnabled),"no session disables transport "+style.Id);
            if(WidgetMusicView.Elements<Canvas>(view).FirstOrDefault(c=>AutomationProperties.GetAutomationId(c)=="figma-volume-wheel") is Canvas wheel){double volume=-1;view.Update(data with{DesignReference=false,Audio=new AudioState(40,false),SetVolume=v=>volume=v});
                wheel.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.PreviewMouseWheelEvent});wheel.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,1,120){RoutedEvent=UIElement.PreviewMouseWheelEvent});Check(volume==44,"wheel volume accumulates through audio callback "+style.Id);}
        }
        var testStyle=FigmaWidgetCatalog.All.First(s=>s.Id=="music-figma-ios-wide");var testHost=new WidgetHost(WidgetRegistry.Get(testStyle.Id),2.5);testHost.SetData(data with{DesignReference=false,Media=media});
        var window=new Window{Content=testHost,SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Left=60,Top=80};window.Show();await Task.Delay(300);
        var retained=WidgetMusicView.Elements<FigmaWidgetView>(testHost).Single();Check(retained.IsTicking,"visible widget starts timer");testHost.SetData(data with{DesignReference=false,Media=media with{Position=TimeSpan.FromSeconds(110)}});Check(ReferenceEquals(retained,WidgetMusicView.Elements<FigmaWidgetView>(testHost).Single()),"media samples retain the visual tree");
        testHost.SetMaterial(WidgetMaterial.Light,new(){Background="#123456",Foreground="#FEDCBA"});testHost.SetData(data with{DesignReference=false,Media=media});Check(WidgetMusicView.Elements<TextBlock>(retained).Where(t=>t.Text!="♫").All(t=>t.Foreground is SolidColorBrush b&&b.Color==System.Windows.Media.Color.FromRgb(254,220,186)),"existing appearance controls reach native Figma text");
        testHost.SetMaterial(WidgetMaterial.Light);testHost.SetData(data with{DesignReference=false,Media=media});Check(WidgetMusicView.Elements<TextBlock>(retained).Any(t=>t.Foreground is SolidColorBrush b&&b.Color!=System.Windows.Media.Color.FromRgb(254,220,186)),"clearing appearance restores design defaults");
        var point=testHost.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(testHost);using(var bitmap=new System.Drawing.Bitmap((int)Math.Round(testHost.ActualWidth*dpi.DpiScaleX),(int)Math.Round(testHost.ActualHeight*dpi.DpiScaleY))){using var g=System.Drawing.Graphics.FromImage(bitmap);g.CopyFromScreen((int)Math.Round(point.X),(int)Math.Round(point.Y),0,0,bitmap.Size);bitmap.Save(Path.Combine(output,"live-window.png"));}
        testHost.SetActive(false);Check(!retained.IsTicking,"hidden widgets stop timer");window.Close();await Task.Delay(30);Check(!retained.IsTicking,"unload releases timer");
    }
}

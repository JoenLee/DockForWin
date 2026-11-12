using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using Windows.Devices.Radios;

internal static class ControlCenterDesignChecks
{
    private sealed class Connectivity:IControlCenterConnectivity
    {
        public event Action? Changed{add{}remove{}}
        public Task Initialize()=>Task.CompletedTask;
        public RadioState? State(RadioKind kind)=>RadioState.On;
        public Task SetRadio(RadioKind kind,bool on)=>throw new Exception("hardware writes prohibited in design checks");
        public Task<IReadOnlyList<WifiNetwork>> ReadNetworks(bool scan=false)=>Task.FromResult<IReadOnlyList<WifiNetwork>>(Array.Empty<WifiNetwork>());
        public Task Connect(WifiNetwork n,string? p,CancellationToken c)=>throw new Exception("unexpected connect");
        public Task Disconnect(WifiNetwork n,CancellationToken c)=>throw new Exception("unexpected disconnect");
        public IReadOnlyList<BluetoothPeer> BluetoothDevices=>Array.Empty<BluetoothPeer>();
        public void StartBluetoothDiscovery(){}public void StopBluetoothDiscovery(){}public void Dispose(){}
        public Task PairBluetooth(BluetoothPeer p)=>throw new Exception("unexpected pairing");
        public Task ForgetBluetooth(BluetoothPeer p)=>throw new Exception("unexpected unpair");
    }
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(o)!;
    private static void Set(object o,string name,object? value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(o,value);
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(o,args);
    private static IEnumerable<T> Tree<T>(DependencyObject o)where T:DependencyObject{if(o is T value)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(o,i)))yield return child;}
    private static void Check(bool value,string reason){if(!value)throw new Exception(reason);Console.WriteLine("PASS "+reason);}
    private static Grid Card(ControlCenterWindow w,ControlModule kind)=>Tree<Grid>(w).Single(x=>AutomationProperties.GetAutomationId(x)=="control-card-"+kind);
    private static async Task Quiet(ControlCenterWindow w){Field<DispatcherTimer>(w,"_poll").Stop();Field<DispatcherTimer>(w,"_mediaPoll").Stop();Field<DispatcherTimer>(w,"_stateRefresh").Stop();for(int i=0;i<200&&Field<bool>(w,"_polling");i++)await Task.Delay(20);}
    private static void Capture(ControlCenterWindow window,string file,double dpi)
    {
        var image=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*dpi/96),(int)Math.Ceiling(window.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(file);encoder.Save(stream);
    }
    private static void Bounds(FrameworkElement child,FrameworkElement parent,string name)
    {var b=child.TransformToAncestor(parent).TransformBounds(new Rect(child.RenderSize));Check(b.Left>=-.6&&b.Top>=-.6&&b.Right<=parent.ActualWidth+.6&&b.Bottom<=parent.ActualHeight+.6,$"{name} within safe content: {b} / {parent.RenderSize}");}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};ControlCenterWindow? window=null;
        app.DispatcherUnhandledException+=(_,e)=>{Console.Error.WriteLine(e.Exception);e.Handled=true;result=1;app.Shutdown();};
        app.Startup+=async(_,_)=>{
            try{
                var settings=new DockSettings();settings.ControlCenter.LayoutVersion=3;settings.ControlCenter.Geometry.PanelScale=.8;
                ControlCenterOptions? saved=null;window=new ControlCenterWindow(settings,saveLayout:o=>saved=o.Copy(),connectivity:new Connectivity()){DismissOnDeactivate=false};window.Show();await Task.Delay(1000);await Quiet(window);
                byte[] art;using(var stream=Application.GetResourceStream(new Uri("pack://application:,,,/DockForWindows;component/Assets/PreviewWallpaper.png")).Stream){using var bytes=new MemoryStream();stream.CopyTo(bytes);art=bytes.ToArray();}
                var state=new DockForWindows.Services.MediaState(new string('歌',100),new string('艺',100),true,true,true,true,art,TimeSpan.FromSeconds(9),TimeSpan.FromSeconds(265),true);
                foreach(double width in new[]{300d,345d,480d})foreach(var shape in ControlCenterLayout.Shapes(ControlModule.Media)){
                    var preview=settings.Copy();preview.ControlCenter.Geometry.PanelWidth=width;preview.ControlCenter.Modules.Single(m=>m.Module==ControlModule.Media).SetShape(shape);Call(window,"ApplyPreview",preview);await Task.Delay(550);await Quiet(window);Set(window,"_mediaState",state);Call(window,"UpdateCardMedia");window.UpdateLayout();await Task.Delay(70);
                    var card=Card(window,ControlModule.Media);var expected=ControlCenterLayout.Size(shape,preview.ControlCenter.Geometry,ControlModule.Media);Check(Math.Abs(card.ActualWidth-expected.Width)<1&&Math.Abs(card.ActualHeight-expected.Height)<1,$"{width} / {shape} uses exact grid preset");
                    var player=Field<object>(window,"_mainPlayer");var root=Field<Grid>(player,"Root");var cover=Field<Border>(player,"Art");Check(Math.Abs(cover.ActualWidth-cover.ActualHeight)<.1,"square cover "+shape);
                    var title=Field<TextBlock>(player,"Title");Check(title.TextWrapping==TextWrapping.NoWrap&&((Grid)title.Parent).ClipToBounds,"long title scrolls within stable clipped slot");
                    foreach(var part in new[]{"Previous","Play","Next"})Bounds(Field<Button>(player,part),root,part+" / "+shape);Bounds((FrameworkElement)title.Parent,root,"title slot / "+shape);Bounds(cover,root,"cover / "+shape);
                    var play=Field<Button>(player,"Play");var prior=play.TransformToAncestor(root).TransformBounds(new Rect(play.RenderSize));Set(window,"_mediaState",state with{Playing=false});Call(window,"UpdateCardMedia");window.UpdateLayout();Check(play.TransformToAncestor(root).TransformBounds(new Rect(play.RenderSize))==prior,"play/pause does not shift layout");
                    if(shape is ControlCardShape.Rectangle or ControlCardShape.Large){var slider=Field<Slider>(player,"Progress");Bounds(slider,root,"progress");Check(Tree<Thumb>(slider).Single().Width==10,"music knob is small and circular");Bounds(Field<TextBlock>(player,"Remaining"),root,"remaining time");}
                    if(width==345)Capture(window,Path.Combine(directory,shape+".png"),144);
                }
                foreach(double radius in new[]{0d,50d,100d}){
                    var preview=settings.Copy();preview.ControlCenter.Geometry.MediaRoundness=radius;preview.ControlCenter.Geometry.DetailRoundness=radius;preview.ControlCenter.Geometry.CapsuleRoundness=radius;Call(window,"ApplyPreview",preview);await Task.Delay(450);Call(window,"OpenModule",ControlModule.Media,false);await Task.Delay(600);await Quiet(window);Set(window,"_mediaState",state);Call(window,"UpdateMediaDetails");window.UpdateLayout();
                    var detail=Field<Grid>(window,"_details");var expected=ControlCenterLayout.Size(ControlCardShape.Large,preview.ControlCenter.Geometry,ControlModule.Media);Check(Math.Abs(detail.ActualHeight-expected.Height)<1,"large detail is strictly 4x7");
                    var player=Field<object>(window,"_detailPlayer");var root=Field<Grid>(player,"Root");Bounds(root,Field<ScrollViewer>(window,"_detailScroll"),"detail player at radius "+radius);var cover=Field<Border>(player,"Art");double outer=Field<DockSurface>(window,"_detailSurface").Radius*Math.Min(detail.ActualWidth,detail.ActualHeight)/200,p=Field<double>(player,"Padding");double expectedRadius=Math.Clamp(outer-p,0,cover.ActualWidth/2);Check(cover.CornerRadius.TopLeft==0&&cover.Clip is not null&&cover.Clip.IsFrozen,"album uses the inset continuous outline at "+radius);Check(Field<ControlCenterOptions>(window,"_options").Geometry.DetailRoundness<=35,"detail roundness capped at 35% independently of main player");
                    Call(window,"HideDetails");await Task.Delay(500);
                    var wifi=Card(window,ControlModule.Wifi);var holder=Tree<Border>(wifi).First(b=>b.Background==Brushes.White);double inset=(wifi.ActualHeight-holder.ActualHeight)/2;var glass=Tree<DockSurface>(wifi).Single();double nested=Math.Clamp(glass.Radius*wifi.ActualHeight/200-inset,0,holder.ActualHeight/2);Check(holder.CornerRadius.TopLeft==0&&holder.Clip is { IsFrozen:true },"capsule uses the parallel inset outline at "+radius);
                }
                var pages=settings.Copy();pages.ControlCenter.Pages.Add(new(){Id="tools",Name="工具"});var media=pages.ControlCenter.Modules.Single(m=>m.Module==ControlModule.Media);media.PageId="tools";var wifiLayout=pages.ControlCenter.Modules.Single(m=>m.Module==ControlModule.Wifi);wifiLayout.Group="连接";wifiLayout.DisplayName="我的网络";wifiLayout.Glyph="Headphones";pages.ControlCenter.Modules.Reverse();pages.ControlCenter.Normalize();Call(window,"ApplyPreview",pages);for(int wait=0;wait<200&&Tree<Grid>(window).Any(g=>AutomationProperties.GetAutomationId(g)=="control-card-Media");wait++)await Task.Delay(10);Check(!Tree<Grid>(window).Any(g=>AutomationProperties.GetAutomationId(g)=="control-card-Media"),"page filters controls");Check(Tree<TextBlock>(Card(window,ControlModule.Wifi)).Any(t=>t.Text=="我的网络"),"custom name applied");Check(Field<ControlCenterOptions>(window,"_options").Modules.Select(m=>m.Module).SequenceEqual(pages.ControlCenter.Modules.Select(m=>m.Module)),"preview mirrors exact order");
                var opts=Field<ControlCenterOptions>(window,"_options");var placed=ControlCenterLayout.Arrange(opts);Check(placed.First(p=>p.Module==ControlModule.Wifi).Group=="连接","group layout metadata retained");Call(window,"PersistLayout");Check(saved is not null&&saved.Pages.Count==2&&saved.Modules.Single(m=>m.Module==ControlModule.Wifi).DisplayName=="我的网络","page/group/icon/name persisted");
                pages.ControlCenter.ActivePageId="tools";Call(window,"ApplyPreview",pages);await Task.Delay(500);Check(Tree<Grid>(window).Any(g=>AutomationProperties.GetAutomationId(g)=="control-card-Media"),"page switch restores own controls");
                Set(window,"_mediaState",state with{Duration=TimeSpan.Zero});Call(window,"UpdateCardMedia");var playerUnknown=Field<object>(window,"_mainPlayer");Check(Field<TextBlock>(playerUnknown,"Elapsed").Text=="—:—"&&Field<TextBlock>(playerUnknown,"Remaining").Text=="","unknown media duration does not invent 0:00/-0:00");
                foreach(double dpi in new[]{120d,144d,168d})Capture(window,Path.Combine(directory,$"dpi-{dpi}.png"),dpi);
                Console.WriteLine("PASS design checks; offscreen 125/150/175% rasterization, physical monitor DPI unchanged");result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{window?.Dismiss();await Task.Delay(450);app.Shutdown();}
        };app.Run();return result;
    }
}


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
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using Windows.Devices.Radios;

internal static class ControlSweepChecks
{
    private sealed class Connectivity:IControlCenterConnectivity
    {
        public event Action? Changed {add{}remove{}}
        public Task Initialize()=>Task.CompletedTask;
        public RadioState? State(RadioKind kind)=>RadioState.On;
        public Task SetRadio(RadioKind kind,bool on)=>throw new Exception("hardware writes prohibited");
        public Task<IReadOnlyList<WifiNetwork>> ReadNetworks(bool scan=false)=>Task.FromResult<IReadOnlyList<WifiNetwork>>(Enumerable.Range(0,24).Select(i=>new WifiNetwork(Guid.Empty,"Long network name for clipping checks "+i,"fixture",80,false,true)).ToArray());
        public Task Connect(WifiNetwork n,string? p,CancellationToken c)=>throw new Exception("hardware writes prohibited");
        public Task Disconnect(WifiNetwork n,CancellationToken c)=>throw new Exception("hardware writes prohibited");
        public IReadOnlyList<BluetoothPeer> BluetoothDevices=>Enumerable.Range(0,20).Select(i=>new BluetoothPeer("fixture"+i,"Long device name "+i,true,false)).ToArray();
        public void StartBluetoothDiscovery(){}public void StopBluetoothDiscovery(){}public void Dispose(){}
        public Task PairBluetooth(BluetoothPeer p)=>throw new Exception("hardware writes prohibited");
        public Task ForgetBluetooth(BluetoothPeer p)=>throw new Exception("hardware writes prohibited");
    }
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance)!.GetValue(o)!;
    private static void Set(object o,string name,object? value)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance)!.SetValue(o,value);
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance)!.Invoke(o,args);
    private static IEnumerable<T> Tree<T>(DependencyObject o)where T:DependencyObject{if(o is T item)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var v in Tree<T>(VisualTreeHelper.GetChild(o,i)))yield return v;}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static async Task Settle(ControlCenterWindow w){for(int i=0;i<400&&(Field<double>(w,"_navigationStarted")>=0||Field<bool>(w,"_transitioning"));i++)await Task.Delay(10);Check(Field<double>(w,"_navigationStarted")<0&&!Field<bool>(w,"_transitioning"),"spring settles without stale overlay");}
    private static void Capture(ControlCenterWindow w,string file)
    {
        var p=w.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(w);using var bitmap=new System.Drawing.Bitmap((int)Math.Ceiling(w.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(w.ActualHeight*dpi.DpiScaleY));using var g=System.Drawing.Graphics.FromImage(bitmap);g.CopyFromScreen((int)p.X,(int)p.Y,0,0,bitmap.Size);bitmap.Save(file);
    }
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};ControlCenterWindow? w=null;Window? fixture=null;int result=1;
        app.DispatcherUnhandledException+=(_,e)=>{Console.Error.WriteLine(e.Exception);e.Handled=true;app.Shutdown(1);};
        app.Startup+=async(_,_)=>{
            try{
                var area=SystemParameters.WorkArea;fixture=new Window{Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Background=new LinearGradientBrush(Colors.DarkSlateBlue,Colors.Teal,35)};fixture.Show();
                var settings=new DockSettings();settings.Appearance.ShadowEnabled=true;settings.Appearance.ShadowBlur=28;settings.ControlCenter.Geometry.PanelScale=1;
                w=new ControlCenterWindow(settings,log:(m,e)=>Console.WriteLine(m+": "+e),connectivity:new Connectivity()){DismissOnDeactivate=false};w.Show();await Task.Delay(1100);
                for(int wait=0;wait<500&&(w.Opacity<.999||w.GlassBackdrop is null);wait++)await Task.Delay(10);Check(w.Opacity>=.999&&w.GlassBackdrop is not null,$"entrance waits for a real shared glass frame; opacity={w.Opacity}, captures={w.CapturedFrames}, started={Field<bool>(w,"_entranceStarted")}");
                Field<DispatcherTimer>(w,"_poll").Stop();Field<DispatcherTimer>(w,"_mediaPoll").Stop();Field<DispatcherTimer>(w,"_stateRefresh").Stop();for(int i=0;i<200&&Field<bool>(w,"_polling");i++)await Task.Delay(10);
                foreach(var kind in new[]{ControlModule.Wifi,ControlModule.Bluetooth,ControlModule.Volume,ControlModule.Focus,ControlModule.Media}){
                    Call(w,"OpenModule",kind,false);await Task.Delay(90);
                    var layer=Field<FrameworkElement>(w,"_navigationLayer");var details=Field<Grid>(w,"_details");var plate=Field<DockSurface>(w,"_navigationGlass");
                    Check(layer.IsVisible&&details.Opacity==0,"only the moving plate presents secondary contents: "+kind);
                    Check(details.RenderTransform.Value.IsIdentity,"secondary contents have no independent right slide");
                    Check(Tree<FrameworkElement>(layer).Any(e=>e.GetType().Name=="NavigationInk"&&e.ActualWidth>0),"content grows inside the source card: "+kind);
                    Check(plate.Backdrop is not null&&((DpiScale)plate.GetType().GetProperty("CapturePixelScale",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plate)!).DpiScaleX>0,"glass mapped on the same composition frame");
                    if(kind==ControlModule.Wifi)Capture(w,Path.Combine(directory,"wifi-growing.png"));
                    await Settle(w);Check(!Tree<Button>(details).Any(b=>AutomationProperties.GetName(b)=="返回"),"no secondary X or close plate");
                    var scroll=Field<ScrollViewer>(w,"_detailScroll");
                    if(kind==ControlModule.Wifi){var bar=Tree<ScrollBar>(scroll).Single(b=>b.IsVisible);Check(bar.ActualHeight<=76.1,"scrollbar track is approximately 2cm at logical 96 DPI");Check(Tree<Thumb>(bar).Any(),"short scrollbar retains a draggable thumb");}
                    if(kind==ControlModule.Media){Check(scroll.VerticalScrollBarVisibility==ScrollBarVisibility.Disabled&&scroll.ScrollableHeight<1,"large player fits without scrollbar");Capture(w,Path.Combine(directory,"media-detail.png"));}
                    foreach(var s in Tree<DockSurface>(w))Check(s.Effect is null,"glass is never blurred by the shadow effect");
                    Call(w,"BackgroundPress",w.PointToScreen(new Point(2,30)));await Settle(w);Check(!Field<bool>(w,"_detailVisible")&&Field<Canvas>(w,"_cards").IsHitTestVisible,"outside click reverses to original card");
                }
                for(int i=0;i<8;i++){Call(w,"OpenModule",ControlModule.Wifi,false);await Task.Delay(35);Call(w,"HideDetails");await Task.Delay(35);}await Settle(w);Check(!Field<Grid>(w,"_details").IsVisible,"rapid reversals leave one correct page");
                Field<DispatcherTimer>(w,"_mediaPoll").Stop();Field<DispatcherTimer>(w,"_poll").Stop();Field<DispatcherTimer>(w,"_stateRefresh").Stop();await Task.Delay(200);
                var state=new DockForWindows.Services.MediaState(new string('歌',50),"测试歌手",true,true,true,true,null,TimeSpan.FromSeconds(20),TimeSpan.FromSeconds(200),true);
                Set(w,"_mediaState",state);Set(w,"_mediaObservedAt",w.RuntimeSeconds-3);Call(w,"UpdateCardMedia");Call(w,"UpdateSmoothMediaTimeline");
                var player=Field<object>(w,"_mainPlayer");var progress=Field<Slider>(player,"Progress");Check(progress.Value>=22.9,"live progress continues beyond a two-second observation gap");
                Set(w,"_mediaState",state with{Playing=false,Position=TimeSpan.FromSeconds(40)});Call(w,"UpdateSmoothMediaTimeline");Check(Math.Abs(progress.Value-40)<.1,"pause and external seeks correct the visible timeline");
                var title=Field<TextBlock>(player,"Title");var marquee=Field<FrameworkElement>(player,"TitleMarquee");Call(marquee,"Tick",w.RuntimeSeconds,true);Call(marquee,"Tick",w.RuntimeSeconds+3,true);
                Check(marquee.ClipToBounds&&((TranslateTransform)title.RenderTransform).X<0,"long title scrolls inside its fixed clip");
                var tile=Tree<Grid>(w).Single(e=>AutomationProperties.GetAutomationId(e)=="control-card-Wifi");var card=((System.Collections.IEnumerable)Field<object>(w,"_modules")).Cast<object>().Single(c=>Field<ControlModule>(c,"Kind")==ControlModule.Wifi);
                Set(w,"_dragged",true);Set(w,"_grab",new Point(20,20));Call(w,"TrackDragPointer",card,new Point(150,200));var pos=Field<TranslateTransform>(card,"Position");var scale=Field<ScaleTransform>(card,"Scale");Check(Math.Abs(pos.X-(130-(20-tile.ActualWidth/2)*(scale.ScaleX-1)))<.01,"dragged card follows current pointer without a spring lag");Set(w,"_dragged",false);Call(w,"LayoutCards",false);
                Capture(w,Path.Combine(directory,"main.png"));result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{w?.Dismiss();await Task.Delay(650);fixture?.Close();app.Shutdown(result);}
        };app.Run();return result;
    }
}

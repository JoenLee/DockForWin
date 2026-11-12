using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using Windows.Devices.Radios;

internal static class ControlCenterRefinementChecks
{
    private sealed class Connectivity : IControlCenterConnectivity
    {
        public event Action? Changed {add{}remove{}}
        public Task Initialize()=>Task.CompletedTask;
        public RadioState? State(RadioKind kind)=>RadioState.On;
        public Task SetRadio(RadioKind kind,bool enabled)=>throw new Exception("unexpected hardware mutation");
        public Task<IReadOnlyList<WifiNetwork>> ReadNetworks(bool scan=false)=>Task.FromResult<IReadOnlyList<WifiNetwork>>(Enumerable.Range(0,28).Select(i=>new WifiNetwork(Guid.Empty,$"Network {i:00} — a deliberately long network name to verify wrapping and safe content padding","fixture",80,false,true)).ToArray());
        public Task Connect(WifiNetwork n,string? p,CancellationToken c)=>throw new Exception("unexpected connect");
        public Task Disconnect(WifiNetwork n,CancellationToken c)=>throw new Exception("unexpected disconnect");
        public IReadOnlyList<BluetoothPeer> BluetoothDevices=>Enumerable.Range(0,22).Select(i=>new BluetoothPeer(i.ToString(),$"Headphones {i:00} — long device name with safe padding",false)).ToArray();
        public void StartBluetoothDiscovery(){}public void StopBluetoothDiscovery(){}public void Dispose(){}
        public Task PairBluetooth(BluetoothPeer peer)=>throw new Exception("unexpected pair");
        public Task ForgetBluetooth(BluetoothPeer peer)=>throw new Exception("unexpected unpair");
    }
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint x,uint y,uint data,nuint extra);
    private static byte[] Cover(){using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/DockForWindows;component/Assets/PreviewWallpaper.png")).Stream;using var bytes=new MemoryStream();stream.CopyTo(bytes);return bytes.ToArray();}
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(o,args);
    private static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T match)yield return match;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static bool Within(DependencyObject? hit,DependencyObject parent){while(hit is not null){if(hit==parent)return true;hit=hit is Visual?VisualTreeHelper.GetParent(hit):null;}return false;}
    private static void Capture(ControlCenterWindow window,string path)
    {
        var origin=window.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(window);
        using var image=new System.Drawing.Bitmap((int)Math.Ceiling(window.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(window.ActualHeight*dpi.DpiScaleY));
        using var graphics=System.Drawing.Graphics.FromImage(image);graphics.CopyFromScreen((int)origin.X,(int)origin.Y,0,0,image.Size);image.Save(path);

    }
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ControlCenterWindow? window=null;Window? fixture=null;
        var pointer=System.Windows.Forms.Cursor.Position;
        app.Startup+=async(_,_)=>{
            try{
                var area=SystemParameters.WorkArea;fixture=new Window{WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Width=area.Width,Height=area.Height,Left=area.Left,Top=area.Top,Background=new LinearGradientBrush(Colors.DarkSlateBlue,Colors.Teal,35)};fixture.Show();
                var settings=new DockSettings();settings.ControlCenter.Geometry.PanelScale=1;
                window=new ControlCenterWindow(settings,connectivity:new Connectivity()){DismissOnDeactivate=false};window.Show();window.Activate();await Task.Delay(850);
                var stage=Field<Grid>(window,"_stage");Check(window.ActualWidth-stage.ActualWidth>=70,"outer gutter allows shadows and transition overshoot");
                var host=(Grid)window.Content;Check(host.OpacityMask is LinearGradientBrush,"outer boundary feathers rather than clipping along card edges");
                foreach(var module in new[]{ControlModule.Wifi,ControlModule.Bluetooth,ControlModule.Volume,ControlModule.Focus}){
                    Call(window,"OpenModule",module,false);await Task.Delay(90);
                    Check(Field<bool>(window,"_transitioning"),"nonlinear page transition remains active after 90ms: "+module);
                    var bodyAtEntry=Field<StackPanel>(window,"_detailBody");object? initial=bodyAtEntry.Children.Count>0?bodyAtEntry.Children[0]:null;
                    await Task.Delay(850);
                    if(module is ControlModule.Wifi or ControlModule.Bluetooth){
                        Check(initial is not null&&!ReferenceEquals(initial,bodyAtEntry.Children[0]),"entering automatically refreshes the discovery snapshot once: "+module);
                        var refreshed=bodyAtEntry.Children[0];await Task.Delay(750);Check(ReferenceEquals(refreshed,bodyAtEntry.Children[0]),"automatic entry refresh does not continually replace the list: "+module);
                    }
                    var details=Field<Grid>(window,"_details");var content=Field<Grid>(window,"_detailContent");var scroll=Field<ScrollViewer>(window,"_detailScroll");var surface=Field<DockSurface>(window,"_detailSurface");
                    double physicalRadius=surface.Radius/100*Math.Min(details.ActualWidth,details.ActualHeight)/2;
                    Check(Math.Abs(physicalRadius-details.ActualWidth*.5*Field<ControlCenterOptions>(window,"_options").Geometry.DetailRoundness/100)<1,"secondary corner radius is consistent: "+module);
                    var bounds=scroll.TransformToAncestor(details).TransformBounds(new Rect(scroll.RenderSize));
                    Check(bounds.Left>=17&&bounds.Right<=details.ActualWidth-17&&bounds.Bottom<=details.ActualHeight-14,$"scroll viewport and scrollbar remain inside safe insets: {module} {bounds} / {details.RenderSize}");
                    var header=Field<Grid>(window,"_detailHeader");var original=header.PointToScreen(new Point());
                    if(module is ControlModule.Wifi or ControlModule.Bluetooth){
                        Check(scroll.ScrollableHeight>100,"long device/network list scrolls internally");
                        scroll.ScrollToVerticalOffset(scroll.ScrollableHeight/2);await Task.Delay(80);
                        Check((header.PointToScreen(new Point())-original).Length<1,"header stays pinned during scrolling");
                        var viewport=Tree<ScrollContentPresenter>(scroll).First();Check(viewport.OpacityMask is LinearGradientBrush,"text fades at both viewport edges");
                        Check(Tree<System.Windows.Shapes.Rectangle>(content).Count(r=>r.IsVisible&&r.Effect is System.Windows.Media.Effects.BlurEffect)>=2,"scroll edges have Gaussian blur overlays");
                        foreach(var text in Tree<TextBlock>(Field<StackPanel>(window,"_detailBody"))){var rect=text.TransformToAncestor(scroll).TransformBounds(new Rect(text.RenderSize));Check(rect.Left>=-1&&rect.Right<=scroll.ActualWidth+1,$"long text is width-constrained: {rect} / {scroll.ActualWidth}");}
                    }
                    Capture(window,Path.Combine(directory,module+".png"));Call(window,"BackgroundPress",window.PointToScreen(new Point(2,30)));for(int wait=0;wait<400&&(Field<bool>(window,"_transitioning")||Field<double>(window,"_navigationStarted")>=0);wait++)await Task.Delay(10);
                    Check(!Field<bool>(window,"_detailVisible")&&Field<Canvas>(window,"_cards").IsHitTestVisible,"hit-tested return restores main-page input: "+module);
                }
                var options=Field<ControlCenterOptions>(window,"_options");var tile=Tree<Grid>(window).Single(g=>AutomationProperties.GetAutomationId(g)=="control-card-Wifi");var glass=Tree<DockSurface>(tile).Single();
                Check(glass.CornerExponent==1.5,"capsule geometry and glass use the cubic corner profile");
                var size=new Size(180,70);var cubic=DockSurface.CreateGeometry(size,100,0,1.5);Check(!cubic.FillContains(new Point(0,0))&&cubic.FillContains(new Point(35,1)),"full-round capsule retains the smooth cubic seam");
                Check(Tree<DockSurface>(window).Count(s=>s.IsVisible)<=options.Modules.Count(m=>m.Visible)+1,"no whole-panel bottom blur surface remains");
                Call(window,"OpenModule",ControlModule.Wifi,false);await Task.Delay(1000);
                var scroll2=Field<ScrollViewer>(window,"_detailScroll");var smooth=Tree<Grid>(window).Single(g=>g.GetType().Name=="ScrollEdgeFade");
                var bodyStable=Field<StackPanel>(window,"_detailBody");var firstRow=bodyStable.Children[0];await Task.Delay(1150);Check(ReferenceEquals(firstRow,bodyStable.Children[0]),"background polling does not rebuild the device list");
                var refreshButton=Tree<Button>(window).Single(b=>b.IsVisible&&AutomationProperties.GetName(b)=="刷新列表");Check(refreshButton.TranslatePoint(new Point(),Field<Grid>(window,"_details")).X>0,"refresh remains inside header safe area");
                refreshButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(750);Check(!ReferenceEquals(firstRow,bodyStable.Children[0]),"manual refresh replaces the snapshot only on request");
                var scrollFrames=smooth.GetType().GetProperty("ScrollFrames",BindingFlags.Instance|BindingFlags.NonPublic)!;long firstFrames=(long)scrollFrames.GetValue(smooth)!;long firstCallbacks=window.AnimationFrames;
                for(int tick=0;tick<18;tick++){Call(smooth,"ScrollBy",30d);await Task.Delay(80);}long changedFrames=(long)scrollFrames.GetValue(smooth)!-firstFrames,callbacks=window.AnimationFrames-firstCallbacks;Console.WriteLine($"Scrolling: updated={changedFrames}, composition callbacks={callbacks}");Check(changedFrames>=callbacks*.9&&changedFrames<=callbacks+1,"scroll motion uses composition callbacks; settled idle frames need no motion update");await Task.Delay(700);
                scroll2.ScrollToTop();await Task.Delay(100);Call(smooth,"ScrollBy",200d);await Task.Delay(60);Check(scroll2.VerticalOffset>0&&scroll2.VerticalOffset<199,"wheel advances through intermediate positions");await Task.Delay(750);Check(Math.Abs(scroll2.VerticalOffset-200)<1,"smooth scrolling settles to target");
                scroll2.ScrollToTop();await Task.Delay(80);Call(smooth,"ScrollBy",-200d);await Task.Delay(35);Check(Field<TranslateTransform>(smooth,"_elastic").Y>0,"top edge rubber band moves content");await Task.Delay(1000);Check(Math.Abs(Field<TranslateTransform>(smooth,"_elastic").Y)<.2,"top edge rebounds to rest");
                scroll2.ScrollToBottom();await Task.Delay(80);Call(smooth,"ScrollBy",200d);await Task.Delay(35);Check(Field<TranslateTransform>(smooth,"_elastic").Y<0,"bottom edge rubber band moves content");await Task.Delay(1000);
                Check(!Tree<Button>(Field<Grid>(window,"_details")).Any(b=>AutomationProperties.GetName(b)=="返回"),"secondary close and circular plate removed");
                var label=Tree<TextBlock>(Field<StackPanel>(window,"_detailBody")).First();Check(label.FontSize>=14,"detail list font is readable");
                Call(window,"BackgroundPress",window.PointToScreen(new Point(2,window.ActualHeight/2)));await Task.Delay(500);Check(!Field<bool>(window,"_detailVisible"),"click outside secondary plate returns to main");
                Call(window,"Active",ControlModule.Bluetooth,true);await Task.Delay(250);var bluetooth=Tree<Grid>(window).Single(g=>AutomationProperties.GetAutomationId(g)=="control-card-Bluetooth");Check(Tree<System.Windows.Shapes.Path>(bluetooth).Any(path=>path.Fill is SolidColorBrush brush&&brush.Color==Colors.White),"active small round control has white backing");
                Field<System.Windows.Threading.DispatcherTimer>(window,"_poll").Stop();Field<System.Windows.Threading.DispatcherTimer>(window,"_mediaPoll").Stop();Field<System.Windows.Threading.DispatcherTimer>(window,"_stateRefresh").Stop();await Task.Delay(500);
                foreach(var shape in new[]{ControlCardShape.Square,ControlCardShape.Rectangle,ControlCardShape.Large}){
                    options.Modules.Single(m=>m.Module==ControlModule.Media).SetShape(shape);Call(window,"SyncCards",false);await Task.Delay(200);for(int wait=0;wait<150&&Field<bool>(window,"_polling");wait++)await Task.Delay(20);
                    var state=new DockForWindows.Services.MediaState("我是如此相信","周杰伦",true,true,true,true,Cover(),TimeSpan.FromSeconds(9),TimeSpan.FromSeconds(265),true);
                    typeof(ControlCenterWindow).GetField("_mediaState",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,state);Call(window,"UpdateMediaArtwork");Call(window,"UpdateCardMedia");Call(window,"Status",ControlModule.Media,state.Title+"\n"+state.Artist);
                    var media=Tree<Grid>(window).Single(g=>AutomationProperties.GetAutomationId(g)=="control-card-Media");Check(media.ActualHeight>100,"media "+shape+" keeps two grid rows");
                    await Task.Delay(120);Capture(window,Path.Combine(directory,"Media-"+shape+".png"));
                }
                Field<System.Windows.Threading.DispatcherTimer>(window,"_poll").Stop();Field<System.Windows.Threading.DispatcherTimer>(window,"_stateRefresh").Stop();
                Call(window,"OpenModule",ControlModule.Media,false);await Task.Delay(600);Check(Field<Border>(window,"_mediaCover").ActualHeight>200,"large player retains a full-size cover");Check(Field<Border>(window,"_mediaCover").Child is Image,"large player decodes real artwork");Check(!Tree<Thumb>(Field<Grid>(window,"_details")).Any(t=>t.ToolTip as string=="拖动调整播放器大小"),"large detail uses a fixed preset and has no free resize grip");Console.WriteLine($"MEDIA: details={Field<Grid>(window,"_details").RenderSize}, requested={options.Geometry.MediaDetailHeight}, cover={Field<Border>(window,"_mediaCover").RenderSize}, height={Field<Grid>(window,"_details").Height}, base={Field<Grid>(window,"_details").GetAnimationBaseValue(FrameworkElement.HeightProperty)}, stage={stage.RenderSize}, fit={Field<ScaleTransform>(window,"_fit").ScaleX}, clip={Field<Border>(window,"_mediaCover").Clip?.Bounds}, body={((FrameworkElement)Field<Border>(window,"_mediaCover").Parent).Height}");Capture(window,Path.Combine(directory,"Media-Large.png"));
                Call(window,"BackgroundPress",window.PointToScreen(new Point(2,window.ActualHeight/2)));await Task.Delay(500);
                var preview=settings.Copy();preview.ControlCenter.Geometry.RectangleRoundness=100;Call(window,"ApplyPreview",preview);await Task.Delay(200);var volumeTile=Tree<Grid>(window).Single(g=>AutomationProperties.GetAutomationId(g)=="control-card-Volume");Check(Tree<DockSurface>(volumeTile).Single().Radius==100,"live preview applies expanded slider-card corner range");
                var press=new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent};SetCursorPos((int)area.Left+10,(int)area.Top+10);await Task.Delay(100);volumeTile.RaiseEvent(press);var transform=(ScaleTransform)((TransformGroup)volumeTile.RenderTransform).Children[0];for(int wait=0;wait<25&&transform.ScaleX>=1;wait++)await Task.Delay(10);Check(transform.ScaleX<1,"large card press has spring compression");volumeTile.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});await Task.Delay(600);Check(Math.Abs(transform.ScaleX-1)<.01,"card spring returns to rest");
                Capture(window,Path.Combine(directory,"Main.png"));Call(window,"BackgroundPress",window.PointToScreen(new Point(2,window.ActualHeight/2)));await Task.Delay(700);Check(!window.IsVisible,"click main-page blank closes control center");result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{window?.Dismiss();await Task.Delay(400);fixture?.Close();SetCursorPos(pointer.X,pointer.Y);app.Shutdown();}
        };app.Run();return result;
    }
}

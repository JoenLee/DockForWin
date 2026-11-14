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

internal static class ControlCenterPresentationChecks
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
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(o,args);
    private static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T match)yield return match;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static bool Within(DependencyObject? hit,DependencyObject parent){while(hit is not null){if(hit==parent)return true;hit=hit is Visual?VisualTreeHelper.GetParent(hit):null;}return false;}
    private static Button Back(ControlCenterWindow window)=>Tree<Button>(window).Single(b=>b.IsVisible&&AutomationProperties.GetName(b)=="返回");
    private static async Task RealClick(ControlCenterWindow window,Button button)
    {
        window.Activate();await Task.Delay(100);var center=new Point(button.ActualWidth/2,button.ActualHeight/2);var point=button.TranslatePoint(center,window);
        Check(Within(window.InputHitTest(point) as DependencyObject,button),"button center passes real WPF hit testing");
        int clicks=0;button.Click+=(_,_)=>clicks++;
        var screen=button.PointToScreen(center);SetCursorPos((int)screen.X,(int)screen.Y);await Task.Delay(40);mouse_event(2,0,0,0,0);await Task.Delay(35);mouse_event(4,0,0,0,0);await Task.Delay(470);Console.WriteLine($"Native input: clicks={clicks}, expected={screen}, cursor={System.Windows.Forms.Cursor.Position}, over={System.Windows.Input.Mouse.DirectlyOver?.GetType().Name}");
    }
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
                // The outer boundary feathers rather than clipping along card edges: the halo fades on its own falloff, and the
                // window-wide side feather (a full-window layer) is applied while content reaches into the gutters.
                var host=(Grid)window.Content;var halo=Field<FrameworkElement>(window,"_classicBackdrop");
                Check(halo.OpacityMask is DrawingBrush&&host.OpacityMask is null,"still panel: halo falloff only, no full-window mask");
                Call(window,"SetGutterActive",true);Check(host.OpacityMask is LinearGradientBrush,"outer boundary feathers rather than clipping along card edges");
                Call(window,"SetGutterActive",false);Check(host.OpacityMask is null,"feather is released once nothing is in the gutters");
                foreach(var module in new[]{ControlModule.Wifi,ControlModule.Bluetooth,ControlModule.Volume,ControlModule.Focus}){
                    Call(window,"OpenModule",module,false);await Task.Delay(90);
                    Check(Field<bool>(window,"_transitioning"),"nonlinear page transition remains active after 90ms: "+module);
                    await Task.Delay(500);
                    var details=Field<Grid>(window,"_details");var content=Field<Grid>(window,"_detailContent");var scroll=Field<ScrollViewer>(window,"_detailScroll");var surface=Field<DockSurface>(window,"_detailSurface");
                    double physicalRadius=surface.Radius/100*Math.Min(details.ActualWidth,details.ActualHeight)/2;
                    Check(Math.Abs(physicalRadius-settings.ControlCenter.Geometry.DetailRoundness)<.1,"secondary corner radius is consistent: "+module);
                    var bounds=scroll.TransformToAncestor(details).TransformBounds(new Rect(scroll.RenderSize));
                    Check(bounds.Left>=17&&bounds.Right<=details.ActualWidth-17&&bounds.Bottom<=details.ActualHeight-14,"scroll viewport and scrollbar remain inside safe insets: "+module);
                    var back=Back(window);var original=back.PointToScreen(new Point());
                    if(module is ControlModule.Wifi or ControlModule.Bluetooth){
                        Check(scroll.ScrollableHeight>100,"long device/network list scrolls internally");
                        scroll.ScrollToVerticalOffset(scroll.ScrollableHeight/2);await Task.Delay(80);
                        Check((back.PointToScreen(new Point())-original).Length<1,"return stays pinned during scrolling");
                        var viewport=Tree<ScrollContentPresenter>(scroll).First();Check(viewport.OpacityMask is LinearGradientBrush,"text fades at both viewport edges");
                        Check(Tree<System.Windows.Shapes.Rectangle>(content).Count(r=>r.IsVisible&&r.Effect is System.Windows.Media.Effects.BlurEffect)>=2,"scroll edges have Gaussian blur overlays");
                        foreach(var text in Tree<TextBlock>(Field<StackPanel>(window,"_detailBody"))){var rect=text.TransformToAncestor(scroll).TransformBounds(new Rect(text.RenderSize));Check(rect.Left>=-1&&rect.Right<=scroll.ActualWidth+1,"long text is width-constrained");}
                    }
                    Capture(window,Path.Combine(directory,module+".png"));await RealClick(window,back);
                    Check(!Field<bool>(window,"_detailVisible")&&Field<Canvas>(window,"_cards").IsHitTestVisible,"native mouse return restores main-page input: "+module);
                }
                var options=Field<ControlCenterOptions>(window,"_options");var tile=Tree<Grid>(window).Single(g=>AutomationProperties.GetAutomationId(g)=="control-card-Wifi");var glass=Tree<DockSurface>(tile).Single();
                Check(glass.CornerExponent==1.5,"capsule geometry and glass use the cubic corner profile");
                var size=new Size(180,70);var cubic=DockSurface.CreateGeometry(size,100,0,1.5);Check(!cubic.FillContains(new Point(0,0))&&cubic.FillContains(new Point(35,1)),"full-round capsule retains the smooth cubic seam");
                Capture(window,Path.Combine(directory,"Main.png"));result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{window?.Dismiss();await Task.Delay(400);fixture?.Close();SetCursorPos(pointer.X,pointer.Y);app.Shutdown();}
        };app.Run();return result;
    }
}

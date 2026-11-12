using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using Windows.Devices.Radios;

internal static class ControlCenterInteractionChecks
{
    private sealed class Connectivity : IControlCenterConnectivity
    {
        public event Action? Changed;
        public bool Wifi=true,Bluetooth=true,Fail;
        public int Toggles,Scans,Connections,Pairs,Unpairs;
        public bool Discovering;
        public IReadOnlyList<WifiNetwork> Networks=new[]{new WifiNetwork(Guid.Empty,"Fixture network","",82,false,true,7,4)};
        public IReadOnlyList<BluetoothPeer> Peers=new[]{new BluetoothPeer("fixture","Fixture headphones",false,false)};
        public Task Initialize()=>Task.CompletedTask;
        public RadioState? State(RadioKind kind)=>(kind==RadioKind.WiFi?Wifi:Bluetooth)?RadioState.On:RadioState.Off;
        public async Task SetRadio(RadioKind kind,bool enabled){Toggles++;await Task.Delay(140);if(Fail)throw new InvalidOperationException("fixture denied");if(kind==RadioKind.WiFi)Wifi=enabled;else Bluetooth=enabled;Changed?.Invoke();}
        public Task<IReadOnlyList<WifiNetwork>> ReadNetworks(bool scan=false){if(scan)Scans++;return Task.FromResult(Networks);}
        public async Task Connect(WifiNetwork network,string? password,CancellationToken cancellation){Connections++;await Task.Delay(100,cancellation);if(Fail)throw new InvalidOperationException("fixture authentication failed");Networks=new[]{network with{Connected=true,Profile="fixture"}};Changed?.Invoke();}
        public Task Disconnect(WifiNetwork network,CancellationToken cancellation){Networks=new[]{network with{Connected=false}};Changed?.Invoke();return Task.CompletedTask;}
        public IReadOnlyList<BluetoothPeer> BluetoothDevices=>Peers;
        public void StartBluetoothDiscovery()=>Discovering=true;
        public void StopBluetoothDiscovery()=>Discovering=false;
        public Task PairBluetooth(BluetoothPeer peer){Pairs++;Peers=new[]{peer with{Paired=true}};Changed?.Invoke();return Task.CompletedTask;}
        public Task ForgetBluetooth(BluetoothPeer peer){Unpairs++;Peers=new[]{peer with{Paired=false}};Changed?.Invoke();return Task.CompletedTask;}
        public void Notify()=>Changed?.Invoke();
        public void Dispose()=>Discovering=false;
    }
    private static object? Call(object target,string name,params object?[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject
    {if(root is T match)yield return match;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static Button Button(DependencyObject root,string name)=>Children<Button>(root).First(b=>b.IsVisible&&AutomationProperties.GetName(b)==name);
    private static void Click(Button button){if(!button.IsEnabled)throw new Exception("disabled: "+AutomationProperties.GetName(button));button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static async Task Until(Func<bool> condition,string message,int timeout=6000)
    {var end=DateTime.UtcNow.AddMilliseconds(timeout);while(!condition()&&DateTime.UtcNow<end)await Task.Delay(35);if(!condition())throw new Exception("timeout: "+message);}
    private static string Page(ControlCenterWindow w)=>Field<object>(w,"_page").ToString()!;
    private static Grid Card(ControlCenterWindow w,ControlModule kind)=>Children<Grid>(w).First(c=>AutomationProperties.GetAutomationId(c)=="control-card-"+kind);
    private static void MouseUp(UIElement element)=>element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
    internal static int Run()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ControlCenterWindow? window=null;
        app.DispatcherUnhandledException+=(_,e)=>{Console.Error.WriteLine(e.Exception);e.Handled=true;result=1;app.Shutdown();};
        app.Startup+=async(_,_)=>{
            try{
                var native=new Connectivity();var settings=new DockSettings();settings.ControlCenter.Geometry.PanelScale=.8;
                ControlCenterOptions? saved=null;bool rejectSave=false;
                window=new ControlCenterWindow(settings,saveLayout:o=>{if(rejectSave)throw new Exception("fixture save denied");saved=o.Copy();},connectivity:native){DismissOnDeactivate=false};
                window.Show();await Until(()=>Field<bool>(window,"_ready")&&!Field<bool>(window,"_polling"),"loaded");await Task.Delay(650);
                var order=Field<ControlCenterOptions>(window,"_options").Modules.Select(m=>m.Module).ToArray();
                var wifi=Card(window,ControlModule.Wifi);var toggle=Button(wifi,"Wi-Fi");
                Click(toggle);toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));await Until(()=>!native.Wifi&&!Field<bool>(window,"_polling"),"toggle settled");
                Check(native.Toggles==1&&Page(window)=="Main","icon toggles once without opening details");
                native.Fail=true;Click(toggle);await Until(()=>toggle.IsEnabled,"failed operation settled");
                Check(!native.Wifi&&Field<TextBlock>(window,"_message").Text.Contains("fixture denied"),"radio failure keeps real state and reports error");native.Fail=false;
                Click(toggle);await Until(()=>native.Wifi&&toggle.IsEnabled,"radio restored");
                Click(Button(wifi,"展开Wi-Fi"));
                await Until(()=>Page(window)=="Module"&&Children<Button>(window).Any(b=>b.IsVisible&&AutomationProperties.GetName(b).StartsWith("Fixture network")),"wifi details");
                Check(native.Toggles==3,"dedicated chevron opens details without changing radio");
                var detailToggle=Children<ControlCenterToggle>(window).First(t=>t.IsVisible);var peer=UIElementAutomationPeer.CreatePeerForElement(detailToggle)!;
                ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();await Until(()=>!native.Wifi&&detailToggle.IsEnabled,"detail radio");
                Check(!native.Wifi,"loaded animated toggle supports accessibility without dependency property crash");
                await Task.Delay(200);detailToggle=Children<ControlCenterToggle>(window).First(t=>t.IsVisible);peer=UIElementAutomationPeer.CreatePeerForElement(detailToggle)!;((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();await Until(()=>native.Wifi,"detail radio on");await Task.Delay(250);
                Click(Children<Button>(window).First(b=>b.IsVisible&&AutomationProperties.GetName(b).StartsWith("Fixture network")));
                await Until(()=>Page(window)=="WifiPassword","password page");Call(window,"BackDetails");await Until(()=>Page(window)=="Module","password back");
                Check(Page(window)=="Module","password back returns to Wi-Fi details");
                Click(Children<Button>(window).First(b=>b.IsVisible&&AutomationProperties.GetName(b).StartsWith("Fixture network")));await Until(()=>Page(window)=="WifiPassword","password reopened");
                Children<PasswordBox>(window).Single().Password="fixture-only";native.Fail=true;Click(Button(window,"连接"));await Until(()=>native.Connections==1&&!Field<bool>(window,"_polling"),"failed connect");await Task.Delay(250);
                Check(Page(window)=="WifiPassword"&&Children<PasswordBox>(window).Single().Password.Length==0,"authentication failure stays on input page and clears secret");
                native.Fail=false;Children<PasswordBox>(window).Single().Password="fixture-only";Click(Button(window,"连接"));await Until(()=>Page(window)=="Module","connect success");
                Check(native.Networks[0].Connected,"successful connection reflects confirmed network state");
                Click(Button(window,"刷新列表"));await Until(()=>native.Scans==1,"scan requested");await Task.Delay(1600);
                Check(Page(window)=="Module","same-page refresh works");
                Call(window,"BackDetails");await Until(()=>Page(window)=="Main","main");await Task.Delay(350);
                Call(window,"OpenModule",ControlModule.Bluetooth,false);await Until(()=>native.Discovering,"bluetooth discovery");await Task.Delay(350);
                Click(Button(window,"Fixture headphones · 可配对"));Click(Button(window,"配对此设备"));await Until(()=>native.Pairs==1&&Page(window)=="Module","paired");await Task.Delay(350);
                Check(native.Peers[0].Paired,"Bluetooth pairing updates actual service state");
                Click(Button(window,"Fixture headphones · 已配对"));Click(Button(window,"取消配对…"));Check(native.Unpairs==0,"unpair requires confirmation");Click(Button(window,"确认取消配对"));await Until(()=>native.Unpairs==1&&Page(window)=="Module","unpaired");
                native.Bluetooth=false;native.Notify();await Until(()=>!native.Discovering,"external radio state");Check(!native.Discovering,"external Bluetooth switch stops discovery and refreshes details");native.Bluetooth=true;native.Notify();await Until(()=>native.Discovering,"external radio restore");
                Call(window,"BackDetails");Check(!native.Discovering,"return stops nearby discovery");await Task.Delay(350);
                Click(Button(window,"编辑控制"));var battery=Card(window,ControlModule.Battery);var remove=battery.ContextMenu!.Items.OfType<MenuItem>().Single(i=>(string)i.Header=="移除此控件");remove.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                await Task.Delay(300);Check(saved is not null&&!saved.Modules.Single(m=>m.Module==ControlModule.Battery).Visible,"remove persists visibility and completes shrink animation");
                Click(Button(window,"添加控件"));Click(Button(window,"电池"));await Until(()=>Page(window)=="Main","gallery add");await Task.Delay(400);
                Check(saved!.Modules.Single(m=>m.Module==ControlModule.Battery).Visible,"gallery restores module while editing");
                rejectSave=true;battery=Card(window,ControlModule.Battery);battery.ContextMenu!.Items.OfType<MenuItem>().Single(i=>(string)i.Header=="移除此控件").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));await Task.Delay(350);
                Check(Field<ControlCenterOptions>(window,"_options").Modules.Single(m=>m.Module==ControlModule.Battery).Visible,"failed save restores existing layout");rejectSave=false;
                Click(Button(window,"编辑控制"));
                for(int i=0;i<8;i++){Call(window,"OpenModule",ControlModule.Wifi,false);await Task.Delay(28);Call(window,"HideDetails");await Task.Delay(28);}await Task.Delay(500);
                Check(Page(window)=="Main"&&Field<Canvas>(window,"_cards").IsVisible&&!Field<Grid>(window,"_details").IsVisible,"rapid navigation cannot leave stale animation overlay");
                Check(Field<ControlCenterOptions>(window,"_options").Modules.Select(m=>m.Module).SequenceEqual(order),"navigation and failed save preserve module order");
                var legacy=new ControlCenterOptions{LayoutVersion=1};legacy.Modules.Reverse();legacy.Modules[0].Visible=false;var first=legacy.Modules[0].Module;legacy.Normalize();Check(legacy.Modules[0].Module==first&&!legacy.Modules[0].Visible,"layout normalization preserves user order and hidden controls");
                foreach(var module in Enum.GetValues<ControlModule>()){
                    Call(window,"OpenModule",module,false);await Task.Delay(350);
                    Check(Page(window)=="Module"&&Field<TextBlock>(window,"_message").Text.Length==0,"open module without click exception: "+module);
                    Call(window,"HideDetails");await Task.Delay(320);
                }
                Console.WriteLine($"Animation callbacks={window.AnimationFrames}; captures={window.CapturedFrames}");result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{if(window is not null){window.Dismiss();await Task.Delay(400);}app.Shutdown();}
        };
        app.Run();return result;
    }
}

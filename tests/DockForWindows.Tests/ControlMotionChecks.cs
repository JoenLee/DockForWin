using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using Windows.Devices.Radios;

internal static class ControlMotionChecks
{
    private sealed class Connectivity:IControlCenterConnectivity
    {
        public event Action? Changed {add{}remove{}}
        public Task Initialize()=>Task.CompletedTask;
        public RadioState? State(RadioKind kind)=>RadioState.On;
        public Task SetRadio(RadioKind kind,bool enabled)=>throw new Exception("No hardware writes in animation test");
        public Task<IReadOnlyList<WifiNetwork>> ReadNetworks(bool scan=false)=>Task.FromResult<IReadOnlyList<WifiNetwork>>(Enumerable.Range(0,18).Select(i=>new WifiNetwork(Guid.Empty,"Network "+i,"fixture",80,false,true)).ToArray());
        public Task Connect(WifiNetwork n,string? p,CancellationToken c)=>throw new Exception("No hardware writes");
        public Task Disconnect(WifiNetwork n,CancellationToken c)=>throw new Exception("No hardware writes");
        public IReadOnlyList<BluetoothPeer> BluetoothDevices=>Array.Empty<BluetoothPeer>();
        public void StartBluetoothDiscovery(){}public void StopBluetoothDiscovery(){}public void Dispose(){}
        public Task PairBluetooth(BluetoothPeer peer)=>throw new Exception("No hardware writes");
        public Task ForgetBluetooth(BluetoothPeer peer)=>throw new Exception("No hardware writes");
    }
    private static void Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(o,args);
    private static double Percentile(List<double> values,double p){var a=values.Order().ToArray();return a[Math.Min(a.Length-1,(int)(a.Length*p))];}
    internal static int Run(string path)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;
        var settings=new SettingsService("D:/DockForWindows/data").Load();settings.ControlCenter.Modules.Single(m=>m.Module==ControlModule.Wifi).Visible=true;
        var center=new ControlCenterWindow(settings,connectivity:new Connectivity()){DismissOnDeactivate=false};
        
        var area=SystemParameters.WorkArea;var fixture=new Window{Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Background=new LinearGradientBrush(Colors.DarkSlateBlue,Colors.Teal,35)};
        fixture.Loaded+=async(_,_)=>{
            var clock=Stopwatch.StartNew();var frames=new List<double>();var latency=new List<double>();double last=0,pending=-1;long before=0;int layouts=0;
            void Frame(object? s,EventArgs e){double now=clock.Elapsed.TotalMilliseconds;if(last>0)frames.Add(now-last);last=now;if(pending>=0){latency.Add(now-pending);pending=-1;}if(Environment.GetEnvironmentVariable("DOCK_MOTION_PROBE")=="no-ink"&&center.GetType().GetField("_navigationLayer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(center) is FrameworkElement layer){var ink=layer.GetType().GetField("_ink",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(layer) as FrameworkElement;if(ink is not null)ink.Visibility=Visibility.Collapsed;}}
            void Layout(object? s,EventArgs e)=>layouts++;
            try{
                center.Show();await Task.Delay(1800);
                
                if(Environment.GetEnvironmentVariable("DOCK_MOTION_PROBE")=="flat"){
                    Call(center,"OpenModule",ControlModule.Wifi,false);await Task.Delay(800);Call(center,"HideDetails");await Task.Delay(650);
                    foreach(var surface in (IEnumerable<DockSurface>)center.GetType().GetField("_surfaces",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(center)!){surface.UseGlass=false;surface.InvalidateVisual();}
                }
                CompositionTarget.Rendering+=Frame;center.LayoutUpdated+=Layout;before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<12;i++){pending=clock.Elapsed.TotalMilliseconds;Call(center,"OpenModule",ControlModule.Wifi,false);await Task.Delay(560);pending=clock.Elapsed.TotalMilliseconds;Call(center,"HideDetails");await Task.Delay(560);}
                for(int i=0;i<10;i++){pending=clock.Elapsed.TotalMilliseconds;if(i%2==0)Call(center,"OpenModule",ControlModule.Wifi,false);else Call(center,"HideDetails");await Task.Delay(75);}
                await Task.Delay(650);CompositionTarget.Rendering-=Frame;center.LayoutUpdated-=Layout;
                var report=$"frames={frames.Count}; callback FPS={1000/frames.Average():F2}; mean={frames.Average():F2}ms; P95={Percentile(frames,.95):F2}ms; P99={Percentile(frames,.99):F2}ms; max={frames.Max():F2}ms; response mean={latency.Average():F2}ms; response P95={Percentile(latency,.95):F2}ms; layout events={layouts}; UI allocations={(GC.GetAllocatedBytesForCurrentThread()-before)/1048576d:F2}MB; captures={center.CapturedFrames}; WPF tier={RenderCapability.Tier>>16}";
                File.WriteAllText(path,report);Console.WriteLine(report);Console.WriteLine("PASS Wi-Fi repeated open/return and rapid reversals; callbacks are not GPU presentation FPS");result=0;
            }catch(Exception ex){Console.WriteLine(ex);}
            finally{CompositionTarget.Rendering-=Frame;center.LayoutUpdated-=Layout;center.Dismiss();await Task.Delay(650);fixture.Close();app.Shutdown(result);}
        };app.Run(fixture);return result;
    }
}

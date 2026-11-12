using System;
using System.Threading.Tasks;
using System.Windows;
using DockForWindows.Services;
using Windows.Devices.Radios;

internal static class ControlCenterServiceChecks
{
    // Read-only diagnostics: never switch radios, routes, volume, power mode or pairing.
    internal static int Run()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;
        app.Startup+=async(_,_)=>{
            using var connectivity=new ConnectivityService();using var media=new MediaControlService();
            try{
                async Task Probe(string name,Func<Task<string>> read){try{Console.WriteLine(name+": "+await read());}catch(Exception ex){Console.WriteLine(name+" unavailable: "+ex.Message);}}
                await Probe("radios",async()=>{await connectivity.Initialize();return $"Wi-Fi={connectivity.State(RadioKind.WiFi)}, Bluetooth={connectivity.State(RadioKind.Bluetooth)}";});
                await Probe("WLAN list",async()=>(await ((IControlCenterConnectivity)connectivity).ReadNetworks()).Count+" networks");
                await Probe("Bluetooth discovery",async()=>{connectivity.StartBluetoothDiscovery();await Task.Delay(4000);return connectivity.BluetoothDevices.Count+" devices";});
                await Probe("audio",()=>Task.Run(()=>SystemControlService.ReadAudio().ToString()));
                await Probe("audio outputs",()=>Task.Run(()=>AudioDeviceService.Outputs().Count+" endpoints"));
                await Probe("audio inputs",()=>Task.Run(()=>AudioDeviceService.Inputs().Count+" endpoints"));
                await Probe("microphone",()=>Task.Run(()=>SystemControlService.ReadMicrophone().ToString()));
                await Probe("performance mode",()=>Task.Run(()=>SystemControlService.ReadPerformanceMode()?.ToString()??"unavailable"));
                foreach(var display in System.Windows.Forms.Screen.AllScreens){var device=display.DeviceName;
                    await Probe("refresh "+device,()=>Task.Run(()=>$"current={DisplayControlService.ReadRefreshRate(device)}, supported={string.Join(',',DisplayControlService.RefreshRates(device))}"));
                    await Probe("HDR "+device,()=>Task.Run(()=>DisplayControlService.ReadHdr(device).ToString()));
                }
                await Probe("native tools",()=>Task.Run(()=>$"capture={SystemToolService.CanCapture}, record={SystemToolService.CanRecord}, dictate={SystemToolService.CanDictate}"));
                using(var awake=new AwakeService()){awake.Start(TimeSpan.FromMilliseconds(120));await Task.Delay(30);awake.Start(TimeSpan.FromMilliseconds(280));await Task.Delay(140);if(!awake.Active)throw new Exception("old awake timer ended new request");await Task.Delay(250);if(awake.Active)throw new Exception("awake request did not expire");Console.WriteLine("PASS own-process awake request expires and old timer cannot stop new request");}
                await Probe("brightness",()=>Task.Run(()=>SystemControlService.ReadBrightness()?.ToString()??"unsupported monitor"));
                await Probe("power",()=>Task.Run(()=>SystemControlService.ReadPower().ToString()));
                await Probe("projection",()=>Task.Run(ProjectionService.ReadTopology));
                await Probe("focus",()=>Task.FromResult(MediaControlService.ReadFocus()?.ToString()??"unsupported"));
                await Probe("media",async()=>{await media.Initialize();return (await media.Read()) is null?"no media session":"live session available";});
                Console.WriteLine("PASS native read-only probes completed; hardware writes were not tested");result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{app.Shutdown();}
        };app.Run();return result;
    }
}

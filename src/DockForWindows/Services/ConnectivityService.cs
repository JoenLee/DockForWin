using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Windows.Devices.Radios;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DockForWindows.Services;

public sealed record WifiNetwork(Guid Adapter,string Name,string Profile,uint Signal,bool Connected,bool Secure,int Authentication=0,int Cipher=0);
public sealed record BluetoothPeer(string Id,string Name,bool Connected,bool Paired=true);

// Native WLAN owns credentials and connection negotiation; radio changes are confirmed by Windows.
public sealed class ConnectivityService : IControlCenterConnectivity
{
    private IReadOnlyList<Radio> _radios=Array.Empty<Radio>();
    private bool _disposed;
    private DeviceWatcher? _bluetoothWatcher;
    private TaskCompletionSource<bool>? _bluetoothEnumeration;
    private readonly Dictionary<string,DeviceInformation> _bluetoothDevices=new();
    private readonly object _bluetoothGate=new();
    public event Action? Changed;
    public async Task Initialize()
    {
        var radios=(await Radio.GetRadiosAsync()).ToArray();if(_disposed)return;_radios=radios;
        foreach(var r in _radios)r.StateChanged+=RadioChanged;
    }
    private void RadioChanged(Radio sender,object args)=>Changed?.Invoke();
    public RadioState? State(RadioKind kind)=>_radios.FirstOrDefault(r=>r.Kind==kind)?.State;
    public async Task SetRadio(RadioKind kind,bool enabled)
    {
        if(await Radio.RequestAccessAsync()!=RadioAccessStatus.Allowed)throw new UnauthorizedAccessException("Windows 未允许控制此无线设备，请检查隐私权限或硬件开关。");
        var radios=_radios.Where(r=>r.Kind==kind).ToArray();if(radios.Length==0)throw new InvalidOperationException("未检测到此无线设备。");
        foreach(var radio in radios)if(await radio.SetStateAsync(enabled?RadioState.On:RadioState.Off)!=RadioAccessStatus.Allowed)throw new UnauthorizedAccessException("系统策略拒绝修改无线设备状态。");
        for(int i=0;i<30&&radios.Any(r=>r.State!=(enabled?RadioState.On:RadioState.Off));i++)await Task.Delay(100);
        if(radios.Any(r=>r.State!=(enabled?RadioState.On:RadioState.Off)))throw new InvalidOperationException("无线设备未确认切换，可能被硬件开关或系统策略限制。");
        Changed?.Invoke();
    }
    public async Task<IReadOnlyList<BluetoothPeer>> PairedDevices()
    {
        var devices=await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true),new[]{"System.Devices.Aep.IsConnected"});
        return devices.Select(d=>new BluetoothPeer(d.Id,d.Name,d.Properties.TryGetValue("System.Devices.Aep.IsConnected",out var value)&&value is true)).ToArray();
    }
    Task<IReadOnlyList<WifiNetwork>> IControlCenterConnectivity.ReadNetworks(bool scan)=>Task.Run(()=>Networks(scan));
    public bool CanListNetworks=>WifiLocationConsent.AllowsNetworkList();
    public const string LocationRequiredMessage="读取附近 Wi-Fi 需要 Windows 位置权限，可在系统隐私设置中允许。";
    async Task IControlCenterConnectivity.Connect(WifiNetwork network,string? password,CancellationToken cancellation)
    {
        await Task.Run(()=>{if(password is null)Connect(network);else ConnectNew(network,password);},cancellation);
        await WaitForConnection(network,cancellation);
    }
    async Task IControlCenterConnectivity.Disconnect(WifiNetwork network,CancellationToken cancellation)
    {
        await Task.Run(()=>Disconnect(network),cancellation);
        for(int i=0;i<20;i++){cancellation.ThrowIfCancellationRequested();if(!(await Task.Run(()=>Networks(),cancellation)).Any(n=>n.Adapter==network.Adapter&&n.Name==network.Name&&n.Connected))return;await Task.Delay(300,cancellation);}
        throw new InvalidOperationException("系统尚未确认断开网络。");
    }
    public IReadOnlyList<BluetoothPeer> BluetoothDevices
    {
        get{lock(_bluetoothGate)return _bluetoothDevices.Values.Where(d=>!string.IsNullOrWhiteSpace(d.Name)).Select(d=>new BluetoothPeer(d.Id,d.Name,d.Properties.TryGetValue("System.Devices.Aep.IsConnected",out var value)&&value is true,d.Pairing.IsPaired)).OrderByDescending(d=>d.Connected).ThenByDescending(d=>d.Paired).ThenBy(d=>d.Name).ToArray();}
    }
    public void StartBluetoothDiscovery()
    {
        if(_disposed||_bluetoothWatcher is not null)return;
        // Association endpoints include classic Bluetooth and Bluetooth LE.
        const string selector="System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";
        var watcher=DeviceInformation.CreateWatcher(selector,new[]{"System.Devices.Aep.IsConnected","System.Devices.Aep.IsPaired"},DeviceInformationKind.AssociationEndpoint);
        watcher.Added+=BluetoothAdded;watcher.Updated+=BluetoothUpdated;watcher.Removed+=BluetoothRemoved;watcher.EnumerationCompleted+=BluetoothCompleted;
        _bluetoothEnumeration=new(TaskCreationOptions.RunContinuationsAsynchronously);_bluetoothWatcher=watcher;
        try{watcher.Start();}catch{StopBluetoothDiscovery();throw;}
    }
    private void BluetoothAdded(DeviceWatcher sender,DeviceInformation info){lock(_bluetoothGate){if(_disposed||sender!=_bluetoothWatcher)return;_bluetoothDevices[info.Id]=info;}Changed?.Invoke();}
    private void BluetoothUpdated(DeviceWatcher sender,DeviceInformationUpdate update){lock(_bluetoothGate){if(_disposed||sender!=_bluetoothWatcher)return;if(_bluetoothDevices.TryGetValue(update.Id,out var info))info.Update(update);}Changed?.Invoke();}
    private void BluetoothRemoved(DeviceWatcher sender,DeviceInformationUpdate update){lock(_bluetoothGate){if(_disposed||sender!=_bluetoothWatcher)return;_bluetoothDevices.Remove(update.Id);}Changed?.Invoke();}
    private void BluetoothCompleted(DeviceWatcher sender,object args){if(sender==_bluetoothWatcher)_bluetoothEnumeration?.TrySetResult(true);Changed?.Invoke();}
    public async Task WaitForBluetoothEnumeration(CancellationToken cancellation)
    {var task=_bluetoothEnumeration?.Task;if(task is null)return;try{await task.WaitAsync(TimeSpan.FromSeconds(4),cancellation);}catch(TimeoutException){/* Show the discovery snapshot obtained so far. */}}
    public void StopBluetoothDiscovery()
    {
        var watcher=_bluetoothWatcher;_bluetoothWatcher=null;_bluetoothEnumeration?.TrySetResult(false);_bluetoothEnumeration=null;if(watcher is null)return;
        watcher.Added-=BluetoothAdded;watcher.Updated-=BluetoothUpdated;watcher.Removed-=BluetoothRemoved;watcher.EnumerationCompleted-=BluetoothCompleted;
        if(watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)watcher.Stop();
        lock(_bluetoothGate)_bluetoothDevices.Clear();
    }
    public async Task PairBluetooth(BluetoothPeer peer)
    {
        var info=await DeviceInformation.CreateFromIdAsync(peer.Id,null,DeviceInformationKind.AssociationEndpoint);
        if(info is null)throw new InvalidOperationException("设备已不在附近，请重新扫描。");
        var result=await info.Pairing.PairAsync();
        if(result.Status is not (DevicePairingResultStatus.Paired or DevicePairingResultStatus.AlreadyPaired))throw new InvalidOperationException($"蓝牙配对未完成：{result.Status}");
        Changed?.Invoke();
    }
    public async Task ForgetBluetooth(BluetoothPeer peer)
    {
        var info=await DeviceInformation.CreateFromIdAsync(peer.Id,null,DeviceInformationKind.AssociationEndpoint);
        if(info is null)throw new InvalidOperationException("未找到此设备。");
        var result=await info.Pairing.UnpairAsync();
        if(result.Status is not (DeviceUnpairingResultStatus.Unpaired or DeviceUnpairingResultStatus.AlreadyUnpaired))throw new InvalidOperationException($"取消配对未完成：{result.Status}");
        Changed?.Invoke();
    }
    public void Dispose(){_disposed=true;StopBluetoothDiscovery();foreach(var r in _radios)r.StateChanged-=RadioChanged;_radios=Array.Empty<Radio>();}

    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Adapter{public Guid Id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Description;public int State;}
    [StructLayout(LayoutKind.Sequential)]private struct Ssid{public uint Length;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Bytes;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Network
    {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Profile;public Ssid Ssid;public int BssType;public uint BssCount;[MarshalAs(UnmanagedType.Bool)]public bool Connectable;public uint Reason;public uint PhyCount;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=8)]public int[] Phys;[MarshalAs(UnmanagedType.Bool)]public bool MorePhy;public uint Signal;[MarshalAs(UnmanagedType.Bool)]public bool Security;public int Auth,Cipher;public uint Flags,Reserved;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Connection{public int Mode;[MarshalAs(UnmanagedType.LPWStr)]public string Profile;public nint Ssid,Bssid;public int BssType;public uint Flags;}
    [DllImport("wlanapi.dll")]private static extern uint WlanOpenHandle(uint version,nint reserved,out uint negotiated,out nint handle);
    [DllImport("wlanapi.dll")]private static extern uint WlanCloseHandle(nint handle,nint reserved);
    [DllImport("wlanapi.dll")]private static extern uint WlanEnumInterfaces(nint handle,nint reserved,out nint list);
    [DllImport("wlanapi.dll")]private static extern uint WlanGetAvailableNetworkList(nint handle,ref Guid id,uint flags,nint reserved,out nint list);
    [DllImport("wlanapi.dll")]private static extern uint WlanScan(nint handle,ref Guid id,nint ssid,nint data,nint reserved);
    [DllImport("wlanapi.dll")]private static extern uint WlanDisconnect(nint handle,ref Guid id,nint reserved);
    [DllImport("wlanapi.dll",CharSet=CharSet.Unicode)]private static extern uint WlanConnect(nint handle,ref Guid id,ref Connection parameters,nint reserved);
    [DllImport("wlanapi.dll",CharSet=CharSet.Unicode)]private static extern uint WlanSetProfile(nint handle,ref Guid id,uint flags,string xml,string? security,[MarshalAs(UnmanagedType.Bool)]bool overwrite,nint reserved,out uint reason);
    [DllImport("wlanapi.dll")]private static extern void WlanFreeMemory(nint memory);
    private static void Check(uint result){if(result!=0)throw new Win32Exception((int)result,result==5?LocationRequiredMessage:new Win32Exception((int)result).Message);}
    private static T Wlan<T>(Func<nint,T> action){Check(WlanOpenHandle(2,0,out _,out var handle));try{return action(handle);}finally{WlanCloseHandle(handle,0);}}
    private static List<Adapter> Adapters(nint handle)
    {
        Check(WlanEnumInterfaces(handle,0,out var memory));try{var result=new List<Adapter>();int count=Marshal.ReadInt32(memory),size=Marshal.SizeOf<Adapter>();for(int i=0;i<count;i++)result.Add(Marshal.PtrToStructure<Adapter>(memory+8+i*size));return result;}finally{WlanFreeMemory(memory);}
    }
    public static IReadOnlyList<WifiNetwork> Networks(bool scan=false)=>Wlan(handle=>
    {
        var result=new List<WifiNetwork>();foreach(var adapter in Adapters(handle))
        {
            var id=adapter.Id;if(scan)Check(WlanScan(handle,ref id,0,0,0));
            Check(WlanGetAvailableNetworkList(handle,ref id,0,0,out var memory));
            try{int count=Marshal.ReadInt32(memory),size=Marshal.SizeOf<Network>();for(int i=0;i<count;i++){var n=Marshal.PtrToStructure<Network>(memory+8+i*size);string name=Encoding.UTF8.GetString(n.Ssid.Bytes,0,(int)Math.Min(n.Ssid.Length,32));if(name.Length>0)result.Add(new(id,name,n.Profile,n.Signal,(n.Flags&1)!=0,n.Security,n.Auth,n.Cipher));}}finally{WlanFreeMemory(memory);}
        }
        return (IReadOnlyList<WifiNetwork>)result.OrderByDescending(n=>n.Connected).ThenByDescending(n=>n.Signal).GroupBy(n=>(n.Adapter,n.Name)).Select(g=>g.First()).ToArray();
    });
    public static void Connect(WifiNetwork network)=>Wlan(handle=>
    {
        if(string.IsNullOrEmpty(network.Profile))throw new InvalidOperationException("此网络尚未保存凭据，请先通过 Windows Wi-Fi 面板连接一次。");
        var id=network.Adapter;var p=new Connection{Mode=0,Profile=network.Profile,BssType=1};Check(WlanConnect(handle,ref id,ref p,0));return true;
    });
    public static void Disconnect(WifiNetwork network)=>Wlan(handle=>{var id=network.Adapter;Check(WlanDisconnect(handle,ref id,0));return true;});
    public static bool SupportsPassword(WifiNetwork n)=>!n.Secure||n.Authentication is 4 or 7 or 9;
    public static void ConnectNew(WifiNetwork network,string password)
    {
        if(!SupportsPassword(network))throw new NotSupportedException("企业认证或此加密类型需要 Windows 原生网络面板。");
        if(network.Secure&&(password.Length<8||password.Length>63))throw new ArgumentException("无线密码应为 8–63 个字符。");
        string Escape(string value)=>System.Security.SecurityElement.Escape(value)??"";
        string auth=!network.Secure?"open":network.Authentication==9?"WPA3SAE":network.Authentication==4?"WPAPSK":"WPA2PSK";
        string cipher=!network.Secure?"none":network.Cipher==2?"TKIP":"AES";
        string key=network.Secure?$"<sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>{Escape(password)}</keyMaterial></sharedKey>":"";
        string xml=$"<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{Escape(network.Name)}</name><SSIDConfig><SSID><hex>{Convert.ToHexString(Encoding.UTF8.GetBytes(network.Name))}</hex><name>{Escape(network.Name)}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>auto</connectionMode><MSM><security><authEncryption><authentication>{auth}</authentication><encryption>{cipher}</encryption><useOneX>false</useOneX></authEncryption>{key}</security></MSM></WLANProfile>";
        Wlan(handle=>{var id=network.Adapter;Check(WlanSetProfile(handle,ref id,2,xml,null,false,0,out _));return true;});Connect(network with{Profile=network.Name});
    }
    public static async Task WaitForConnection(WifiNetwork network,CancellationToken cancellation=default)
    {
        for(int i=0;i<20;i++){await Task.Delay(600,cancellation);var current=await Task.Run(()=>Networks(),cancellation);if(current.Any(n=>n.Adapter==network.Adapter&&n.Name==network.Name&&n.Connected))return;}
        throw new InvalidOperationException("未能连接此网络，请检查密码、信号或网络认证要求。");
    }
}

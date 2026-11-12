using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Radios;

namespace DockForWindows.Services;

public interface IControlCenterConnectivity : IDisposable
{
    event Action? Changed;
    Task Initialize();
    RadioState? State(RadioKind kind);
    Task SetRadio(RadioKind kind, bool enabled);
    Task<IReadOnlyList<WifiNetwork>> ReadNetworks(bool scan = false);
    Task Connect(WifiNetwork network, string? password, CancellationToken cancellation);
    Task Disconnect(WifiNetwork network, CancellationToken cancellation);
    IReadOnlyList<BluetoothPeer> BluetoothDevices { get; }
    void StartBluetoothDiscovery();
    void StopBluetoothDiscovery();
    Task WaitForBluetoothEnumeration(CancellationToken cancellation)=>Task.CompletedTask;
    Task PairBluetooth(BluetoothPeer peer);
    Task ForgetBluetooth(BluetoothPeer peer);
}

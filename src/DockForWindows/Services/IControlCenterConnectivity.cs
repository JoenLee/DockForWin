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
    // False when listing nearby networks would need a location permission Windows has not granted
    // (Windows 11 24H2+). Callers then explain the requirement instead of triggering the system prompt.
    bool CanListNetworks => true;
    Task Connect(WifiNetwork network, string? password, CancellationToken cancellation);
    Task Disconnect(WifiNetwork network, CancellationToken cancellation);
    IReadOnlyList<BluetoothPeer> BluetoothDevices { get; }
    void StartBluetoothDiscovery();
    void StopBluetoothDiscovery();
    Task WaitForBluetoothEnumeration(CancellationToken cancellation)=>Task.CompletedTask;
    Task PairBluetooth(BluetoothPeer peer);
    Task ForgetBluetooth(BluetoothPeer peer);
}

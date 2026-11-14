using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using Windows.Devices.Radios;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private enum ControlPage { Main, Module, Gallery, WifiPassword, BluetoothDevice, DisplayTrial, ControlEditor, PageEditor }
    private enum OperationPhase { Idle, Busy, Error }
    private sealed class OperationState { public OperationPhase Phase;public string? Error; }
    private readonly Dictionary<ControlModule,OperationState> _operations=new();
    private readonly CancellationTokenSource _lifetime=new();
    private readonly List<(ControlCenterToggle Toggle,Func<bool?> Read)> _detailToggles=new();
    private ControlPage _page;
    private ScrollViewer _detailScroll=null!;
    private bool _systemDialog;
    private string? _detailSignature;
    private OperationState Operation(ControlModule kind)
    {if(!_operations.TryGetValue(kind,out var state))_operations[kind]=state=new();return state;}
    private bool IsBusy(ControlModule kind)=>Operation(kind).Phase==OperationPhase.Busy;
    private async Task Execute(ControlModule kind,string message,Func<Task> action)
    {
        if(_closing||IsBusy(kind))return;
        var state=Operation(kind);state.Phase=OperationPhase.Busy;state.Error=null;
        var card=_modules.FirstOrDefault(c=>c.Kind==kind);if(card is not null){card.Body.IsEnabled=false;card.Status.Text=message;}
        try{await action();state.Phase=OperationPhase.Idle;}
        catch(OperationCanceledException)when(_closing){state.Phase=OperationPhase.Idle;}
        catch(Exception ex){state.Phase=OperationPhase.Error;state.Error=ex.Message;if(!_closing)Report(ex);}
        finally
        {
            if(card is not null)card.Body.IsEnabled=true;
            if(!_closing){await PollSystem();SyncDetailToggles();RefreshVisibleDetails(true);if(state.Phase==OperationPhase.Error)_message.Text=state.Error;}
        }
    }
    private void SyncDetailToggles()
    {
        foreach(var (toggle,read) in _detailToggles)
        {
            var value=read();toggle.IsEnabled=value.HasValue&&(_openKind is not {} kind||!IsBusy(kind));
            if(value.HasValue)toggle.IsOn=value.Value;
        }
    }
    private void RefreshVisibleDetails(bool force=false)
    {
        UpdateDeviceLabels();if(!force)return;
        if(Mouse.LeftButton==MouseButtonState.Pressed||_closing||_transitioning||!_detailVisible||_page!=ControlPage.Module||_openKind is not {} kind||IsBusy(kind))return;
        string signature=kind switch{
            ControlModule.Wifi=>$"{_connectivity.State(RadioKind.WiFi)}|"+string.Join("|",_networks.Select(n=>$"{n.Adapter}:{n.Name}:{n.Connected}:{n.Signal/10}")),
            ControlModule.Bluetooth=>$"{_connectivity.State(RadioKind.Bluetooth)}|"+string.Join("|",_connectivity.BluetoothDevices.Select(d=>$"{d.Id}:{d.Name}:{d.Paired}:{d.Connected}")),
            _=>""};
        if(signature.Length>0&&signature!=_detailSignature){_detailSignature=signature;OpenModule(kind,true);}
    }
    private void BackDetails()
    {
        if(_page==ControlPage.DisplayTrial){_rateTrial?.Dispose();_rateTrial=null;OpenModule(ControlModule.RefreshRate,true);}
        else if(_page==ControlPage.WifiPassword){_page=ControlPage.Module;OpenModule(ControlModule.Wifi,true);}
        else if(_page==ControlPage.BluetoothDevice){_page=ControlPage.Module;OpenModule(ControlModule.Bluetooth,true);}
        else HideDetails();
    }
    private void AddRadioToggle(RadioKind kind)
    {
        var module=kind==RadioKind.WiFi?ControlModule.Wifi:ControlModule.Bluetooth;
        AddToggleDetail(kind==RadioKind.WiFi?"Wi-Fi":LocalizedText.Get("蓝牙"),
            ()=>_connectivity.State(kind) is {} state?state==RadioState.On:null,
            on=>Execute(module,on?LocalizedText.Get("正在开启…"):LocalizedText.Get("正在关闭…"),()=>_connectivity.SetRadio(kind,on)));
    }
    private void OpenBluetoothDevice(BluetoothPeer peer)
    {
        _page=ControlPage.BluetoothDevice;ShowDetails(peer.Name);
        AddStatusRow("Bluetooth",peer.Name,peer.Connected?LocalizedText.Get("已连接"):peer.Paired?LocalizedText.Get("已配对，未连接"):LocalizedText.Get("可配对"));
        if(!peer.Paired)AddDetail(LocalizedText.Get("配对此设备"),()=>Run(async()=>{
            _systemDialog=true;
            try{await Execute(ControlModule.Bluetooth,LocalizedText.Get("正在配对…"),()=>_connectivity.PairBluetooth(peer));}
            finally{_systemDialog=false;}
            if(!_closing&&_page==ControlPage.BluetoothDevice&&Operation(ControlModule.Bluetooth).Phase!=OperationPhase.Error)BackDetails();
        }),true);
        else
        {
            _detailBody.Children.Add(new TextBlock{Text=LocalizedText.Get("此设备的连接和断开由 Windows 蓝牙驱动管理。控制中心不会把取消配对当作断开连接。"),TextWrapping=TextWrapping.Wrap,FontSize=11,Margin=new Thickness(0,8,0,8)});
            AddDetail(LocalizedText.Get("打开 Windows 连接管理…"),()=>Native("ms-settings:bluetooth"),true);
            AddDetail(LocalizedText.Get("取消配对…"),()=>{
                // Unpairing removes the remembered device, so require an explicit second action.
                ShowDetails(LocalizedText.Get("取消配对 ")+peer.Name);
                AddDetail(LocalizedText.Get("确认取消配对"),()=>Run(async()=>{
                    await Execute(ControlModule.Bluetooth,LocalizedText.Get("正在取消配对…"),()=>_connectivity.ForgetBluetooth(peer));
                    if(!_closing&&_page==ControlPage.BluetoothDevice&&Operation(ControlModule.Bluetooth).Phase!=OperationPhase.Error)BackDetails();
                }),true);UpdateDetailLayout();
            },true);
        }
        UpdateDetailLayout();
    }
    private void DetailHotspot(FrameworkElement element,ControlModule kind)
    {
        element.Cursor=Cursors.Hand;AutomationProperties.SetName(element,ControlCenterCatalog.Name(kind)+LocalizedText.Get("快捷操作"));
        element.MouseEnter+=(_,_)=>element.Opacity=.76;element.MouseLeave+=(_,_)=>element.Opacity=1;
        element.MouseLeftButtonUp+=(_,e)=>{if(!_editing&&!_dragged){OpenModule(kind);e.Handled=true;}};
        element.Focusable=true;element.KeyDown+=(_,e)=>{if((e.Key is Key.Enter or Key.Space)&&!_editing){PrimaryAction(kind);e.Handled=true;}};
    }
}

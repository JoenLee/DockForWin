using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using Windows.Devices.Radios;
using Forms=System.Windows.Forms;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private string? _networkError;private bool _networkPolling,_networksRead;
    private void SystemChanged(){if(!_closing&&!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(()=>{if(!_closing&&!_stateRefresh.IsEnabled)_stateRefresh.Start();});}
    // Each reading updates its own card as soon as it arrives. The WLAN list (seconds when a driver or the
    // location consent stalls it) and WMI brightness no longer hold every other status at "正在读取".
    private async Task PollSystem()
    {
        if(_polling||_closing)return;_polling=true;
        try
        {
            _ = PollNetworks();
            var brightness=Task.Run(()=>{try{return(Value:SystemControlService.ReadBrightness(),Error:(string?)null);}catch(Exception ex){return(Value:(byte?)null,Error:ex.Message);}});
            var state=await Task.Run(()=>
            {
                AudioState? audio=null;bool? dark=null,saver=null;
                try{audio=SystemControlService.ReadAudio();}catch(Exception ex){_log?.Invoke(LocalizedText.Get("读取音频"),ex);}
                try{dark=SystemControlService.ReadDarkMode();}catch{ }
                try{saver=SystemControlService.ReadPowerSaver();}catch{ }
                return(audio,power:SystemControlService.ReadPower(),dark,saver);
            });if(_closing)return;
            _updating=true;
            try{
                _volume.IsEnabled=state.audio is not null;if(state.audio is not null){_muted=state.audio.Muted;if(!_volume.IsMouseCaptureWithin&&!_volumeBusy&&_pendingVolume is null)_volume.Value=state.audio.Volume;Status(ControlModule.Volume,_muted?LocalizedText.Get("已静音"):$"{_volume.Value:0}%");if(_mute is not null)SetIcon(_mute,_muted?"Muted":"Volume");}else Status(ControlModule.Volume,LocalizedText.Get("无音频设备"));
                Status(ControlModule.Battery,state.power.Percent is int p?$"{p}% · {(state.power.PluggedIn?LocalizedText.Get("充电中"):LocalizedText.Get("电池供电"))}":LocalizedText.Get("已接通电源"));
                UpdateWifiStatus();var bt=_connectivity.State(RadioKind.Bluetooth);Status(ControlModule.Bluetooth,bt==RadioState.On?LocalizedText.Get("已打开"):bt is null?LocalizedText.Get("无蓝牙设备"):LocalizedText.Get("已关闭"));
                Active(ControlModule.Bluetooth,bt==RadioState.On);
                Status(ControlModule.DarkMode,state.dark is null?LocalizedText.Get("系统不支持"):state.dark==true?LocalizedText.Get("深色"):LocalizedText.Get("浅色"));Active(ControlModule.DarkMode,state.dark==true);
                Status(ControlModule.PowerSaver,state.saver is null?LocalizedText.Get("系统不支持"):state.saver==true?LocalizedText.Get("已开启"):LocalizedText.Get("已关闭"));Active(ControlModule.PowerSaver,state.saver==true);
            }finally{_updating=false;}
            var light=await brightness;if(_closing)return;_updating=true;
            _brightness.IsEnabled=light.Value.HasValue;if(light.Value is byte b){if(!_brightness.IsMouseCaptureWithin&&!_brightnessDelay.IsEnabled&&!_brightnessBusy)_brightness.Value=b;Status(ControlModule.Brightness,$"{_brightness.Value:0}%");}else{Status(ControlModule.Brightness,LocalizedText.Get("显示器不支持调节"));_brightness.ToolTip=light.Error??LocalizedText.Get("内置屏幕使用 WMI；未提供亮度接口的外接屏幕请使用显示器按键。");}
            try{Status(ControlModule.Focus,MediaControlService.ReadFocus()==true?LocalizedText.Get("专注中"):LocalizedText.Get("Windows 专注"));}catch{Status(ControlModule.Focus,LocalizedText.Get("Windows 专注"));}try{Status(ControlModule.Projection,await Task.Run(ProjectionService.ReadTopology));}catch{Status(ControlModule.Projection,LocalizedText.Get("投影与无线显示"));}
            await UpdateExtraState();if(_closing)return;
            _ = RefreshMedia();
        }
        catch(Exception ex){Report(ex);}finally{_updating=false;_polling=false;if(!_closing){SyncDetailToggles();RefreshVisibleDetails();}}
    }
    // Runs beside PollSystem with its own guard. Without location consent (Windows 11 24H2+) it does not
    // call the WLAN list at all, so a status poll never raises the system location prompt.
    private async Task PollNetworks()
    {
        if(_networkPolling||_closing)return;
        if(!_connectivity.CanListNetworks){SetNetworkError(ConnectivityService.LocationRequiredMessage,null);UpdateWifiStatus();return;}
        _networkPolling=true;
        try{
            IReadOnlyList<WifiNetwork>? networks=null;Exception? error=null;
            try{networks=await _connectivity.ReadNetworks();}catch(Exception ex){error=ex;}
            if(_closing)return;
            if(networks is not null){_networks=networks;_networksRead=true;}
            SetNetworkError(error?.Message,error);UpdateWifiStatus();
        }
        catch(Exception ex){Report(ex);}finally{_networkPolling=false;}
    }
    private void SetNetworkError(string? message,Exception? error)
    {
        if(message==_networkError)return;_networkError=message;
        if(message is not null)_log?.Invoke(error is null?LocalizedText.Get("读取 Wi-Fi：")+message:LocalizedText.Get("读取 Wi-Fi"),error);
    }
    private void UpdateWifiStatus()
    {
        var wifi=_connectivity.State(RadioKind.WiFi);Active(ControlModule.Wifi,wifi==RadioState.On);
        string? text=wifi==RadioState.Off?LocalizedText.Get("已关闭"):wifi is null?LocalizedText.Get("无无线设备"):_networkError is not null?(_networkError.Contains(LocalizedText.Get("位置权限"),StringComparison.OrdinalIgnoreCase)?LocalizedText.Get("需要位置权限"):LocalizedText.Get("状态不可用")):_networksRead?_networks.FirstOrDefault(n=>n.Connected)?.Name??LocalizedText.Get("未连接"):null;
        if(text is not null)Status(ControlModule.Wifi,text);
    }
    private async Task ToggleRadio(RadioKind kind){await _connectivity.SetRadio(kind,_connectivity.State(kind)!=RadioState.On);await PollSystem();}
    private Task ToggleMute()=>Execute(ControlModule.Volume,LocalizedText.Get("正在切换…"),()=>Task.Run(()=>SystemControlService.SetMute(!_muted)));
    private async Task WriteVolume()
    {
        if(_volumeBusy||_closing)return;_volumeBusy=true;
        try{
            // Keep only the latest slider value; COM endpoint calls never queue behind pointer events.
            while(!_closing&&_pendingVolume is double value){_pendingVolume=null;await Task.Run(()=>SystemControlService.SetVolume(value));await Task.Delay(24,_lifetime.Token);}
        }catch(OperationCanceledException)when(_closing){}catch(Exception ex){_pendingVolume=null;if(!_closing)Report(ex);}
        finally{_volumeBusy=false;}
    }
    private async Task WriteBrightness()
    {
        if(_brightnessBusy||_closing)return;_brightnessBusy=true;double value=_pendingBrightness;try{if(!await Task.Run(()=>SystemControlService.SetBrightness(value)))throw new InvalidOperationException(LocalizedText.Get("显示器未接受亮度调节。"));}catch(Exception ex){Report(ex);}finally{_brightnessBusy=false;if(!_closing&&Math.Abs(_pendingBrightness-value)>.5)_brightnessDelay.Start();}
    }
    private void OpenModule(ControlModule kind,bool refresh=false)=>Run(async()=>
    {
        if(_closing||(_detailVisible&&_openKind==kind&&!refresh))return;_openKind=kind;_page=ControlPage.Module;if(kind!=ControlModule.Bluetooth)_connectivity.StopBluetoothDiscovery();
        // 从一级界面点击进入时，以被点击的模块为“来源”做放大展开转场；已在二级页内切换模块则用交叉淡入淡出。
        var source=_detailVisible?null:_modules.FirstOrDefault(c=>c.Kind==kind);
        ShowDetails(ModuleName(kind),source,refresh);int page=_transition;bool Current()=>!_closing&&_detailVisible&&page==_transition;
        switch(kind)
        {
            case ControlModule.Wifi:
                AddRadioToggle(RadioKind.WiFi);
                if(_connectivity.State(RadioKind.WiFi)==RadioState.On&&!_connectivity.CanListNetworks){
                    // Listing would make Windows show its own location prompt (signed ".NET Host"); explain instead.
                    AddStatusRow("Wifi",LocalizedText.Get("需要位置权限"),LocalizedText.Get("Windows 11 列出附近的 Wi-Fi 需要开启位置服务。开启后回到这里刷新即可。"));
                    AddDetail(LocalizedText.Get("打开位置设置…"),()=>Native(WifiLocationConsent.SettingsUri));
                }
                else if(_connectivity.State(RadioKind.WiFi)==RadioState.On){
                    var networks=await _connectivity.ReadNetworks(scan:!refresh);if(!Current())return;_networks=networks;
                    foreach(var n in networks){
                        var button=AddDetail(NetworkText(n),()=>Run(async()=>{
                            var actual=_networks.FirstOrDefault(x=>x.Adapter==n.Adapter&&x.Name==n.Name)??n;
                            if(actual.Connected)await Execute(kind,LocalizedText.Get("正在断开…"),()=>_connectivity.Disconnect(actual,_lifetime.Token));
                            else if(string.IsNullOrEmpty(actual.Profile)){ConnectNetwork(actual);return;}
                            else await Execute(kind,LocalizedText.Get("正在连接…"),()=>_connectivity.Connect(actual,null,_lifetime.Token));
                        }));
                        AttachWifiSignal(button,()=>_networks.FirstOrDefault(x=>x.Adapter==n.Adapter&&x.Name==n.Name)??n);
                        _deviceRows.Add((button,()=>NetworkText(_networks.FirstOrDefault(x=>x.Adapter==n.Adapter&&x.Name==n.Name)??n)));
                    }
                    if(networks.Count==0)AddStatusRow("Wifi",LocalizedText.Get("未发现可用网络"),LocalizedText.Get("请确认无线网络已开启，再刷新。"));
                }
                AddDetail(LocalizedText.Get("Windows 网络 / 企业认证…"),()=>Native("ms-availablenetworks:"));
                break;
            case ControlModule.Bluetooth:
                AddRadioToggle(RadioKind.Bluetooth);
                if(_connectivity.State(RadioKind.Bluetooth)==RadioState.On){
                    _connectivity.StartBluetoothDiscovery();
                    var peers=_connectivity.BluetoothDevices;
                    foreach(var peer in peers){var button=AddDetail(BluetoothText(peer),()=>OpenBluetoothDevice(_connectivity.BluetoothDevices.FirstOrDefault(x=>x.Id==peer.Id)??peer));_deviceRows.Add((button,()=>BluetoothText(_connectivity.BluetoothDevices.FirstOrDefault(x=>x.Id==peer.Id)??peer)));}
                    if(peers.Count==0)AddStatusRow("Bluetooth",LocalizedText.Get("正在查找设备"),LocalizedText.Get("让设备进入可发现模式，列表将自动更新。"));
                }
                if(_connectivity.State(RadioKind.Bluetooth)!=RadioState.On)_connectivity.StopBluetoothDiscovery();
                AddDetail(LocalizedText.Get("Windows 蓝牙管理…"),()=>Native("ms-settings:bluetooth"));break;            case ControlModule.Volume:
                var outputs=await Task.Run(AudioDeviceService.Outputs);if(!Current())return;foreach(var item in outputs)AddDetail((item.Default?"✓ ":"")+item.Name,()=>Run(async()=>{await Execute(kind,LocalizedText.Get("正在切换…"),async()=>{await Task.Run(()=>AudioDeviceService.SetDefault(item.Id));await PollSystem();});if(!_closing&&_openKind==kind)OpenModule(kind,true);}));AddDetail(LocalizedText.Get("更多声音设置…"),()=>Native("ms-settings:sound"));break;
            case ControlModule.Projection:
                AddDetail(LocalizedText.Get("仅电脑屏幕"),()=>Display(1));AddDetail(LocalizedText.Get("复制"),()=>Display(2));AddDetail(LocalizedText.Get("扩展"),()=>Display(4));AddDetail(LocalizedText.Get("仅第二屏幕"),()=>Display(8));AddDetail(LocalizedText.Get("连接无线显示器…"),()=>Native("ms-settings-connectabledevices:devicediscovery"));break;
            case ControlModule.Focus:
                _detailBody.Children.Add(new TextBlock{Text=LocalizedText.Get("与 Windows 专注时段同步。此系统限制第三方应用直接启停，请使用原生专注控制。"),TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,10,0,10)});AddDetail(LocalizedText.Get("打开 Windows 专注"),()=>Native("ms-settings:quietmomentshome"));break;
            case ControlModule.Brightness:AddDetail(LocalizedText.Get("显示设置…"),()=>Native("ms-settings:display"));break;
            case ControlModule.Battery:
                var power=SystemControlService.ReadPower();AddStatusRow("Battery",power.Percent is int percent?$"{percent}%":LocalizedText.Get("无电池"),power.PluggedIn?LocalizedText.Get("已接通电源"):LocalizedText.Get("电池供电"));BuildPowerToggle();AddDetail(LocalizedText.Get("电源与电池…"),()=>Native("ms-settings:powersleep"));break;
            case ControlModule.DarkMode:
                AddToggleDetail(LocalizedText.Get("深色模式"),SystemControlService.ReadDarkMode,on=>Execute(kind,LocalizedText.Get("正在切换…"),()=>Task.Run(()=>SystemControlService.SetDarkMode(on))));
                AddDetail(LocalizedText.Get("显示设置…"),()=>Native("ms-settings:display"));break;
            case ControlModule.PowerSaver:BuildPowerToggle();AddDetail(LocalizedText.Get("电源与电池…"),()=>Native("ms-settings:powersleep"));break;            case ControlModule.Media:BuildMediaDetails();break;
            default:await BuildExtraDetails(kind);break;
        }
        if(Current()){UpdateDetailLayout();if(!refresh&&kind is ControlModule.Wifi or ControlModule.Bluetooth)_ = RefreshEnteredDevices(kind,page);}
    });
    private void ConnectNetwork(WifiNetwork network)
    {
        if(!ConnectivityService.SupportsPassword(network)){Native("ms-availablenetworks:");return;}
        _page=ControlPage.WifiPassword;ShowDetails(network.Name);int page=_transition;var password=new PasswordBox{Padding=new Thickness(10),Margin=new Thickness(0,12,0,8),FontSize=12};
        if(network.Secure){_detailBody.Children.Add(new TextBlock{Text=LocalizedText.Get("输入 Wi-Fi 密码"),FontSize=12,Margin=new Thickness(0,12,0,0)});_detailBody.Children.Add(password);password.Focus();}
        AddDetail(LocalizedText.Get("连接"),()=>Run(async()=>{
            if(IsBusy(ControlModule.Wifi))return;
            string secret=password.Password;password.Clear();password.IsEnabled=false;
            try{
                await Execute(ControlModule.Wifi,LocalizedText.Get("正在连接…"),()=>_connectivity.Connect(network,secret,_lifetime.Token));
                if(!_closing&&page==_transition&&Operation(ControlModule.Wifi).Phase!=OperationPhase.Error)BackDetails();
            }finally{password.IsEnabled=true;}
        }),true);        AddDetail(LocalizedText.Get("使用 Windows 网络面板…"),()=>Native("ms-availablenetworks:"));
        UpdateDetailLayout();
    }
    private void ShowDetails(string title,Card? from=null,bool refresh=false)
    {
        if(!_detailVisible&&!refresh)_returnCard=from;double offset=refresh?_detailScroll.VerticalOffset:0;_detailScroll.VerticalScrollBarVisibility=_openKind==ControlModule.Media&&_page==ControlPage.Module?ScrollBarVisibility.Disabled:ScrollBarVisibility.Auto;_detailToggles.Clear();_mediaProgress=null;_detailPlay=_detailPrevious=_detailNext=null;
        if(_mediaResize is not null)_details.Children.Remove(_mediaResize);_detailBody.Children.Clear();_microphoneSlider=null;_detailPlayer=null;_deviceRows.Clear();_detailTitle.Text=title;_detailRefresh.Visibility=_page==ControlPage.Module&&_openKind is ControlModule.Wifi or ControlModule.Bluetooth?Visibility.Visible:Visibility.Collapsed;_mediaCover=null;_mediaTitle=null;_mediaArtist=null;_mediaElapsed=null;_mediaRemaining=null;_mediaVolume=null;_mediaResize=null;
        _stage.Width=((_openKind==ControlModule.Media&&_page==ControlPage.Module)?_options.Geometry.PanelWidth-36:_options.Geometry.PanelWidth-36)*_options.Geometry.ComponentScale;Width=(_stage.Width+SideGutter*2)*_fit.ScaleX;
        _detailVisible=true;((Grid)Content).Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));_message.Text="";if(!refresh)Transition(true,from);else if(_navigationStarted<0){_cards.Visibility=Visibility.Collapsed;_details.Visibility=Visibility.Visible;_details.IsHitTestVisible=true;}
        UpdateDetailLayout();int version=_transition;Dispatcher.BeginInvoke(()=>{if(version==_transition)_detailScroll.ScrollToVerticalOffset(offset);});
    }
    private void HideDetails(){var from=_returnCard??(_openKind is ControlModule k?_modules.FirstOrDefault(c=>c.Kind==k):null);_detailVisible=false;_page=ControlPage.Main;_detailToggles.Clear();_detailSignature=null;_connectivity.StopBluetoothDiscovery();_openKind=null;_stage.Width=(_options.Geometry.PanelWidth-36)*_options.Geometry.ComponentScale;Width=(_stage.Width+SideGutter*2)*_fit.ScaleX;_message.Text="";Transition(false,from);SetStageHeight(_mainHeight,true);_chromeDirty=true;}
    private Button AddDetail(string text,Action action,bool editor=false)
    {var button=TextButton(text,action,editor);button.RenderTransform=Transform.Identity;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.MinHeight=48;button.ClipToBounds=true;if(button.Content is TextBlock label){label.FontSize=Math.Max(14,_options.Geometry.FontSize);label.TextWrapping=TextWrapping.Wrap;label.TextTrimming=TextTrimming.CharacterEllipsis;label.Margin=new Thickness(12,8,12,8);}var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);var hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);hit.AppendChild(presenter);button.Template=new ControlTemplate(typeof(Button)){VisualTree=hit};_detailBody.Children.Add(new Border{Child=button,Tag="detail-divider",BorderBrush=new SolidColorBrush(Color.FromArgb(100,128,128,128)),BorderThickness=new Thickness(0,0,0,1),Margin=new Thickness(0,0,10,0)});return button;}
    private void AddStatusRow(string glyph,string title,string subtitle)
    {var row=new Grid{Margin=new Thickness(0,6,0,6),MinHeight=38};row.ColumnDefinitions.Add(new(){Width=new GridLength(30)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.Children.Add(new ControlCenterIcon(glyph){Width=18,Height=18,Brush=Foreground,VerticalAlignment=VerticalAlignment.Center});var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(new TextBlock{Text=title,FontSize=14,TextTrimming=TextTrimming.CharacterEllipsis});labels.Children.Add(new TextBlock{Text=subtitle,FontSize=12,Opacity=.75,TextWrapping=TextWrapping.Wrap});Grid.SetColumn(labels,1);row.Children.Add(labels);_detailBody.Children.Add(row);}
    private void AddToggleDetail(string text,Func<bool?> read,Func<bool,Task> toggled)
    {
        var row=new Grid{Margin=new Thickness(0,6,0,6),MinHeight=42};
        row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var label=new TextBlock{Text=text,FontSize=14,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
        var accent=_palette.Accent is SolidColorBrush a?a.Color:Palette.Brush("#007AFF").Color;var initial=read();
        var toggle=new ControlCenterToggle(accent,Palette.Brush(_palette.Interface.Track).Color){IsOn=initial==true,IsEnabled=initial.HasValue,Animate=Animate,VerticalAlignment=VerticalAlignment.Center};
        AutomationProperties.SetName(toggle,text);
        toggle.Toggled+=async on=>{toggle.IsEnabled=false;try{await toggled(on);}catch(Exception ex){Report(ex);}finally{if(!_closing){var actual=read();toggle.IsOn=actual==true;toggle.IsEnabled=actual.HasValue;}}};
        _detailToggles.Add((toggle,read));
        row.Children.Add(label);Grid.SetColumn(toggle,1);row.Children.Add(toggle);_detailBody.Children.Add(row);
    }
    private void BuildPowerToggle()
    {
        AddToggleDetail(LocalizedText.Get("节能电源模式"),SystemControlService.ReadPowerSaver,on=>Execute(ControlModule.PowerSaver,LocalizedText.Get("正在切换…"),()=>Task.Run(()=>SystemControlService.SetPowerSaver(on))));
        _detailBody.Children.Add(new TextBlock{Text=LocalizedText.Get("此开关控制 Windows 的节能电源模式（最佳能效）。系统节电功能由“电源与电池”设置管理。"),FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)});
    }    private void UpdateDetailLayout()
    {
        if(!_detailVisible)return;double zoom=_options.Geometry.ComponentScale;
        double available=Math.Max(1,_stage.Width-2*ControlCenterTokens.Padding(_options.Geometry.CardPadding)*zoom-SystemParameters.VerticalScrollBarWidth-10*zoom);
        _detailBody.Measure(new Size(available,double.PositiveInfinity));
        double height=_openKind==ControlModule.Media&&_page==ControlPage.Module?ControlCenterLayout.Size(ControlCardShape.Large,_options.Geometry,ControlModule.Media).Height:Math.Min(510*zoom,Math.Max(112*zoom,_detailBody.DesiredSize.Height+64*zoom));
        FitMediaBody();SetStageHeight(height,true);UpdateDetailClip();TextForeground(_details,_detailLight==false?DarkInk:Brushes.White);Dispatcher.BeginInvoke(RefreshChrome);
    }
    private bool _transitioning;private readonly TranslateTransform _detailOffset=new(),_mainOffset=new();
    private void UpdateDetailClip()
    {
        if(_details.ActualWidth<=0||_details.ActualHeight<=0)return;
        double z=_options.Geometry.ComponentScale,r=DetailPhysicalRadius(_details.RenderSize);double safe=Math.Max(20,ControlCenterTokens.Padding(_options.Geometry.CardPadding))*z,cornerInset=r*(1-1/Math.Sqrt(2)),center=Math.Max(24*z,cornerInset+14*z);_detailHeader.Margin=new Thickness(Math.Max(safe,cornerInset+8*z),center-15*z,center-15*z,0);_detailContent.Margin=new Thickness(Math.Max(safe,cornerInset+8*z),16*z,Math.Max(safe,cornerInset+8*z),20*z);_detailContent.RowDefinitions[0].Height=new GridLength(Math.Max(38*z,center+7*z));
        double half=Math.Min(_details.ActualWidth,_details.ActualHeight)/2;
        _detailSurface.Radius=Math.Clamp(r/half*100,0,99.9);
        _detailSurface.InvalidateVisual();_detailContent.Clip=null;
    }
    private void AnimateDetailHeight(double height,bool animate)
    {
        double current=double.IsFinite(_details.Height)?_details.Height:Math.Max(1,_details.ActualHeight);
        if(double.IsFinite(_details.Height)&&Math.Abs(current-height)<.5)return;
        _details.BeginAnimation(HeightProperty,null);_details.Height=height;
        if(animate&&Animate&&_navigationStarted<0)ControlMotion.To(_details,HeightProperty,height,current);
    }
    private void Transition(bool forward,Card? from=null)
    {
        int version=++_transition;_transitioning=Animate;
        var incoming=forward?_details:(FrameworkElement)_cards;var outgoing=forward?_cards:(FrameworkElement)_details;
        bool wasVisible=incoming.Visibility==Visibility.Visible;incoming.Visibility=outgoing.Visibility=Visibility.Visible;
        incoming.IsHitTestVisible=true;outgoing.IsHitTestVisible=false;
        // Every page stays on its own baseline. Content growth is drawn by the morphing plate.
        _details.RenderTransform=Transform.Identity;_cards.RenderTransform=Transform.Identity;
        StartNavigationGlass(forward,from);
        if(_navigationStarted>=0)return;
        if(!Animate){FinishNavigationGlass();return;}
        double speed=ControlMotion.PageSpeed*(forward?1:ControlMotion.ExitSpeed);
        ControlMotion.To(incoming,OpacityProperty,1,wasVisible?null:0,speed:speed,completed:()=>{if(version==_transition){outgoing.Visibility=Visibility.Collapsed;_transitioning=false;_chromeDirty=true;}});
        ControlMotion.To(outgoing,OpacityProperty,0,speed:speed);
    }

    private Slider? _mediaProgress;private Button? _detailPlay,_detailPrevious,_detailNext;
    private void SetIcon(Button button,string glyph)
    {if(button.Content is ControlCenterIcon current){current.ChangeGlyph(glyph,Animate);return;}double size=button.Content is ControlCenterIcon oldIcon?oldIcon.Width:button==_play?23:button==_mediaIcon?22:button==_mute?17:18;button.Content=new ControlCenterIcon(glyph){Width=size,Height=size,Brush=button.Foreground};}
    private void Display(uint topology)=>Run(async()=>{await Task.Run(()=>ProjectionService.SetTopology(topology));await PollSystem();HideDetails();});
    private void Native(string uri){try{Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});Dismiss();}catch(Exception ex){Report(ex);}}
    private async void Run(Func<Task> action)=>await Safe(action);
    private async Task Safe(Func<Task> action){try{await action();}catch(Exception ex){if(!_closing)Report(ex);}}
    private void Report(Exception ex){_message.Text=ex.Message;_log?.Invoke(LocalizedText.Get("控制中心"),ex);}
}





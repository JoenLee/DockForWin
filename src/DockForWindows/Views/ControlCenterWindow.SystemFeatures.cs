using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DockForWindows.Models;
using DockForWindows.Services;
using Forms=System.Windows.Forms;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private MobileHotspotState _hotspot=new(false,null,"正在读取 Windows 状态");
    private string? _chosenDisplay;private string CurrentDisplay=>_chosenDisplay??_screen?.DeviceName??Forms.Screen.PrimaryScreen?.DeviceName??"";
    private AudioState? _microphoneState;private Slider? _microphoneSlider;private double? _pendingMicrophone;private bool _microphoneWriting;
    private IReadOnlyList<AudioOutput> _outputDevices=Array.Empty<AudioOutput>(),_inputDevices=Array.Empty<AudioOutput>();
    private DisplayHdrState _hdrState=new(false,false,false,false,"正在读取显示器能力");private Guid? _performanceMode;private int? _refreshRate;private DisplayControlService.RateTrial? _rateTrial;
    private readonly Dictionary<ControlModule,string> _featureErrors=new();
    private static readonly Guid EfficientMode=new("961cc777-2547-4f9d-8174-7d86181b8a7a"),FastMode=new("ded574b5-45a0-4f42-8737-46345c09c238");
    private bool Needs(ControlModule kind)=>_modules.Any(c=>c.Kind==kind)||_openKind==kind;
    private void PrimaryAction(ControlModule kind)
    {
        if(_featureErrors.ContainsKey(kind)){OpenModule(kind);return;}
        switch(kind){
            case ControlModule.Wifi:case ControlModule.Bluetooth:case ControlModule.DarkMode:case ControlModule.PowerSaver:ToggleModule(kind);break;
            case ControlModule.NightLight:case ControlModule.Airplane:OpenModule(kind);break;
            case ControlModule.MobileHotspot:if(!_hotspot.Available||!_hotspot.Enabled.HasValue){OpenModule(kind);break;}Run(()=>Execute(kind,"正在切换…",()=>MobileHotspotService.Set(_hotspot.Enabled!=true)));break;
            case ControlModule.Volume:Run(ToggleMute);break;
            case ControlModule.Hdr:Run(()=>Execute(kind,"正在切换…",()=>Task.Run(()=>DisplayControlService.SetHdr(CurrentDisplay,!_hdrState.Enabled))));break;
            case ControlModule.Microphone:Run(()=>Execute(kind,"正在切换…",()=>Task.Run(()=>SystemControlService.SetMicrophoneMute(!(_microphoneState?.Muted??false)))));break;
            case ControlModule.KeepAwake:Run(()=>Execute(kind,"正在切换…",()=>Task.Run(()=>{if(AwakeService.Shared.Active)AwakeService.Shared.Stop();else AwakeService.Shared.Start(TimeSpan.FromMinutes(30));})));break;
            case ControlModule.Performance:Run(()=>Execute(kind,"正在切换…",()=>Task.Run(()=>SystemControlService.SetPerformanceMode(_performanceMode==EfficientMode?Guid.Empty:_performanceMode==Guid.Empty?FastMode:EfficientMode))));break;
            case ControlModule.Screenshot:if(!SystemToolService.CanCapture){OpenModule(kind);break;}ExternalTool(()=>{SystemToolService.OpenScreenshot();return Task.CompletedTask;});break;
            case ControlModule.ScreenRecording:ExternalTool(()=>{SystemToolService.OpenRecording();return Task.CompletedTask;});break;
            case ControlModule.VoiceInput:ExternalTool(()=>{SystemToolService.OpenDictation();return Task.CompletedTask;});break;
            default:OpenModule(kind);break;
        }
    }
    private async void ExternalTool(Func<Task> action)
    {
        if(!await DismissForTool())return;
        try{await action();}catch(Exception ex){_log?.Invoke("系统工具",ex);MessageBox.Show(ex.Message,"系统操作未完成",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    private async Task UpdateExtraState()
    {
        string device=CurrentDisplay;var needed=Enum.GetValues<ControlModule>().Where(Needs).ToHashSet();
        var state=await Task.Run(()=>{
            var errors=new Dictionary<ControlModule,string>();int? rate=null;Guid? power=null;AudioState? mic=null;IReadOnlyList<AudioOutput> outputs=Array.Empty<AudioOutput>(),inputs=Array.Empty<AudioOutput>();DisplayHdrState? hdr=null;MobileHotspotState? hotspot=null;
            void Read(ControlModule kind,Action action){if(!needed.Contains(kind))return;try{action();}catch(Exception ex){errors[kind]=ex.Message;}}
            Read(ControlModule.MobileHotspot,()=>hotspot=MobileHotspotService.Read());Read(ControlModule.RefreshRate,()=>rate=DisplayControlService.ReadRefreshRate(device));Read(ControlModule.Performance,()=>{power=SystemControlService.ReadPerformanceMode();if(power is null)errors[ControlModule.Performance]="Windows 未提供电源模式接口。";});
            Read(ControlModule.AudioOutput,()=>{outputs=AudioDeviceService.Outputs();if(outputs.Count==0)errors[ControlModule.AudioOutput]="没有可用音频输出设备。";});
            Read(ControlModule.Microphone,()=>{mic=SystemControlService.ReadMicrophone();inputs=AudioDeviceService.Inputs();});
            Read(ControlModule.Hdr,()=>{hdr=DisplayControlService.ReadHdr(device);if(!hdr.Supported||hdr.PolicyLimited)errors[ControlModule.Hdr]=hdr.Reason??"HDR 当前不可用。";});
            
            if(needed.Contains(ControlModule.ScreenRecording)&&!SystemToolService.CanRecord)errors[ControlModule.ScreenRecording]="需要支持录屏快捷入口的 Windows 11 截图工具。";
            if(needed.Contains(ControlModule.VoiceInput)&&!SystemToolService.CanDictate)errors[ControlModule.VoiceInput]="此 Windows 版本未提供听写入口。";
            return(rate,power,mic,outputs,inputs,hdr,hotspot,errors);
        });if(_closing)return;
        if(state.hotspot is not null)_hotspot=state.hotspot;Status(ControlModule.MobileHotspot,_hotspot.Description);Active(ControlModule.MobileHotspot,_hotspot.Enabled==true);Status(ControlModule.NightLight,"Windows 夜间模式");Status(ControlModule.Airplane,"Windows 飞行设置");
        _refreshRate=state.rate;_performanceMode=state.power;_microphoneState=state.mic;_outputDevices=state.outputs;_inputDevices=state.inputs;if(state.hdr is not null)_hdrState=state.hdr;
        _featureErrors.Clear();foreach(var error in state.errors)_featureErrors[error.Key]=error.Value;
        Status(ControlModule.RefreshRate,_refreshRate is {} hz?$"{hz} Hz":"不可用");Status(ControlModule.Performance,_performanceMode==EfficientMode?"节能":_performanceMode==Guid.Empty?"平衡":_performanceMode==FastMode?"高性能":"不可用 / 自定义");
        Status(ControlModule.AudioOutput,_outputDevices.FirstOrDefault(x=>x.Default)?.Name??"无输出设备");Status(ControlModule.Microphone,_microphoneState is null?"无输入设备":_microphoneState.Muted?"已静音":$"{_microphoneState.Volume:0}%");Active(ControlModule.Microphone,_microphoneState is {Muted:false});
        Status(ControlModule.Hdr,!_hdrState.Supported?"不支持 HDR":_hdrState.Enabled?_hdrState.Active?"已开启":"已启用，暂未激活":"已关闭");Active(ControlModule.Hdr,_hdrState.Supported&&_hdrState.Enabled);
        Status(ControlModule.KeepAwake,AwakeService.Shared.Active?AwakeService.Shared.Until is {} until?$"剩余 {Math.Max(0,(int)Math.Ceiling((until-DateTimeOffset.UtcNow).TotalMinutes))} 分钟":"直到退出程序":"已关闭");Active(ControlModule.KeepAwake,AwakeService.Shared.Active);
        Status(ControlModule.Screenshot,"区域 / 窗口 / 全屏");Status(ControlModule.ScreenRecording,"系统录屏工具");Status(ControlModule.VoiceInput,"Windows 听写");
        foreach(var card in _modules){bool available=!_featureErrors.TryGetValue(card.Kind,out string? error);if(card.Primary is not null)card.Primary.IsEnabled=available&&!IsBusy(card.Kind);card.Root.ToolTip=error;}
        if(_microphoneSlider is not null&&!_microphoneSlider.IsMouseCaptureWithin&&!_microphoneWriting&&_pendingMicrophone is null){_microphoneSlider.IsEnabled=_microphoneState is not null;if(_microphoneState is not null)_microphoneSlider.Value=_microphoneState.Volume;}
        if(_rateTrial is not null&&_page==ControlPage.DisplayTrial){if(DateTimeOffset.UtcNow>=_rateTrial.Deadline){var error=_rateTrial.RestoreError;_rateTrial.Dispose();_rateTrial=null;OpenModule(ControlModule.RefreshRate,true);if(error is not null)Report(error);else _message.Text="未确认更改，已恢复原刷新率。";}else _message.Text=$"请在 {Math.Max(0,(int)Math.Ceiling((_rateTrial.Deadline-DateTimeOffset.UtcNow).TotalSeconds))} 秒内确认，否则自动恢复。";}
    }
    private async Task BuildExtraDetails(ControlModule kind)
    {
        await UpdateExtraState();if(_closing||_openKind!=kind)return;
        if(_featureErrors.TryGetValue(kind,out var reason))AddStatusRow(kind.ToString(),"当前不可用",reason);
        switch(kind){
            case ControlModule.NightLight:AddStatusRow("NightLight","Windows 夜间模式","由 Windows 管理色温、时间计划和开关；此应用不写入未公开的系统配置，也不推测当前开启状态。");AddDetail("打开夜间模式设置…",()=>Native("ms-settings:nightlight"));break;
            case ControlModule.Airplane:AddStatusRow("Airplane","Windows 飞行模式","在 Windows 原生设置中管理全部无线设备。不会将关闭 Wi-Fi 冒充系统飞行模式。");AddDetail("打开飞行模式设置…",()=>Native("ms-settings:network-airplanemode"));break;
            case ControlModule.MobileHotspot:AddStatusRow("MobileHotspot","移动热点",_hotspot.Description);AddToggleDetail("共享 Internet 连接",()=>_hotspot.Available?_hotspot.Enabled:null,on=>Execute(kind,"正在切换…",()=>MobileHotspotService.Set(on)));AddDetail("网络名称、密码及共享方式…",()=>Native("ms-settings:network-mobilehotspot"));break;
            case ControlModule.RefreshRate:
                AddDisplayChoices(kind);if(_refreshRate is not null){var rates=await Task.Run(()=>DisplayControlService.RefreshRates(CurrentDisplay));if(_closing||_openKind!=kind)return;foreach(int rate in rates)AddDetail(($"{(_refreshRate==rate?"✓ ":"")}{rate} Hz"),()=>Run(()=>TryRate(rate)));}AddDetail("Windows 高级显示…",()=>Native("ms-settings:display-advanced"));break;
            case ControlModule.Performance:
                foreach(var option in new[]{("节能 / 最佳能效",EfficientMode),("平衡",Guid.Empty),("高性能",FastMode)}){var value=option;AddDetail((_performanceMode==value.Item2?"✓ ":"")+value.Item1,()=>Run(()=>Execute(kind,"正在切换…",()=>Task.Run(()=>SystemControlService.SetPerformanceMode(value.Item2))))).IsEnabled=_performanceMode.HasValue;}AddDetail("电源设置…",()=>Native("ms-settings:powersleep"));break;
            case ControlModule.AudioOutput:
                foreach(var item in _outputDevices)AddDetail((item.Default?"✓ ":"")+item.Name,()=>Run(async()=>{await Execute(kind,"正在切换…",()=>Task.Run(()=>AudioDeviceService.SetDefault(item.Id)));if(!_closing)OpenModule(kind,true);}));AddDetail("声音设置…",()=>Native("ms-settings:sound"));break;
            case ControlModule.Hdr:
                AddDisplayChoices(kind);AddToggleDetail("使用 HDR",()=>_hdrState.Supported&&!_hdrState.PolicyLimited?_hdrState.Enabled:null,on=>Execute(kind,"正在切换…",()=>Task.Run(()=>DisplayControlService.SetHdr(CurrentDisplay,on))));AddDetail("Windows HDR 设置…",()=>Native("ms-settings:display"));break;
            case ControlModule.KeepAwake:
                foreach(int minutes in new[]{15,30,60})AddDetail($"保持唤醒 {minutes} 分钟",()=>Run(()=>Execute(kind,"正在启用…",()=>Task.Run(()=>AwakeService.Shared.Start(TimeSpan.FromMinutes(minutes))))));AddDetail("保持唤醒直到退出",()=>Run(()=>Execute(kind,"正在启用…",()=>Task.Run(()=>AwakeService.Shared.Start(null)))));AddToggleDetail("同时保持屏幕亮起",()=>AwakeService.Shared.Active?AwakeService.Shared.DisplayOn:null,on=>Execute(kind,"正在更新…",()=>Task.Run(()=>AwakeService.Shared.Start(AwakeService.Shared.Until is {} time?time-DateTimeOffset.UtcNow:null,on))));AddDetail("结束保持唤醒",()=>Run(()=>Execute(kind,"正在结束…",()=>Task.Run(AwakeService.Shared.Stop))));break;
            case ControlModule.Microphone:
                AddToggleDetail("麦克风静音",()=>_microphoneState?.Muted,on=>Execute(kind,"正在切换…",()=>Task.Run(()=>SystemControlService.SetMicrophoneMute(on))));_microphoneSlider=new Slider{Minimum=0,Maximum=100,IsEnabled=_microphoneState is not null,Value=_microphoneState?.Volume??0,Margin=new Thickness(8,8,12,8)};var slider=_microphoneSlider;slider.ValueChanged+=(_,_)=>{if(!slider.IsMouseCaptureWithin&&!slider.IsKeyboardFocusWithin)return;_pendingMicrophone=slider.Value;Run(WriteMicrophone);};_detailBody.Children.Add(slider);
                foreach(var input in _inputDevices)AddDetail((input.Default?"✓ ":"")+input.Name,()=>Run(async()=>{await Execute(kind,"正在切换…",()=>Task.Run(()=>AudioDeviceService.SetDefault(input.Id)));if(!_closing)OpenModule(kind,true);}));AddDetail("麦克风权限设置…",()=>Native("ms-settings:privacy-microphone"));break;
            case ControlModule.Screenshot:
                AddDetail("区域截图…",()=>ExternalTool(()=>{SystemToolService.OpenScreenshot();return Task.CompletedTask;})).IsEnabled=SystemToolService.CanCapture;
                AddDetail("选择窗口截图…",ShowWindowCapture);foreach(var screen in Forms.Screen.AllScreens){var target=screen;AddDetail($"全屏截图 · {target.DeviceName}（复制到剪贴板）",()=>ExternalTool(async()=>{var image=await Task.Run(()=>ScreenshotService.Monitor(target.DeviceName));Clipboard.SetImage(image);}));}break;
            case ControlModule.ScreenRecording:AddStatusRow("ScreenRecording","使用 Windows 截图工具录屏","由系统面板选择区域、音频并开始或停止，不显示未经系统确认的录制状态。");AddDetail("打开系统录屏…",()=>PrimaryAction(kind));break;
            case ControlModule.VoiceInput:AddStatusRow("VoiceInput","Windows 听写输入","先在目标应用中放好输入光标，再打开听写面板。麦克风与语言包由 Windows 管理。");AddDetail("打开听写…",()=>PrimaryAction(kind));break;
        }
    }
    private void AddDisplayChoices(ControlModule kind)
    {foreach(var screen in Forms.Screen.AllScreens){var target=screen;AddDetail((CurrentDisplay==target.DeviceName?"✓ ":"")+$"{target.DeviceName} · {target.Bounds.Width}×{target.Bounds.Height}",()=>{_chosenDisplay=target.DeviceName;OpenModule(kind,true);});}}
    private async Task TryRate(int rate)
    {
        _rateTrial?.Dispose();var trial=await Task.Run(()=>DisplayControlService.TryRefreshRate(CurrentDisplay,rate));if(_closing||_openKind!=ControlModule.RefreshRate){trial.Dispose();return;}_rateTrial=trial;_page=ControlPage.DisplayTrial;ShowDetails("保留刷新率更改？");AddDetail($"保留 {rate} Hz",()=>{_rateTrial?.Confirm();_rateTrial=null;OpenModule(ControlModule.RefreshRate,true);});AddDetail("恢复原刷新率",()=>{_rateTrial?.Dispose();_rateTrial=null;OpenModule(ControlModule.RefreshRate,true);});UpdateDetailLayout();
    }
    private void ShowWindowCapture()
    {ShowDetails("选择截图窗口");foreach(var item in WindowTracker.Snapshot())AddDetail(item.Title,()=>ExternalTool(async()=>{var image=await ScreenshotService.Window(item.Handle);Clipboard.SetImage(image);}));UpdateDetailLayout();}
    private async Task WriteMicrophone()
    {if(_microphoneWriting)return;_microphoneWriting=true;try{while(!_closing&&_pendingMicrophone is {} value){_pendingMicrophone=null;await Task.Run(()=>SystemControlService.SetMicrophoneVolume(value));await Task.Delay(24);}}finally{_microphoneWriting=false;}}
}

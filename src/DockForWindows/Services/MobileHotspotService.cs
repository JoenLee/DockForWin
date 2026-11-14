using System;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace DockForWindows.Services;

internal sealed record MobileHotspotState(bool Available,bool? Enabled,string Description);
internal static class MobileHotspotService
{
    private static NetworkOperatorTetheringManager Manager()
    {
        var profile=NetworkInformation.GetInternetConnectionProfile()??throw new InvalidOperationException(LocalizedText.Get("没有可共享的 Internet 连接，请在 Windows 移动热点设置中选择网络。"));
        var capability=NetworkOperatorTetheringManager.GetTetheringCapabilityFromConnectionProfile(profile);
        if(capability!=TetheringCapability.Enabled)throw new InvalidOperationException(LocalizedText.Get("Windows 当前不允许共享此连接（")+capability+"）。");
        return NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
    }
    internal static MobileHotspotState Read()
    {
        try{var manager=Manager();var state=manager.TetheringOperationalState;return state switch{
            TetheringOperationalState.On=>new(true,true,LocalizedText.Format($"已开启 · {manager.ClientCount} 台设备")),
            TetheringOperationalState.Off=>new(true,false,LocalizedText.Get("已关闭")),
            _=>new(false,null,LocalizedText.Get("Windows 正在切换热点状态，请稍后重试"))};}
        catch(Exception error){return new(false,null,error.Message);}
    }
    internal static async Task Set(bool enabled)
    {
        var manager=Manager();var result=enabled?await manager.StartTetheringAsync():await manager.StopTetheringAsync();
        if(result.Status!=TetheringOperationStatus.Success)throw new InvalidOperationException(LocalizedText.Get("移动热点操作未完成：")+result.Status+" "+result.AdditionalErrorMessage);
    }
}

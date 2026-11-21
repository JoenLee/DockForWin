using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DockForWindows.Models;
using DockForWindows.Views;

namespace DockForWindows.Services;

internal sealed record WidgetStorageState(string Drive,long Total,long Free,string? Error=null);
internal sealed partial class WidgetRuntime
{
    private readonly Dictionary<string,(WidgetAiSnapshot Snapshot,DateTime Due)> _usage=new(StringComparer.OrdinalIgnoreCase);
    private PowerState? _power;
    private WidgetStorageState? _storage;
    private DateTime _systemDue;
    private WidgetRenderData PopulateExtras(WidgetRenderData data,WidgetKind kind)
    {
        if(kind==WidgetKind.Music){_audioWanted=true;PollMusicAudio();return data with{Seek=SeekMedia,Audio=_audio,OutputName=_outputName,IsFavorite=MusicFavorite(),ToggleFavorite=ToggleMusicFavorite,AudioError=_audioError,SetVolume=SetMusicVolume,MotionEnabled=WidgetDesktopService.Current?.AnimationsEnabled??data.MotionEnabled};}
        if(kind==WidgetKind.AiUsage){string key=data.Source.UsageFile;_usage.TryGetValue(key,out var entry);
            if(entry.Due<=DateTime.Now&&_requests.Add("usage:"+key)){_usage[key]=(entry.Snapshot??WidgetAiSnapshot.Missing(),DateTime.Now.AddMinutes(1));_ =ReadUsage(key);}
            return data with{AiUsage=entry.Snapshot??WidgetAiSnapshot.Missing()};}
        if(kind==WidgetKind.Status){if(_systemDue<=DateTime.Now){_systemDue=DateTime.Now.AddSeconds(10);_ =ReadSystem();}return data with{Power=_power,Storage=_storage};}
        return data;
    }
    private async Task ReadUsage(string file)
    {
        try{var token=_lifetime.Token;var value=await Task.Run(()=>WidgetAiUsageService.Read(file,token),token);if(!_disposed)_usage[file]=(value,DateTime.Now.AddMinutes(1));}
        catch(OperationCanceledException){}
        finally{_requests.Remove("usage:"+file);if(!_disposed)Changed?.Invoke(WidgetKind.AiUsage);}
    }
    private async Task ReadSystem()
    {
        try{var value=await Task.Run(()=>{var power=SystemControlService.ReadPower();try{var drive=new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!);return(power,new WidgetStorageState(drive.Name,drive.TotalSize,drive.AvailableFreeSpace));}catch(IOException e){return(power,new WidgetStorageState("",0,0,e.Message));}},_lifetime.Token);
            if(!_disposed){_power=value.power;_storage=value.Item2;Changed?.Invoke(WidgetKind.Status);}}
        catch(OperationCanceledException){}catch(Exception e){if(!_disposed){_storage=new("",0,0,e.Message);Changed?.Invoke(WidgetKind.Status);}}
    }
    private async void SeekMedia(TimeSpan value){try{await _media.Seek(value);if(!_disposed)await ReadMedia();}catch(Exception e){if(!_disposed){MediaError=e.Message;Changed?.Invoke(WidgetKind.Music);}}}
}

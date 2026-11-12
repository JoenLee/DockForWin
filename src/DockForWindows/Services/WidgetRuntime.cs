using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DockForWindows.Models;

namespace DockForWindows.Services;

internal sealed class WidgetRuntime : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly MediaControlService _media=new();
    private readonly WidgetWeatherService _weather=new();
    private readonly CancellationTokenSource _lifetime=new();
    private readonly Dictionary<string,(WidgetWeatherSnapshot? Weather,string? Error,DateTime Next)> _forecasts=new();
    private readonly HashSet<string> _requests=new();
    private readonly Dictionary<string,(IReadOnlyList<WidgetNote> Notes,string? Error,DateTime Next)> _folders=new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _timer;
    private readonly Func<WidgetOptions> _options;
    private readonly Func<WidgetOptions,bool>? _save;
    private long _minute;
    private DateTime _date=DateTime.Today;
    private bool _disposed,_reading,_mediaAgain;
    private readonly string? _cacheDirectory;
    private readonly Dictionary<string,byte[]> _historyArt=new();
    internal IReadOnlyDictionary<string,byte[]> HistoryArt=>_historyArt;
    internal IReadOnlyList<WidgetMusicEntry> History=>_options().MusicHistory;
    internal MediaState? Media {get;private set;}
    internal string? MediaError {get;private set;}
    internal event Action<WidgetKind?>? Changed;
    internal WidgetRuntime(Dispatcher dispatcher,Func<WidgetOptions> options,Func<WidgetOptions,bool>? save=null,string? cacheDirectory=null)
    {
        _dispatcher=dispatcher;_options=options;_save=save;_cacheDirectory=cacheDirectory;_minute=DateTime.Now.Ticks/TimeSpan.TicksPerMinute;
        _timer=new DispatcherTimer(TimeSpan.FromSeconds(1),DispatcherPriority.Background,Tick,dispatcher);_timer.Start();
        _media.Changed+=MediaChanged;_ = Initialize();
    }
    private async Task Initialize(){try{await LoadArt();if(_disposed)return;await _media.Initialize();if(!_disposed)await ReadMedia();}catch(Exception e){if(!_disposed){MediaError="系统媒体会话不可用："+e.Message;Changed?.Invoke(WidgetKind.Music);}}}
    private async Task LoadArt()
    {
        if(_cacheDirectory==null)return;foreach(var item in _options().MusicHistory.Take(8)){
            string key=item.ArtworkKey;if(!ValidKey(key)||_historyArt.ContainsKey(key))continue;
            try{var path=Path.Combine(_cacheDirectory,key+".jpg");var info=new FileInfo(path);if(info.Exists&&info.Length<=512000){var bytes=await File.ReadAllBytesAsync(path,_lifetime.Token);if(_disposed)return;_historyArt[key]=bytes;}}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    private static bool ValidKey(string key)=>key!=null&&key.Length==64&&key.All(Uri.IsHexDigit);
    private async Task<string> CacheArt(byte[]? source)
    {
        if(source==null||source.Length==0)return "";string key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source));if(_historyArt.ContainsKey(key))return key;
        var bytes=await Task.Run(()=>{using var input=new MemoryStream(source);var image=new System.Windows.Media.Imaging.BitmapImage();image.BeginInit();image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;image.DecodePixelWidth=160;image.StreamSource=input;image.EndInit();image.Freeze();var encoder=new System.Windows.Media.Imaging.JpegBitmapEncoder{QualityLevel=82};encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using var output=new MemoryStream();encoder.Save(output);return output.ToArray();});
        if(_disposed)return "";if(_historyArt.Count>=8)_historyArt.Remove(_historyArt.Keys.First());_historyArt[key]=bytes;
        if(_cacheDirectory!=null){try{Directory.CreateDirectory(_cacheDirectory);await File.WriteAllBytesAsync(Path.Combine(_cacheDirectory,key+".jpg"),bytes,_lifetime.Token);
            var retained=_options().MusicHistory.Select(x=>x.ArtworkKey).Append(key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var file in Directory.EnumerateFiles(_cacheDirectory,"*.jpg")){string name=Path.GetFileNameWithoutExtension(file);if(ValidKey(name)&&!retained.Contains(name))File.Delete(file);}}
            catch(IOException){}catch(UnauthorizedAccessException){}}
        return key;
    }
    private void MediaChanged(){if(!_disposed)_dispatcher.BeginInvoke(async()=>{if(!_disposed)await ReadMedia();});}
    private async Task ReadMedia()
    {
        if(_reading){_mediaAgain=true;return;}_reading=true;
        try{var value=await _media.Read();if(_disposed)return;Media=value;MediaError=null;
            if(value!=null&&!string.IsNullOrWhiteSpace(value.Title)){
                var options=_options().Copy();var history=options.MusicHistory;
                if(history.Count==0||history[0].Title!=value.Title||history[0].Artist!=value.Artist){string artwork="";try{artwork=await CacheArt(value.Artwork);}catch(Exception e){MediaError="封面缓存不可用："+e.Message;}
                    if(_disposed)return;options=_options().Copy();options.MusicHistory.Insert(0,new(){Title=value.Title,Artist=value.Artist,Played=DateTime.Now,ArtworkKey=artwork});options.MusicHistory=options.MusicHistory.Take(30).ToList();if(_save!=null)_save(options);else _options().MusicHistory=options.MusicHistory;}
            }
            Changed?.Invoke(WidgetKind.Music);
        }catch(Exception e){if(!_disposed){MediaError="读取媒体失败："+e.Message;Changed?.Invoke(WidgetKind.Music);}}
        finally{_reading=false;if(_mediaAgain&&!_disposed){_mediaAgain=false;_ =ReadMedia();}}
    }
    internal async void Command(string command){try{await _media.Command(command);await ReadMedia();}catch(Exception e){MediaError=e.Message;Changed?.Invoke(WidgetKind.Music);}}
    private void Tick(object? sender,EventArgs e)
    {
        long minute=DateTime.Now.Ticks/TimeSpan.TicksPerMinute;if(minute!=_minute){_minute=minute;Changed?.Invoke(WidgetKind.Clock);if(DateTime.Today!=_date){_date=DateTime.Today;Changed?.Invoke(WidgetKind.Calendar);}if(_forecasts.Values.Any(x=>x.Next<=DateTime.Now))Changed?.Invoke(WidgetKind.Weather);Changed?.Invoke(WidgetKind.Notes);}
        if(Media?.Playing==true&&DateTime.Now.Second%10==0)_ =ReadMedia();
    }
    private static string Key(WidgetSource source)=>FormattableString.Invariant($"{source.Latitude:F6},{source.Longitude:F6}");
    internal (WidgetWeatherSnapshot? Weather,string? Error) Weather(WidgetSource source)
    {
        if(source.Latitude is not double lat||source.Longitude is not double lon)return(null,"选择天气城市");string key=Key(source);
        _forecasts.TryGetValue(key,out var result);
        if(result.Next<=DateTime.Now&&_requests.Add(key))_ =ReadWeather(key,lat,lon);
        return(result.Weather,result.Error);
    }
    private async Task ReadWeather(string key,double latitude,double longitude)
    {
        try{var value=await _weather.ReadAsync(latitude,longitude,_lifetime.Token);if(!_disposed)_forecasts[key]=(value,null,DateTime.Now.AddMinutes(15));}
        catch(OperationCanceledException){return;}
        catch(Exception e){if(!_disposed){_forecasts.TryGetValue(key,out var previous);_forecasts[key]=(previous.Weather,"天气更新失败："+e.Message,DateTime.Now.AddMinutes(5));}}
        finally{_requests.Remove(key);if(!_disposed)Changed?.Invoke(WidgetKind.Weather);}
    }
    internal (IReadOnlyList<WidgetNote>? Notes,string? Error) Folder(string folder)
    {
        if(string.IsNullOrWhiteSpace(folder))return(null,null);_folders.TryGetValue(folder,out var result);
        if(result.Next<=DateTime.Now&&_requests.Add("folder:"+folder))_ =ReadFolder(folder);
        return(result.Notes,result.Error);
    }
    private async Task ReadFolder(string folder)
    {
        try{var notes=await Task.Run(()=>{
            if(!Directory.Exists(folder))throw new IOException("笔记文件夹不存在。");var result=new List<WidgetNote>();
            foreach(var f in Directory.EnumerateFiles(folder).Where(f=>Path.GetExtension(f).ToLowerInvariant() is ".txt" or ".md").Select(f=>new FileInfo(f)).OrderByDescending(f=>f.LastWriteTime).Take(30)){
                _lifetime.Token.ThrowIfCancellationRequested();using var reader=new StreamReader(f.FullName);var chars=new char[4096];int count=reader.ReadBlock(chars,0,chars.Length);result.Add(new(){Title=Path.GetFileNameWithoutExtension(f.Name),Body=new string(chars,0,count),Folder=Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)),Modified=f.LastWriteTime,FilePath=f.FullName});}
            return (IReadOnlyList<WidgetNote>)result;},_lifetime.Token);
            if(!_disposed)_folders[folder]=(notes,null,DateTime.Now.AddMinutes(1));
        }catch(OperationCanceledException){return;}catch(Exception e){if(!_disposed)_folders[folder]=(Array.Empty<WidgetNote>(),"笔记读取失败："+e.Message,DateTime.Now.AddMinutes(1));}
        finally{_requests.Remove("folder:"+folder);if(!_disposed)Changed?.Invoke(WidgetKind.Notes);}
    }
    public void Dispose(){if(_disposed)return;_disposed=true;_timer.Stop();_timer.Tick-=Tick;_lifetime.Cancel();_media.Changed-=MediaChanged;_media.Dispose();_lifetime.Dispose();Changed=null;}
}

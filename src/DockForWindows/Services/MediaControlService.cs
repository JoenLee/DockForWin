using System;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.UI.Shell;
using Windows.Foundation.Metadata;

namespace DockForWindows.Services;

public sealed record MediaState(string Title,string Artist,bool Playing,bool CanPlay,bool CanPrevious,bool CanNext,byte[]? Artwork,TimeSpan Position,TimeSpan Duration,bool CanSeek,double PlaybackRate=1);
public sealed class MediaControlService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _disposed;private string? _artKey;private byte[]? _artwork;
    public event Action? Changed;
    public async Task Initialize(){var manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();if(_disposed)return;_manager=manager;_manager.CurrentSessionChanged+=SessionChanged;Bind();}
    private void SessionChanged(GlobalSystemMediaTransportControlsSessionManager sender,CurrentSessionChangedEventArgs args){Bind();Changed?.Invoke();}
    private void Bind()
    {
        if(_session is not null){_session.MediaPropertiesChanged-=PropertiesChanged;_session.PlaybackInfoChanged-=PlaybackChanged;_session.TimelinePropertiesChanged-=TimelineChanged;}
        _artKey=null;_artwork=null;_session=_manager?.GetCurrentSession();
        if(_session is not null){_session.MediaPropertiesChanged+=PropertiesChanged;_session.PlaybackInfoChanged+=PlaybackChanged;_session.TimelinePropertiesChanged+=TimelineChanged;}
    }
    private void PropertiesChanged(GlobalSystemMediaTransportControlsSession sender,MediaPropertiesChangedEventArgs args){_artKey=null;Changed?.Invoke();}
    private void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender,PlaybackInfoChangedEventArgs args)=>Changed?.Invoke();
    private void TimelineChanged(GlobalSystemMediaTransportControlsSession sender,TimelinePropertiesChangedEventArgs args)=>Changed?.Invoke();
    public async Task<MediaState?> Read()
    {
        var session=_session;if(session is null)return null;var properties=await session.TryGetMediaPropertiesAsync();var playback=session.GetPlaybackInfo();var controls=playback.Controls;
        string key=session.SourceAppUserModelId+properties.Title+properties.Artist;
        if(key!=_artKey)
        {
            _artKey=key;_artwork=null;
            if(properties.Thumbnail is not null)
            {
                using var stream=await properties.Thumbnail.OpenReadAsync();uint size=(uint)Math.Min(stream.Size,2*1024*1024);
                using var input=stream.GetInputStreamAt(0);using var reader=new Windows.Storage.Streams.DataReader(input);
                uint count=await reader.LoadAsync(size);var bytes=new byte[count];reader.ReadBytes(bytes);_artwork=bytes;
            }
        }
        var timeline=session.GetTimelineProperties();bool playing=playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        double rate=playback.PlaybackRate??1;var duration=timeline.EndTime-timeline.StartTime;
        if(duration<TimeSpan.Zero)duration=TimeSpan.Zero;
        double elapsed=playing&&timeline.LastUpdatedTime.Year>=2000?Math.Max(0,(DateTimeOffset.UtcNow-timeline.LastUpdatedTime).TotalSeconds):0;
        var position=PositionAt(timeline.Position-timeline.StartTime,duration,playing,rate,elapsed);
        return new(properties.Title,properties.Artist,playing,controls.IsPlayPauseToggleEnabled,controls.IsPreviousEnabled,controls.IsNextEnabled,_artwork,position,duration,controls.IsPlaybackPositionEnabled&&duration>TimeSpan.Zero,rate);
    }
    public static TimeSpan PositionAt(TimeSpan position,TimeSpan duration,bool playing,double rate,double elapsed)=>TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds+(playing?Math.Max(0,elapsed)*rate:0),0,Math.Max(0,duration.TotalSeconds)));
    public async Task Command(string command)
    {
        var session=_session??throw new InvalidOperationException("没有支持系统媒体控制的播放器。");
        bool result=command switch{"previous"=>await session.TrySkipPreviousAsync(),"next"=>await session.TrySkipNextAsync(),_=>await session.TryTogglePlayPauseAsync()};
        if(!result)throw new InvalidOperationException("播放器拒绝了此控制操作。");
    }
    public static bool? ReadFocus()=>ApiInformation.IsTypePresent("Windows.UI.Shell.FocusSessionManager")&&FocusSessionManager.IsSupported?FocusSessionManager.GetDefault().IsFocusActive:null;
    public async Task Seek(TimeSpan position){var session=_session??throw new InvalidOperationException("没有正在播放的媒体。");position+=session.GetTimelineProperties().StartTime;if(!await session.TryChangePlaybackPositionAsync(position.Ticks))throw new InvalidOperationException("播放器不支持跳转。");}
    public void Dispose(){_disposed=true;if(_manager is not null)_manager.CurrentSessionChanged-=SessionChanged;if(_session is not null){_session.MediaPropertiesChanged-=PropertiesChanged;_session.PlaybackInfoChanged-=PlaybackChanged;_session.TimelinePropertiesChanged-=TimelineChanged;}_session=null;_manager=null;_artwork=null;}
}

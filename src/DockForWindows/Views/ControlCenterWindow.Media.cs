using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private Border? _mediaCover;private TextBlock? _mediaTitle,_mediaArtist,_mediaElapsed,_mediaRemaining;private Slider? _mediaVolume;private Thumb? _mediaResize;
    private void BuildMediaDetails()
    {
        double z=_options.Geometry.ComponentScale;var frame=ControlCenterLayout.Size(ControlCardShape.Large,_options.Geometry,ControlModule.Media);double r=DetailPhysicalRadius(frame)/z,cornerInset=r*(1-1/Math.Sqrt(2));double p=Math.Max(Math.Max(20,ControlCenterTokens.Padding(_options.Geometry.CardPadding)),cornerInset+8),header=Math.Max(38,Math.Max(24,cornerInset+14)+7);
        var size=new Size(Math.Max(1,frame.Width/z-2*p),Math.Max(1,frame.Height/z-36-header));_detailPlayer=CreatePlayer(ControlCardShape.Large,size,p,()=>{});_detailPlayer.Detail=true;_detailBody.Children.Add(_detailPlayer.Root);
        _mediaCover=_detailPlayer.Art;_mediaTitle=_detailPlayer.Title;_mediaArtist=_detailPlayer.Artist;_mediaElapsed=_detailPlayer.Elapsed;_mediaRemaining=_detailPlayer.Remaining;_mediaProgress=_detailPlayer.Progress;_mediaVolume=_detailPlayer.Volume;_detailPlay=_detailPlayer.Play;_detailPrevious=_detailPlayer.Previous;_detailNext=_detailPlayer.Next;UpdateMediaDetails();
    }
    private void FitMediaBody(){if(_detailPlayer is not null)UpdatePlayerCorners(_detailPlayer);}
    private static string MediaTime(TimeSpan value)=>$"{(int)Math.Max(0,value.TotalMinutes)}:{Math.Max(0,value.Seconds):00}";
    private void UpdateMediaDetails(){if(_detailPlayer is not null)UpdatePlayer(_detailPlayer);}
    private bool _mediaReading;
    private readonly System.Windows.Threading.DispatcherTimer _mediaPoll=new(){Interval=TimeSpan.FromMilliseconds(500)};
    private void MediaChanged(){if(!_closing&&!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(()=>Run(RefreshMedia));}
    private async System.Threading.Tasks.Task RefreshMedia()
    {
        if(_mediaReading||_closing)return;_mediaReading=true;
        try{
            _mediaState=await _media.Read();_mediaObservedAt=_runtime.Elapsed.TotalSeconds;if(_closing)return;Status(ControlModule.Media,_mediaState is null?"未在播放":_mediaState.Title+(string.IsNullOrWhiteSpace(_mediaState.Artist)?"":"\n"+_mediaState.Artist));UpdateMediaArtwork();
            if(_play is not null&&_previous is not null&&_next is not null){SetIcon(_play,_mediaState?.Playing==true?"Pause":"Play");_play.IsEnabled=_mediaState?.CanPlay==true;_previous.IsEnabled=_mediaState?.CanPrevious==true;_next.IsEnabled=_mediaState?.CanNext==true;}
            if(_detailPlay is not null){SetIcon(_detailPlay,_mediaState?.Playing==true?"Pause":"Play");_detailPlay.IsEnabled=_mediaState?.CanPlay==true;_detailPrevious!.IsEnabled=_mediaState?.CanPrevious==true;_detailNext!.IsEnabled=_mediaState?.CanNext==true;}
            UpdateCardMedia();UpdateMediaDetails();
            if(_mediaProgress is not null&&_mediaState is null)_mediaProgress.IsEnabled=false;
        }catch(Exception ex){if(!_closing)_log?.Invoke("读取媒体进度",ex);}
        finally{_mediaReading=false;}
    }

}

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    private sealed class PlayerParts
    {
        public Grid Root=new();public Border Art=new(){Background=new SolidColorBrush(Color.FromArgb(24,255,255,255))};public Grid CoverSlot=new();
        public Button Cover=null!,Previous=null!,Play=null!,Next=null!;public TextBlock Title=new(),Artist=new(),Elapsed=new(),Remaining=new();public Slider Progress=new(){Style=ControlCenterTokens.MediaProgress,Minimum=0,Maximum=1};public Slider? Volume;
        public MediaMarquee TitleMarquee=null!;public long FormattedSecond=-1;public bool Detail;public double CornerRadius=-1,CornerInset=-1;public Size CornerSize;public double Padding;public byte[]? Artwork;public bool HasArtwork;public Size Size;
    }
    private double _mediaObservedAt;
    private void UpdateSmoothMediaTimeline()
    {
        double now=_runtime.Elapsed.TotalSeconds;_mainPlayer?.TitleMarquee.Tick(now,Animate);_detailPlayer?.TitleMarquee.Tick(now,Animate);var state=_mediaState;if(state is null||state.Duration<=TimeSpan.Zero)return;double seconds=MediaControlService.PositionAt(state.Position,state.Duration,state.Playing,state.PlaybackRate,now-_mediaObservedAt).TotalSeconds;
        void Update(PlayerParts? view){if(view is null||!view.Root.IsVisible||view.Progress.IsMouseCaptureWithin)return;if(view.Progress.Maximum>1&&Math.Abs(view.Progress.Value-seconds)>.015)view.Progress.Value=Math.Min(seconds,view.Progress.Maximum);long second=(long)seconds;if(view.FormattedSecond==second)return;view.FormattedSecond=second;view.Elapsed.Text=MediaTime(TimeSpan.FromSeconds(seconds));view.Remaining.Text="−"+MediaTime(TimeSpan.FromSeconds(state.Duration.TotalSeconds-seconds));}
        Update(_mainPlayer);Update(_detailPlayer);
    }
    private PlayerParts? _mainPlayer,_detailPlayer;private byte[]? _decodedArtworkBytes;private BitmapImage? _decodedArtwork;
    private Slider? _cardMediaProgress,_cardPlayerVolume;private TextBlock? _cardElapsed,_cardRemaining;
    private double PlayerPadding(ControlCardShape shape)
    {
        double height=ControlCenterLayout.Size(shape,_options.Geometry,ControlModule.Media).Height/_options.Geometry.ComponentScale;
        double requested=shape==ControlCardShape.Strip?12:ControlCenterTokens.Padding(_options.Geometry.CardPadding);
        double minimum=shape==ControlCardShape.Strip?36:shape==ControlCardShape.Square?96:shape==ControlCardShape.Rectangle?100:0;
        return minimum==0?requested:Math.Min(requested,Math.Max(8,Math.Floor((height-minimum)/8)*4));
    }
    private PlayerParts CreatePlayer(ControlCardShape shape,Size size,double padding,Action open)
    {
        var view=new PlayerParts{Size=size,Padding=padding};var root=view.Root;root.ClipToBounds=true;
        view.Title=new TextBlock{FontSize=shape==ControlCardShape.Large?16:14,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,MinWidth=0,Height=22};
        view.Artist=new TextBlock{FontSize=12,Opacity=.72,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,MinWidth=0,Height=18};
        var info=new Grid{MinWidth=0};info.RowDefinitions.Add(new(){Height=new GridLength(22)});info.RowDefinitions.Add(new(){Height=new GridLength(18)});view.TitleMarquee=new MediaMarquee(view.Title);info.Children.Add(view.TitleMarquee);Grid.SetRow(view.Artist,1);info.Children.Add(view.Artist);
        view.Cover=ActionButton("Media",LocalizedText.Get("展开播放器"),open,20,motion:false);view.Cover.Content=view.Art;view.Cover.Padding=new Thickness(0);view.CoverSlot.Children.Add(view.Cover);
        view.CoverSlot.SizeChanged+=(_,_)=>{double edge=Math.Max(1,Math.Min(view.CoverSlot.ActualWidth,view.CoverSlot.ActualHeight));view.Cover.Width=view.Cover.Height=edge;view.Art.Width=view.Art.Height=edge;UpdatePlayerCorners(view);};
        view.Previous=ActionButton("Previous",LocalizedText.Get("上一首"),()=>Run(()=>_media.Command("previous")),16);view.Play=ActionButton("Play",LocalizedText.Get("播放或暂停"),()=>Run(()=>_media.Command("play")),20);view.Next=ActionButton("Next",LocalizedText.Get("下一首"),()=>Run(()=>_media.Command("next")),16);
        var keys=new UniformGrid{Columns=3,Height=28};foreach(var button in new[]{view.Previous,view.Play,view.Next}){button.Width=button.Height=28;button.HorizontalAlignment=HorizontalAlignment.Center;keys.Children.Add(button);}
        var output=ActionButton("Projection",LocalizedText.Get("音频输出设备"),()=>OpenModule(ControlModule.AudioOutput),16);output.Width=output.Height=28;output.VerticalAlignment=VerticalAlignment.Top;
        var header=new Grid{MinWidth=0};header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=new GridLength(28)});header.Children.Add(info);Grid.SetColumn(output,1);header.Children.Add(output);info.Margin=new Thickness(0,0,8,0);
        var timeline=new Grid{Height=32};timeline.RowDefinitions.Add(new(){Height=new GridLength(16)});timeline.RowDefinitions.Add(new(){Height=new GridLength(16)});timeline.Children.Add(view.Progress);
        var times=new Grid{Margin=new Thickness(5,-1,5,0)};view.Elapsed.FontSize=view.Remaining.FontSize=11;view.Elapsed.Height=view.Remaining.Height=16;view.Elapsed.Opacity=view.Remaining.Opacity=.68;view.Remaining.HorizontalAlignment=HorizontalAlignment.Right;Typography.SetNumeralAlignment(view.Elapsed,FontNumeralAlignment.Tabular);Typography.SetNumeralAlignment(view.Remaining,FontNumeralAlignment.Tabular);times.Children.Add(view.Elapsed);times.Children.Add(view.Remaining);Grid.SetRow(times,1);timeline.Children.Add(times);
        view.Progress.PreviewMouseLeftButtonUp+=(_,_)=>{if(view.Progress.IsEnabled)Dispatcher.BeginInvoke(()=>Run(async()=>{await _media.Seek(TimeSpan.FromSeconds(view.Progress.Value));await RefreshMedia();}));};
        if(shape==ControlCardShape.Strip){
            double album=Math.Max(24,size.Height);root.ColumnDefinitions.Add(new(){Width=new GridLength(album)});root.ColumnDefinitions.Add(new(){Width=new GridLength(8)});root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});root.ColumnDefinitions.Add(new(){Width=new GridLength(84)});
            root.Children.Add(view.CoverSlot);Grid.SetColumn(info,2);header.Children.Remove(info);root.Children.Add(info);Grid.SetColumn(keys,3);root.Children.Add(keys);view.Title.FontSize=12;view.Title.Height=18;info.RowDefinitions[0].Height=new GridLength(18);view.Artist.Height=16;view.Artist.FontSize=10;info.RowDefinitions[1].Height=new GridLength(16);view.Progress.Visibility=Visibility.Collapsed;
        }else if(shape==ControlCardShape.Square){
            root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=new GridLength(36)});root.RowDefinitions.Add(new(){Height=new GridLength(28)});
            var top=new Grid();double edge=Math.Clamp(size.Height-68,28,44);view.CoverSlot.Width=view.CoverSlot.Height=edge;view.CoverSlot.HorizontalAlignment=HorizontalAlignment.Left;view.CoverSlot.VerticalAlignment=VerticalAlignment.Top;top.Children.Add(view.CoverSlot);output.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Remove(output);top.Children.Add(output);root.Children.Add(top);header.Children.Remove(info);Grid.SetRow(info,1);root.Children.Add(info);info.Margin=new Thickness();view.Title.Height=20;info.RowDefinitions[0].Height=new GridLength(20);view.Artist.Height=16;info.RowDefinitions[1].Height=new GridLength(16);Grid.SetRow(keys,2);root.Children.Add(keys);view.Progress.Visibility=Visibility.Collapsed;
        }else if(shape==ControlCardShape.Rectangle){
            double edge=Math.Max(1,size.Height);root.ColumnDefinitions.Add(new(){Width=new GridLength(edge)});root.ColumnDefinitions.Add(new(){Width=new GridLength(12)});root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});root.Children.Add(view.CoverSlot);
            var right=new Grid{MinWidth=0};right.RowDefinitions.Add(new(){Height=new GridLength(40)});right.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});right.RowDefinitions.Add(new(){Height=new GridLength(28)});right.RowDefinitions.Add(new(){Height=new GridLength(32)});right.Children.Add(header);Grid.SetRow(keys,2);right.Children.Add(keys);Grid.SetRow(timeline,3);right.Children.Add(timeline);Grid.SetColumn(right,2);root.Children.Add(right);
        }else{
            double edge=Math.Max(1,Math.Min(size.Width,size.Height-156));root.RowDefinitions.Add(new(){Height=new GridLength(edge)});root.Children.Add(view.CoverSlot);
            foreach(var row in new[]{new GridLength(1,GridUnitType.Star),new GridLength(40),new GridLength(1,GridUnitType.Star),new GridLength(32),new GridLength(1,GridUnitType.Star),new GridLength(28),new GridLength(1,GridUnitType.Star),new GridLength(28)})root.RowDefinitions.Add(new(){Height=row});
            Grid.SetRow(header,2);root.Children.Add(header);Grid.SetRow(timeline,4);root.Children.Add(timeline);Grid.SetRow(keys,6);root.Children.Add(keys);
            var sound=new Grid();sound.ColumnDefinitions.Add(new(){Width=new GridLength(28)});sound.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});sound.ColumnDefinitions.Add(new(){Width=new GridLength(28)});sound.Children.Add(new ControlCenterIcon("Volume"){Width=16,Height=16,Brush=Foreground,VerticalAlignment=VerticalAlignment.Center});view.Volume=new Slider{Minimum=0,Maximum=100,Value=_volume.Value,Style=ControlCenterTokens.MediaProgress};var volume=view.Volume;volume.ValueChanged+=(_,_)=>{if(!_updating&&volume.IsMouseCaptureWithin)_volume.Value=volume.Value;};Grid.SetColumn(volume,1);sound.Children.Add(volume);var outputs=ActionButton("Chevron",LocalizedText.Get("音频输出设备"),()=>OpenModule(ControlModule.AudioOutput),12);Grid.SetColumn(outputs,2);sound.Children.Add(outputs);Grid.SetRow(sound,8);root.Children.Add(sound);
        }
        root.Width=size.Width;root.Height=size.Height;return view;
    }
    private void BuildMediaCard(Card c,Button cover)
    {
        double z=_options.Geometry.ComponentScale,p=PlayerPadding(c.Shape);var outer=ControlCenterLayout.Size(c.Shape,_options.Geometry,ControlModule.Media);var size=new Size(Math.Max(1,outer.Width/z-2*p),Math.Max(1,outer.Height/z-2*p));
        _mainPlayer=CreatePlayer(c.Shape,size,p,()=>OpenModule(ControlModule.Media));c.Body.Children.Add(_mainPlayer.Root);c.Title=_mainPlayer.Title;c.Status=_mainPlayer.Artist;_play=_mainPlayer.Play;_previous=_mainPlayer.Previous;_next=_mainPlayer.Next;_mediaIcon=_mainPlayer.Cover;_cardMediaProgress=_mainPlayer.Progress;_cardPlayerVolume=_mainPlayer.Volume;_cardElapsed=_mainPlayer.Elapsed;_cardRemaining=_mainPlayer.Remaining;UpdatePlayer(_mainPlayer);
    }
    private void UpdateMediaArtwork(){if(_mainPlayer is not null)UpdatePlayerArtwork(_mainPlayer);}
    private void UpdateCardMedia(){if(_mainPlayer is not null)UpdatePlayer(_mainPlayer);}
    private void UpdatePlayerCorners(PlayerParts view)
    {
        if(!double.IsFinite(view.Art.Width)||!double.IsFinite(view.Art.Height))return;
        var size=new Size(Math.Max(0,view.Art.Width),Math.Max(0,view.Art.Height));
        double fraction=(view.Detail?Math.Min(35,_options.Geometry.DetailRoundness):_options.Geometry.MediaRoundness)/100;
        double outer=view.Detail?DetailPhysicalRadius(ControlCenterLayout.Size(ControlCardShape.Large,_options.Geometry,ControlModule.Media))/_options.Geometry.ComponentScale:Math.Min(view.Size.Width+2*view.Padding,view.Size.Height+2*view.Padding)/2*fraction;
        if(view.CornerSize==size&&view.CornerRadius==outer&&view.CornerInset==view.Padding)return;
        view.CornerSize=size;view.CornerRadius=outer;view.CornerInset=view.Padding;
        // Inset the actual continuous outline, rather than giving a new superellipse a smaller radius.
        // A superellipse's parallel curve is not another superellipse with r-padding.
        view.Art.CornerRadius=new CornerRadius();
        view.Art.Clip=ControlCenterTokens.InsetOutline(size,outer,view.Padding,3-Math.Pow(fraction,8));
    }
    private void UpdatePlayerArtwork(PlayerParts view)
    {
        byte[]? artwork=_mediaState?.Artwork;if(view.HasArtwork&&ReferenceEquals(view.Artwork,artwork)){UpdatePlayerCorners(view);return;}view.HasArtwork=true;view.Artwork=artwork;
        if(!ReferenceEquals(_decodedArtworkBytes,artwork)){_decodedArtworkBytes=artwork;_decodedArtwork=null;if(artwork is not null){try{using var stream=new MemoryStream(artwork);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=768;image.StreamSource=stream;image.EndInit();image.Freeze();_decodedArtwork=image;}catch(Exception ex){_log?.Invoke(LocalizedText.Get("媒体封面"),ex);}}}
        view.Art.Child=_decodedArtwork is null?new ControlCenterIcon("Media"){Width=20,Height=20,Brush=view.Title.Foreground,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}:new Image{Source=_decodedArtwork,Stretch=Stretch.UniformToFill};UpdatePlayerCorners(view);
    }
    private void UpdatePlayer(PlayerParts view)
    {
        var state=_mediaState;view.Title.Text=string.IsNullOrWhiteSpace(state?.Title)?LocalizedText.Get("未在播放"):state.Title;view.Artist.Text=state?.Artist??"";SetIcon(view.Play,state?.Playing==true?"Pause":"Play");view.Play.IsEnabled=state?.CanPlay==true;view.Previous.IsEnabled=state?.CanPrevious==true;view.Next.IsEnabled=state?.CanNext==true;
        bool known=state is not null&&state.Duration>TimeSpan.Zero;view.FormattedSecond=-1;view.Progress.IsEnabled=known&&state!.CanSeek;if(!view.Progress.IsMouseCaptureWithin){view.Progress.Maximum=known?Math.Max(1,state!.Duration.TotalSeconds):1;view.Progress.Value=known?Math.Clamp(state!.Position.TotalSeconds,0,view.Progress.Maximum):0;}view.Elapsed.Text=known?MediaTime(state!.Position):state is null?"—:—":LocalizedText.Get("此播放器未提供进度");view.Remaining.Text=known?"−"+MediaTime(TimeSpan.FromSeconds(Math.Max(0,(state!.Duration-state.Position).TotalSeconds))):"";
        // A timeline without seeking reads as progress only; say so instead of looking broken.
        view.Progress.ToolTip=known&&!state!.CanSeek?LocalizedText.Get("此播放器不支持在这里拖动进度"):null;ToolTipService.SetShowOnDisabled(view.Progress,true);
        if(view.Volume is not null&&!view.Volume.IsMouseCaptureWithin){view.Volume.Value=_volume.Value;view.Volume.IsEnabled=_volume.IsEnabled;}UpdatePlayerArtwork(view);
    }
}

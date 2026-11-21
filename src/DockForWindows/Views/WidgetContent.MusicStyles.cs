using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal static partial class WidgetContent
{
    private static FrameworkElement MusicTimeline(WidgetRenderData data,bool reference=false)
    {
        var media=data.Media;bool known=media?.Duration>TimeSpan.Zero;var box=new StackPanel();
        var slider=new Slider{Minimum=0,Maximum=known?media!.Duration.TotalSeconds:1,Value=known?Math.Clamp(media!.Position.TotalSeconds,0,media.Duration.TotalSeconds):0,IsEnabled=known&&media!.CanSeek&&data.Seek!=null,Height=17,Margin=new Thickness(0,4,0,0),IsMoveToPointEnabled=true};
        if(reference){slider.Style=ControlCenterTokens.MediaProgress;slider.SetResourceReference(Control.ForegroundProperty,"WidgetAccent");}
        AutomationProperties.SetAutomationId(slider,"widget-music-seek");AutomationProperties.SetName(slider,L("播放进度","Playback position"));
        slider.AddHandler(UIElement.MouseLeftButtonUpEvent,new MouseButtonEventHandler((_,_)=>{if(slider.IsEnabled)data.Seek?.Invoke(TimeSpan.FromSeconds(slider.Value));}),true);slider.PreviewKeyUp+=(_,e)=>{if(e.Key is Key.Left or Key.Right or Key.Home or Key.End&&slider.IsEnabled)data.Seek?.Invoke(TimeSpan.FromSeconds(slider.Value));};box.Children.Add(slider);
        if(reference){
            string Time(TimeSpan value)=>value.TotalHours>=1?value.ToString(@"h\:mm\:ss"):value.ToString(@"mm\:ss");
            var row=new Grid();Columns(row,1,1);
            var elapsed=T(known?Time(media!.Position):"—:—",10,"WidgetMuted");AutomationProperties.SetAutomationId(elapsed,"widget-music-elapsed");At(row,elapsed);
            var remaining=T(known?"−"+Time(media!.Duration-media.Position):"—:—",10,"WidgetMuted");AutomationProperties.SetAutomationId(remaining,"widget-music-remaining");remaining.HorizontalAlignment=HorizontalAlignment.Right;At(row,remaining,1);box.Children.Add(row);
        }else{var times=T(known?$"{media!.Position:mm\\:ss} / {media.Duration:mm\\:ss}":"—:— / —:—",10,"WidgetMuted");AutomationProperties.SetAutomationId(times,"widget-music-times");box.Children.Add(times);}
        return box;
    }
    private static FrameworkElement Record(WidgetRenderData data,double extent)=>new WidgetRecordPlayer(data,extent);
    private static FrameworkElement StyledMusic(WidgetDefinition d,WidgetRenderData data)
    {
        var media=data.Media;string title=string.IsNullOrWhiteSpace(media?.Title)?L("尚未播放","Nothing playing"):media.Title;string artist=string.IsNullOrWhiteSpace(media?.Artist)?L("等待系统媒体会话","Waiting for a media session"):media.Artist;
        var root=new Grid{Margin=new Thickness(d.Layout==WidgetLayout.MusicMini?12:17),ClipToBounds=true,ToolTip=data.Error};
        if(d.Layout==WidgetLayout.MusicMini){Columns(root,0,1,0);root.ColumnDefinitions[0].Width=new GridLength(44);root.ColumnDefinitions[2].Width=GridLength.Auto;At(root,Art(media?.Artwork,40));var info=Stack(T(title,13,bold:true),T(artist,10,"WidgetMuted"));info.Margin=new Thickness(8,0,6,0);info.VerticalAlignment=VerticalAlignment.Center;At(root,info,1);At(root,Transport(data,true),2);return root;}
        if(d.Layout==WidgetLayout.MusicPoster){Rows(root,1,-1,-1);double cover=Math.Max(60,Math.Min(d.Size.Width-45,d.Size.Height-180));At(root,Art(media?.Artwork,cover));var info=Stack(T(title,20,bold:true),T(artist,11,"WidgetMuted"));info.Margin=new Thickness(0,8,0,0);At(root,info,row:1);var controls=new StackPanel();controls.Children.Add(MusicTimeline(data));controls.Children.Add(Transport(data));At(root,controls,row:2);return root;}
        if(d.Layout==WidgetLayout.MusicRetro){
            var frame=new Border{BorderThickness=new Thickness(1.5),CornerRadius=new CornerRadius(2),Padding=new Thickness(9)};frame.SetResourceReference(Border.BorderBrushProperty,"WidgetMuted");var inner=new Grid();Rows(inner,-1,1,-1,-1);frame.Child=inner;root.Children.Add(frame);
            var heading=new Border{Padding=new Thickness(5,3,5,3),Margin=new Thickness(-5,-5,-5,8)};heading.SetResourceReference(Border.BackgroundProperty,"WidgetTrack");heading.Child=T("NOW PLAYING",11,bold:true);At(inner,heading);
            var main=new Grid();Columns(main,1,1.4);At(main,Art(media?.Artwork,Math.Min(105,d.Size.Width*.29)));var info=Stack(T(title,16,bold:true),T(artist,11,"WidgetMuted"));info.Margin=new Thickness(9,0,0,0);info.VerticalAlignment=VerticalAlignment.Center;At(main,info,1);At(inner,main,row:1);At(inner,MusicTimeline(data),row:2);At(inner,Transport(data),row:3);return root;
        }
        bool compact=d.Size.Columns==2;
        if(compact){Rows(root,1,-1,-1);At(root,Record(data,Math.Min(70,d.Size.Width-56)));At(root,Stack(T(title,12,bold:true),T(artist,9,"WidgetMuted")),row:1);At(root,Transport(data,true),row:2);return root;}
        Columns(root,0,1);root.ColumnDefinitions[0].Width=new GridLength(d.Layout==WidgetLayout.MusicCassette?130:112);
        if(d.Layout==WidgetLayout.MusicCassette){
            var tape=new Border{Width=122,Height=91,CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(2),Padding=new Thickness(8)};tape.SetResourceReference(Border.BorderBrushProperty,"WidgetMuted");var reels=new Grid();Rows(reels,-1,1,-1);reels.Children.Add(T("SIDE A",9,"WidgetMuted"));
            var spools=new Grid();Columns(spools,1,1);At(spools,Art(media?.Artwork,32));At(spools,new Ellipse{Width=30,Height=30,Stroke=WidgetBrushes.Get("#788088"),StrokeThickness=7},1);At(reels,spools,row:1);At(reels,T("STEREO · 90",8,"WidgetMuted"),row:2);tape.Child=reels;At(root,tape);
        }else if(d.Layout is WidgetLayout.MusicVinyl or WidgetLayout.MusicVinylWide)At(root,Record(data,104));
        else{var cover=Art(media?.Artwork,98);At(root,cover);}
        var right=new Grid{Margin=new Thickness(9,0,0,0)};Rows(right,-1,1,-1);At(right,T(d.Layout==WidgetLayout.MusicFrosted?"NOW PLAYING":L("正在播放","NOW PLAYING"),9,"WidgetAccent",true));var meta=Stack(T(title,17,bold:true),T(artist,11,"WidgetMuted"));meta.VerticalAlignment=VerticalAlignment.Center;At(right,meta,row:1);At(right,Transport(data,true),row:2);At(root,right,1);return root;
    }
}

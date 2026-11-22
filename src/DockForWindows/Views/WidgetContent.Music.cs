using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal static partial class WidgetContent
{
    internal static ControlTemplate PlayerButtonTemplate=>PlainButton;
    internal static FrameworkElement PlayerArt(byte[]? bytes,double extent)=>Art(bytes,extent);
    internal static FrameworkElement PlayerLayout(WidgetDefinition definition,WidgetRenderData data)
        =>ReferencePlayer(definition.Id)?ReferencePlayerLayout(definition,data):WidgetSampleTokens.Applies(definition.Layout)?ReferenceSample(definition,data):Music(definition,data);
    private static readonly Dictionary<byte[],BitmapSource> Images=new();
    private static FrameworkElement Art(byte[]? bytes,double extent)
    {
        Brush background=WidgetBrushes.Get("#393A40");bool loaded=false;
        if(bytes is {Length:>0})try{
            if(!Images.TryGetValue(bytes,out var source)){
                var bitmap=new BitmapImage();using var stream=new MemoryStream(bytes);bitmap.BeginInit();
                bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=512;bitmap.StreamSource=stream;
                bitmap.EndInit();bitmap.Freeze();if(Images.Count>=24)Images.Clear();Images[bytes]=source=bitmap;
            }
            var brush=new ImageBrush(source){Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center};brush.Freeze();background=brush;loaded=true;
        }catch(NotSupportedException){}catch(IOException){}catch(ArgumentException){}catch(System.Runtime.InteropServices.COMException){}
        // Painting an ImageBrush into a rounded Border clips the bitmap itself, including non-square artwork.
        var cover=new Border{Width=extent,Height=extent,CornerRadius=new CornerRadius(Math.Min(12,extent*.12)),Background=background,
            HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        AutomationProperties.SetAutomationId(cover,"widget-music-art");
        if(!loaded)cover.Child=new TextBlock{Text="♫",FontSize=extent*.36,Foreground=WidgetBrushes.Get("#B3B5BF"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        return cover;
    }
    private static FrameworkElement Transport(WidgetRenderData data,bool compact=false)
    {
        var m=data.Media;var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,6,0,0)};
        foreach(var entry in new[]{("previous",m?.CanPrevious==true,LocalizedText.Get("上一首")),("toggle",m?.CanPlay==true,m?.Playing==true?LocalizedText.Get("暂停"):LocalizedText.Get("播放")),("next",m?.CanNext==true,LocalizedText.Get("下一首"))}){
            bool primary=entry.Item1=="toggle";double side=compact?32:38;
            var icon=new Grid{Width=primary?18:16,Height=18,IsHitTestVisible=false};
            string path=entry.Item1 switch{
                "previous"=>"M1,2 L3,2 3,16 1,16 Z M15,2 L5,9 15,16 Z",
                "next"=>"M13,2 L15,2 15,16 13,16 Z M1,2 L11,9 1,16 Z",
                _ when m?.Playing==true=>"M3,1 L7,1 7,17 3,17 Z M11,1 L15,1 15,17 11,17 Z",
                _=>"M4,1 L17,9 4,17 Z"};
            var shape=new System.Windows.Shapes.Path{Data=Geometry.Parse(path),Stretch=Stretch.Uniform,Margin=new Thickness(1)};
            shape.SetResourceReference(Shape.FillProperty,"WidgetInk");icon.Children.Add(shape);
            var button=new Button{Template=PlainButton,Content=icon,Width=side,Height=side,MinHeight=0,Padding=new Thickness(primary?6:8),Margin=new Thickness(compact?2:5,0,compact?2:5,0),
                Background=Brushes.Transparent,BorderThickness=new Thickness(0),IsEnabled=entry.Item2&&data.MediaCommand!=null,ToolTip=entry.Item3};
            AutomationProperties.SetName(button,entry.Item3);AutomationProperties.SetAutomationId(button,"widget-music-"+entry.Item1);
            string command=entry.Item1;button.Click+=(_,e)=>{data.MediaCommand?.Invoke(command);e.Handled=true;};row.Children.Add(button);
        }
        row.Children.Add(new WidgetVolumeButton(data){Margin=new Thickness(compact?2:5,0,compact?2:5,0)});
        return row;
    }
    private static FrameworkElement Music(WidgetDefinition d,WidgetRenderData data)
    {
        if(d.Layout is WidgetLayout.MusicVinyl or WidgetLayout.MusicVinylWide or WidgetLayout.MusicPoster or WidgetLayout.MusicFrosted or WidgetLayout.MusicRetro or WidgetLayout.MusicCassette or WidgetLayout.MusicMini)return StyledMusic(d,data);
        bool small=d.Size.Columns==2,now=d.Layout==WidgetLayout.MusicNow&&d.Size.Rows>=4;
        bool history=d.Size.Rows>=4&&d.Layout is WidgetLayout.MusicList or WidgetLayout.MusicLarge;
        var m=data.Media;var g=new Grid{Margin=new Thickness(small?16:22),ClipToBounds=true};
        string title=string.IsNullOrWhiteSpace(m?.Title)?LocalizedText.Get("尚未播放"):m.Title;
        string artist=string.IsNullOrWhiteSpace(m?.Artist)?LocalizedText.Get("打开支持系统媒体控制的播放器"):m.Artist;
        if(now){
            Rows(g,1,-1,-1);double extent=Math.Min(d.Size.Width-100,(d.Size.Height-150)*.82);
            At(g,Art(m?.Artwork,Math.Max(60,extent)));
            var detail=Stack(T(title,20,bold:true),T(artist,12,"WidgetMuted"));detail.Margin=new Thickness(0,10,0,4);At(g,detail,row:1);
            var footer=new StackPanel();
            bool known=m?.Duration>TimeSpan.Zero;
            var progress=new ProgressBar{Maximum=known?m!.Duration.TotalSeconds:1,Value=known?m!.Position.TotalSeconds:0,Height=3,BorderThickness=new Thickness(0),Margin=new Thickness(0,7,0,5),IsEnabled=known};
            AutomationProperties.SetAutomationId(progress,"widget-music-readonly-progress");progress.SetResourceReference(Control.ForegroundProperty,"WidgetAccent");footer.Children.Add(progress);
            var times=T(known?$"{m!.Position:mm\\:ss} / {m.Duration:mm\\:ss}":"—:— / —:—",10,"WidgetMuted");AutomationProperties.SetAutomationId(times,"widget-music-times");footer.Children.Add(times);footer.Children.Add(Transport(data));
            if(data.Error!=null)footer.Children.Add(T(data.Error,10,"WidgetMuted"));At(g,footer,row:2);return g;
        }
        if(small){
            Rows(g,1,-1,-1);var cover=Art(m?.Artwork,66);cover.HorizontalAlignment=HorizontalAlignment.Left;At(g,cover);
            At(g,Stack(T(title,13,bold:true),T(artist,10,"WidgetMuted")),row:1);At(g,Transport(data,true),row:2);return g;
        }
        Rows(g,history?-1:1,history?1:-1);var main=new Grid();main.ColumnDefinitions.Add(new(){Width=GridLength.Auto});main.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        At(main,Art(m?.Artwork,history?86:88));var info=Stack(T(title,history?16:17,bold:true),T(artist,11,"WidgetMuted"),Transport(data,true));
        info.Margin=new Thickness(14,0,0,0);info.VerticalAlignment=VerticalAlignment.Center;At(main,info,1);At(g,main);
        if(history){var list=new StackPanel{Margin=new Thickness(0,13,0,0)};list.Children.Add(T(LocalizedText.Get("本应用记录的最近播放"),11,"WidgetMuted"));
            foreach(var item in data.Options.MusicHistory.Take(d.Size.Rows>=6?6:3)){
                list.Children.Add(Rule());var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(34)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
                At(row,Art(data.HistoryArt?.GetValueOrDefault(item.ArtworkKey),34));var meta=Stack(T(item.Title,13,bold:true),T(item.Artist+" · "+item.Played.ToString(LocalizedText.Get("M月d日")),10,"WidgetMuted"));meta.Margin=new Thickness(10,0,0,0);At(row,meta,1);list.Children.Add(row);
            }
            if(data.Options.MusicHistory.Count==0)list.Children.Add(T(LocalizedText.Get("播放后自动记录；历史项目无法直接重播"),11,"WidgetMuted"));
            if(data.Error!=null)list.Children.Add(T(data.Error,10,"WidgetMuted"));At(g,list,row:1);
        }else if(data.Error!=null)At(g,T(data.Error,10,"WidgetMuted"),row:1);
        return g;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal static partial class WidgetContent
{
    private static bool ExtraLayout(WidgetLayout layout)=>layout is WidgetLayout.ClockDot or WidgetLayout.ClockDotWide or WidgetLayout.ClockOrbit or WidgetLayout.WeatherDot or WidgetLayout.WeatherSplit or WidgetLayout.CalendarDot or WidgetLayout.CalendarMiniGrid or WidgetLayout.ProgressRings or WidgetLayout.CountdownDot or WidgetLayout.CountdownWide or WidgetLayout.BatterySegments or WidgetLayout.BatteryRing or WidgetLayout.StorageGauge or WidgetLayout.AiCodex or WidgetLayout.AiClaude or WidgetLayout.AiDual or WidgetLayout.AiHistory;
    private static string L(string chinese,string english)=>UiText.IsEnglish?english:chinese;
    private static FrameworkElement Dots(string text,double height){var digits=new WidgetDotNumber(text);return new Viewbox{Child=digits,Height=height,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left};}
    private static FrameworkElement Gauge(double? fraction,double side,string caption)
    {
        var grid=new Grid{Width=side,Height=side,Margin=new Thickness(3)};grid.Children.Add(new WidgetRing(fraction){Width=side,Height=side});
        var label=T(fraction is {} value?$"{value*100:0}%":"—",side*.19,bold:true);label.HorizontalAlignment=HorizontalAlignment.Center;grid.Children.Add(label);
        var stack=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};stack.Children.Add(grid);var title=T(caption,10,"WidgetMuted");title.HorizontalAlignment=HorizontalAlignment.Center;stack.Children.Add(title);return stack;
    }
    private static FrameworkElement Extra(WidgetDefinition d,WidgetRenderData data)
    {
        if(d.Kind==WidgetKind.AiUsage)return AiUsage(d,data);
        var body=Body(d);var now=data.Now??TimeZoneInfo.ConvertTime(DateTimeOffset.Now,TimeZoneInfo.FindSystemTimeZoneById(data.Source.TimeZone)).DateTime;
        if(d.Layout==WidgetLayout.ProgressRings){
            if(d.Size.Columns>=4)Columns(body,1,2);else Rows(body,-1,1);At(body,Stack(T(now.ToString("MMMM",UiText.Culture).ToUpperInvariant(),12,"WidgetAccent",true),Dots(now.Day.ToString("00"),d.Size.Columns==2?35:52)));
            var row=new UniformGrid{Columns=3,VerticalAlignment=VerticalAlignment.Bottom};double side=Math.Min(62,(d.Size.Width*(d.Size.Columns>=4?2d/3:1)-48)/3-7);
            row.Children.Add(Gauge(1-(now-new DateTime(now.Year,1,1)).TotalDays/(DateTime.IsLeapYear(now.Year)?366:365),side,L("年剩余","Year left")));
            row.Children.Add(Gauge(1-(now.Day-1+now.TimeOfDay.TotalDays)/DateTime.DaysInMonth(now.Year,now.Month),side,L("月剩余","Month left")));
            row.Children.Add(Gauge(1-now.TimeOfDay.TotalDays,side,L("日剩余","Day left")));At(body,row,d.Size.Columns>=4?1:0,d.Size.Columns>=4?0:1);return body;
        }
        if(d.Layout is WidgetLayout.CountdownDot or WidgetLayout.CountdownWide){
            Rows(body,-1,1,-1);At(body,T(data.Source.EventName.Length>0?data.Source.EventName:L("倒数日","Countdown"),12,"WidgetAccent",true));
            if(data.Source.TargetDate is not {} target){At(body,Stack(Dots("--",44),T(L("设置目标日期","Choose a target date"),11,"WidgetMuted")),row:1);return body;}
            int days=(target.Date-now.Date).Days;var number=Stack(Dots(Math.Abs(days).ToString(),Math.Min(60,d.Size.Height*.3)),T(days<0?L("天前","days ago"):L("天后","days to go"),11,"WidgetMuted"));number.VerticalAlignment=VerticalAlignment.Center;At(body,number,row:1);At(body,T(target.ToString("yyyy.MM.dd"),10,"WidgetMuted"),row:2);return body;
        }
        if(d.Kind==WidgetKind.Status){
            if(d.Layout==WidgetLayout.StorageGauge){Rows(body,-1,1,-1);At(body,T(L("系统磁盘","System storage"),12,bold:true));var storage=data.Storage;
                double? used=storage is {Total:>0}?(double)(storage.Total-storage.Free)/storage.Total:null;At(body,Gauge(used,Math.Min(98,d.Size.Height-88),storage?.Drive??""),row:1);At(body,T(storage is {Total:>0}?$"{storage.Free/1073741824d:0.#} GB "+L("可用","free"):storage?.Error??L("正在读取…","Reading…"),10,"WidgetMuted"),row:2);return body;}
            Rows(body,-1,1,-1);At(body,T(L("电池","BATTERY"),12,bold:true));int? percent=data.Power?.Percent;
            if(d.Layout==WidgetLayout.BatteryRing)At(body,Gauge(percent/100d,Math.Min(100,d.Size.Height-85),data.Power?.PluggedIn==true?L("接通电源","Plugged in"):""),row:1);
            else{var segments=new UniformGrid{Columns=5,Height=42,VerticalAlignment=VerticalAlignment.Center};for(int i=0;i<5;i++){var bar=new Border{CornerRadius=new CornerRadius(7),Margin=new Thickness(3),Opacity=percent is {} p&&p>i*20?1:.15};bar.SetResourceReference(Border.BackgroundProperty,"WidgetInk");segments.Children.Add(bar);}At(body,segments,row:1);}
            At(body,T(percent is {} p2?$"{p2}%":L("无电池数据","No battery data"),percent.HasValue?24:11,"WidgetMuted"),row:2);return body;
        }
        if(d.Layout==WidgetLayout.CalendarMiniGrid){Rows(body,-1,1);At(body,T(now.ToString("MMMM yyyy",UiText.Culture),12,"WidgetAccent",true));At(body,Month(now,true,selected:now),row:1);return body;}
        if(d.Layout==WidgetLayout.CalendarDot){Rows(body,-1,1,-1);At(body,T(now.ToString("MMMM",UiText.Culture).ToUpperInvariant(),12,"WidgetAccent",true));At(body,Dots(now.Day.ToString("00"),Math.Min(65,d.Size.Height*.38)),row:1);At(body,T(now.ToString("dddd",UiText.Culture)+" · "+Lunar(now),10,"WidgetMuted"),row:2);return body;}
        if(d.Layout is WidgetLayout.WeatherDot or WidgetLayout.WeatherSplit){Rows(body,-1,1,-1);var w=data.Weather;At(body,T(data.Source.City.Length>0?data.Source.City:L("当前位置","Current location"),12,bold:true));
            var temp=Stack(Dots(w is null?"--":Math.Round(w.Temperature).ToString(),Math.Min(55,d.Size.Height*.32)),T(w is null?L("天气未接入","No weather data"):WidgetWeatherService.Description(w.Code),11,"WidgetMuted"));
            if(d.Size.Columns>=4){Columns(body,2,1);At(body,temp,row:1);At(body,WidgetIcons.Weather(w?.Code??3,w?.IsDay??true,56),1,1);}else At(body,temp,row:1);
            At(body,T(w is null||w.Days.Count==0?data.Error??L("选择城市","Choose a city"):FormattableString.Invariant($"{w.Days[0].Low:0}° / {w.Days[0].High:0}°"),10,"WidgetMuted"),row:2);return body;}
        if(d.Layout==WidgetLayout.ClockOrbit){var clock=new WidgetClock(TimeZoneInfo.FindSystemTimeZoneById(data.Source.TimeZone),false,data.Now,data.MotionEnabled){Margin=new Thickness(10)};var ring=new Ellipse{Margin=new Thickness(3),StrokeThickness=2};ring.SetResourceReference(Shape.StrokeProperty,"WidgetAccent");body.Children.Add(ring);body.Children.Add(clock);return body;}
        Rows(body,-1,1,-1);At(body,T(now.ToString("dddd",UiText.Culture).ToUpperInvariant(),11,"WidgetAccent",true));
        if(d.Size.Columns==2){var numbers=new StackPanel{VerticalAlignment=VerticalAlignment.Center};numbers.Children.Add(Dots(now.ToString("HH"),Math.Min(100,(d.Size.Height-85)/2)));numbers.Children.Add(Dots(now.ToString("mm"),Math.Min(100,(d.Size.Height-85)/2)));At(body,numbers,row:1);}else At(body,Dots(now.ToString("HH:mm"),55),row:1);
        At(body,T(now.ToString("yyyy.MM.dd",UiText.Culture)+" · "+Lunar(now),10,"WidgetMuted"),row:2);return body;
    }
    private static FrameworkElement AiUsage(WidgetDefinition d,WidgetRenderData data)
    {
        var value=data.AiUsage??WidgetAiSnapshot.Missing();var body=Body(d);bool dual=d.Layout==WidgetLayout.AiDual,history=d.Layout==WidgetLayout.AiHistory;
        if(dual||history)Columns(body,1,1);
        FrameworkElement Account(WidgetAiAccount account){var grid=new Grid();Rows(grid,-1,1,1,-1);At(grid,T(account.Name,Math.Min(20,d.Size.Width*.09),bold:true));
            FrameworkElement Window(string title,WidgetUsageWindow window){var stack=new StackPanel{Margin=new Thickness(0,4,8,0)};var heading=new Grid();Columns(heading,1,1);At(heading,T(title,11,bold:true));var percent=T(window.RemainingPercent is {} p?$"{p:0}%":"—%",17,bold:true);percent.HorizontalAlignment=HorizontalAlignment.Right;At(heading,percent,1);stack.Children.Add(heading);
                var track=new ProgressBar{Minimum=0,Maximum=100,Value=window.RemainingPercent??0,Height=5,BorderThickness=new Thickness(0),Margin=new Thickness(0,2,0,2),Background=WidgetBrushes.Get("#40504A")};track.Foreground=WidgetBrushes.Get(account.Name=="Claude"?"#FFAA62":"#9EEA6E");stack.Children.Add(track);
                stack.Children.Add(T(window.RemainingPercent is null?L("暂无数据","No data"):window.ResetsAt is {} reset?reset.ToLocalTime().ToString("MM/dd HH:mm"):L("剩余额度","Remaining"),8,"WidgetMuted"));return stack;}
            At(grid,Window("5h",account.FiveHour),row:1);At(grid,Window("Week",account.Week),row:2);At(grid,T(account.UpdatedAt is {} stamp?stamp.ToLocalTime().ToString(stamp.ToLocalTime().Date==DateTime.Today?"HH:mm":"MM/dd HH:mm")+" · "+L("剩余","left"):account.FiveHour.RemainingPercent!=null||account.Week.RemainingPercent!=null?L("已导入","Imported"):L("未接入","Not connected"),8,"WidgetMuted"),row:3);return grid;}
        var selected=d.Layout==WidgetLayout.AiClaude?value.Claude:value.Codex;var accountView=Account(selected);if(dual||history)accountView.Margin=new Thickness(0,0,14,0);At(body,accountView);
        if(dual)At(body,Account(value.Claude),1);
        if(history){var chart=new Grid{Margin=new Thickness(8,0,0,0)};Rows(chart,-1,1,-1);At(chart,T("Daily Tokens",13,bold:true));var bars=new UniformGrid{Columns=7,VerticalAlignment=VerticalAlignment.Bottom,Height=Math.Min(95,d.Size.Height-100)};long maximum=Math.Max(1,selected.Daily.Count==0?1:selected.Daily.Max(x=>x.Tokens));
            var end=DateOnly.FromDateTime((data.Now??DateTime.Now).Date);for(int i=6;i>=0;i--){var day=end.AddDays(-i);var item=selected.Daily.FirstOrDefault(x=>x.Date==day);var column=new StackPanel{VerticalAlignment=VerticalAlignment.Bottom};var bar=new Border{Height=item is null?1:Math.Max(1,(bars.Height-20)*item.Tokens/maximum),Margin=new Thickness(3,0,3,4),Background=WidgetBrushes.Get(item is null?"#40464C":"#4588FF")};column.Children.Add(bar);var label=T(day.Day.ToString(),9,"WidgetMuted");label.HorizontalAlignment=HorizontalAlignment.Center;column.Children.Add(label);bars.Children.Add(column);}At(chart,bars,row:1);At(chart,T(selected.Daily.Count==0?L("暂无本机记录","No local records"):L("本机部分记录 · 含缓存","Local sample · incl. cache"),9,"WidgetMuted"),row:2);At(body,chart,1);}
        if(value.Error!=null)body.ToolTip=value.Error;return body;
    }
}

internal sealed class WidgetDotNumber:FrameworkElement
{
    private readonly Geometry _geometry;
    internal static readonly DependencyProperty InkProperty=DependencyProperty.Register("Ink",typeof(Brush),typeof(WidgetDotNumber),new FrameworkPropertyMetadata(Brushes.White,FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly string[] Digits={"01110100011001110101110011000101110","00100011000010000100001000010001110","01110100010000100110010001000011111","11110000010000101110000010000111110","00010001100101010010111110001000010","11111100001000011110000010000111110","01110100001000011110100011000101110","11111000010001000100010000100001000","01110100011000101110100011000101110","01110100011000101111000010000101110"};
    internal WidgetDotNumber(string text,bool pixels=false)
    {
        Width=Math.Max(1,text.Length*6-1);Height=7;var dots=new GeometryGroup();int offset=0;
        foreach(char ch in text){string cells=char.IsAsciiDigit(ch)?Digits[ch-'0']:ch==':'?"00000001000010000000001000010000000":ch=='-'?"00000000000000011111000000000000000":"00000000000000000000000000000000000";
            int columns=pixels&&ch==':'?1:5;
            if(pixels&&ch==':')cells="0010100";
            if(pixels&&ch=='0')cells="01110100011000110001100011000101110";
            for(int i=0;i<Math.Min(columns*7,cells.Length);i++)if(cells[i]=='1'){
                var center=new Point(offset+i%columns+.5,i/columns+.5);
                dots.Children.Add(pixels?new RectangleGeometry(new Rect(center.X-.34,center.Y-.34,.68,.68)):new EllipseGeometry(center,.37,.37));
            }
            offset+=columns+1;
        }
        if(pixels)Width=Math.Max(1,offset-1);
        dots.Freeze();_geometry=dots;SetResourceReference(InkProperty,"WidgetInk");
    }
    protected override Size MeasureOverride(Size available)=>new(Width,Height);
    protected override void OnRender(DrawingContext dc)=>dc.DrawGeometry((Brush)GetValue(InkProperty),null,_geometry);
}
internal sealed class WidgetRing(double? value):FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        var rect=new Rect(5,5,Math.Max(0,ActualWidth-10),Math.Max(0,ActualHeight-10));if(rect.Width<=0||rect.Height<=0)return;
        var track=new Pen(WidgetBrushes.Get("#286D7B85"),5);dc.DrawEllipse(null,track,new Point(ActualWidth/2,ActualHeight/2),rect.Width/2,rect.Height/2);
        if(value is not {} percent||percent<=0)return;var pen=new Pen(TryFindResource("WidgetAccent") as Brush??Brushes.Orange,5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
        if(percent>=.9999){dc.DrawEllipse(null,pen,new Point(ActualWidth/2,ActualHeight/2),rect.Width/2,rect.Height/2);return;}
        double angle=Math.Clamp(percent,0,1)*2*Math.PI;var geometry=new StreamGeometry();using(var path=geometry.Open()){path.BeginFigure(new Point(ActualWidth/2,5),false,false);path.ArcTo(new Point(ActualWidth/2+rect.Width/2*Math.Sin(angle),ActualHeight/2-rect.Height/2*Math.Cos(angle)),new Size(rect.Width/2,rect.Height/2),0,angle>Math.PI,SweepDirection.Clockwise,true,false);}geometry.Freeze();dc.DrawGeometry(null,pen,geometry);
    }
}

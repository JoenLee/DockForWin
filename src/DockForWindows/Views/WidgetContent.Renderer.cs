using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DockForWindows.Models;
using DockForWindows.Services;
using MediaState=DockForWindows.Services.MediaState;

namespace DockForWindows.Views;

internal sealed record WidgetRenderData(WidgetOptions Options,WidgetSource Source,WidgetWeatherSnapshot? Weather=null,MediaState? Media=null,
    IReadOnlyList<WidgetNote>? FolderNotes=null,string? Error=null,bool Preview=true,Action<Guid>? Complete=null,Action<Guid>? EditTask=null,Action<Guid>? EditNote=null,Action<string>? MediaCommand=null,
    DateTime? Month=null,DateTime? SelectedDate=null,Action<DateTime>? SelectDate=null,IReadOnlyDictionary<string,byte[]>? HistoryArt=null)
{
    internal IReadOnlyList<WidgetTask>? RemoteTasks {get;init;}
    internal WidgetAiSnapshot? AiUsage {get;init;}
    internal PowerState? Power {get;init;}
    internal WidgetStorageState? Storage {get;init;}
    internal DateTime? Now {get;init;}
    internal Action<TimeSpan>? Seek {get;init;}
    internal AudioState? Audio {get;init;}
    internal string? AudioError {get;init;}
    internal Action<double>? SetVolume {get;init;}
    internal bool MotionEnabled {get;init;}=true;
    internal bool DesignReference {get;init;}
    internal string? OutputName {get;init;}
    internal bool IsFavorite {get;init;}
    internal Action? ToggleFavorite {get;init;}
}

internal static partial class WidgetContent
{
    private static readonly ControlTemplate PlainButton=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='.75'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter Property='Opacity' Value='.5'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
    private static CultureInfo Chinese => UiText.Culture;
    internal static FrameworkElement Render(WidgetDefinition d,WidgetRenderData data)=>FigmaWidgetCatalog.Find(d.Id)!=null?new FigmaWidgetView(d,data):DocumentClockStyles.Find(d.Id) is {} css?DocumentClock(d,data,css):d.Kind==WidgetKind.Music?new WidgetMusicView(d,data):WidgetSampleTokens.Applies(d.Layout)?ReferenceSample(d,data):ExtraLayout(d.Layout)?Extra(d,data):d.Kind switch{
        WidgetKind.Weather=>Weather(d,data),WidgetKind.Calendar=>Calendar(d,data),WidgetKind.Clock=>Clock(d,data),
        WidgetKind.Reminders=>Reminders(d,data),WidgetKind.Notes=>Notes(d,data),_=>Music(d,data)};
    private static FrameworkElement T(string text,double size=12,string ink="WidgetInk",bool bold=false)
    {
        var t=WidgetTypography.Create(text,size,bold);t.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty,ink);
        if(t is TextBlock b){b.TextTrimming=TextTrimming.CharacterEllipsis;b.TextWrapping=TextWrapping.NoWrap;}
        t.VerticalAlignment=VerticalAlignment.Center;return t;
    }
    private static Grid Body(WidgetDefinition d)=>new(){Margin=new Thickness(d.Size.Columns==2?17:23),ClipToBounds=true};
    private static StackPanel Stack(params FrameworkElement[] children){var p=new StackPanel();foreach(var c in children)p.Children.Add(c);return p;}
    private static void At(Grid grid,FrameworkElement child,int column=0,int row=0){Grid.SetColumn(child,column);Grid.SetRow(child,row);grid.Children.Add(child);}
    private static void Columns(Grid g,params double[] widths){foreach(var w in widths)g.ColumnDefinitions.Add(new(){Width=new GridLength(w,GridUnitType.Star)});}
    private static void Rows(Grid g,params double[] heights){foreach(var h in heights)g.RowDefinitions.Add(new(){Height=h<0?GridLength.Auto:new GridLength(h,GridUnitType.Star)});}
    private static Border Rule()=>new(){Height=1,Margin=new Thickness(0,8,0,8),Background=WidgetBrushes.Get("#16888888")};
    private static FrameworkElement Empty(string title,string detail)=>Stack(T(title,17,bold:true),T(detail,11,"WidgetMuted"));
    internal static string Lunar(DateTime date)
    {
        try{var c=new ChineseLunisolarCalendar();int m=c.GetMonth(date),leap=c.GetLeapMonth(c.GetYear(date));bool isLeap=m==leap;if(leap>0&&m>=leap)m--;int day=c.GetDayOfMonth(date);
            if(UiText.IsEnglish)return $"Lunar {(isLeap?"leap ":"")}{m}/{day}";
            string[] months={LocalizedText.Get("正"),LocalizedText.Get("二"),LocalizedText.Get("三"),LocalizedText.Get("四"),LocalizedText.Get("五"),LocalizedText.Get("六"),LocalizedText.Get("七"),LocalizedText.Get("八"),LocalizedText.Get("九"),LocalizedText.Get("十"),LocalizedText.Get("冬"),LocalizedText.Get("腊")};string[] digits={LocalizedText.Get("一"),LocalizedText.Get("二"),LocalizedText.Get("三"),LocalizedText.Get("四"),LocalizedText.Get("五"),LocalizedText.Get("六"),LocalizedText.Get("七"),LocalizedText.Get("八"),LocalizedText.Get("九"),LocalizedText.Get("十")};
            string dayText=day<=10?LocalizedText.Get("初")+digits[day-1]:day<20?LocalizedText.Get("十")+digits[day-11]:day==20?LocalizedText.Get("二十"):day<30?LocalizedText.Get("廿")+digits[day-21]:LocalizedText.Get("三十");
            return (isLeap?LocalizedText.Get("闰"):"")+months[m-1]+LocalizedText.Get("月")+dayText;
        }catch(ArgumentOutOfRangeException){return LocalizedText.Get("农历不可用");}
    }
    private static FrameworkElement Month(DateTime month,bool large=false,string ink="WidgetInk",Action<DateTime>? select=null,DateTime? selected=null)
    {
        var g=new Grid();Rows(g,-1,1);var names=new UniformGrid{Columns=7,Margin=new Thickness(0,0,0,7)};
        foreach(var name in UiText.IsEnglish?new[]{"M","T","W","T","F","S","S"}:new[]{LocalizedText.Get("一"),LocalizedText.Get("二"),LocalizedText.Get("三"),LocalizedText.Get("四"),LocalizedText.Get("五"),LocalizedText.Get("六"),LocalizedText.Get("日")}){var label=T(name,large?12:9,ink);label.HorizontalAlignment=HorizontalAlignment.Center;label.Opacity=.6;names.Children.Add(label);}At(g,names);
        var days=new UniformGrid{Columns=7,Rows=6};var first=new DateTime(month.Year,month.Month,1);int offset=((int)first.DayOfWeek+6)%7;
        for(int i=0;i<42;i++){long ticks=first.Ticks+(i-offset)*TimeSpan.TicksPerDay;if(ticks<DateTime.MinValue.Ticks||ticks>DateTime.MaxValue.Ticks){days.Children.Add(new Grid());continue;}var day=new DateTime(ticks);var box=new Grid();var label=T(day.Day.ToString(),large?16:11,ink,day.Date==DateTime.Today);label.HorizontalAlignment=HorizontalAlignment.Center;
            if(day.Month!=month.Month)label.Opacity=.23;
            if(day.Date==DateTime.Today){var circle=new Ellipse{Width=large?28:21,Height=large?28:21};circle.SetResourceReference(Shape.FillProperty,"WidgetAccent");circle.Opacity=.3;box.Children.Add(circle);}
            box.Children.Add(label);if(select!=null){var button=new Button{Template=PlainButton,Background=Brushes.Transparent,Content=box,Padding=new Thickness(0),MinHeight=0,ToolTip=day.ToString(LocalizedText.Get("yyyy年M月d日 dddd"),Chinese)};button.Click+=(_,_)=>select(day);if(day.Date==selected?.Date)box.Opacity=.75;days.Children.Add(button);}else days.Children.Add(box);}
        At(g,days,row:1);return g;
    }
    private static FrameworkElement Weather(WidgetDefinition d,WidgetRenderData data)
    {
        if(d.Layout==WidgetLayout.CalendarWeather)return Calendar(d,data);
        var g=Body(d);var w=data.Weather;bool small=d.Size.Columns==2,tall=d.Size.Rows>=4;
        Rows(g,-1,1,tall?1:-1);
        var head=new Grid();Columns(head,1,1);At(head,T(data.Source.City.Length==0?LocalizedText.Get("天气"):data.Source.City,small?13:14,bold:true));
        FrameworkElement time=small?(w==null?WidgetIcons.Create("cloud",21):WidgetIcons.Weather(w.Code,w.IsDay,21)):T(w?.ModelTime.ToString("HH:mm")??"—:—",11,"WidgetMuted");time.HorizontalAlignment=HorizontalAlignment.Right;At(head,time,1);At(g,head);
        var main=new Grid();Columns(main,small?1:1.2,small?0:1.6);
        var degree=T(w==null?"—°":$"{w.Temperature:0}°",small?67:tall?104:94,bold:false);At(main,degree);
        bool hourly=d.Layout==WidgetLayout.WeatherHourly||d.Layout==WidgetLayout.WeatherPoster;
        var detail=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
        var icon=w==null?WidgetIcons.Create("cloud",small?22:30):WidgetIcons.Weather(w.Code,w.IsDay,small?22:30);icon.HorizontalAlignment=HorizontalAlignment.Left;
        if(!small){detail.Children.Add(icon);detail.Children.Add(T(w==null?LocalizedText.Get("选择城市"):WidgetWeatherService.Description(w.Code),13));detail.Children.Add(T(w==null?LocalizedText.Get("最高 —°  最低 —°"):LocalizedText.Format($"最高 {w.Days[0].High:0}°  最低 {w.Days[0].Low:0}°"),10,"WidgetMuted"));}
        if(!tall&&!small&&(d.Size.Columns>=6||hourly)){detail.Children.Clear();detail.Children.Add(Forecast(w,hourly,d.Size.Columns>=6?4:3));}
        At(main,detail,1);At(g,main,row:1);
        if(small){At(g,Stack(T(w==null?LocalizedText.Get("选择城市"):WidgetWeatherService.Description(w.Code),12),T(w==null?LocalizedText.Get("实时天气暂不可用"):$"{w.Days[0].High:0}° / {w.Days[0].Low:0}°",10,"WidgetMuted")),row:2);}
        else if(tall)At(g,Forecast(w,hourly,d.Size.Rows>=6?8:5),row:2);
        else{var foot=T(data.Error??(w==null?LocalizedText.Get("选择天气城市以获取预报"):LocalizedText.Format($"{WidgetWeatherService.Description(w.Code)} · 最高 {w.Days[0].High:0}°  最低 {w.Days[0].Low:0}°")),10,"WidgetMuted");At(g,foot,row:2);}
        if(w!=null&&(d.Layout==WidgetLayout.WeatherOutlook||d.Layout==WidgetLayout.Standard&&d.Id.Contains("forecast"))){main.Children.Clear();main.ColumnDefinitions.Clear();main.Children.Add(Forecast(w,false,small?2:5));}
        if(d.Layout is WidgetLayout.WeatherHourly or WidgetLayout.WeatherPoster){var scene=new Grid();var glow=new Ellipse{Width=210,Height=210,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(-40,-125,0,0),IsHitTestVisible=false,Opacity=w?.IsDay==false?.12:.5,Fill=SkyGlow};scene.Children.Add(glow);scene.Children.Add(g);return scene;}return g;
    }
    private static readonly Brush SkyGlow=CreateSkyGlow();
    private static Brush CreateSkyGlow(){var brush=new RadialGradientBrush(Color.FromArgb(160,255,248,211),Colors.Transparent);brush.Freeze();return brush;}
    private static FrameworkElement Forecast(WidgetWeatherSnapshot? w,bool hourly,int count)
    {
        if(w==null)return Empty(LocalizedText.Get("预报暂不可用"),LocalizedText.Get("设置天气城市后显示"));
        var p=new UniformGrid();
        if(hourly){var hours=w.Hours.Where(h=>h.Time>=w.ModelTime).Take(count).ToArray();if(hours.Length==0)return Empty(LocalizedText.Get("逐小时预报不可用"),LocalizedText.Get("保留当前天气"));p.Columns=hours.Length;
            foreach(var h in hours)p.Children.Add(ForecastCell(h.Time.ToString(LocalizedText.Get("H时")),h.Code,$"{h.Temperature:0}°",""));}
        else{var days=w.Days.Take(count).ToArray();p.Columns=days.Length;foreach(var day in days)p.Children.Add(ForecastCell(day.Date.ToString("ddd",Chinese),day.Code,$"{day.High:0}°",$"{day.Low:0}°"));}
        return p;
    }
    private static FrameworkElement ForecastCell(string title,int code,string high,string low)
    {
        var p=Stack(T(title,10,"WidgetMuted"),WidgetIcons.Weather(code,true,23),T(high,16,bold:true));if(low.Length>0)p.Children.Add(T(low,11,"WidgetMuted"));p.HorizontalAlignment=HorizontalAlignment.Center;p.VerticalAlignment=VerticalAlignment.Center;
        foreach(FrameworkElement child in p.Children){child.HorizontalAlignment=HorizontalAlignment.Center;child.Margin=new Thickness(0,3,0,3);}return p;
    }
    private static FrameworkElement Calendar(WidgetDefinition d,WidgetRenderData data)
    {
        var now=DateTime.Now;var displayed=data.Month??now;var selected=data.SelectedDate??now;var g=Body(d);bool small=d.Size.Columns==2,tall=d.Size.Rows>=4;
        if(d.Layout==WidgetLayout.CalendarClock){Rows(g,-1,1,-1);At(g,T(LocalizedText.Get("本地时间"),12,"WidgetMuted"));At(g,T(now.ToString("HH:mm"),d.Size.Columns>=4?76:43,bold:true),row:1);At(g,T(now.ToString(LocalizedText.Get("M月d日 dddd"),Chinese),12,"WidgetAccent"),row:2);return g;}
        if(d.Id=="calendar-month"){g.Children.Add(Month(displayed,tall,select:data.SelectDate,selected:selected));return g;}
        if(d.Id=="calendar-agenda")d=d with{Layout=WidgetLayout.CalendarUpcoming};
        if(d.Id=="calendar-planner"){
            if(tall)Rows(g,1,1);else Columns(g,1.1,1);At(g,Month(displayed,tall,select:data.SelectDate,selected:selected));
            var agenda=Stack(T(selected.ToString(LocalizedText.Get("M月d日")),15,"WidgetAccent",true));var dayEvents=data.Options.Events.Where(e=>e.Start.Date==selected.Date).OrderBy(e=>e.Start).Take(tall?5:3).ToArray();foreach(var e in dayEvents){var line=T(e.Start.ToString("HH:mm")+"  "+e.Title,12);line.Margin=new Thickness(0,10,0,0);agenda.Children.Add(line);}if(dayEvents.Length==0)agenda.Children.Add(T(LocalizedText.Get("暂无本应用日程"),11,"WidgetMuted"));agenda.Margin=new Thickness(tall?0:18,tall?15:0,0,0);At(g,agenda,tall?0:1,tall?1:0);return g;
        }
        if(d.Layout is WidgetLayout.CalendarUpcoming or WidgetLayout.CalendarTimeline or WidgetLayout.CalendarGroups){
            Rows(g,-1,1);At(g,Stack(T(now.ToString(LocalizedText.Get("M月d日 dddd"),Chinese),16,"WidgetAccent",true),T(Lunar(now),11,"WidgetMuted")));
            var list=new StackPanel{Margin=new Thickness(0,15,0,0)};DateTime? last=null;int maximum=d.Size.Rows>=6?10:tall?6:2;
            var events=data.Options.Events.Where(e=>e.Start>=now.Date).OrderBy(e=>e.Start).Take(maximum).ToArray();
            if(d.Layout==WidgetLayout.CalendarTimeline){foreach(var day in new[]{now.Date,now.Date.AddDays(1)}){list.Children.Add(T(day==now.Date?LocalizedText.Get("今天"):LocalizedText.Get("明天"),14,"WidgetAccent",true));var items=events.Where(e=>e.Start.Date==day).ToArray();if(items.Length==0)list.Children.Add(T(LocalizedText.Get("暂无日程"),12,"WidgetMuted"));foreach(var e in items){var row=T("●  "+e.Start.ToString("HH:mm")+"  "+e.Title,13);row.Margin=new Thickness(0,9,0,9);list.Children.Add(row);}list.Children.Add(Rule());}At(g,list,row:1);return g;}
            foreach(var e in events){if(last!=e.Start.Date){var heading=T(e.Start.Date==now.Date?LocalizedText.Get("今天"):e.Start.Date==now.Date.AddDays(1)?LocalizedText.Get("明天"):e.Start.ToString(LocalizedText.Get("M月d日")),13,"WidgetAccent",true);heading.Margin=new Thickness(0,9,0,6);list.Children.Add(heading);last=e.Start.Date;}
                list.Children.Add(T(e.Start.ToString("HH:mm")+"  "+e.Title,13));list.Children.Add(Rule());}
            if(events.Length==0)list.Children.Add(Empty(LocalizedText.Get("接下来一片从容"),LocalizedText.Get("暂无本应用日程")));At(g,list,row:1);return g;
        }
        if(small||d.Layout==WidgetLayout.CalendarDate&&d.Size.Columns==2){
            Rows(g,-1,1,-1);At(g,T(now.ToString(LocalizedText.Get("M月 · dddd"),Chinese),13,"WidgetAccent",true));At(g,T(now.Day.ToString(),93),row:1);At(g,T(Lunar(now),12,"WidgetMuted"),row:2);return g;
        }
        if(tall)Rows(g,-1,1,1);else Columns(g,1,1.9);
        var info=Stack(T(displayed.ToString(LocalizedText.Get("yyyy年 M月"),Chinese),15,"WidgetAccent",true),T(Lunar(selected),13),T(d.Layout is WidgetLayout.CalendarGiant or WidgetLayout.CalendarPoster or WidgetLayout.CalendarMinimal?now.ToString("HH:mm"):selected.ToString("dddd",Chinese),14,"WidgetAccent"));info.VerticalAlignment=VerticalAlignment.Top;
        if(d.Layout!=WidgetLayout.CalendarGiant){info.Children.Insert(1,T(selected.Day.ToString(),tall?100:64));}At(g,info);
        var calendar=new Grid();if(d.Layout is WidgetLayout.CalendarGiant or WidgetLayout.CalendarPoster){var huge=T(selected.Day.ToString(),tall?228:158,"WidgetAccent",true);huge.Opacity=.26;huge.HorizontalAlignment=HorizontalAlignment.Right;calendar.Children.Add(huge);}
        calendar.Children.Add(Month(displayed,tall,select:data.SelectDate,selected:selected));At(g,calendar,column:tall?0:1,row:tall?1:0);
        if(d.Layout==WidgetLayout.CalendarWeather){
            g.Children.Clear();g.RowDefinitions.Clear();g.ColumnDefinitions.Clear();if(tall)Rows(g,1,1);else Columns(g,1.25,1);
            At(g,Month(displayed,tall,select:data.SelectDate,selected:selected));var w=data.Weather;var p=Stack(T(data.Source.City.Length==0?LocalizedText.Get("天气"):data.Source.City,14,"WidgetMuted"),T(w==null?"—°":$"{w.Temperature:0}°",tall?88:68),T(w==null?LocalizedText.Get("选择天气城市"):WidgetWeatherService.Description(w.Code),13),T(w==null?LocalizedText.Get("最高 —° / 最低 —°"):LocalizedText.Format($"最高 {w.Days[0].High:0}° / 最低 {w.Days[0].Low:0}°"),11,"WidgetMuted"));p.Margin=new Thickness(tall?0:22,tall?15:0,0,0);At(g,p,column:tall?0:1,row:tall?1:0);
        }else if(tall){var next=data.Options.Events.Where(e=>e.Start>=now).OrderBy(e=>e.Start).Take(3);var agenda=Stack(T(LocalizedText.Get("接下来"),14,"WidgetAccent",true));foreach(var e in next)agenda.Children.Add(T(e.Start.ToString("MM-dd HH:mm")+"  "+e.Title,13));if(agenda.Children.Count==1)agenda.Children.Add(T(LocalizedText.Get("暂无日程"),12,"WidgetMuted"));agenda.Margin=new Thickness(0,18,0,0);At(g,agenda,row:2);}
        return g;
    }
    private static FrameworkElement Clock(WidgetDefinition d,WidgetRenderData data)
    {
        var g=Body(d);var zone=TimeZoneInfo.FindSystemTimeZoneById(data.Source.TimeZone);var now=TimeZoneInfo.ConvertTime(DateTimeOffset.Now,zone).DateTime;
        if(d.Layout==WidgetLayout.ClockWorld){var zones=data.Source.WorldZones;var layout=new UniformGrid{Columns=d.Size.Rows>=4?2:Math.Min(3,Math.Max(1,zones.Count))};
            foreach(var id in zones){var z=TimeZoneInfo.FindSystemTimeZoneById(id);var local=TimeZoneInfo.ConvertTime(DateTimeOffset.Now,z);var p=Stack(T(ZoneName(z),12,"WidgetMuted"),T(local.ToString("HH:mm"),d.Size.Rows>=4?38:31),T(local.ToString(LocalizedText.Get("M月d日")),10,"WidgetMuted"));p.Margin=new Thickness(5,8,5,8);p.VerticalAlignment=VerticalAlignment.Center;layout.Children.Add(p);}g.Children.Add(layout);return g;}
        bool simple=d.Layout is WidgetLayout.ClockClassic or WidgetLayout.ClockMinimal;
        if(simple){g.Children.Add(new WidgetClock(zone,d.Layout==WidgetLayout.ClockMinimal,data.Now,data.MotionEnabled));return g;}
        bool tall=d.Size.Rows>=4;if(tall)Rows(g,1,1);else Columns(g,1,1.2);
        At(g,new WidgetClock(zone,false,data.Now,data.MotionEnabled));FrameworkElement right=d.Layout==WidgetLayout.ClockMonth?Month(now,tall):Stack(T(now.ToString("dddd",Chinese),13,"WidgetAccent",true),T(now.Day.ToString(),72),T(now.ToString(LocalizedText.Get("yyyy年 M月"),Chinese),12,"WidgetMuted"),T(Lunar(now),11,"WidgetMuted"));
        right.Margin=new Thickness(tall?0:20,tall?18:0,0,0);At(g,right,column:tall?0:1,row:tall?1:0);return g;
    }
    internal static string ZoneName(TimeZoneInfo zone)=>zone.Id switch{"China Standard Time"=>LocalizedText.Get("北京"),"GMT Standard Time"=>LocalizedText.Get("伦敦"),"Eastern Standard Time"=>LocalizedText.Get("纽约"),"Tokyo Standard Time"=>LocalizedText.Get("东京"),_=>zone.DisplayName};
    private static FrameworkElement Reminders(WidgetDefinition d,WidgetRenderData data)
    {
        var g=Body(d);Rows(g,-1,1);var tasks=(data.RemoteTasks??data.Options.Tasks).Where(t=>data.Source.List.Length==0||t.List==data.Source.List).ToArray();
        var head=new Grid();Columns(head,1,1);At(head,T(data.Source.List.Length==0?LocalizedText.Get("提醒事项"):data.Source.List,16,"WidgetAccent",true));var count=T(data.Source.MicrosoftAccount&&data.Error!=null?"—":tasks.Count(t=>!t.Completed).ToString(),31,"WidgetAccent",true);count.HorizontalAlignment=HorizontalAlignment.Right;At(head,count,1);At(g,head);
        var content=new Grid{Margin=new Thickness(0,10,0,0)};bool date=d.Layout==WidgetLayout.ReminderDate&&d.Size.Columns>=4;if(date){Columns(content,1,2);At(content,Stack(T(DateTime.Now.ToString("ddd",Chinese),12,"WidgetAccent"),T(DateTime.Today.Day.ToString(),61)));}
        var list=new StackPanel();if(data.Error!=null)list.Children.Add(T(data.Error,11,"WidgetMuted"));int max=d.Size.Rows>=6?10:d.Size.Rows>=4?6:3;string? group=null;
        foreach(var t in tasks.OrderBy(t=>t.Completed).ThenBy(t=>t.Due??DateTime.MaxValue).Take(max)){
            if(d.Layout is WidgetLayout.ReminderGroups or WidgetLayout.ReminderDetail&&t.List!=group){group=t.List;var h=T(group,12,"WidgetAccent",true);h.Margin=new Thickness(0,9,0,6);list.Children.Add(h);}
            var row=new Grid{Margin=new Thickness(0,4,0,4)};row.ColumnDefinitions.Add(new(){Width=new GridLength(25)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
            var check=new Button{Template=PlainButton,Width=18,Height=18,MinHeight=0,Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),ToolTip=data.Preview?LocalizedText.Get("预览不会改变任务"):LocalizedText.Get("完成任务")};
            var circle=new Ellipse{Width=15,Height=15,StrokeThickness=1.4};circle.SetResourceReference(Shape.StrokeProperty,"WidgetAccent");if(t.Completed)circle.SetResourceReference(Shape.FillProperty,"WidgetAccent");check.Content=circle;
            check.Click+=async(_,_)=>{if(data.Preview)return;check.IsEnabled=false;check.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(1,.35,TimeSpan.FromMilliseconds(160)));await System.Threading.Tasks.Task.Delay(140);try{if(check.IsLoaded)data.Complete?.Invoke(t.Id);}finally{if(check.IsLoaded){check.IsEnabled=true;check.BeginAnimation(UIElement.OpacityProperty,null);}}};At(row,check);
            var label=T(t.Title,13,bold:false);label.Opacity=t.Completed?.4:1;var detail=Stack(label);if(t.Due.HasValue)detail.Children.Add(T(t.Due.Value.ToString(LocalizedText.Get("M月d日 HH:mm")),10,"WidgetMuted"));if(d.Size.Rows>=6&&t.Detail.Length>0){var text=new TextBlock{Text=t.Detail,TextWrapping=TextWrapping.Wrap,MaxHeight=35,FontSize=11};text.SetResourceReference(TextBlock.ForegroundProperty,"WidgetMuted");detail.Children.Add(text);}
            detail.ToolTip=t.Detail.Length>0?t.Detail:t.Title;detail.MouseLeftButtonUp+=(_,e)=>{if(!data.Preview)data.EditTask?.Invoke(t.Id);e.Handled=true;};At(row,detail,1);list.Children.Add(row);}
        if(tasks.Length==0)list.Children.Add(Empty(data.Source.MicrosoftAccount?"Microsoft To Do":LocalizedText.Get("一切就绪"),data.Source.MicrosoftAccount?(data.Error==null?LocalizedText.Get("暂无待办"):""):LocalizedText.Get("在组件库中添加待办")));At(content,list,date?1:0);At(g,content,row:1);return g;
    }
    private static FrameworkElement Notes(WidgetDefinition d,WidgetRenderData data)
    {
        var g=new Grid{ClipToBounds=true};g.RowDefinitions.Add(new(){Height=new GridLength(d.Size.Columns==2?43:50)});g.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var heading=new Border{Background=WidgetBrushes.Surface("#F4CC5E",true),Padding=new Thickness(18,8,18,8)};var title=T(data.Source.List.Length==0?LocalizedText.Get("备忘录"):data.Source.List,15,bold:true);title.SetValue(System.Windows.Documents.TextElement.ForegroundProperty,WidgetBrushes.Get("#55441D"));heading.Child=title;At(g,heading);
        var notes=(data.FolderNotes??data.Options.Notes).Where(n=>data.Source.List.Length==0||n.Folder==data.Source.List).OrderByDescending(n=>n.Modified).Take(d.Size.Rows>=6?7:d.Size.Rows>=4?4:d.Size.Columns==2?1:2).ToArray();
        var p=new StackPanel{Margin=new Thickness(18,13,18,12)};
        if(data.Error!=null)p.Children.Add(T(data.Error,11,"WidgetMuted"));
        foreach(var n in notes){var item=Stack(T(n.Title.Length==0?LocalizedText.Get("无标题"):n.Title,14,bold:true),T(n.Modified.ToString(LocalizedText.Get("M月d日 HH:mm")),10,"WidgetMuted"));var summary=new TextBlock{Text=n.Body,FontSize=12,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=d.Size.Rows>=4?40:33,Margin=new Thickness(0,5,0,0)};summary.SetResourceReference(TextBlock.ForegroundProperty,"WidgetMuted");item.Children.Add(summary);item.ToolTip=n.Title;item.MouseLeftButtonUp+=(_,e)=>{if(!data.Preview){if(n.WebUrl.Length>0){if(WidgetMicrosoftService.SafeNoteUrl(n.WebUrl))try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(n.WebUrl){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message,LocalizedText.Get("打开笔记"));}}else if(n.FilePath.Length>0){try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(n.FilePath){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message,LocalizedText.Get("打开笔记"));}}else data.EditNote?.Invoke(n.Id);}e.Handled=true;};p.Children.Add(item);p.Children.Add(Rule());}
        if(notes.Length==0)p.Children.Add(Empty(data.Source.MicrosoftAccount?"OneNote":LocalizedText.Get("捕捉一个想法"),data.Source.MicrosoftAccount?(data.Error==null?LocalizedText.Get("暂无笔记"):""):LocalizedText.Get("添加笔记，或选择笔记文件夹")));At(g,p,row:1);return g;
    }
}

internal sealed class WidgetClock : Grid
{
    private readonly TimeZoneInfo _zone;
    private readonly RotateTransform _hour=new(),_minute=new(),_second=new();
    private readonly DateTime? _fixture;
    private readonly bool _motion;
    private readonly System.Windows.Threading.DispatcherTimer _resync = new() { Interval = TimeSpan.FromSeconds(1) };
    internal WidgetClock(TimeZoneInfo zone,bool minimal,DateTime? fixture=null,bool motion=true)
    {
        _zone=zone;_fixture=fixture;_motion=motion;_resync.Tick+=(_,_)=>Start();var face=new Grid{Width=144,Height=144};var view=new Viewbox{Stretch=Stretch.Uniform,Child=face};Children.Add(view);
        for(int i=0;i<60;i++){if(minimal&&i%5!=0)continue;var tick=new Rectangle{Width=i%5==0?3:1.6,Height=i%5==0?8:3.5,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,2,0,0),Opacity=i%5==0?.95:.55};tick.SetResourceReference(Shape.FillProperty,"WidgetInk");var wrapper=new Grid{RenderTransform=new RotateTransform(i*6),RenderTransformOrigin=new Point(.5,.5)};wrapper.Children.Add(tick);face.Children.Add(wrapper);}
        if(!minimal)for(int i=1;i<=12;i++){double angle=i*Math.PI/6;var label=new TextBlock{Text=i.ToString(),FontSize=11,FontWeight=FontWeights.Bold,FontFamily=new FontFamily("Segoe UI Variable"),Width=23,TextAlignment=TextAlignment.Center};label.SetResourceReference(TextBlock.ForegroundProperty,"WidgetInk");var c=new Canvas();Canvas.SetLeft(label,60.5+Math.Sin(angle)*52);Canvas.SetTop(label,64-Math.Cos(angle)*52);c.Children.Add(label);face.Children.Add(c);}
        Hand(_hour,34,4,"WidgetInk");Hand(_minute,49,2.7,"WidgetInk");Hand(_second,56,1.1,"WidgetAccent");var dot=new Ellipse{Width=6,Height=6};dot.SetResourceReference(Shape.FillProperty,"WidgetAccent");face.Children.Add(dot);
        Loaded+=(_,_)=>Start();Unloaded+=(_,_)=>Stop();IsVisibleChanged+=(_,_)=>{if(IsVisible&&IsLoaded)Start();else Stop();};Start();
        void Hand(RotateTransform rotate,double length,double width,string ink){var hand=new Grid{RenderTransform=rotate,RenderTransformOrigin=new Point(.5,.5)};var line=new Rectangle{Width=width,Height=length,RadiusX=width/2,RadiusY=width/2,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,72-length,0,0)};line.SetResourceReference(Shape.FillProperty,ink);hand.Children.Add(line);face.Children.Add(hand);}
    }
    private void Start()
    {
        var utc=_fixture is DateTime sample?new DateTimeOffset(DateTime.SpecifyKind(sample,DateTimeKind.Utc)):DateTimeOffset.UtcNow;
        RefreshTime(utc);
        if(IsLoaded&&IsVisible&&!_fixture.HasValue)_resync.Start();else _resync.Stop();
    }
    internal void RefreshTime(DateTimeOffset utc)
    {
        double seconds=TimeZoneInfo.ConvertTime(utc,_zone).TimeOfDay.TotalSeconds;
        Animate(_hour,seconds/120,43200);Animate(_minute,seconds/10,3600);Animate(_second,seconds*6,60);
    }
    private void Animate(RotateTransform target,double angle,double seconds)
    {
        target.BeginAnimation(RotateTransform.AngleProperty,null);target.Angle=angle%360;
        if(IsLoaded&&IsVisible&&!_fixture.HasValue&&_motion&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast)
            target.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(angle%360,angle%360+360,TimeSpan.FromSeconds(seconds)){RepeatBehavior=RepeatBehavior.Forever});
    }
    private void Stop(){_resync.Stop();foreach(var r in new[]{_hour,_minute,_second})r.BeginAnimation(RotateTransform.AngleProperty,null);}
}

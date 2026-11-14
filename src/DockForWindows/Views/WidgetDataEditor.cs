using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DockForWindows.Services;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal static class WidgetDataEditor
{
    internal static Window Window(string title,Panel body,double width=440,double height=450)=>new(){Title=title,Width=width,Height=height,MinWidth=360,MinHeight=300,WindowStartupLocation=WindowStartupLocation.CenterScreen,
        FontFamily=new FontFamily("Segoe UI Variable, Microsoft YaHei UI, Segoe UI"),FontSize=13,Background=WidgetBrushes.Get("#F3F6F5"),Foreground=WidgetBrushes.Get("#233837"),Content=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};
    internal static TextBox Field(Panel body,string title,string value,bool multiline=false){body.Children.Add(new TextBlock{Text=title,Margin=new Thickness(0,10,0,5)});var box=new TextBox{Text=value,Padding=new Thickness(7),AcceptsReturn=multiline,TextWrapping=multiline?TextWrapping.Wrap:TextWrapping.NoWrap,MinHeight=multiline?130:30,MaxLength=multiline?100000:300};body.Children.Add(box);return box;}
    internal static void Task(WidgetOptions original,Guid? id,Func<WidgetOptions,bool> save)
    {
        var task=original.Tasks.FirstOrDefault(x=>x.Id==id)?.Copy()??new();var body=new StackPanel{Margin=new Thickness(22)};var window=Window(LocalizedText.Get("提醒事项"),body);
        var title=Field(body,LocalizedText.Get("任务"),task.Title);var list=Field(body,LocalizedText.Get("列表"),task.List);var detail=Field(body,LocalizedText.Get("详情"),task.Detail,true);var due=Field(body,LocalizedText.Get("截止日期（可留空） · yyyy-MM-dd HH:mm"),task.Due?.ToString("yyyy-MM-dd HH:mm")??"");
        var done=new CheckBox{Content=LocalizedText.Get("已完成"),IsChecked=task.Completed,Margin=new Thickness(0,12,0,12)};body.Children.Add(done);var status=new TextBlock{TextWrapping=TextWrapping.Wrap};body.Children.Add(status);
        var commit=new Button{Content=LocalizedText.Get("保存"),Padding=new Thickness(14,7,14,7),Margin=new Thickness(0,10,0,0)};body.Children.Add(commit);
        commit.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(title.Text)){status.Text=LocalizedText.Get("请输入任务标题。");return;}DateTime? date=null;if(due.Text.Trim().Length>0){if(!DateTime.TryParseExact(due.Text.Trim(),"yyyy-MM-dd HH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var parsed)){status.Text=LocalizedText.Get("截止日期格式无效。");return;}date=parsed;}
            var options=original.Copy();task.Title=title.Text.Trim();task.List=list.Text.Trim();task.Detail=detail.Text;task.Due=date;task.Completed=done.IsChecked==true;options.Tasks.RemoveAll(x=>x.Id==task.Id);options.Tasks.Add(task.Copy());if(save(options))window.Close();else status.Text=LocalizedText.Get("保存失败，原数据保留。");};window.ShowDialog();
    }
    internal static void Note(WidgetOptions original,Guid? id,Func<WidgetOptions,bool> save)
    {
        var note=original.Notes.FirstOrDefault(x=>x.Id==id)?.Copy()??new();var body=new StackPanel{Margin=new Thickness(22)};var window=Window(LocalizedText.Get("备忘录 · 自动保存"),body,500,520);
        var title=Field(body,LocalizedText.Get("标题"),note.Title);var folder=Field(body,LocalizedText.Get("文件夹"),note.Folder);var text=Field(body,LocalizedText.Get("内容"),note.Body,true);text.MinHeight=250;
        var status=new TextBlock{Text=LocalizedText.Get("输入后自动保存。"),Margin=new Thickness(0,12,0,0)};body.Children.Add(status);
        bool dirty=false;WidgetOptions latest=original.Copy();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};
        bool Commit(){if(!dirty)return true;note.Title=title.Text.Trim();note.Body=text.Text;note.Folder=folder.Text.Trim();note.Modified=DateTime.Now;var options=latest.Copy();options.Notes.RemoveAll(x=>x.Id==note.Id);options.Notes.Add(note.Copy());bool ok=save(options);status.Text=ok?LocalizedText.Get("已保存 · ")+DateTime.Now.ToString("HH:mm:ss"):LocalizedText.Get("保存失败，请保持窗口打开后重试。");if(ok){latest=options;dirty=false;}return ok;}
        void Changed(object sender,TextChangedEventArgs e){dirty=true;status.Text=LocalizedText.Get("待保存…");timer.Stop();timer.Start();}title.TextChanged+=Changed;folder.TextChanged+=Changed;text.TextChanged+=Changed;
        timer.Tick+=(_,_)=>{timer.Stop();Commit();};window.Closing+=(_,e)=>{timer.Stop();if(!Commit())e.Cancel=true;};window.Closed+=(_,_)=>timer.Stop();window.ShowDialog();
    }
}

internal static class WidgetInstanceEditor
{
    internal static void Show(WidgetInstance original,Action<WidgetInstance> save)
    {
        var copy=original.Copy();if(copy.Material==WidgetMaterial.Glass)copy.Material=WidgetMaterial.Light;var body=new StackPanel{Margin=new Thickness(22)};var window=WidgetDataEditor.Window(LocalizedText.Get("组件设置 · ")+WidgetRegistry.Get(copy.DefinitionId).Title,body,520,760);
        var preview=new WidgetHost(WidgetRegistry.Get(copy.DefinitionId) with{Size=copy.Size},copy.Exponent);var previewBox=new Viewbox{Child=preview,Stretch=Stretch.Uniform,Height=210,Margin=new Thickness(0,0,0,15)};body.Children.Add(previewBox);
        void Refresh(){preview.Resize(copy.Size);preview.SetMaterial(copy.Material,copy.Appearance);var data=WidgetDesktopService.Current?.Data(copy)??new WidgetRenderData(new(),copy.Source);preview.SetContent(WidgetContent.Render(preview.Definition,data with{Preview=true}));}
        ComboBox Choice(string title,object[] values,int index,Action<int> change){body.Children.Add(new TextBlock{Text=title,Margin=new Thickness(0,9,0,4)});var box=new ComboBox{ItemsSource=values,SelectedIndex=index,MinHeight=30};box.SelectionChanged+=(_,_)=>{if(box.SelectedIndex>=0){change(box.SelectedIndex);Refresh();}};body.Children.Add(box);return box;}
        var sizes=WidgetSizes.For(WidgetRegistry.Get(copy.DefinitionId)).ToArray();Choice(LocalizedText.Get("尺寸"),sizes.Cast<object>().ToArray(),Array.IndexOf(sizes,copy.Size),i=>copy.Size=sizes[i]);
        Choice(LocalizedText.Get("材质"),new object[]{LocalizedText.Get("浅色"),LocalizedText.Get("深色")},copy.Material==WidgetMaterial.Dark?1:0,i=>copy.Material=(WidgetMaterial)i);
        var zones=TimeZoneInfo.GetSystemTimeZones().ToArray();Choice(LocalizedText.Get("时区"),zones.Select(z=>(object)z.DisplayName).ToArray(),Array.FindIndex(zones,z=>z.Id==copy.Source.TimeZone),i=>copy.Source.TimeZone=zones[i].Id);
        var worlds=WidgetDataEditor.Field(body,LocalizedText.Get("世界时钟时区 ID（分号分隔）"),string.Join(";",copy.Source.WorldZones));worlds.TextChanged+=(_,_)=>{copy.Source.WorldZones=worlds.Text.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();copy.Source.Normalize();Refresh();};
        var list=WidgetDataEditor.Field(body,LocalizedText.Get("列表 / 笔记文件夹筛选（留空显示全部）"),copy.Source.List);list.TextChanged+=(_,_)=>{copy.Source.List=list.Text;Refresh();};
        var folder=WidgetDataEditor.Field(body,LocalizedText.Get("本地笔记文件夹（只读 .txt / .md）"),copy.Source.NotesFolder);folder.TextChanged+=(_,_)=>{copy.Source.NotesFolder=folder.Text;Refresh();};
        var location=new CheckBox{Content=UiText.IsEnglish?"Use current location":"使用当前位置",IsChecked=copy.Source.UseCurrentLocation,Margin=new Thickness(0,8,0,4)};location.Checked+=(_,_)=>{copy.Source.UseCurrentLocation=true;Refresh();};location.Unchecked+=(_,_)=>{copy.Source.UseCurrentLocation=false;Refresh();};body.Children.Add(location);
        var microsoft=new CheckBox{Content="Microsoft To Do / OneNote",IsChecked=copy.Source.MicrosoftAccount,Margin=new Thickness(0,8,0,4)};microsoft.Checked+=(_,_)=>{copy.Source.MicrosoftAccount=true;Refresh();};microsoft.Unchecked+=(_,_)=>{copy.Source.MicrosoftAccount=false;Refresh();};body.Children.Add(microsoft);
        var city=WidgetDataEditor.Field(body,LocalizedText.Get("天气城市"),copy.Source.City);var cityResults=new ComboBox{MinHeight=30,Margin=new Thickness(0,8,0,0)};body.Children.Add(cityResults);
        var search=new Button{Content=LocalizedText.Get("查找城市"),Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,7,0,7)};body.Children.Add(search);search.Click+=async(_,_)=>{search.IsEnabled=false;try{cityResults.ItemsSource=await new Services.WidgetWeatherService().SearchAsync(city.Text,default);}catch(Exception e){MessageBox.Show(e.Message,LocalizedText.Get("查找城市"));}finally{search.IsEnabled=true;}};
        cityResults.SelectionChanged+=(_,_)=>{if(cityResults.SelectedItem is Services.WidgetLocation location){copy.Source.UseCurrentLocation=false;copy.Source.City=location.Name;copy.Source.Latitude=location.Latitude;copy.Source.Longitude=location.Longitude;Refresh();}};
        var bg=WidgetDataEditor.Field(body,LocalizedText.Get("背景 HEX（留空跟随模板）"),copy.Appearance.Background);var fg=WidgetDataEditor.Field(body,LocalizedText.Get("文字 HEX"),copy.Appearance.Foreground);var accent=WidgetDataEditor.Field(body,LocalizedText.Get("强调色 HEX"),copy.Appearance.Accent);var end=WidgetDataEditor.Field(body,LocalizedText.Get("渐变终点 HEX（可留空）"),copy.Appearance.GradientEnd);
        void Colors(){copy.Appearance.Background=bg.Text;copy.Appearance.Foreground=fg.Text;copy.Appearance.Accent=accent.Text;copy.Appearance.GradientEnd=end.Text;Refresh();}bg.TextChanged+=(_,_)=>Colors();fg.TextChanged+=(_,_)=>Colors();accent.TextChanged+=(_,_)=>Colors();end.TextChanged+=(_,_)=>Colors();
        Choice(LocalizedText.Get("数字字体"),new object[]{LocalizedText.Get("标准"),LocalizedText.Get("圆润"),LocalizedText.Get("空心"),LocalizedText.Get("艺术")},(int)copy.Appearance.NumberStyle,i=>copy.Appearance.NumberStyle=(WidgetNumberStyle)i);
        Choice(LocalizedText.Get("字重"),new object[]{LocalizedText.Get("跟随模板"),LocalizedText.Get("细体"),LocalizedText.Get("常规"),LocalizedText.Get("加粗")},(int)copy.Appearance.TextWeight,i=>copy.Appearance.TextWeight=(WidgetTextWeight)i);
        void Range(string title,double min,double max,double value,Action<double> change){body.Children.Add(new TextBlock{Text=title,Margin=new Thickness(0,10,0,4)});var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=.05};slider.ValueChanged+=(_,_)=>{change(slider.Value);Refresh();};body.Children.Add(slider);}
        Range(LocalizedText.Get("字号"),.75,1.35,copy.Appearance.TextScale,v=>copy.Appearance.TextScale=v);Range(LocalizedText.Get("阴影强度"),0,2,copy.Appearance.Shadow,v=>copy.Appearance.Shadow=v);Range(LocalizedText.Get("背景不透明度"),.1,1,copy.Appearance.Opacity,v=>copy.Appearance.Opacity=v);
        Choice(LocalizedText.Get("曲率"),new object[]{LocalizedText.Get("柔和 · 2.5"),LocalizedText.Get("饱满 · 3")},copy.Appearance.Curvature==3?1:0,i=>copy.Appearance.Curvature=i==1?3:2.5);
        var commit=new Button{Content=LocalizedText.Get("保存组件设置"),Padding=new Thickness(15,9,15,9),Margin=new Thickness(0,18,0,0)};body.Children.Add(commit);
        commit.Click+=(_,_)=>{foreach(var value in new[]{bg.Text,fg.Text,accent.Text,end.Text})if(value.Trim().Length>0&&!ThemeAppearance.TryColor(value,out _)){MessageBox.Show(LocalizedText.Get("颜色格式无效，请输入 #RRGGBB 或留空。"));return;}copy.Appearance.Normalize();copy.Source.Normalize();save(copy);window.Close();};Refresh();window.ShowDialog();
    }
}


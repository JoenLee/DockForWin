using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using DockForWindows.Services;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal sealed partial class WidgetGalleryPage
{
    private readonly Dictionary<string,FrameworkElement> _categoryPreviews=new();
    private ScrollViewer? _inspectorScroll;
    private void BuildInspector(StackPanel oldBody,FrameworkElement appearance)
    {
        var root=new Grid{Margin=new Thickness(20,18,20,20)};
        root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        root.ColumnDefinitions.Add(new(){Width=new GridLength(16)});
        root.ColumnDefinitions.Add(new(){Width=new GridLength(264)});
        AutomationProperties.SetAutomationId(root,"widget-inspector-layout");
        var previews=new Grid();root.Children.Add(previews);
        var parameters=new StackPanel();AutomationProperties.SetAutomationId(parameters,"widget-inspector-parameters");
        if(appearance.Parent is Panel parent)parent.Children.Remove(appearance);
        var appearanceRows=new StackPanel{Margin=new Thickness(0,0,0,12)};
        foreach(var control in ((Panel)appearance).Children.Cast<FrameworkElement>().ToArray()){
            ((Panel)appearance).Children.Remove(control);control.Width=double.NaN;control.MinWidth=0;control.Margin=new Thickness(0,0,0,10);control.HorizontalAlignment=HorizontalAlignment.Stretch;appearanceRows.Children.Add(control);
        }
        parameters.Children.Add(appearanceRows);
        foreach(var kind in Enum.GetValues<WidgetKind>()){
            string key=kind.ToString().ToLowerInvariant();var surface=kind switch{WidgetKind.Weather=>_weatherSection,WidgetKind.Calendar=>_calendarSection,_=>(Border)_extraSections[key]};
            var body=(StackPanel)surface.Child;var carousel=Carousel(key);int at=body.Children.IndexOf(carousel);var title=(FrameworkElement)body.Children[at-1];var heading=(FrameworkElement)body.Children[0];
            body.Children.Remove(heading);body.Children.Remove(title);body.Children.Remove(carousel);
            var preview=new Grid();preview.RowDefinitions.Add(new(){Height=GridLength.Auto});preview.RowDefinitions.Add(new(){Height=GridLength.Auto});preview.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
            heading.Margin=new Thickness(0,0,0,8);preview.Children.Add(heading);title.Margin=new Thickness(0,0,0,4);Grid.SetRow(title,1);preview.Children.Add(title);
            carousel.UseInspectorLayout();Grid.SetRow(carousel,2);preview.Children.Add(carousel);
            var previewSurface=new Border{Child=new WidgetPreviewFrame(preview),Padding=new Thickness(0),CornerRadius=new CornerRadius(14)};previewSurface.SetResourceReference(Border.BackgroundProperty,"SettingsCard");
            AutomationProperties.SetAutomationId(previewSurface,"widget-preview-"+key);previews.Children.Add(previewSurface);_categoryPreviews[key]=previewSurface;
            if(surface.Parent is Panel holder)holder.Children.Remove(surface);surface.Style=null;surface.Padding=new Thickness(0);surface.HorizontalAlignment=HorizontalAlignment.Stretch;surface.Margin=new Thickness(0);surface.Background=System.Windows.Media.Brushes.Transparent;surface.BorderThickness=new Thickness(0);parameters.Children.Add(surface);
        }
        _city.MinWidth=0;_searchCity.Content=LocalizedText.Get("查找");_searchCity.Padding=new Thickness(8,0,8,0);
        _inspectorScroll=new ScrollViewer{Content=parameters,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(14,12,18,16)};
        AutomationProperties.SetAutomationId(_inspectorScroll,"widget-inspector-scroll");
        _inspectorScroll.Loaded+=(_,_)=>{_inspectorScroll.ApplyTemplate();if(_inspectorScroll.Template.FindName("PART_VerticalScrollBar",_inspectorScroll) is System.Windows.Controls.Primitives.ScrollBar bar)bar.SetResourceReference(StyleProperty,"SettingsInspectorScrollBar");};
        var right=new Border{Child=_inspectorScroll,CornerRadius=new CornerRadius(14)};right.SetResourceReference(Border.BackgroundProperty,"SettingsCard");Grid.SetColumn(right,2);root.Children.Add(right);
        VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;Content=root;
        SizeChanged+=(_,_)=>root.Height=Math.Max(260,ActualHeight-root.Margin.Top-root.Margin.Bottom);
        SettingsScrollMotion.Attach(_inspectorScroll,()=>(Parent as SettingsPageHost)?.MotionEnabled?.Invoke()??SystemParameters.ClientAreaAnimation);
    }
    private FrameworkElement BuildSourceEditor(WidgetCarousel carousel,string category)
    {
        bool syncing=false;var body=new StackPanel();var panel=new Expander{Header=LocalizedText.Get("数据源"),Content=body,Margin=new Thickness(0,10,0,0),FontSize=12};AutomationProperties.SetAutomationId(panel,"widget-source-"+category);
        WidgetHost Current()=>carousel.Hosts[carousel.SelectedIndex];
        void Change(Action<WidgetSource> update){if(syncing)return;var host=Current();var source=Source(host.Definition).Copy();update(source);source.Normalize();_options.Sources[host.Definition.Id]=source;RenderHost(host);UpdateDataControls(category);}
        var zones=TimeZoneInfo.GetSystemTimeZones().ToArray();
        var clock=new StackPanel();body.Children.Add(clock);clock.Children.Add(Note(LocalizedText.Get("时区")));var zone=new ComboBox{ItemsSource=zones.Select(z=>z.DisplayName).ToArray(),MinHeight=30};AutomationProperties.SetAutomationId(zone,"widget-source-zone-"+category);clock.Children.Add(zone);zone.SelectionChanged+=(_,_)=>{if(zone.SelectedIndex>=0)Change(s=>s.TimeZone=zones[zone.SelectedIndex].Id);};
        TextBox Field(Panel owner,string title,string id,Action<string> change){owner.Children.Add(Note(title));var field=new TextBox{MinWidth=0,Margin=new Thickness(0,4,0,0),MaxLength=1024};AutomationProperties.SetAutomationId(field,id);AutomationProperties.SetName(field,title);field.TextChanged+=(_,_)=>change(field.Text);owner.Children.Add(field);return field;}
        var worlds=Field(clock,LocalizedText.Get("世界时钟时区 ID（分号分隔）"),"widget-source-worlds-"+category,text=>Change(s=>s.WorldZones=text.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList()));
        var lists=new StackPanel();body.Children.Add(lists);var list=Field(lists,LocalizedText.Get("列表筛选（留空显示全部）"),"widget-source-list-"+category,text=>Change(s=>s.List=text));

        var notes=new StackPanel();body.Children.Add(notes);var folder=Field(notes,LocalizedText.Get("笔记文件夹（只读 .txt / .md）"),"widget-source-folder-"+category,text=>Change(s=>s.NotesFolder=text));
        var weather=new StackPanel();body.Children.Add(weather);var city=Field(weather,LocalizedText.Get("当前模板天气城市"),"widget-source-city-"+category,_=>{});var results=new ComboBox{MinHeight=30,Margin=new Thickness(0,5,0,0),Visibility=Visibility.Collapsed};weather.Children.Add(results);
        var current=ActionButton(UiText.IsEnglish?"Use current location":"使用当前位置",async()=>{var id=Current().Definition.Id;Change(s=>s.UseCurrentLocation=true);if(_runtime!=null){await _runtime.LocateAsync();if(Current().Definition.Id==id&&IsLoaded)RenderHost(Current());}},"widget-source-location-"+category);weather.Children.Add(current);
        var status=Note("");var search=ActionButton(LocalizedText.Get("查找城市"),()=>{},"widget-source-search-"+category);search.Margin=new Thickness(0,6,0,0);weather.Children.Add(search);weather.Children.Add(status);string? requested=null;
        void RuntimeStatus(WidgetKind? kind){if(kind!=WidgetKind.Weather||_runtime==null||!panel.IsVisible||weather.Visibility!=Visibility.Visible)return;var source=Source(Current().Definition);var result=_runtime.Weather(source);status.Text=result.Error??(result.Weather==null?LocalizedText.Get("正在更新天气…"):(UiText.IsEnglish?"Updated ":"更新于 ")+result.Weather.RetrievedAt.ToLocalTime().ToString("HH:mm"));}
        SourceStatusChanged+=RuntimeStatus;panel.IsVisibleChanged+=(_,_)=>RuntimeStatus(WidgetKind.Weather);
        search.Click+=async(_,_)=>{var id=Current().Definition.Id;search.IsEnabled=false;status.Text=LocalizedText.Get("正在查找…");try{var locations=await _weatherService.SearchAsync(city.Text,_lifetime?.Token??CancellationToken.None);if(Current().Definition.Id!=id||!IsVisible)return;requested=id;results.ItemsSource=locations;results.Visibility=Visibility.Visible;status.Text=locations.Count==0?LocalizedText.Get("没有找到城市。"):LocalizedText.Get("请选择城市。");}catch(OperationCanceledException){}catch(Exception ex){status.Text=LocalizedText.Get("查找失败：")+ex.Message;}finally{search.IsEnabled=true;}};
        results.SelectionChanged+=(_,_)=>{if(syncing||requested!=Current().Definition.Id||results.SelectedItem is not Services.WidgetLocation location)return;Change(s=>{s.City=location.Name;s.Latitude=location.Latitude;s.Longitude=location.Longitude;s.UseCurrentLocation=false;});city.Text=location.Name;status.Text=LocalizedText.Get("已更新当前模板的数据源。");};
        void Refresh(){syncing=true;var host=Current();var source=Source(host.Definition);zone.SelectedIndex=Array.FindIndex(zones,z=>z.Id==source.TimeZone);worlds.Text=string.Join(";",source.WorldZones);list.Text=source.List;folder.Text=source.NotesFolder;city.Text=source.City;results.Visibility=Visibility.Collapsed;requested=null;status.Text="";
            clock.Visibility=host.Definition.Kind==WidgetKind.Clock?Visibility.Visible:Visibility.Collapsed;lists.Visibility=host.Definition.Kind is WidgetKind.Reminders or WidgetKind.Notes?Visibility.Visible:Visibility.Collapsed;notes.Visibility=host.Definition.Kind==WidgetKind.Notes&&!source.MicrosoftAccount?Visibility.Visible:Visibility.Collapsed;weather.Visibility=host.Definition.Kind==WidgetKind.Weather||host.Definition.Layout==WidgetLayout.CalendarWeather?Visibility.Visible:Visibility.Collapsed;
            panel.Visibility=host.Definition.Kind==WidgetKind.Music||host.Definition.Kind==WidgetKind.Calendar&&host.Definition.Layout!=WidgetLayout.CalendarWeather?Visibility.Collapsed:Visibility.Visible;syncing=false;}
        if(category is "reminders" or "notes")body.Children.Insert(2,MicrosoftAccountEditor(category,enabled=>{Change(s=>s.MicrosoftAccount=enabled);Refresh();}));carousel.SelectionChanged+=_=>Refresh();Refresh();return panel;
    }
}

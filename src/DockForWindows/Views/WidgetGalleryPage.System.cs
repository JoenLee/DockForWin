using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal sealed partial class WidgetGalleryPage
{
    internal void ShowCategory(WidgetKind kind)
    {
        _weatherSection.Visibility=kind==WidgetKind.Weather?Visibility.Visible:Visibility.Collapsed;
        _calendarSection.Visibility=kind==WidgetKind.Calendar?Visibility.Visible:Visibility.Collapsed;
        foreach(var pair in _extraSections)pair.Value.Visibility=pair.Key==kind.ToString().ToLowerInvariant()?Visibility.Visible:Visibility.Collapsed;
        foreach(var pair in _categoryPreviews)pair.Value.Visibility=pair.Key==kind.ToString().ToLowerInvariant()?Visibility.Visible:Visibility.Collapsed;
        _inspectorScroll?.ScrollToTop();ScrollToTop();if(_runtime!=null){if(kind==WidgetKind.Weather)RenderWeather();if(kind==WidgetKind.Calendar)RenderCalendar();}Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,()=>{UpdateVisibleHosts();RefreshCalendarIfNeeded();});
    }
    private readonly Dictionary<string,WidgetCarousel> _extraCarousels=new();
    private readonly Dictionary<string,FrameworkElement> _extraSections=new();
    private readonly Dictionary<string,FrameworkElement> _localDataControls=new(),_cloudDataControls=new();
    private void UpdateDataControls(string category)
    {
        if(!_localDataControls.TryGetValue(category,out var local))return;
        bool cloud=Source(Carousel(category).Hosts[Carousel(category).SelectedIndex].Definition).MicrosoftAccount;
        local.Visibility=cloud?Visibility.Collapsed:Visibility.Visible;_cloudDataControls[category].Visibility=cloud?Visibility.Visible:Visibility.Collapsed;
    }
    private WidgetRuntime? _runtime;
    private bool _ownsRuntime;private event Action<WidgetKind?>? SourceStatusChanged;
    private WidgetCarousel Carousel(string category)=>category switch{"weather"=>WeatherCarousel,"calendar"=>CalendarCarousel,_=>_extraCarousels[category]};
    private WidgetSource Source(WidgetDefinition d)=>_options.Sources.TryGetValue(d.Id,out var s)?s:DocumentClockStyles.DefaultSource(d.Id)??FigmaWidgetCatalog.DefaultSource(d.Id)??new(){City=_options.City,Latitude=_options.Latitude,Longitude=_options.Longitude,UseCurrentLocation=!_options.Latitude.HasValue};
    private FrameworkElement TemplateHeading(WidgetCarousel carousel)
    {
        var title=new TextBlock{FontSize=15,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,9,0,3)};var desc=Note("",new Thickness(0,0,0,0));
        void Refresh(){var d=carousel.Hosts[carousel.SelectedIndex].Definition;title.Text=d.Title;desc.Text=WidgetSizes.Description(d);}carousel.SelectionChanged+=_=>Refresh();Refresh();
        var p=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};p.Children.Add(title);p.Children.Add(desc);return p;
    }
    private FrameworkElement TemplateControls(WidgetCarousel carousel,string category)
    {
        bool sync=false;var p=new StackPanel{HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,7,0,0)};
        var sizes=new ComboBox{MinWidth=95,MinHeight=30,Margin=new Thickness(0)};AutomationProperties.SetAutomationId(sizes,"widget-size-"+category);p.Children.Add(sizes);
        void Refresh(){sync=true;var host=carousel.Hosts[carousel.SelectedIndex];sizes.ItemsSource=WidgetSizes.For(host.Definition);sizes.SelectedItem=host.Definition.Size;sync=false;}
        sizes.SelectionChanged+=(_,_)=>{if(sync||sizes.SelectedItem is not WidgetSize size)return;carousel.ResizeSelected(size);var host=carousel.Hosts[carousel.SelectedIndex];_options.Sizes[host.Definition.Id]=size;RenderHost(host);UpdateVisibleHosts();};
        p.Children.Add(BuildSourceEditor(carousel,category));
        foreach(var host in carousel.Hosts)if(_options.Sizes.TryGetValue(host.Definition.Id,out var size))host.Resize(size);
        carousel.SelectionChanged+=_=>{Refresh();UpdateVisibleHosts();};Refresh();return p;
    }
    private void BuildAdditionalCategories(StackPanel body)
    {
        foreach(var kind in new[]{WidgetKind.Clock,WidgetKind.Reminders,WidgetKind.Notes,WidgetKind.Music,WidgetKind.Status,WidgetKind.AiUsage}){
            string key=kind.ToString().ToLowerInvariant();var carousel=new WidgetCarousel(WidgetRegistry.Definitions.Where(d=>d.Kind==kind).ToArray(),_options.Selections.GetValueOrDefault(key),_options.Exponent);
            _extraCarousels[key]=carousel;AutomationProperties.SetAutomationId(carousel,key+"-carousel");carousel.SelectionChanged+=index=>_options.Selections[key]=index;
            var (surface,section)=Section(WidgetSizes.Category(kind),key);_extraSections[key]=surface;body.Children.Add(surface);
            section.Children.Add(TemplateHeading(carousel));section.Children.Add(carousel);section.Children.Add(TemplateControls(carousel,key));AddPlacementButton(section,key);section.Children.Add(BuildColorEditor(carousel,key));
            int localStart=section.Children.Count;if(kind==WidgetKind.Reminders){section.Children.Add(ActionButton(LocalizedText.Get("＋ 添加提醒"),()=>WidgetDataEditor.Task(_options,null,copy=>{_options.Tasks=copy.Tasks;RefreshExtras(kind);return true;}),"widget-task-add"));section.Children.Add(Note(LocalizedText.Get("组件预览不会改变任务；添加与编辑保留在设置草稿，保存后生效。")));
                var manage=new Expander{Header=LocalizedText.Get("管理待办"),Margin=new Thickness(0,10,0,0)};section.Children.Add(manage);manage.Expanded+=(_,_)=>{var list=new StackPanel();foreach(var t in _options.Tasks.ToArray()){
                    var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,4)};var title=new TextBlock{Text=t.Title,Width=86,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};row.Children.Add(title);
                    row.Children.Add(ActionButton(LocalizedText.Get("编辑"),()=>WidgetDataEditor.Task(_options,t.Id,copy=>{_options.Tasks=copy.Tasks;RefreshExtras(kind);return true;}),"widget-task-edit-"+t.Id));row.Children.Add(ActionButton(LocalizedText.Get("删除"),()=>{_options.Tasks.RemoveAll(x=>x.Id==t.Id);list.Children.Remove(row);RefreshExtras(kind);},"widget-task-delete-"+t.Id));list.Children.Add(row);}manage.Content=list;};}
            if(kind==WidgetKind.Notes){section.Children.Add(ActionButton(LocalizedText.Get("＋ 新建备忘录"),()=>WidgetDataEditor.Note(_options,null,copy=>{_options.Notes=copy.Notes;RefreshExtras(kind);return true;}),"widget-note-add"));
                var manage=new Expander{Header=LocalizedText.Get("管理应用备忘录"),Margin=new Thickness(0,10,0,0)};section.Children.Add(manage);manage.Expanded+=(_,_)=>{var list=new StackPanel();foreach(var n in _options.Notes.ToArray()){
                    var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,4)};row.Children.Add(new TextBlock{Text=n.Title,Width=220,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center});
                    row.Children.Add(ActionButton(LocalizedText.Get("编辑"),()=>WidgetDataEditor.Note(_options,n.Id,copy=>{_options.Notes=copy.Notes;RefreshExtras(kind);return true;}),"widget-note-edit-"+n.Id));row.Children.Add(ActionButton(LocalizedText.Get("删除"),()=>{_options.Notes.RemoveAll(x=>x.Id==n.Id);list.Children.Remove(row);RefreshExtras(kind);},"widget-note-delete-"+n.Id));list.Children.Add(row);}manage.Content=list;};}
            if(kind is WidgetKind.Reminders or WidgetKind.Notes){
                var local=new StackPanel();foreach(var child in section.Children.Cast<FrameworkElement>().Skip(localStart).ToArray()){section.Children.Remove(child);local.Children.Add(child);}section.Children.Add(local);_localDataControls[key]=local;
                var cloud=ActionButton(kind==WidgetKind.Reminders?(UiText.IsEnglish?"Open Microsoft To Do":"打开 Microsoft To Do"):(UiText.IsEnglish?"Open OneNote":"打开 OneNote"),()=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(kind==WidgetKind.Reminders?"https://to-do.live.com/tasks/":"https://www.onenote.com/notebooks"){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message);}},"widget-cloud-open-"+key);
                section.Children.Add(cloud);_cloudDataControls[key]=cloud;carousel.SelectionChanged+=_=>UpdateDataControls(key);UpdateDataControls(key);
            }
        }
        foreach(var carousel in new[]{WeatherCarousel,CalendarCarousel}.Concat(_extraCarousels.Values))carousel.FrameChanged+=UpdateVisibleHosts;
        Loaded+=(_,_)=>Dispatcher.BeginInvoke(UpdateVisibleHosts);ScrollChanged+=(_,_)=>UpdateVisibleHosts();
    }
    private void StartRuntime()
    {
        _runtime=WidgetDesktopService.Current?.Runtime;_ownsRuntime=_runtime==null;_runtime??=new(Dispatcher,()=>_options);
        _runtime.Changed+=RuntimeChanged;RefreshExtras(null);foreach(var host in WeatherCarousel.Hosts.Concat(CalendarCarousel.Hosts))if(host.IsVisible)RenderHost(host);
    }
    private void StopRuntime(){if(_runtime==null)return;_runtime.Changed-=RuntimeChanged;if(_ownsRuntime)_runtime.Dispose();_runtime=null;}
    private void RuntimeChanged(WidgetKind? kind){SourceStatusChanged?.Invoke(kind);if(kind==WidgetKind.Music&&WidgetDesktopService.Current is {} service)_options.MusicHistory=service.Runtime.History.Select(x=>x.Copy()).ToList();if(kind is WidgetKind.Weather){if(OnScreen(_weatherSection))RenderWeather();if(OnScreen(_calendarSection))RenderCalendar();}else RefreshExtras(kind);}
    private void RefreshExtras(WidgetKind? kind){foreach(var pair in _extraCarousels){if(kind!=null&&pair.Value.Hosts[0].Definition.Kind!=kind)continue;if(IsLoaded&&!OnScreen(_extraSections[pair.Key]))continue;foreach(var host in pair.Value.Hosts)RenderHost(host);}}
    private void RenderHost(WidgetHost host)
    {
        if(Mouse.Captured is DependencyObject captured&&host.IsAncestorOf(captured))return;
        var source=Source(host.Definition);WidgetWeatherSnapshot? weather=_weather;string? error=null;
        bool weatherKind=host.Definition.Kind==WidgetKind.Weather||host.Definition.Layout==WidgetLayout.CalendarWeather;
        if(_runtime!=null&&weatherKind){var result=_runtime.Weather(source);weather=result.Weather;error=result.Error;}
        if(_runtime!=null&&weatherKind)source=_runtime.ResolvedSource(source);var notes=host.Definition.Kind==WidgetKind.Notes?_runtime?.Folder(source.NotesFolder):null;
        host.SetWeather(weather);host.SetMaterial(_options.Material,_options.CardColors.GetValueOrDefault(host.Definition.Id));
        var data=new WidgetRenderData(_options,source,weather,_runtime?.Media,notes?.Notes,notes?.Error??(weatherKind?error:host.Definition.Kind==WidgetKind.Music?_runtime?.MediaError:null),Month:_month,SelectedDate:_selected,SelectDate:host.Definition.Kind==WidgetKind.Calendar?SelectDate:null,HistoryArt:_runtime?.HistoryArt,MediaCommand:_runtime==null?null:_runtime.Command);
        if(_runtime!=null)data=_runtime.Populate(data,host.Definition.Kind);host.SetData(data);
    }
    private void UpdateVisibleHosts()
    {
        if(!IsLoaded)return;foreach(var host in Hosts){bool active=host.IsVisible&&host.TransformToAncestor(this).TransformBounds(new Rect(host.RenderSize)).IntersectsWith(new Rect(RenderSize));if(host.SetActive(active)&&active)RenderHost(host);}
    }
}

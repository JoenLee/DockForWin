using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal sealed partial class WidgetGalleryPage
{
    private readonly Dictionary<string,WidgetCarousel> _extraCarousels=new();
    private readonly Dictionary<string,FrameworkElement> _extraSections=new();
    private WidgetRuntime? _runtime;
    private bool _ownsRuntime;
    private WidgetCarousel Carousel(string category)=>category switch{"weather"=>WeatherCarousel,"calendar"=>CalendarCarousel,_=>_extraCarousels[category]};
    private WidgetSource Source(WidgetDefinition d)=>_options.Sources.TryGetValue(d.Id,out var s)?s:new(){City=_options.City,Latitude=_options.Latitude,Longitude=_options.Longitude};
    private FrameworkElement TemplateHeading(WidgetCarousel carousel)
    {
        var title=new TextBlock{FontSize=15,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,9,0,3)};var desc=Note("",new Thickness(0,0,0,0));
        void Refresh(){var d=carousel.Hosts[carousel.SelectedIndex].Definition;title.Text=d.Title;desc.Text=WidgetSizes.Description(d);}carousel.SelectionChanged+=_=>Refresh();Refresh();
        var p=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};p.Children.Add(title);p.Children.Add(desc);return p;
    }
    private FrameworkElement TemplateControls(WidgetCarousel carousel,string category)
    {
        bool sync=false;var p=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,7,0,0)};
        var sizes=new ComboBox{MinWidth=95,MinHeight=30,Margin=new Thickness(0,0,8,0)};AutomationProperties.SetAutomationId(sizes,"widget-size-"+category);p.Children.Add(sizes);
        void Refresh(){sync=true;var host=carousel.Hosts[carousel.SelectedIndex];sizes.ItemsSource=WidgetSizes.For(host.Definition);sizes.SelectedItem=host.Definition.Size;sync=false;}
        sizes.SelectionChanged+=(_,_)=>{if(sync||sizes.SelectedItem is not WidgetSize size)return;carousel.ResizeSelected(size);var host=carousel.Hosts[carousel.SelectedIndex];_options.Sizes[host.Definition.Id]=size;RenderHost(host);UpdateVisibleHosts();};
        var source=ActionButton("数据源与详细外观…",()=>{
            var host=carousel.Hosts[carousel.SelectedIndex];var instance=WidgetRegistry.CreateInstance(host.Definition.Id,_options.Material,_options);instance.Size=host.Definition.Size;instance.Source=Source(host.Definition).Copy();
            WidgetInstanceEditor.Show(instance,copy=>{_options.Sources[copy.DefinitionId]=copy.Source.Copy();_options.CardColors[copy.DefinitionId]=copy.Appearance.Copy();_options.Sizes[copy.DefinitionId]=copy.Size;_options.Material=copy.Material;carousel.ResizeSelected(copy.Size);ApplyAppearance();RenderHost(host);Refresh();});
        },"widget-source-"+category);p.Children.Add(source);
        foreach(var host in carousel.Hosts)if(_options.Sizes.TryGetValue(host.Definition.Id,out var size))host.Resize(size);
        carousel.SelectionChanged+=_=>{Refresh();UpdateVisibleHosts();};Refresh();return p;
    }
    private void BuildAdditionalCategories(StackPanel body)
    {
        var navigator=new WrapPanel{Margin=new Thickness(0,0,0,12)};var search=new TextBox{MinWidth=180,MaxLength=100,ToolTip="搜索小组件名称",Margin=new Thickness(0,0,10,7)};AutomationProperties.SetAutomationId(search,"widget-template-search");AutomationProperties.SetName(search,"搜索模板");
        // A magnifier and a placeholder until something is typed.
        search.Padding=new Thickness(30,2,9,2);var searchHost=new Grid{Margin=search.Margin};search.Margin=new Thickness(0);searchHost.Children.Add(search);
        var glass=new System.Windows.Shapes.Path{Data=System.Windows.Media.Geometry.Parse("M6,1.2 A4.8,4.8 0 1 1 5.99,1.2 Z M9.6,9.6 L13,13"),StrokeThickness=1.5,StrokeStartLineCap=System.Windows.Media.PenLineCap.Round,StrokeEndLineCap=System.Windows.Media.PenLineCap.Round,Width=14,Height=14,Stretch=System.Windows.Media.Stretch.None,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(11,0,0,0),IsHitTestVisible=false};
        glass.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"SettingsTertiary");searchHost.Children.Add(glass);
        var hint=new TextBlock{Text="搜索模板",FontSize=13,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(31,0,0,0),IsHitTestVisible=false};hint.SetResourceReference(TextBlock.ForegroundProperty,"SettingsTertiary");searchHost.Children.Add(hint);
        search.TextChanged+=(_,_)=>hint.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
        navigator.Children.Add(searchHost);body.Children.Insert(0,navigator);
        var sections=new Dictionary<string,FrameworkElement>{{"weather",_weatherSection},{"calendar",_calendarSection}};
        foreach(var kind in new[]{WidgetKind.Clock,WidgetKind.Reminders,WidgetKind.Notes,WidgetKind.Music}){
            string key=kind.ToString().ToLowerInvariant();var carousel=new WidgetCarousel(WidgetRegistry.Definitions.Where(d=>d.Kind==kind).ToArray(),_options.Selections.GetValueOrDefault(key),_options.Exponent);
            _extraCarousels[key]=carousel;AutomationProperties.SetAutomationId(carousel,key+"-carousel");carousel.SelectionChanged+=index=>_options.Selections[key]=index;
            var (surface,section)=Section(WidgetSizes.Category(kind),key);_extraSections[key]=surface;sections[key]=surface;body.Children.Add(surface);
            section.Children.Add(TemplateHeading(carousel));section.Children.Add(carousel);section.Children.Add(TemplateControls(carousel,key));AddPlacementButton(section,key);section.Children.Add(BuildColorEditor(carousel,key));
            if(kind==WidgetKind.Reminders){section.Children.Add(ActionButton("＋ 添加提醒",()=>WidgetDataEditor.Task(_options,null,copy=>{_options.Tasks=copy.Tasks;RefreshExtras(kind);return true;}),"widget-task-add"));section.Children.Add(Note("组件预览不会改变任务；添加与编辑保留在设置草稿，保存后生效。"));
                var manage=new Expander{Header="管理待办",Margin=new Thickness(0,10,0,0)};section.Children.Add(manage);manage.Expanded+=(_,_)=>{var list=new StackPanel();foreach(var t in _options.Tasks.ToArray()){
                    var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,4)};var title=new TextBlock{Text=t.Title,Width=220,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};row.Children.Add(title);
                    row.Children.Add(ActionButton("编辑",()=>WidgetDataEditor.Task(_options,t.Id,copy=>{_options.Tasks=copy.Tasks;RefreshExtras(kind);return true;}),"widget-task-edit-"+t.Id));row.Children.Add(ActionButton("删除",()=>{_options.Tasks.RemoveAll(x=>x.Id==t.Id);list.Children.Remove(row);RefreshExtras(kind);},"widget-task-delete-"+t.Id));list.Children.Add(row);}manage.Content=list;};}
            if(kind==WidgetKind.Notes){section.Children.Add(ActionButton("＋ 新建备忘录",()=>WidgetDataEditor.Note(_options,null,copy=>{_options.Notes=copy.Notes;RefreshExtras(kind);return true;}),"widget-note-add"));
                var manage=new Expander{Header="管理应用备忘录",Margin=new Thickness(0,10,0,0)};section.Children.Add(manage);manage.Expanded+=(_,_)=>{var list=new StackPanel();foreach(var n in _options.Notes.ToArray()){
                    var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,4)};row.Children.Add(new TextBlock{Text=n.Title,Width=220,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center});
                    row.Children.Add(ActionButton("编辑",()=>WidgetDataEditor.Note(_options,n.Id,copy=>{_options.Notes=copy.Notes;RefreshExtras(kind);return true;}),"widget-note-edit-"+n.Id));row.Children.Add(ActionButton("删除",()=>{_options.Notes.RemoveAll(x=>x.Id==n.Id);list.Children.Remove(row);RefreshExtras(kind);},"widget-note-delete-"+n.Id));list.Children.Add(row);}manage.Content=list;};}
        }
        foreach(var kind in Enum.GetValues<WidgetKind>()){string key=kind.ToString().ToLowerInvariant();navigator.Children.Add(ActionButton(WidgetSizes.Category(kind),()=>sections[key].BringIntoView(),"widget-category-"+key));}
        search.TextChanged+=(_,_)=>{string query=search.Text.Trim();foreach(var kind in Enum.GetValues<WidgetKind>()){
            string key=kind.ToString().ToLowerInvariant();var carousel=Carousel(key);var matches=carousel.Hosts.Select((h,i)=>(h,i)).Where(x=>query.Length==0||x.h.Definition.Title.Contains(query,StringComparison.OrdinalIgnoreCase)||WidgetSizes.Category(kind).Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();
            sections[key].Visibility=matches.Length>0?Visibility.Visible:Visibility.Collapsed;if(query.Length>0&&matches.Length>0)carousel.Select(matches[0].i,false);}}
        ;Loaded+=(_,_)=>Dispatcher.BeginInvoke(UpdateVisibleHosts);ScrollChanged+=(_,_)=>UpdateVisibleHosts();
    }
    private void StartRuntime()
    {
        _runtime=WidgetDesktopService.Current?.Runtime;_ownsRuntime=_runtime==null;_runtime??=new(Dispatcher,()=>_options);
        _runtime.Changed+=RuntimeChanged;RefreshExtras(null);
    }
    private void StopRuntime(){if(_runtime==null)return;_runtime.Changed-=RuntimeChanged;if(_ownsRuntime)_runtime.Dispose();_runtime=null;}
    private void RuntimeChanged(WidgetKind? kind){if(kind==WidgetKind.Music&&WidgetDesktopService.Current is {} service)_options.MusicHistory=service.Runtime.History.Select(x=>x.Copy()).ToList();if(kind is WidgetKind.Weather){if(OnScreen(_weatherSection))RenderWeather();if(OnScreen(_calendarSection))RenderCalendar();}else RefreshExtras(kind);}
    private void RefreshExtras(WidgetKind? kind){foreach(var pair in _extraCarousels){if(kind!=null&&pair.Value.Hosts[0].Definition.Kind!=kind)continue;if(IsLoaded&&!OnScreen(_extraSections[pair.Key]))continue;foreach(var host in pair.Value.Hosts)RenderHost(host);}}
    private void RenderHost(WidgetHost host)
    {
        var source=Source(host.Definition);WidgetWeatherSnapshot? weather=_weather;string? error=null;
        bool weatherKind=host.Definition.Kind==WidgetKind.Weather||host.Definition.Layout==WidgetLayout.CalendarWeather;
        if(_runtime!=null&&weatherKind){var result=_runtime.Weather(source);if(_options.Sources.ContainsKey(host.Definition.Id)||weather==null)weather=result.Weather;error=result.Error;}
        var notes=host.Definition.Kind==WidgetKind.Notes?_runtime?.Folder(source.NotesFolder):null;
        host.SetWeather(weather);host.SetMaterial(_options.Material,_options.CardColors.GetValueOrDefault(host.Definition.Id));
        host.SetContent(WidgetContent.Render(host.Definition,new(_options,source,weather,_runtime?.Media,notes?.Notes,notes?.Error??(weatherKind?error:host.Definition.Kind==WidgetKind.Music?_runtime?.MediaError:null),Month:_month,SelectedDate:_selected,SelectDate:host.Definition.Kind==WidgetKind.Calendar?SelectDate:null,HistoryArt:_runtime?.HistoryArt)));
    }
    private void UpdateVisibleHosts()
    {
        if(!IsLoaded)return;foreach(var host in Hosts){bool active=host.IsVisible&&host.TransformToAncestor(this).TransformBounds(new Rect(host.RenderSize)).IntersectsWith(new Rect(RenderSize));if(host.SetActive(active)&&active)RenderHost(host);}
    }
}

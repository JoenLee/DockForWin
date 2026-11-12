using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DockForWindows.Views;

internal sealed partial class SettingsWindow
{
    private sealed record SettingsSection(Panel Parent,StackPanel Outer,string Title,string Help);
    private readonly List<SettingsSection> _settingsSections=new();
    private readonly Dictionary<string,ScrollViewer> _settingsPages=new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> _controlRoutes=new(StringComparer.Ordinal);
    private readonly Dictionary<string,double> _restoreOffsets=new(StringComparer.Ordinal);
    internal sealed record NavigationState(int Section,SettingsPageHost.NavigationHistory History,Dictionary<string,double> Offsets);
    private static readonly string[] FullKeys=Keys;
    private static readonly string[] FullTitles=LegacyTitles;
    private void InstallSettingsPages(IReadOnlyList<ScrollViewer> pages,StackPanel preview,FrameworkElement footer,int selected)
    {
        for(int i=0;i<pages.Count;i++){
            int section=i;var page=pages[i];var body=(StackPanel)page.Content;
            body.Margin=new Thickness(28,22,28,24);
            body.Children.Insert(0,new TextBlock{Text=FullTitles[i],FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,22)});
            body.Children.Insert(1,new TextBlock{Text=new[]{"底板、图标与桌面视觉","统一图标与倒影","波浪、弹跳与交互反馈","固定项目与最近使用","显示器、停靠与隐藏","登录与系统集成","应用窗口的外观偏好","入口、材质与卡片配置"}[i],FontSize=12,Foreground=(Brush)FindResource("SettingsMuted"),Margin=new Thickness(0,0,0,22)});
            var groups=_settingsSections.Where(s=>ReferenceEquals(s.Parent,body)).ToArray();
            SetSettingsSearch(i,string.Join(" ",groups.Select(g=>g.Title+" "+g.Help)));
            for(int g=1;g<groups.Length;g++){
                var group=groups[g];string key=$"settings-section-{i}-{g}";int at=body.Children.IndexOf(group.Outer);body.Children.Remove(group.Outer);
                var detailBody=new StackPanel{Margin=new Thickness(28,22,28,24)};detailBody.Children.Add(new TextBlock{Text=group.Title,FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,22)});detailBody.Children.Add(group.Outer);
                var detail=new ScrollViewer{Content=detailBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
                RegisterFullPage(key,section,group.Title,true,detail);
                var summary=group.Help.Split('。')[0];if(summary.Length>64)summary=summary[..64]+"…";
                var text=new StackPanel();text.Children.Add(new TextBlock{Text=group.Title,FontSize=14});text.Children.Add(new TextBlock{Text=summary,FontSize=11,Foreground=_ui.Secondary,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,18,0)});
                var link=SettingsButton("",(_,_)=>NavigateSettingsPage(key));link.Content=text;link.HorizontalContentAlignment=HorizontalAlignment.Stretch;link.Padding=new Thickness(16,13,30,13);link.Margin=new Thickness(0,0,0,12);
                var row=new Grid();row.Children.Add(link);row.Children.Add(new TextBlock{Text="›",FontSize=23,Foreground=_ui.Secondary,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,12),IsHitTestVisible=false});AutomationProperties.SetAutomationId(link,key);AutomationProperties.SetName(link,group.Title);body.Children.Insert(at,row);
            }
            if(i==0){var related=new StackPanel();var surface=new Border{Style=(Style)FindResource("SettingsSurface"),Padding=new Thickness(16,5,16,5),Margin=new Thickness(0,0,0,18),Child=related};body.Children.Insert(2,surface);OverviewLink(related,"图标与倒影","统一尺寸、间距和倒影效果","icons");}
            RegisterFullPage(FullKeys[i],i,FullTitles[i],i is 1 or 3 or 4 or 5 or 6,page);
        }
        preview.Margin=new Thickness(28,20,28,24);var previewPage=new ScrollViewer{Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        for(int section=0;section<8;section++)RegisterFullPage("settings-preview-"+section,section,"预览",true,previewPage);
        PageHost.Navigated+=RestoreCurrentScroll;
        Loaded+=(_,_)=>RestoreCurrentScroll();
        CompleteSettingsShell(footer,selected);
    }
    private void RegisterFullPage(string key,int section,string title,bool detail,ScrollViewer page)
    {
        _settingsPages.Add(key,page);RegisterSettingsPage(key,section,title,detail,()=>page);
        foreach(var control in WalkSettings(page)){string id=AutomationProperties.GetAutomationId(control);if(id.Length>0)_controlRoutes[id]=key;}
    }
    internal IReadOnlyCollection<string> SettingsControlIds=>_controlRoutes.Keys;
    internal bool ShowSetting(string id)
    {
        if(!_controlRoutes.TryGetValue(id,out var route))return false;
        if(route.StartsWith("settings-preview-",StringComparison.Ordinal))route="settings-preview-"+(PageHost.CurrentSection<8?PageHost.CurrentSection:0);NavigateSettingsPage(route);return true;
    }
    internal NavigationState CaptureNavigationState()=>new(PageHost.CurrentSection,PageHost.CaptureHistory(),_settingsPages.ToDictionary(p=>p.Key,p=>p.Value.VerticalOffset,StringComparer.Ordinal));
    internal void RestoreNavigationState(NavigationState? state)
    {
        if(state is null)return;
        foreach(var pair in state.Offsets)_restoreOffsets[pair.Key]=pair.Value;
        PageHost.RestoreHistory(state.History);RestoreCurrentScroll();
    }
    private void RestoreCurrentScroll()
    {
        string key=PageHost.CurrentKey;
        if(!_restoreOffsets.TryGetValue(key,out double offset)||!_settingsPages.TryGetValue(key,out var page))return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>{if(IsLoaded&&PageHost.CurrentKey==key){page.ScrollToVerticalOffset(offset);_restoreOffsets.Remove(key);}});
    }
    private static IEnumerable<DependencyObject> WalkSettings(DependencyObject root)
    {
        var seen=new HashSet<DependencyObject>();var stack=new Stack<DependencyObject>();stack.Push(root);
        while(stack.Count>0){var item=stack.Pop();if(!seen.Add(item))continue;yield return item;foreach(var child in LogicalTreeHelper.GetChildren(item))if(child is DependencyObject logical)stack.Push(logical);if(item is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)stack.Push(VisualTreeHelper.GetChild(item,i));}
    }
    private Button SettingsButton(string title,RoutedEventHandler action,bool primary=false)
    {var button=new Button{Content=title,Margin=new Thickness(6,0,0,0)};button.Click+=action;if(primary){button.Background=Palette.Brush("#00575B");button.Foreground=Brushes.White;button.BorderBrush=Brushes.Transparent;}return button;}
}

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
        string[] subtitles={"底板、材质与桌面视觉","尺寸、间距与倒影","波浪、弹跳与交互反馈","固定项目与最近使用","显示器、停靠与隐藏","登录与系统集成","设置窗口的浅色与深色","入口、材质与卡片配置"};
        for(int i=0;i<pages.Count;i++){
            int section=i;var page=pages[i];var body=(StackPanel)page.Content;
            body.Margin=new Thickness(DesignSystem.PageInset,4,DesignSystem.PageInset,28);
            // Top-level pages are named by their tab; only the pages one level down carry a title.
            if(i!=7)body.Children.Insert(0,PageHeader(i,FullTitles[i],subtitles[i]));else body.Margin=new Thickness(DesignSystem.PageInset,18,DesignSystem.PageInset,28);
            var groups=_settingsSections.Where(s=>ReferenceEquals(s.Parent,body)).ToArray();
            SetSettingsSearch(i,string.Join(" ",groups.Select(g=>g.Title+" "+g.Help)));
            if(i==0){
                // 外观与主题 is an inspector (SettingsWindow.Appearance); every detailed group becomes a page it links to.
                var details=new Dictionary<int,string>();
                for(int g=0;g<groups.Length;g++){
                    var group=groups[g];string key=$"settings-section-{i}-{g}";body.Children.Remove(group.Outer);
                    if(group.Outer.Children.Count>0&&group.Outer.Children[0] is TextBlock groupTitle)groupTitle.Visibility=Visibility.Collapsed;
                    var detailBody=PageBody();detailBody.Children.Add(PageHeader(i,group.Title,""));detailBody.Children.Add(group.Outer);
                    if(g==0){var resetButton=body.Children.OfType<Button>().FirstOrDefault();if(resetButton is not null){body.Children.Remove(resetButton);detailBody.Children.Add(resetButton);}}
                    RegisterFullPage(key,section,group.Title,true,new ScrollViewer{Content=detailBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});details[g]=key;
                }
                RegisterFullPage(FullKeys[0],0,FullTitles[0],false,(ScrollViewer)BuildAppearanceInspector(g=>details.TryGetValue(g,out var k)?k:null));
                RegisterFullPage("dock-advanced",0,"高级设置",true,BuildDockAdvanced(details,groups.Select(g=>g.Title).ToArray()));
                continue;
            }
            // A group named like its page would only repeat the title above it.
            foreach(var group in groups)if((group.Title==FullTitles[i]||group.Title=="控制中心"&&i==7)&&group.Outer.Children.Count>0&&group.Outer.Children[0] is TextBlock repeated)repeated.Visibility=Visibility.Collapsed;
            // The Dock's detail pages (sections 1–6) are already one level down: everything stays on the page.
            if(i is >=1 and <=6){RegisterFullPage(FullKeys[i],i,FullTitles[i],true,page);continue;}
            // The control center keeps its first group inline; the rest become rows of one list that open detail pages.
            StackPanel? more=null;
            StackPanel More(int at){if(more is not null)return more;more=new StackPanel{Margin=new Thickness(DesignSystem.RowInset,0,DesignSystem.RowInset,0)};var surface=GroupSurface(more);surface.Margin=new Thickness(0,0,0,26);body.Children.Insert(at,surface);body.Children.Insert(at,GroupTitle("布局与材质"));return more;}
            if(i==0)OverviewLink(More(groups.Length>0?body.Children.IndexOf(groups[0].Outer)+1:1),"图标与倒影","统一尺寸、间距和倒影效果","icons");
            for(int g=1;g<groups.Length;g++){
                var group=groups[g];string key=$"settings-section-{i}-{g}";int at=body.Children.IndexOf(group.Outer);body.Children.Remove(group.Outer);
                // On its own page the header already names the group, so the group's own title steps aside.
                if(group.Outer.Children.Count>0&&group.Outer.Children[0] is TextBlock groupTitle)groupTitle.Visibility=Visibility.Collapsed;
                var detailBody=PageBody();detailBody.Children.Add(PageHeader(i,group.Title,""));detailBody.Children.Add(group.Outer);
                var detail=new ScrollViewer{Content=detailBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
                RegisterFullPage(key,section,group.Title,true,detail);
                var summary=group.Help.Split('。')[0];if(summary.Length>48)summary=summary[..48]+"…";
                AddRow(More(at),LinkRow(group.Title,summary,key,()=>NavigateSettingsPage(key)));
            }
            RegisterFullPage(FullKeys[i],i,FullTitles[i],i is >=1 and <=6,page);
        }
        preview.Margin=new Thickness(DesignSystem.PageInset,4,DesignSystem.PageInset,28);var previewPage=new ScrollViewer{Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        for(int section=0;section<8;section++)RegisterFullPage("settings-preview-"+section,section,"预览",true,previewPage);
        PageHost.Navigated+=RestoreCurrentScroll;PageHost.Navigated+=()=>{if(PageHost.CurrentKey==FullKeys[0])SyncDockInspector();};
        Loaded+=(_,_)=>RestoreCurrentScroll();
        CompleteSettingsShell(footer,selected);
    }
    // 程序坞 › 高级设置: every detailed Dock control, one link away from the inspector.
    private ScrollViewer BuildDockAdvanced(Dictionary<int,string> details,string[] titles)
    {
        var body=PageBody();body.Children.Add(PageHeader(0,"高级设置","材质、图标、动画与位置"));
        StackPanel Group(string title){body.Children.Add(GroupTitle(title));var rows=new StackPanel{Margin=new Thickness(DesignSystem.RowInset,0,DesignSystem.RowInset,0)};var surface=GroupSurface(rows);surface.Margin=new Thickness(0,0,0,22);body.Children.Add(surface);return rows;}
        string[] hints={"主题、配色、透明度与圆角","折射、磨砂、高光与暗边","边框、阴影与运行点颜色"};
        var material=Group("材质");
        for(int g=0;g<titles.Length;g++)if(details.TryGetValue(g,out var found)){string key=found;AddRow(material,LinkRow(titles[g],g<hints.Length?hints[g]:"","advanced-material-"+g,()=>NavigateSettingsPage(key)));}
        var dock=Group("程序坞");
        foreach(var (key,title,hint) in new[]{("icons","图标与倒影","尺寸、间距与倒影"),("motion","动画与交互","波浪、弹跳与反馈"),("apps","应用与文件","固定项目、运行与最近使用"),("placement","位置与隐藏","屏幕边缘、显示器与自动隐藏")})
        {string route=key;AddRow(dock,LinkRow(title,hint,"advanced-"+key,()=>NavigateSettingsPage(route)));}

        return new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};
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
    {var button=new Button{Content=title,Margin=new Thickness(8,0,0,0),MinWidth=72};button.Click+=action;if(primary)button.Style=(Style)FindResource("SettingsPrimaryButton");return button;}
}

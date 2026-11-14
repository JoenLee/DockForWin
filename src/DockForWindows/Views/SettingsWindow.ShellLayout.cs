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
            // The Dock's categories sit in the master–detail page under their own title, without a second line.
            if(i!=7)body.Children.Insert(0,PageHeader(i,FullTitles[i],i is >=1 and <=4?"":subtitles[i]));else body.Margin=new Thickness(DesignSystem.PageInset,18,DesignSystem.PageInset,28);
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
                    RegisterAdvancedPage(key,section,group.Title,new ScrollViewer{Content=detailBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});details[g]=key;
                }
                RegisterFullPage(FullKeys[0],0,FullTitles[0],false,(ScrollViewer)BuildAppearanceInspector(g=>details.TryGetValue(g,out var k)?k:null));
                RegisterSettingsPage("dock-advanced",0,"高级设置",true,BuildAdvancedPane);
                continue;
            }
            // A group named like its page would only repeat the title above it.
            foreach(var group in groups)if((group.Title==FullTitles[i]||group.Title=="控制中心"&&i==7)&&group.Outer.Children.Count>0&&group.Outer.Children[0] is TextBlock repeated)repeated.Visibility=Visibility.Collapsed;
            // The Dock's detail pages (sections 1–6) are already one level down: everything stays on the page.
            if(i is >=1 and <=4){RegisterAdvancedPage(FullKeys[i],i,FullTitles[i],page);continue;}
            if(i is 5 or 6){RegisterFullPage(FullKeys[i],i,FullTitles[i],true,page);continue;}
            // The control center keeps its first group inline; the rest become rows of one list that open detail pages.
            StackPanel? more=null;Border? moreSurface=null;TextBlock? moreTitle=null;
            StackPanel More(int at){if(more is not null)return more;more=new StackPanel{Margin=new Thickness(DesignSystem.RowInset,0,DesignSystem.RowInset,0)};var surface=moreSurface=GroupSurface(more);surface.Margin=new Thickness(0,0,0,26);body.Children.Insert(at,surface);body.Children.Insert(at,moreTitle=GroupTitle("布局与细节"));return more;}
            // Only the control center's three detailed groups become pages; 外观, 打开方式 and 任务栏 stay on the page.
            string[] detailed={"模块与排列","大小与圆角","液态玻璃参数"};
            for(int g=1;g<groups.Length;g++){
                var group=groups[g];if(!detailed.Contains(group.Title))continue;string key=$"settings-section-{i}-{g}";int at=body.Children.IndexOf(group.Outer);body.Children.Remove(group.Outer);
                // On its own page the header already names the group, so the group's own title steps aside.
                if(group.Outer.Children.Count>0&&group.Outer.Children[0] is TextBlock groupTitle)groupTitle.Visibility=Visibility.Collapsed;
                var detailBody=PageBody();detailBody.Children.Add(PageHeader(i,group.Title,""));detailBody.Children.Add(group.Outer);
                var detail=new ScrollViewer{Content=detailBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
                RegisterFullPage(key,section,group.Title,true,detail);
                var summary=group.Help.Split('。')[0];if(summary.Length>48)summary=summary[..48]+"…";
                var link=LinkRow(group.Title,summary,key,()=>NavigateSettingsPage(key));AddRow(More(at),link);
                // The link's second line reports the current state instead of describing the page (audit P14-06).
                if(link.Content is Grid linkContent&&linkContent.Children[0] is StackPanel lines&&lines.Children.Count>1&&lines.Children[1] is TextBlock line)_liveSummaries.Add((line,group.Title));
            }
            // The list of detail pages closes the page.
            if(moreSurface is not null&&moreTitle is not null){body.Children.Remove(moreTitle);body.Children.Remove(moreSurface);body.Children.Add(moreTitle);body.Children.Add(moreSurface);moreSurface.Margin=new Thickness(0);}
            RefreshLiveSummaries();PageHost.Navigated+=RefreshLiveSummaries;
            RegisterFullPage(FullKeys[i],i,FullTitles[i],i is >=1 and <=6,page);
        }
        preview.Margin=new Thickness(DesignSystem.PageInset,4,DesignSystem.PageInset,28);var previewPage=new ScrollViewer{Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        for(int section=0;section<8;section++)RegisterFullPage("settings-preview-"+section,section,"预览",true,previewPage);
        PageHost.Navigated+=RestoreCurrentScroll;PageHost.Navigated+=()=>{if(PageHost.CurrentKey==FullKeys[0])SyncDockInspector();if(_advanced.Any(c=>c.Key==PageHost.CurrentKey))SelectAdvanced(PageHost.CurrentKey,false);};
        Loaded+=(_,_)=>RestoreCurrentScroll();
        CompleteSettingsShell(footer,selected);
    }
    private readonly List<(TextBlock Line,string Title)> _liveSummaries=new();
    private void RefreshLiveSummaries()
    {
        var options=_draft.ControlCenter;
        foreach(var (line,title) in _liveSummaries)line.Text=title switch{
            "模块与排列"=>$"显示 {options.Modules.Count(m=>m.Visible)} 个 · 共 {options.Modules.Count} 个",
            "大小与圆角"=>$"面板 {options.Geometry.PanelScale*100:0}% · 间距 {options.Geometry.Gap:0} px",
            "液态玻璃参数"=>options.MaterialMode!=Models.ControlCenterMaterialMode.LiquidGlass?"仅液态玻璃材质可用":options.OverrideMaterial?"单独设置":"跟随程序坞",
            _=>line.Text};
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

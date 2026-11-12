using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace DockForWindows.Views;

internal partial class SettingsShellWindow
{
    private readonly System.Collections.Generic.List<Action> _overviewRefresh=new();
    private void RegisterOverviewPages()
    {
        _pages.Register("dashboard",8,"概览 Dashboard",false,BuildDashboard);
        _pages.Register("widgets",9,"桌面小组件",false,()=>new WidgetGalleryPage(_draft.Widgets));
        _pages.Register("performance",10,"性能与渲染",false,BuildPerformance);
        _pages.Register("general",11,"通用设置",false,()=>{
            var (page,body)=Page("通用设置","管理应用行为、窗口外观和系统集成");var group=Group(body,"应用偏好");
            OverviewLink(group,"应用与文件","固定项目、运行与最近使用","apps");OverviewLink(group,"位置与隐藏","显示器、停靠和自动隐藏","placement");
            OverviewLink(group,"系统与启动","登录后自动启动","system");OverviewLink(group,"窗口外观","系统、浅色或深色界面","interface");return page;
        });
        _pages.Register("about",12,"关于应用",false,()=>{
            var (page,body)=Page("关于 DockForWin","Windows 原生桌面应用");var group=Group(body,"应用信息");
            group.Children.Add(Muted("版本 "+typeof(SettingsShellWindow).Assembly.GetName().Version?.ToString(3)+"\nC# · WPF · .NET 10\n设置在保存前保留为草稿。",new Thickness(0,16,0,16)));return page;
        });
        _pages.Navigated+=()=>{foreach(var refresh in _overviewRefresh)refresh();};
    }
    private FrameworkElement BuildDashboard()
    {
        var (page,body)=Page("你的桌面","让常用信息与顺手的操作，各得其所。");
        var welcome=new StackPanel();welcome.Children.Add(new TextBlock{Text="少一点打扰，多一点从容。",FontSize=21,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
        welcome.Children.Add(Muted("Dock、控制中心与小组件，在这里统一管理。",new Thickness(0,8,0,15)));
        var browse=Button("探索小组件  ›",()=>_pages.Navigate("widgets"),"dashboard-browse-widgets");browse.HorizontalAlignment=HorizontalAlignment.Left;browse.Margin=new Thickness(0);browse.Background=Palette.Brush("#176F68");browse.Foreground=Brushes.White;browse.BorderThickness=new Thickness(0);welcome.Children.Add(browse);
        var hero=new Border{CornerRadius=new CornerRadius(20),Padding=new Thickness(25,24,25,24),Margin=new Thickness(0,0,0,24),Child=welcome};hero.SetResourceReference(Border.BackgroundProperty,"SettingsHero");body.Children.Add(hero);
        var cards=new Grid();cards.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});cards.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        for(int i=0;i<3;i++)cards.RowDefinitions.Add(new(){Height=GridLength.Auto});
        void Card(int index,string title,Func<string> value,string description,string route){
            var stack=new StackPanel();stack.Children.Add(Muted(title,new Thickness(0,0,0,10)));
            var number=new TextBlock{FontSize=20,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};stack.Children.Add(number);
            stack.Children.Add(Muted(description,new Thickness(0,8,0,12)));
            var link=Button("查看设置  ›",()=>_pages.Navigate(route),"dashboard-"+index);link.HorizontalAlignment=HorizontalAlignment.Left;link.Margin=new Thickness(0);link.BorderThickness=new Thickness(0);link.Padding=new Thickness(0,4,0,4);link.SetResourceReference(Control.ForegroundProperty,"SettingsAccent");stack.Children.Add(link);
            var surface=new Border{Style=(Style)FindResource("SettingsSurface"),Padding=new Thickness(18),Margin=new Thickness(index%2==0?0:7,0,index%2==0?7:0,14),Child=stack};Grid.SetColumn(surface,index%2);Grid.SetRow(surface,index/2);cards.Children.Add(surface);
            void Refresh()=>number.Text=value();_overviewRefresh.Add(Refresh);Refresh();
        }
        Card(0,"控制中心",()=>_draft.UseGlassControlCenter?"已启用":"未启用","入口配置状态 · 保存后应用","control-center");
        Card(1,"当前材质",()=>_draft.ControlCenter.MaterialMode==Models.ControlCenterMaterialMode.ClassicBlur?"Classic Blur":"Liquid Glass","控制中心视觉模式","control-center");
        Card(2,"桌面小组件",()=>"6 类组件","天气、日历、时钟与个人信息 · 独立桌面实例","widgets");
        Card(3,"进程内存",()=>{using var process=Process.GetCurrentProcess();return $"{process.WorkingSet64/1048576d:0} MB";},"Working Set · 进入页面时的真实快照","performance");
        Card(4,"外观",()=>_draft.Theme==Models.DockTheme.Glass?"液态玻璃":"自定义桌面外观","底板、图标、倒影与窗口配色","appearance");
        Card(5,"动画",()=>_draft.Animations?"已开启":"已关闭","沿用现有波浪、弹跳与交互反馈","motion");body.Children.Add(cards);
        var shortcuts=Group(body,"常用操作");OverviewLink(shortcuts,"管理应用与文件","添加入口、运行与最近使用","apps");OverviewLink(shortcuts,"窗口与系统偏好","外观、位置及开机启动","general");
        return page;
    }
    private FrameworkElement BuildPerformance()
    {
        var (page,body)=Page("性能与渲染","只显示已经接入的真实数据；快照不会持续轮询。");var group=Group(body,"当前进程");
        var memory=Muted("",new Thickness(0,16,0,16));group.Children.Add(memory);
        void Refresh(){using var process=Process.GetCurrentProcess();memory.Text=$"Working Set  {process.WorkingSet64/1048576d:0.0} MB\nPrivate Bytes  {process.PrivateMemorySize64/1048576d:0.0} MB\n采样时间  {DateTime.Now:HH:mm:ss}";}
        _overviewRefresh.Add(Refresh);Refresh();var refresh=Button("刷新快照",Refresh,"performance-refresh");refresh.Margin=new Thickness(0,0,0,16);group.Children.Add(refresh);
        var rendering=Group(body,"渲染监测");rendering.Children.Add(Muted("CPU / GPU 占用：尚未接入\n实际呈现 FPS / P95 / P99：尚未接入\n背景更新延迟：尚未接入\nGPU 显存与系统 Committed Memory：尚未接入",new Thickness(0,16,0,16)));
        OverviewLink(rendering,"材质与画质参数","调整现有设置，不改变渲染管线","appearance");return page;
    }
    protected void OverviewLink(Panel group,string title,string subtitle,string route)=>Link(group,title,subtitle,"overview-"+route,()=>_pages.Navigate(route));
    private static Geometry NavigationGeometry(Drawing drawing)
    {
        var geometry=new GeometryGroup();
        void Add(Drawing source){if(source is DrawingGroup group)foreach(var child in group.Children)Add(child);else if(source is GeometryDrawing shape&&shape.Brush is SolidColorBrush brush&&brush.Color.A>0)geometry.Children.Add(shape.Geometry);}
        Add(drawing);geometry.Freeze();return geometry;
    }
    private void ToggleSidebar()
    {
        _collapsed=!_collapsed;_sidebarColumn.Width=new GridLength(_collapsed?68:240);
        foreach(ListBoxItem item in _navigation.Items){var row=(Grid)item.Content;row.Children[1].Visibility=_collapsed?Visibility.Collapsed:Visibility.Visible;}
        foreach(var child in _sidebar.Children.OfType<Border>())if(Grid.GetRow(child)==1)child.Visibility=_collapsed?Visibility.Collapsed:Visibility.Visible;
        var header=(Grid)_sidebar.Children[0];header.Children[0].Visibility=_collapsed?Visibility.Collapsed:Visibility.Visible;
        var toggle=(Button)header.Children[1];toggle.Content=_collapsed?"›":"‹";
        foreach(var brand in _sidebar.Children.OfType<StackPanel>())brand.Visibility=_collapsed?Visibility.Collapsed:Visibility.Visible;
        UpdateSelection(false);
    }
}

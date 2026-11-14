using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal partial class SettingsShellWindow
{
    protected readonly System.Collections.Generic.List<Action> _overviewRefresh=new();
    private static BitmapImage? _heroWallpaper;
    private void RegisterOverviewPages()
    {
        _pages.Register("dashboard",8,LocalizedText.Get("概览"),false,BuildDashboard);
        _pages.Register("widgets",9,LocalizedText.Get("桌面小组件"),false,BuildWidgetHome);
        _pages.Register("widget-gallery",9,LocalizedText.Get("小组件样式与数据"),true,()=>_widgetGallery??=new WidgetGalleryPage(_draft.Widgets));
        _pages.Register("performance",10,LocalizedText.Get("性能"),true,BuildPerformance);
        _pages.Register("general",11,LocalizedText.Get("通用设置"),false,()=>{
            // The tab names the page; preferences that are a single control live right here instead of a page each.
            var (page,body)=Page(LocalizedText.Get("通用"),"");body.Margin=new Thickness(DesignSystem.PageInset,10,DesignSystem.PageInset,28);var group=Group(body,LocalizedText.Get("偏好"));BuildGeneralPreferences(group);
            var more=Group(body,LocalizedText.Get("关于与诊断"));OverviewLink(more,LocalizedText.Get("性能"),LocalizedText.Get("当前进程的内存快照"),"performance");OverviewLink(more,LocalizedText.Get("关于 DockForWin"),LocalizedText.Get("版本 ")+typeof(SettingsShellWindow).Assembly.GetName().Version?.ToString(3),"about");return page;
        });
        _pages.Register("about",12,LocalizedText.Get("关于"),true,BuildAbout);
        _pages.Navigated+=()=>{foreach(var refresh in _overviewRefresh)refresh();};
    }
    private static string ThemeName(DockTheme theme)=>theme switch{DockTheme.Glass=>LocalizedText.Get("液态玻璃"),DockTheme.Light=>LocalizedText.Get("浅色"),DockTheme.Dark=>LocalizedText.Get("深色"),DockTheme.Custom=>LocalizedText.Get("自定义"),_=>LocalizedText.Get("跟随系统")};
    private FrameworkElement BuildDashboard()
    {
        var (page,body)=Page(LocalizedText.Get("概览"),LocalizedText.Get("程序坞、控制中心与小组件的当前状态"),8);
        body.Children.Add(DashboardHero());
        var cards=new Grid{Margin=new Thickness(0,0,0,14)};cards.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});cards.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        for(int i=0;i<3;i++)cards.RowDefinitions.Add(new(){Height=GridLength.Auto});
        void Card(int index,int tileColor,string glyph,string title,Func<string> value,string route){
            var layout=new Grid();layout.ColumnDefinitions.Add(new(){Width=GridLength.Auto});layout.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});layout.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            var tile=DesignSystem.IconTile(tileColor,glyph,32);tile.Margin=new Thickness(0,0,12,0);tile.VerticalAlignment=VerticalAlignment.Center;layout.Children.Add(tile);
            var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};text.Children.Add(Muted(title,new Thickness(0)));
            var number=new TextBlock{FontFamily=DesignSystem.DisplayFont,FontSize=16.5,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,1,0,0),TextTrimming=TextTrimming.CharacterEllipsis};text.Children.Add(number);Grid.SetColumn(text,1);layout.Children.Add(text);
            var chevron=new System.Windows.Shapes.Path{Data=Geometry.Parse("M1,1 L5.5,5.5 L1,10"),StrokeThickness=1.7,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=7,Height=11,VerticalAlignment=VerticalAlignment.Center};chevron.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"SettingsTertiary");Grid.SetColumn(chevron,2);layout.Children.Add(chevron);
            var card=new Button{Content=layout,Template=CardTemplate(),HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(index%2==0?0:6,0,index%2==0?6:0,12)};AutomationProperties.SetAutomationId(card,"dashboard-"+index);AutomationProperties.SetName(card,title);card.Click+=(_,_)=>_pages.Navigate(route);
            Grid.SetColumn(card,index%2);Grid.SetRow(card,index/2);cards.Children.Add(card);
            void Refresh()=>number.Text=value();_overviewRefresh.Add(Refresh);Refresh();
        }
        Card(0,1,"Options",LocalizedText.Get("控制中心"),()=>_draft.UseGlassControlCenter?LocalizedText.Get("已启用"):LocalizedText.Get("未启用"),"control-center");
        Card(1,3,"Palette",LocalizedText.Get("控制中心材质"),()=>_draft.ControlCenter.MaterialMode==ControlCenterMaterialMode.ClassicBlur?LocalizedText.Get("经典磨砂"):LocalizedText.Get("液态玻璃"),"control-center");
        Card(2,2,"Calendar",LocalizedText.Get("桌面小组件"),()=>LocalizedText.Get("6 类组件"),"widgets");
        Card(3,6,"Settings",LocalizedText.Get("开机自启"),()=>_draft.StartWithWindows?LocalizedText.Get("已开启"):LocalizedText.Get("未开启"),"system");
        Card(4,0,"Desktop",LocalizedText.Get("程序坞外观"),()=>ThemeName(_draft.Theme),"appearance");
        Card(5,4,"Sparkles",LocalizedText.Get("动画"),()=>_draft.Animations?LocalizedText.Get("已开启"):LocalizedText.Get("已关闭"),"motion");body.Children.Add(cards);
        var shortcuts=Group(body,LocalizedText.Get("常用操作"));OverviewLink(shortcuts,LocalizedText.Get("管理应用与文件"),LocalizedText.Get("添加应用、文件夹与最近使用"),"apps");OverviewLink(shortcuts,LocalizedText.Get("窗口与系统偏好"),LocalizedText.Get("外观、位置与开机启动"),"general");
        return page;
    }
    // A window onto the desktop: the preview wallpaper in a continuous-corner frame, a title on a soft veil,
    // and (in the full settings window) the user's own dock floating on it in its current material.
    private FrameworkElement DashboardHero()
    {
        if(_heroWallpaper is null){var image=new BitmapImage();image.BeginInit();image.UriSource=new Uri("pack://application:,,,/DockForWindows;component/Assets/PreviewWallpaper.png");image.DecodePixelWidth=1400;image.CacheOption=BitmapCacheOption.OnLoad;image.EndInit();image.Freeze();_heroWallpaper=image;}
        var scene=new Grid{Height=212,ClipToBounds=true};AutomationProperties.SetAutomationId(scene,"dashboard-hero");
        scene.Background=new ImageBrush(_heroWallpaper){Stretch=Stretch.UniformToFill,AlignmentY=AlignmentY.Top};
        var veil=new LinearGradientBrush(new GradientStopCollection{new(Color.FromArgb(120,10,12,20),0),new(Color.FromArgb(0,10,12,20),.62)},0);veil.Freeze();scene.Children.Add(new Border{Background=veil,IsHitTestVisible=false});
        var text=new StackPanel{Margin=new Thickness(26,22,26,0),VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Left,IsHitTestVisible=false,Effect=new DropShadowEffect{BlurRadius=10,ShadowDepth=0,Opacity=.28}};
        text.Children.Add(new TextBlock{Text=LocalizedText.Get("你的桌面"),FontFamily=DesignSystem.DisplayFont,FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White});
        var status=new TextBlock{FontSize=13,Margin=new Thickness(0,4,0,0),Foreground=DesignSystem.Brush(Color.FromArgb(225,255,255,255))};text.Children.Add(status);scene.Children.Add(text);
        void Refresh()=>status.Text=LocalizedText.Format($"{ThemeName(_draft.Theme)} · 控制中心{(_draft.UseGlassControlCenter?LocalizedText.Get("已启用"):LocalizedText.Get("未启用"))}");_overviewRefresh.Add(Refresh);Refresh();
        DecorateDashboardHero(scene);
        const double radius=30;
        var frame=new SquircleBorder{CornerRadius=new CornerRadius(radius),Child=scene};
        var shadow=new System.Windows.Shapes.Path{Fill=Brushes.Black,Opacity=.2,IsHitTestVisible=false,Effect=new DropShadowEffect{BlurRadius=26,ShadowDepth=8,Direction=270,Opacity=.6}};
        frame.SizeChanged+=(_,_)=>shadow.Data=SquircleBorder.Outline(frame.RenderSize,radius);
        var host=new Grid{Margin=new Thickness(0,0,0,24)};host.Children.Add(shadow);host.Children.Add(frame);return host;
    }
    // The full settings window adds the live dock sample; the bare shell keeps just the wallpaper.
    protected virtual void DecorateDashboardHero(Grid scene){}
    // 通用 › 偏好: the bare shell links to the pages; the full settings window puts the controls themselves here.
    protected virtual void BuildGeneralPreferences(Panel group){OverviewLink(group,LocalizedText.Get("系统与启动"),LocalizedText.Get("登录后自动启动"),"system");OverviewLink(group,LocalizedText.Get("窗口外观"),LocalizedText.Get("跟随系统、浅色或深色"),"interface");}
    // Status card: a raised tile that lifts a little further under the pointer.
    private static ControlTemplate CardTemplate()
    {
        var root=new FrameworkElementFactory(typeof(Grid));
        var surface=new FrameworkElementFactory(typeof(Border)){Name="Surface"};surface.SetValue(Border.CornerRadiusProperty,new CornerRadius(14));surface.SetResourceReference(Border.BackgroundProperty,"SettingsCard");surface.SetResourceReference(Border.BorderBrushProperty,"SettingsGroupEdge");surface.SetValue(Border.BorderThicknessProperty,new Thickness(1));root.AppendChild(surface);
        var state=new FrameworkElementFactory(typeof(Border)){Name="State"};state.SetValue(Border.CornerRadiusProperty,new CornerRadius(14));state.SetValue(Border.BackgroundProperty,Brushes.Transparent);state.SetValue(UIElement.IsHitTestVisibleProperty,false);root.AppendChild(state);
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.MarginProperty,new Thickness(16,14,14,14));root.AppendChild(presenter);
        var template=new ControlTemplate(typeof(Button)){VisualTree=root};
        var over=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};over.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("SettingsHover"),"State"));over.Setters.Add(new Setter(UIElement.EffectProperty,new DropShadowEffect{BlurRadius=18,ShadowDepth=4,Direction=270,Opacity=.16},"Surface"));template.Triggers.Add(over);
        var pressed=new Trigger{Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("SettingsPressed"),"State"));template.Triggers.Add(pressed);
        return template;
    }
    private FrameworkElement BuildPerformance()
    {
        var (page,body)=Page(LocalizedText.Get("性能"),LocalizedText.Get("当前进程的内存快照，打开或刷新时读取一次"),10);var group=Group(body,LocalizedText.Get("当前进程"));
        var working=new TextBlock{FontSize=13.5};var privateBytes=new TextBlock{FontSize=13.5};var sampled=new TextBlock{FontSize=13.5};
        foreach(var value in new[]{working,privateBytes,sampled}){value.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");System.Windows.Documents.Typography.SetNumeralAlignment(value,FontNumeralAlignment.Tabular);}
        AddRow(group,LabeledRow(LocalizedText.Get("工作集内存"),working));AddRow(group,LabeledRow(LocalizedText.Get("私有内存"),privateBytes));AddRow(group,LabeledRow(LocalizedText.Get("采样时间"),sampled));
        void Refresh(){using var process=Process.GetCurrentProcess();working.Text=$"{process.WorkingSet64/1048576d:0.0} MB";privateBytes.Text=$"{process.PrivateMemorySize64/1048576d:0.0} MB";sampled.Text=DateTime.Now.ToString("HH:mm:ss");}
        _overviewRefresh.Add(Refresh);Refresh();var refresh=Button(LocalizedText.Get("刷新快照"),Refresh,"performance-refresh");refresh.HorizontalAlignment=HorizontalAlignment.Left;refresh.Margin=new Thickness(0,-12,0,26);body.Children.Add(refresh);
        return page;
    }
    private FrameworkElement BuildAbout()
    {
        var body=PageBody();var page=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};AutomationProperties.SetName(page,LocalizedText.Get("关于"));
        var identity=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,26,0,30)};
        identity.Children.Add(new Image{Source=IosStyles.AppIcon(),Width=88,Height=88,Effect=new DropShadowEffect{BlurRadius=24,ShadowDepth=6,Direction=270,Opacity=.16}});
        identity.Children.Add(new TextBlock{Text="DockForWin",FontFamily=DesignSystem.DisplayFont,FontSize=24,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,16,0,4)});
        identity.Children.Add(Muted(LocalizedText.Get("版本 ")+typeof(SettingsShellWindow).Assembly.GetName().Version?.ToString(3)+LocalizedText.Get(" · 开发预览"),new Thickness(0)).With(t=>t.HorizontalAlignment=HorizontalAlignment.Center));body.Children.Add(identity);
        var group=Group(body,LocalizedText.Get("应用信息"));
        foreach(var (label,value) in new[]{(LocalizedText.Get("技术栈"),"C# · WPF · .NET 10"),(LocalizedText.Get("字体"),LocalizedText.Get("Segoe UI Variable · 微软雅黑 UI · 未来荧黑（标题）"))}){var text=new TextBlock{Text=value,FontSize=13.5};text.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");AddRow(group,LabeledRow(label,text));}
        return page;
    }
    protected void OverviewLink(Panel group,string title,string subtitle,string route)=>Link(group,title,subtitle,"overview-"+route,()=>_pages.Navigate(route));
    private static Geometry NavigationGeometry(Drawing drawing)
    {
        var geometry=new GeometryGroup();
        void Add(Drawing source){if(source is DrawingGroup group)foreach(var child in group.Children)Add(child);else if(source is GeometryDrawing shape&&shape.Brush is SolidColorBrush brush&&brush.Color.A>0)geometry.Children.Add(shape.Geometry);}
        Add(drawing);geometry.Freeze();return geometry;
    }
    // Folded, the sidebar keeps the window controls and the category tiles.
    private void ToggleSidebar()
    {
        _collapsed=!_collapsed;_sidebarColumn.Width=new GridLength(_collapsed?84:204);
        foreach(var (_,text) in _tabs)text.Visibility=_collapsed?Visibility.Collapsed:Visibility.Visible;
        if(_sidebarToggle is not null){_sidebarToggle.ToolTip=_collapsed?LocalizedText.Get("展开侧边栏"):LocalizedText.Get("收起侧边栏");AutomationProperties.SetName(_sidebarToggle,(string)_sidebarToggle.ToolTip);}
    }
}

internal static class ElementExtensions
{
    public static T With<T>(this T element,Action<T> configure) where T:DependencyObject{configure(element);return element;}
}

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal partial class SettingsShellWindow
{
    private FrameworkElement BuildPage(int section)
    {
        var (page,body)=Page(LegacyTitles[section],new[]{"程序坞底板、透明度与边缘","图标大小、间距与倒影","波浪、弹跳与交互反馈","固定项目与运行中的应用","停靠位置与自动隐藏","登录与系统集成","应用窗口的主题与配色","控制中心入口与面板设置"}[section]);
        switch(section){
            case 0:
                var hero=new Grid{Margin=new Thickness(0,0,0,22)};hero.ColumnDefinitions.Add(new(){Width=new GridLength(120)});hero.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
                var picture=new Image{Source=new BitmapImage(new Uri("pack://application:,,,/DockForWindows;component/Assets/PreviewWallpaper.png")),Stretch=Stretch.UniformToFill,Width=104,Height=72};picture.Clip=new RectangleGeometry(new Rect(0,0,104,72),8,8);hero.Children.Add(picture);
                var intro=new StackPanel{VerticalAlignment=VerticalAlignment.Center};intro.Children.Add(new TextBlock{Text="程序坞外观",FontSize=17,FontWeight=FontWeights.SemiBold});intro.Children.Add(Muted("调整主题、透明度与边缘",new Thickness(0,6,0,0)));Grid.SetColumn(intro,1);hero.Children.Add(intro);body.Children.Add(hero);
                var appearance=Group(body,"显示外观");Segment(appearance,"底板主题","shell-dock-theme",new[]{"系统","浅色","深色","自定义","玻璃"},(int)_draft.Theme,i=>_draft.Theme=(DockTheme)i);
                Range(appearance,"底板透明度","shell-transparency",0,100,(1-_draft.Appearance.Opacity)*100,"0'%'",v=>_draft.Appearance.Opacity=1-v/100);
                Link(appearance,"材质与边缘","折射、磨砂、圆角和阴影","shell-material-link",()=>_pages.Navigate("material"));
                var previews=Group(body,"预览与应用");Link(previews,"桌面与控制中心预览","查看全部设置并检查修改","shell-original-appearance",ReviewCurrent);break;
            case 1:
                var icons=Group(body,"图标");Range(icons,"图标尺寸","shell-icon-size",32,72,_draft.IconSize,"0' DIP'",v=>_draft.IconSize=v);Range(icons,"图标间距","shell-icon-spacing",2,18,_draft.Spacing,"0' DIP'",v=>_draft.Spacing=v);
                var reflection=Group(body,"倒影");var toggle=Switch(reflection,"显示倒影","shell-reflections",_draft.Reflections,v=>_draft.Reflections=v);_availability.Add(()=>toggle.IsEnabled=AppearancePolicy.ReflectionsAllowed(_draft));
                body.Children.Add(Muted("底板完全透明时可以使用倒影。",new Thickness(10,6,0,18)));More(body);break;
            case 2:
                var motion=Group(body,"动画");Switch(motion,"启用动画","shell-animations",_draft.Animations,v=>_draft.Animations=v);Link(motion,"动画参数","波浪倍率、上浮高度与弹跳","shell-motion-link",()=>_pages.Navigate("motion-parameters"));
                body.Children.Add(Muted("系统关闭动画时，界面会减少动态效果。",new Thickness(10,6,0,18)));More(body);break;
            case 3:
                var apps=Group(body,"运行与最近使用");Switch(apps,"显示其他运行应用","shell-running-apps",_draft.ShowRunningApps,v=>_draft.ShowRunningApps=v);Switch(apps,"显示最近应用","shell-recent-apps",_draft.ShowRecentApps,v=>_draft.ShowRecentApps=v);Range(apps,"最近应用数量","shell-recent-limit",0,8,_draft.RecentLimit,"0",v=>_draft.RecentLimit=(int)Math.Round(v));
                var pins=Group(body,"固定项目");Link(pins,"管理应用与文件","添加应用、文件夹和固定项目","shell-app-library",ReviewCurrent);break;
            case 4:
                var position=Group(body,"停靠位置");Segment(position,"屏幕边缘","shell-dock-edge",new[]{"底部","左侧","右侧"},(int)_draft.Edge,i=>_draft.Edge=(DockEdge)i);Range(position,"距离屏幕边缘","shell-edge-distance",0,160,_draft.EdgeDistance,"0' DIP'",v=>_draft.EdgeDistance=v);
                var hide=Group(body,"自动隐藏");Switch(hide,"普通窗口下自动隐藏","shell-auto-hide",_draft.AutoHide,v=>_draft.AutoHide=v);Switch(hide,"桌面上也自动隐藏","shell-desktop-hide",_draft.HideOnDesktop,v=>_draft.HideOnDesktop=v);Switch(hide,"最大化或全屏时退下","shell-fullscreen-hide",_draft.HideWhenFullscreen,v=>_draft.HideWhenFullscreen=v);More(body);break;
            case 5:
                var system=Group(body,"登录");Switch(system,"开机自启","shell-startup",_draft.StartWithWindows,v=>_draft.StartWithWindows=v);body.Children.Add(Muted("保存设置后，在当前账户登录时启动。",new Thickness(10,6,0,18)));More(body);break;
            case 6:
                var theme=Group(body,"应用外观");Segment(theme,"界面外观","shell-interface-theme",new[]{"系统","浅色","深色"},(int)_draft.InterfaceTheme,i=>_draft.InterfaceTheme=(InterfaceTheme)i);Link(theme,"界面配色","查看窗口外观与完整设置","shell-interface-colors",ReviewCurrent);
                body.Children.Add(Muted("界面配色与程序坞底板材质分别保存。",new Thickness(10,6,0,18)));break;
            case 7:
                var center=Group(body,"控制中心");Switch(center,"启用控制中心","shell-control-center",_draft.UseGlassControlCenter,v=>_draft.UseGlassControlCenter=v);Link(center,"面板尺寸与间距","调整整体缩放与组件间距","shell-panel-link",()=>_pages.Navigate("panel-proportions"));Link(center,"入口、模块与完整设置","快捷键、模块、图标及布局","shell-control-full",ReviewCurrent);break;
        }
        return page;
    }
    private FrameworkElement BuildMaterial()
    {
        var (page,body)=Page("材质与边缘","调节程序坞的轮廓与玻璃材质");
        var outline=Group(body,"轮廓");Range(outline,"圆润程度","shell-corner-radius",0,100,_draft.Appearance.CornerRadius,"0.0'%'",v=>_draft.Appearance.CornerRadius=v);
        var glass=Group(body,"液态玻璃");Range(glass,"折射强度","shell-glass-intensity",0,100,_draft.GlassIntensity*100,"0'%'",v=>_draft.GlassIntensity=v/100);Range(glass,"高斯磨砂","shell-glass-frost",0,100,_draft.GlassFrost*100,"0'%'",v=>_draft.GlassFrost=v/100);Range(glass,"边缘扭曲度","shell-glass-distortion",0,100,_draft.GlassDistortion*100,"0'%'",v=>_draft.GlassDistortion=v/100);
        _availability.Add(()=>glass.IsEnabled=_draft.Theme==DockTheme.Glass);
        var shadow=Group(body,"阴影");Switch(shadow,"显示悬浮阴影","shell-shadow",_draft.Appearance.ShadowEnabled,v=>_draft.Appearance.ShadowEnabled=v);Range(shadow,"阴影强度","shell-shadow-opacity",0,65,_draft.Appearance.ShadowOpacity*100,"0'%'",v=>_draft.Appearance.ShadowOpacity=v/100);More(body);return page;
    }
    private FrameworkElement BuildMotionParameters()
    {
        var (page,body)=Page("动画参数","保留当前的运动风格与交互反馈");var motion=Group(body,"波浪与弹跳");
        Range(motion,"波浪放大倍率","shell-magnification",1,1.8,_draft.Magnification,"0.00' 倍'",v=>_draft.Magnification=v);
        Range(motion,"波浪上浮高度","shell-hover-lift",0,64,_draft.HoverLift,"0' DIP'",v=>_draft.HoverLift=v);
        Range(motion,"弹跳高度","shell-bounce-height",8,64,_draft.BounceHeight,"0' DIP'",v=>_draft.BounceHeight=v);
        Range(motion,"弹跳时长","shell-bounce-duration",350,1800,_draft.BounceDuration,"0' ms'",v=>_draft.BounceDuration=v);
        _availability.Add(()=>motion.IsEnabled=_draft.Animations&&SystemParameters.ClientAreaAnimation);return page;
    }
    private FrameworkElement BuildPanelProportions()
    {
        var (page,body)=Page("面板尺寸与间距","调整控制中心的整体比例");var group=Group(body,"大小与间距");var geometry=_draft.ControlCenter.Geometry;
        Range(group,"整体面板缩放","shell-panel-scale",65,150,geometry.PanelScale*100,"0'%'",v=>geometry.PanelScale=v/100);Range(group,"组件等比例缩放","shell-component-scale",75,150,geometry.ComponentScale*100,"0'%'",v=>geometry.ComponentScale=v/100);Range(group,"组件间距","shell-component-gap",6,24,geometry.Gap,"0' DIP'",v=>geometry.Gap=v);More(body);return page;
    }
    private static (ScrollViewer Page,StackPanel Body) Page(string title,string description)
    {
        var body=new StackPanel{Margin=new Thickness(28,23,28,24)};body.Children.Add(new TextBlock{Text=title,FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,7)});body.Children.Add(Muted(description,new Thickness(0,0,0,24)));
        var page=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};AutomationProperties.SetName(page,title);return (page,body);
    }
    private static TextBlock Muted(string text,Thickness margin){var label=new TextBlock{Text=text,FontSize=12,TextWrapping=TextWrapping.Wrap,LineHeight=19,Margin=margin};label.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");return label;}
    private static StackPanel Group(Panel body,string title)
    {
        body.Children.Add(new TextBlock{Text=title,FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=Palette.Brush("#77777E"),Margin=new Thickness(10,0,0,7)});
        var content=new StackPanel{Margin=new Thickness(16,1,16,1)};var surface=new Border{Child=content,Margin=new Thickness(0,0,0,22)};surface.SetResourceReference(FrameworkElement.StyleProperty,"SettingsSurface");body.Children.Add(surface);return content;
    }
    private static void AddRow(Panel group,UIElement child)
    {if(group.Children.Count>0)group.Children.Add(new Border{Height=.7,Background=Palette.Brush("#EAEAED")});group.Children.Add(child);}
    private CheckBox Switch(Panel group,string title,string id,bool value,Action<bool> write)
    {
        var box=new CheckBox{Content=title,IsChecked=value};AutomationProperties.SetAutomationId(box,id);AutomationProperties.SetName(box,title);box.Checked+=(_,_)=>{write(true);RefreshPageAvailability();};box.Unchecked+=(_,_)=>{write(false);RefreshPageAvailability();};AddRow(group,box);return box;
    }
    private void Segment(Panel group,string title,string id,string[] options,int value,Action<int> write)
    {
        var row=new Grid{Margin=new Thickness(0,10,0,10)};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1.5,GridUnitType.Star)});row.Children.Add(new TextBlock{Text=title,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,16,0)});
        var control=new SettingsSegmentedControl{ItemsSource=options,SelectedIndex=Math.Clamp(value,0,options.Length-1),FontSize=12};AutomationProperties.SetAutomationId(control,id);AutomationProperties.SetName(control,title);control.SelectionChanged+=(_,_)=>{if(control.SelectedIndex>=0){write(control.SelectedIndex);RefreshPageAvailability();}};Grid.SetColumn(control,1);row.Children.Add(control);AddRow(group,row);
    }
    private static void Range(Panel group,string title,string id,double minimum,double maximum,double value,string format,Action<double> write)
    {
        var row=new Grid{Margin=new Thickness(0,8,0,8)};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1.25,GridUnitType.Star)});row.Children.Add(new TextBlock{Text=title,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,16,0)});
        var field=new Grid();field.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});field.ColumnDefinitions.Add(new(){Width=new GridLength(64)});var slider=new Slider{Minimum=minimum,Maximum=maximum,Value=value,VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetAutomationId(slider,id);AutomationProperties.SetName(slider,title);
        var number=new TextBlock{Text=value.ToString(format),TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center,FontSize=12,Foreground=Palette.Brush("#77777E"),Margin=new Thickness(10,0,0,0)};slider.ValueChanged+=(_,_)=>{number.Text=slider.Value.ToString(format);write(slider.Value);};field.Children.Add(slider);Grid.SetColumn(number,1);field.Children.Add(number);Grid.SetColumn(field,1);row.Children.Add(field);AddRow(group,row);
    }
    private static void Link(Panel group,string title,string subtitle,string id,Action action)
    {
        var text=new StackPanel{Margin=new Thickness(0,3,0,3)};text.Children.Add(new TextBlock{Text=title,FontSize=13});text.Children.Add(Muted(subtitle,new Thickness(0,3,18,0)));
        var content=new Grid();content.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});content.ColumnDefinitions.Add(new(){Width=GridLength.Auto});content.Children.Add(text);var arrow=new TextBlock{Text="›",FontSize=22,Foreground=Palette.Brush("#A0A0A6"),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(arrow,1);content.Children.Add(arrow);
        var button=Button("",action,id);button.Content=content;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Template=LinkTemplate();button.Padding=new Thickness(0,8,0,8);button.Margin=new Thickness();button.BorderThickness=new Thickness(0);AddRow(group,button);
    }
    private static ControlTemplate LinkTemplate()
    {
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetBinding(FrameworkElement.MarginProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});border.AppendChild(presenter);return new ControlTemplate(typeof(Button)){VisualTree=border};
    }
    private void More(Panel body){var link=Button("查看全部设置…",ReviewCurrent,"shell-all-settings-"+body.Children.Count);link.HorizontalAlignment=HorizontalAlignment.Left;link.Margin=new Thickness(0,0,0,8);body.Children.Add(link);}
    private void ReviewCurrent(){var copy=_draft.Copy();copy.Normalize();int section=_pages.CurrentSection;Close();_review(copy,section);}
    private void RefreshPageAvailability(){foreach(var refresh in _availability)refresh();}
}

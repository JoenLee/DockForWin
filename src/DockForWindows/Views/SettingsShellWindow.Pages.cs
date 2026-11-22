using System;
using System.Linq;
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
        var (page,body)=Page(LegacyTitles[section],new[]{LocalizedText.Get("程序坞底板、透明度与边缘"),LocalizedText.Get("图标大小、间距与倒影"),LocalizedText.Get("波浪、弹跳与交互反馈"),LocalizedText.Get("固定项目与运行中的应用"),LocalizedText.Get("停靠位置与自动隐藏"),LocalizedText.Get("登录与系统集成"),LocalizedText.Get("应用窗口的主题与配色"),LocalizedText.Get("控制中心入口与面板设置")}[section]);
        switch(section){
            case 0:
                var hero=new Grid{Margin=new Thickness(0,0,0,22)};hero.ColumnDefinitions.Add(new(){Width=new GridLength(120)});hero.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
                var picture=new Image{Source=new BitmapImage(new Uri("pack://application:,,,/DockForWindows;component/Assets/PreviewWallpaper.png")),Stretch=Stretch.UniformToFill,Width=104,Height=72};picture.Clip=new RectangleGeometry(new Rect(0,0,104,72),8,8);hero.Children.Add(picture);
                var intro=new StackPanel{VerticalAlignment=VerticalAlignment.Center};intro.Children.Add(new TextBlock{Text=LocalizedText.Get("程序坞外观"),FontSize=17,FontWeight=FontWeights.SemiBold});intro.Children.Add(Muted(LocalizedText.Get("调整主题、透明度与边缘"),new Thickness(0,6,0,0)));Grid.SetColumn(intro,1);hero.Children.Add(intro);body.Children.Add(hero);
                var appearance=Group(body,LocalizedText.Get("显示外观"));Segment(appearance,LocalizedText.Get("底板主题"),"shell-dock-theme",new[]{LocalizedText.Get("系统"),LocalizedText.Get("浅色"),LocalizedText.Get("深色"),LocalizedText.Get("自定义"),LocalizedText.Get("玻璃")},(int)_draft.Theme,i=>_draft.Theme=(DockTheme)i);
                Range(appearance,LocalizedText.Get("底板透明度"),"shell-transparency",0,100,(1-_draft.Appearance.Opacity)*100,"0'%'",v=>_draft.Appearance.Opacity=1-v/100);
                Link(appearance,LocalizedText.Get("材质与边缘"),LocalizedText.Get("折射、磨砂、圆角和阴影"),"shell-material-link",()=>_pages.Navigate("material"));
                var previews=Group(body,LocalizedText.Get("预览与应用"));Link(previews,LocalizedText.Get("桌面与控制中心预览"),LocalizedText.Get("查看全部设置并检查修改"),"shell-original-appearance",ReviewCurrent);break;
            case 1:
                var icons=Group(body,LocalizedText.Get("图标"));Range(icons,LocalizedText.Get("图标尺寸"),"shell-icon-size",32,72,_draft.IconSize,"0' DIP'",v=>_draft.IconSize=v);Range(icons,LocalizedText.Get("图标间距"),"shell-icon-spacing",2,18,_draft.Spacing,"0' DIP'",v=>_draft.Spacing=v);
                var reflection=Group(body,LocalizedText.Get("倒影"));var toggle=Switch(reflection,LocalizedText.Get("显示倒影"),"shell-reflections",_draft.Reflections,v=>_draft.Reflections=v);_availability.Add(()=>toggle.IsEnabled=AppearancePolicy.ReflectionsAllowed(_draft));
                body.Children.Add(Muted(LocalizedText.Get("底板完全透明时可以使用倒影。"),new Thickness(10,6,0,18)));More(body);break;
            case 2:
                var motion=Group(body,LocalizedText.Get("动画"));Switch(motion,LocalizedText.Get("启用动画"),"shell-animations",_draft.Animations,v=>_draft.Animations=v);Link(motion,LocalizedText.Get("动画参数"),LocalizedText.Get("波浪倍率、上浮高度与弹跳"),"shell-motion-link",()=>_pages.Navigate("motion-parameters"));
                body.Children.Add(Muted(LocalizedText.Get("系统关闭动画时，界面会减少动态效果。"),new Thickness(10,6,0,18)));More(body);break;
            case 3:
                var apps=Group(body,LocalizedText.Get("运行与最近使用"));Switch(apps,LocalizedText.Get("显示其他运行应用"),"shell-running-apps",_draft.ShowRunningApps,v=>_draft.ShowRunningApps=v);Switch(apps,LocalizedText.Get("显示最近应用"),"shell-recent-apps",_draft.ShowRecentApps,v=>_draft.ShowRecentApps=v);Range(apps,LocalizedText.Get("最近应用数量"),"shell-recent-limit",0,8,_draft.RecentLimit,"0",v=>_draft.RecentLimit=(int)Math.Round(v));
                var pins=Group(body,LocalizedText.Get("固定项目"));Link(pins,LocalizedText.Get("管理应用与文件"),LocalizedText.Get("添加应用、文件夹和固定项目"),"shell-app-library",ReviewCurrent);break;
            case 4:
                var position=Group(body,LocalizedText.Get("停靠位置"));Segment(position,LocalizedText.Get("屏幕边缘"),"shell-dock-edge",new[]{LocalizedText.Get("底部"),LocalizedText.Get("左侧"),LocalizedText.Get("右侧")},(int)_draft.Edge,i=>_draft.Edge=(DockEdge)i);Range(position,LocalizedText.Get("距离屏幕边缘"),"shell-edge-distance",0,160,_draft.EdgeDistance,"0' DIP'",v=>_draft.EdgeDistance=v);
                var hide=Group(body,LocalizedText.Get("自动隐藏"));Switch(hide,LocalizedText.Get("普通窗口下自动隐藏"),"shell-auto-hide",_draft.AutoHide,v=>_draft.AutoHide=v);Switch(hide,LocalizedText.Get("桌面上也自动隐藏"),"shell-desktop-hide",_draft.HideOnDesktop,v=>_draft.HideOnDesktop=v);Switch(hide,LocalizedText.Get("最大化或全屏时退下"),"shell-fullscreen-hide",_draft.HideWhenFullscreen,v=>_draft.HideWhenFullscreen=v);More(body);break;
            case 5:
                var system=Group(body,LocalizedText.Get("登录"));Switch(system,LocalizedText.Get("开机自启"),"shell-startup",_draft.StartWithWindows,v=>_draft.StartWithWindows=v);body.Children.Add(Muted(LocalizedText.Get("保存设置后，在当前账户登录时启动。"),new Thickness(10,6,0,18)));More(body);break;
            case 6:
                var theme=Group(body,LocalizedText.Get("应用外观"));Segment(theme,LocalizedText.Get("界面外观"),"shell-interface-theme",new[]{LocalizedText.Get("系统"),LocalizedText.Get("浅色"),LocalizedText.Get("深色")},(int)_draft.InterfaceTheme,i=>_draft.InterfaceTheme=(InterfaceTheme)i);Link(theme,LocalizedText.Get("界面配色"),LocalizedText.Get("查看窗口外观与完整设置"),"shell-interface-colors",ReviewCurrent);
                body.Children.Add(Muted(LocalizedText.Get("界面配色与程序坞底板材质分别保存。"),new Thickness(10,6,0,18)));break;
            case 7:
                var center=Group(body,LocalizedText.Get("控制中心"));Switch(center,LocalizedText.Get("启用控制中心"),"shell-control-center",_draft.UseGlassControlCenter,v=>_draft.UseGlassControlCenter=v);Link(center,LocalizedText.Get("面板尺寸与间距"),LocalizedText.Get("调整整体缩放与组件间距"),"shell-panel-link",()=>_pages.Navigate("panel-proportions"));Link(center,LocalizedText.Get("入口、模块与完整设置"),LocalizedText.Get("快捷键、模块、图标及布局"),"shell-control-full",ReviewCurrent);break;
        }
        return page;
    }
    private FrameworkElement BuildMaterial()
    {
        var (page,body)=Page(LocalizedText.Get("材质与边缘"),LocalizedText.Get("调节程序坞的轮廓与玻璃材质"));
        var outline=Group(body,LocalizedText.Get("轮廓"));Range(outline,LocalizedText.Get("圆润程度"),"shell-corner-radius",0,100,_draft.Appearance.CornerRadius,"0'%'",v=>_draft.Appearance.CornerRadius=v);
        var glass=Group(body,LocalizedText.Get("液态玻璃"));Range(glass,LocalizedText.Get("折射强度"),"shell-glass-intensity",0,100,_draft.GlassIntensity*100,"0'%'",v=>_draft.GlassIntensity=v/100);Range(glass,LocalizedText.Get("高斯磨砂"),"shell-glass-frost",0,100,_draft.GlassFrost*100,"0'%'",v=>_draft.GlassFrost=v/100);Range(glass,LocalizedText.Get("边缘扭曲度"),"shell-glass-distortion",0,100,_draft.GlassDistortion*100,"0'%'",v=>_draft.GlassDistortion=v/100);
        _availability.Add(()=>glass.IsEnabled=_draft.Theme==DockTheme.Glass);
        var shadow=Group(body,LocalizedText.Get("阴影"));Switch(shadow,LocalizedText.Get("显示悬浮阴影"),"shell-shadow",_draft.Appearance.ShadowEnabled,v=>_draft.Appearance.ShadowEnabled=v);Range(shadow,LocalizedText.Get("阴影强度"),"shell-shadow-opacity",0,65,_draft.Appearance.ShadowOpacity*100,"0'%'",v=>_draft.Appearance.ShadowOpacity=v/100);More(body);return page;
    }
    private FrameworkElement BuildMotionParameters()
    {
        var (page,body)=Page(LocalizedText.Get("动画参数"),LocalizedText.Get("保留当前的运动风格与交互反馈"));var motion=Group(body,LocalizedText.Get("波浪与弹跳"));
        Range(motion,LocalizedText.Get("波浪放大倍率"),"shell-magnification",1,1.8,_draft.Magnification,LocalizedText.Get("0.00' 倍'"),v=>_draft.Magnification=v);
        Range(motion,LocalizedText.Get("波浪上浮高度"),"shell-hover-lift",0,64,_draft.HoverLift,"0' DIP'",v=>_draft.HoverLift=v);
        Range(motion,LocalizedText.Get("弹跳高度"),"shell-bounce-height",8,64,_draft.BounceHeight,"0' DIP'",v=>_draft.BounceHeight=v);
        Range(motion,LocalizedText.Get("弹跳时长"),"shell-bounce-duration",350,1800,_draft.BounceDuration,"0' ms'",v=>_draft.BounceDuration=v);
        _availability.Add(()=>motion.IsEnabled=_draft.Animations&&SystemParameters.ClientAreaAnimation);return page;
    }
    private FrameworkElement BuildPanelProportions()
    {
        var (page,body)=Page(LocalizedText.Get("面板尺寸与间距"),LocalizedText.Get("调整控制中心的整体比例"));var group=Group(body,LocalizedText.Get("大小与间距"));var geometry=_draft.ControlCenter.Geometry;
        Range(group,LocalizedText.Get("整体面板缩放"),"shell-panel-scale",65,150,geometry.PanelScale*100,"0'%'",v=>geometry.PanelScale=v/100);Range(group,LocalizedText.Get("组件等比例缩放"),"shell-component-scale",75,150,geometry.ComponentScale*100,"0'%'",v=>geometry.ComponentScale=v/100);Range(group,LocalizedText.Get("组件间距"),"shell-component-gap",6,24,geometry.Gap,"0' DIP'",v=>geometry.Gap=v);More(body);return page;
    }
    // Page anatomy shared by every settings page: a centred column, a header with the category tile, then
    // inset groups. The toolbar already names the page, so the header carries the tile and a one-line purpose.
    private static (ScrollViewer Page,StackPanel Body) Page(string title,string description,int section=-1)
    {
        var body=PageBody();if(section>=0)body.Children.Add(PageHeader(section,title,description));
        var page=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};AutomationProperties.SetName(page,title);return (page,body);
    }
    // Every page starts on the same line as the tabs and the back button; overlay scroll bars keep it from shifting.
    protected static StackPanel PageBody()=>new(){Margin=new Thickness(DesignSystem.PageInset,4,DesignSystem.PageInset,28)};
    protected static FrameworkElement PageHeader(int section,string title,string description)
    {
        // Flat header: just the title and a quiet line of purpose, no icon tile.
        var header=new Grid{Margin=new Thickness(0,8,0,string.IsNullOrWhiteSpace(description)?12:14)};header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};text.Children.Add(new TextBlock{Text=title,FontFamily=DesignSystem.DisplayFont,FontSize=DesignSystem.Title,FontWeight=FontWeights.SemiBold});
        if(!string.IsNullOrWhiteSpace(description))text.Children.Add(Muted(description,new Thickness(0,3,0,0)));Grid.SetColumn(text,1);header.Children.Add(text);return header;
    }
    protected static TextBlock Muted(string text,Thickness margin){var label=new TextBlock{Text=text,FontSize=12.5,TextWrapping=TextWrapping.Wrap,LineHeight=19,Margin=margin,MaxWidth=640,HorizontalAlignment=HorizontalAlignment.Left};label.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");return label;}
    // SemiBold resolves to Glow Sans' Medium face for Chinese and Segoe's Semibold for Latin.
    protected static TextBlock GroupTitle(string title){var label=new TextBlock{Text=title,FontSize=13,FontWeight=FontWeights.SemiBold,Margin=new Thickness(DesignSystem.RowInset,0,0,8)};label.SetResourceReference(TextBlock.ForegroundProperty,"SettingsInk");return label;}
    protected static Border GroupSurface(UIElement content){var surface=new Border{Child=content};surface.SetResourceReference(FrameworkElement.StyleProperty,"SettingsSurface");return surface;}
    private static StackPanel Group(Panel body,string title)
    {
        body.Children.Add(GroupTitle(title));
        var content=new StackPanel{Margin=new Thickness(DesignSystem.RowInset,0,DesignSystem.RowInset,0)};var surface=GroupSurface(content);surface.Margin=new Thickness(0,0,0,18);body.Children.Add(surface);return content;
    }
    protected static Border RowSeparator(){var line=new Border{Height=1,SnapsToDevicePixels=true};line.SetResourceReference(Border.BackgroundProperty,"SettingsLine");return line;}
    protected static void AddRow(Panel group,UIElement child)
    {if(group.Children.Count>0)group.Children.Add(RowSeparator());group.Children.Add(child);}
    private CheckBox Switch(Panel group,string title,string id,bool value,Action<bool> write)
    {
        var box=new CheckBox{Content=title,IsChecked=value};AutomationProperties.SetAutomationId(box,id);AutomationProperties.SetName(box,title);box.Checked+=(_,_)=>{write(true);RefreshPageAvailability();};box.Unchecked+=(_,_)=>{write(false);RefreshPageAvailability();};AddRow(group,box);return box;
    }
    // A labelled row: text on the left, the control on the right, 46 DIP tall like a macOS list row.
    protected static Grid LabeledRow(string title,UIElement control,double controlWidth=double.NaN)
    {
        var row=new Grid{MinHeight=DesignSystem.RowHeight};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=double.IsNaN(controlWidth)?GridLength.Auto:new GridLength(controlWidth)});
        row.Children.Add(new TextBlock{Text=title,FontSize=DesignSystem.Body,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,16,10)});
        if(control is FrameworkElement element)element.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(control,1);row.Children.Add(control);return row;
    }
    // Segmented control sized by its labels so short choices never stretch across the row.
    protected static SettingsSegmentedControl Segmented(string[] options,int selected)=>new(){ItemsSource=options,SelectedIndex=Math.Clamp(selected,0,Math.Max(0,options.Length-1)),FontSize=12.5,Width=Math.Max(150,Math.Min(360,options.Length*Math.Max(50,options.Max(o=>o.Length)*(UiText.IsEnglish?6.5:13)+20))),HorizontalAlignment=HorizontalAlignment.Right};
    // Slider with a right-aligned value readout, laid out as one control for LabeledRow.
    protected static (Grid Field,Slider Slider,TextBlock Value) SliderField(double minimum,double maximum,double value)
    {
        var field=new Grid{Width=300};field.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});field.ColumnDefinitions.Add(new(){Width=new GridLength(60)});
        var slider=new Slider{Minimum=minimum,Maximum=maximum,Value=value,VerticalAlignment=VerticalAlignment.Center};
        var number=new TextBlock{TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center,FontSize=12.5,Margin=new Thickness(10,0,0,0)};System.Windows.Documents.Typography.SetNumeralAlignment(number,FontNumeralAlignment.Tabular);number.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");
        field.Children.Add(slider);Grid.SetColumn(number,1);field.Children.Add(number);return (field,slider,number);
    }
    private void Segment(Panel group,string title,string id,string[] options,int value,Action<int> write)
    {
        var control=Segmented(options,value);AutomationProperties.SetAutomationId(control,id);AutomationProperties.SetName(control,title);control.SelectionChanged+=(_,_)=>{if(control.SelectedIndex>=0){write(control.SelectedIndex);RefreshPageAvailability();}};AddRow(group,LabeledRow(title,control));
    }
    private static void Range(Panel group,string title,string id,double minimum,double maximum,double value,string format,Action<double> write)
    {
        var (field,slider,number)=SliderField(minimum,maximum,value);AutomationProperties.SetAutomationId(slider,id);AutomationProperties.SetName(slider,title);
        number.Text=value.ToString(format);slider.ValueChanged+=(_,_)=>{number.Text=slider.Value.ToString(format);write(slider.Value);};AddRow(group,LabeledRow(title,field));
    }
    private static void Link(Panel group,string title,string subtitle,string id,Action action)=>AddRow(group,LinkRow(title,subtitle,id,action));
    // Navigation row: title, optional one-line summary and a chevron; the whole row highlights on hover.
    protected static Button LinkRow(string title,string subtitle,string id,Action action)
    {
        var text=new StackPanel{Margin=new Thickness(0,9,0,9),VerticalAlignment=VerticalAlignment.Center};text.Children.Add(new TextBlock{Text=title,FontSize=DesignSystem.Body});if(!string.IsNullOrWhiteSpace(subtitle))text.Children.Add(Muted(subtitle,new Thickness(0,2,18,0)));
        var content=new Grid{MinHeight=DesignSystem.RowHeight};content.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});content.ColumnDefinitions.Add(new(){Width=GridLength.Auto});content.Children.Add(text);
        var chevron=new System.Windows.Shapes.Path{Data=Geometry.Parse("M1,1 L5.5,5.5 L1,10"),StrokeThickness=1.7,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=7,Height=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,2,0)};chevron.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"SettingsTertiary");Grid.SetColumn(chevron,1);content.Children.Add(chevron);
        var button=new Button{Content=content,HorizontalContentAlignment=HorizontalAlignment.Stretch,Template=LinkTemplate(),Padding=new Thickness(DesignSystem.RowInset-8,0,DesignSystem.RowInset-8,0),Margin=new Thickness(-DesignSystem.RowInset,0,-DesignSystem.RowInset,0)};AutomationProperties.SetAutomationId(button,id);AutomationProperties.SetName(button,title);button.Click+=(_,_)=>action();return button;
    }
    protected static ControlTemplate LinkTemplate()
    {
        var hover=new FrameworkElementFactory(typeof(Border)){Name="Hover"};hover.SetValue(Border.BackgroundProperty,Brushes.Transparent);hover.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));hover.SetValue(Border.PaddingProperty,new Thickness(8,0,8,0));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetBinding(FrameworkElement.MarginProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});hover.AppendChild(presenter);
        var template=new ControlTemplate(typeof(Button)){VisualTree=hover};
        var over=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};over.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("SettingsHover"),"Hover"));template.Triggers.Add(over);
        var pressed=new Trigger{Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("SettingsPressed"),"Hover"));template.Triggers.Add(pressed);
        return template;
    }
    private void More(Panel body){var link=Button(LocalizedText.Get("查看全部设置…"),ReviewCurrent,"shell-all-settings-"+body.Children.Count);link.HorizontalAlignment=HorizontalAlignment.Left;link.Margin=new Thickness(0,0,0,8);body.Children.Add(link);}
    private void ReviewCurrent(){var copy=_draft.Copy();copy.Normalize();int section=_pages.CurrentSection;Close();_review(copy,section);}
    private void RefreshPageAvailability(){foreach(var refresh in _availability)refresh();}
}

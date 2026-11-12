using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using Forms = System.Windows.Forms;

namespace DockForWindows.Views;

internal sealed partial class SettingsWindow : SettingsShellWindow
{
    private readonly DockWindow _dock;
    private readonly DockSettings _draft;
    private WidgetOptions _widgetBaseline=null!;
    private readonly Palette _ui;
    private readonly Dictionary<string,Slider> _ranges=new();
    private readonly Dictionary<string,TextBox> _colors=new();
    private readonly Dictionary<string,SquircleBorder> _swatches=new();
    private readonly Dictionary<UIElement,Func<bool>> _availability=new();
    private readonly DockSurface _sampleDock=new();
    private readonly StackPanel _sampleIcons=new(){Orientation=Orientation.Horizontal};
    private readonly TextBlock _previewLabel=new(),_validation=new();
    private readonly Grid _scene=new(){Height=300};
    private readonly BitmapImage _wallpaper;
    private readonly CheckBox _live,_shadow;
    private readonly ComboBox _theme,_preset;
    private readonly Button _save;
    private readonly DispatcherTimer _previewTimer;
    private bool _ready,_updating,_glassOnlyPreview,_sampleDirty=true;
    private CheckBox? _controlLive;private ControlCenterWindow? _controlPreview;
    private bool Glass => _draft.Theme==DockTheme.Glass;
    private bool Custom => _draft.Theme==DockTheme.Custom;
    private bool Plate => AppearancePolicy.HasPlate(_draft);

    public SettingsWindow(DockWindow dock,Palette palette,DockSettings? draft=null,int page=8) : base(draft??dock.Model.Settings,(_,_)=>{},page,true)
    {
        _dock=dock;_draft=SharedDraft;_widgetBaseline=_draft.Widgets.Copy();_draft.InterfaceStyle=InterfaceStyle.Mono;_ui=palette;
        if(_draft.Theme is DockTheme.System or DockTheme.Light or DockTheme.Dark){var resolved=new Palette(_draft);var baseColor=((SolidColorBrush)resolved.Surface).Color;var indicator=((SolidColorBrush)resolved.Indicator).Color;_draft.Appearance.BackgroundColor=$"#{baseColor.R:X2}{baseColor.G:X2}{baseColor.B:X2}";_draft.Appearance.IndicatorColor=$"#{indicator.R:X2}{indicator.G:X2}{indicator.B:X2}";}
        if(draft is null)try{_draft.StartWithWindows=StartupRegistration.ReadCommand() is not null;}catch{ }
        ApplySettingsPalette(palette);Icon=IosStyles.AppIcon();
        Title="程序坞设置 · DockForWindows";FontFamily=new FontFamily("Noto Sans SC, Microsoft YaHei UI, Segoe UI");Foreground=palette.Text;UseLayoutRounding=true;SnapsToDevicePixels=true;
        Width=Math.Min(1040,SystemParameters.WorkArea.Width-36);Height=Math.Min(780,SystemParameters.WorkArea.Height-36);
        MinWidth=Math.Min(760,Width);MinHeight=540;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=palette.WindowBackground;
        SourceInitialized+=(_,_)=>{
            int dark=palette.Dark?1:0;NativeMethods.DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle,20,ref dark,4);
        };
        var pages=new List<ScrollViewer>();
        StackPanel Page(){var body=new StackPanel{Margin=new Thickness(0,0,0,12)};pages.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});return body;}
        var appearance=Page();var icons=Page();var motion=Page();var apps=Page();var behavior=Page();var system=Page();var interfacePage=Page();var controlPage=Page();

        var material=Section(appearance,"底板外观","普通主题仍可调整圆角、颜色、透明度和阴影；仅液态玻璃专属参数受材质限制。修改底色会进入自定义配色。");
        _theme=Choice(material,"底板主题","theme-mode",new[]{"跟随 Windows","浅色","深色","自定义","液态玻璃"},(int)_draft.Theme);
        _theme.SelectionChanged+=(_,_)=>{if(!_ready||_updating)return;_draft.Theme=(DockTheme)_theme.SelectedIndex;if(!Glass&&!Custom){var color=((SolidColorBrush)new Palette(_draft).Surface).Color;_draft.Appearance.BackgroundColor=$"#{color.R:X2}{color.G:X2}{color.B:X2}";_updating=true;_colors["background-color"].Text=_draft.Appearance.BackgroundColor;_swatches["background-color"].Background=Palette.Brush(_draft.Appearance.BackgroundColor);_updating=false;}QueuePreview();};
        _preset=Choice(material,"配色预设","theme-preset",ThemePresets.Names,0);
        _preset.SelectionChanged+=(_,_)=>{if(!_ready||_updating||_preset.SelectedIndex<=0)return;_draft.Appearance=ThemePresets.Create((string)_preset.SelectedItem);_draft.Theme=DockTheme.Custom;RefreshAppearanceControls();QueuePreview();};
        Range(material,"底板透明度","transparency",0,100,(1-_draft.Appearance.Opacity)*100,"0'%'",v=>_draft.Appearance.Opacity=1-v/100,1,false);
        Range(material,"圆润程度","corner-radius",0,100,_draft.Appearance.CornerRadius,"0.0'%'",v=>_draft.Appearance.CornerRadius=v,.1,false);
        ColorField(material,"底板颜色","background-color",_draft.Appearance.BackgroundColor,()=>!Glass);
        var glass=Section(appearance,"液态玻璃","通过真实背景取样与边缘折射呈现透明玻璃。磨砂只叠加高斯模糊，不会放大背景或减少折射。");
        void GlassRange(string label,string id,double min,double max,double value,string format,Action<double> setter,double step=1,bool body=true){Range(glass,label,id,min,max,value,format,v=>{setter(v);_glassOnlyPreview=true;},step,false);Require(_ranges[id],()=>Glass&&(!body||Plate));}
        GlassRange("折射强度","glass-intensity",0,100,_draft.GlassIntensity*100,"0'%'",v=>_draft.GlassIntensity=v/100);
        GlassRange("边缘扭曲度","glass-distortion",0,100,_draft.GlassDistortion*100,"0'%'",v=>_draft.GlassDistortion=v/100);
        GlassRange("高斯磨砂","glass-frost",0,100,_draft.GlassFrost*100,"0.0'%'",v=>_draft.GlassFrost=v/100,.1);_ranges["glass-frost"].IsSnapToTickEnabled=false;
        GlassRange("玻璃透明度","glass-transparency",0,100,(1-_draft.GlassOpacity)*100,"0'%'",v=>_draft.GlassOpacity=1-v/100,1,false);
        GlassRange("上下高光","glass-highlight",0,100,_draft.GlassHighlight*100,"0'%'",v=>_draft.GlassHighlight=v/100);
        GlassRange("高光过渡范围","glass-highlight-width",.5,8,_draft.GlassHighlightWidth,"0.0' px'",v=>_draft.GlassHighlightWidth=v,.1);
        GlassRange("左右暗边","glass-side-darkness",0,100,_draft.GlassSideDarkness*100,"0'%'",v=>_draft.GlassSideDarkness=v/100);
        GlassRange("淡黑衬色上限","glass-tint",0,25,_draft.GlassTint*100,"0.0'%'",v=>_draft.GlassTint=v/100,.1);
        var finish=Section(appearance,"边缘与阴影","普通主题使用边框，液态玻璃使用独立高光。各主题都可设置阴影；无底板时暂不绘制阴影，但仍保留你的设置。");
        ColorField(finish,"边框颜色","border-color",_draft.Appearance.BorderColor,()=>!Glass);
        Range(finish,"边框粗细","border-thickness",0,3,_draft.Appearance.BorderThickness,"0.0' px'",v=>_draft.Appearance.BorderThickness=v,.1,false);Require(_ranges["border-thickness"],()=>!Glass);
        Range(finish,"边框强度","border-opacity",0,100,_draft.Appearance.BorderOpacity*100,"0'%'",v=>_draft.Appearance.BorderOpacity=v/100,1,false);Require(_ranges["border-opacity"],()=>!Glass&&_draft.Appearance.BorderThickness>0);
        _shadow=Toggle(finish,"底板悬浮阴影","shadow-enabled",_draft.Appearance.ShadowEnabled,v=>_draft.Appearance.ShadowEnabled=v);
        Range(finish,"阴影强度","shadow-opacity",0,65,_draft.Appearance.ShadowOpacity*100,"0'%'",v=>_draft.Appearance.ShadowOpacity=v/100,1,false);
        Range(finish,"阴影柔和度","shadow-blur",0,40,_draft.Appearance.ShadowBlur,"0' px'",v=>_draft.Appearance.ShadowBlur=v,1,false);
        Require(_ranges["shadow-opacity"],()=>_draft.Appearance.ShadowEnabled);Require(_ranges["shadow-blur"],()=>_draft.Appearance.ShadowEnabled);
        ColorField(finish,"运行点自定义颜色","indicator-color",_draft.Appearance.IndicatorColor,()=>!Glass);
        var reset=SettingsButton("恢复默认主题",(_,_)=>{_draft.Theme=DockTheme.System;_draft.Appearance=new();_draft.GlassIntensity=.55;_draft.GlassOpacity=1;_draft.GlassFrost=.1;RefreshAppearanceControls();QueuePreview();});AutomationProperties.SetAutomationId(reset,"reset-theme");appearance.Children.Add(reset);

        var sizing=Section(icons,"统一图标","不规则图标会放入固定三次超椭圆外框，白底或黑底跟随系统。纯白图形转为黑色并使用白底，回收站保持原样。");
        Range(sizing,"图标尺寸","icon-size",32,72,_draft.IconSize,"0' px'",v=>_draft.IconSize=v,1,false);Range(sizing,"图标间距","icon-spacing",2,18,_draft.Spacing,"0' px'",v=>_draft.Spacing=v,1,false);
        var reflection=Section(icons,"图标倒影","仅在底板完全透明、完全没有底板时生效；半透明或有底板的主题不可使用。关闭倒影后，下方参数停用。");
        var reflections=Toggle(reflection,"显示倒影","reflections",_draft.Reflections,v=>_draft.Reflections=v);Require(reflections,()=>AppearancePolicy.ReflectionsAllowed(_draft));
        Range(reflection,"倒影强度","reflection-opacity",0,100,_draft.ReflectionOpacity*100,"0'%'",v=>_draft.ReflectionOpacity=v/100,1,false);Range(reflection,"倒影长度","reflection-length",5,100,_draft.ReflectionLength*100,"0'%'",v=>_draft.ReflectionLength=v/100,1,false);Range(reflection,"与图标间距","reflection-gap",0,12,_draft.ReflectionGap,"0' px'",v=>_draft.ReflectionGap=v,1,false);Range(reflection,"倒影柔和度","reflection-blur",0,8,_draft.ReflectionBlur,"0.0' px'",v=>_draft.ReflectionBlur=v,.5,false);
        foreach(var id in new[]{"reflection-opacity","reflection-length","reflection-gap","reflection-blur"})Require(_ranges[id],()=>AppearancePolicy.ReflectionsAllowed(_draft)&&_draft.Reflections);
        var animation=Section(motion,"波浪与弹跳","鼠标经过时向外放大，底板保持贴边；点击应用时弹跳。关闭动画或系统减少动画时，以下参数停用。");
        Toggle(animation,"启用动画","animations",_draft.Animations,v=>_draft.Animations=v);
        Range(animation,"波浪放大倍率","magnification",1,1.8,_draft.Magnification,"0.00' 倍'",v=>_draft.Magnification=v,.05,false);Range(animation,"波浪上浮高度","hover-lift",0,64,_draft.HoverLift,"0' px'",v=>_draft.HoverLift=v,1,false);Range(animation,"弹跳高度","bounce-height",8,64,_draft.BounceHeight,"0' px'",v=>_draft.BounceHeight=v,1,false);Range(animation,"弹跳时长","bounce-duration",350,1800,_draft.BounceDuration,"0' ms'",v=>_draft.BounceDuration=v,50,false);
        foreach(var id in new[]{"magnification","hover-lift","bounce-height","bounce-duration"})Require(_ranges[id],()=>_draft.Animations&&!_ui.ReducedMotion);
        var library=Section(apps,"固定项目","应用、文档和文件夹可以从任意位置拖入。固定和取消固定立即保存，不需要在桌面保留快捷方式。");
        library.Children.Add(SettingsButton("添加应用或文件…",(_,_)=>_dock.AddApplication()));var addFolder=SettingsButton("添加文件夹…",(_,_)=>_dock.AddFolder());addFolder.Margin=new Thickness(0,10,0,0);library.Children.Add(addFolder);
        var recent=Section(apps,"运行与最近使用","固定应用之后显示其他运行或最近使用的项目，以分隔线分开。回收站始终保留在末端。");
        Toggle(recent,"显示其他运行应用","running-apps",_draft.ShowRunningApps,v=>_draft.ShowRunningApps=v);Toggle(recent,"显示最近应用","recent-apps",_draft.ShowRecentApps,v=>_draft.ShowRecentApps=v);Range(recent,"最近应用数量","recent-limit",0,8,_draft.RecentLimit,"0",v=>_draft.RecentLimit=(int)v,1,false);Require(_ranges["recent-limit"],()=>_draft.ShowRecentApps);
        var placement=Section(behavior,"停靠位置","底部、左侧与右侧使用相同的图标居中规则。空间不足时整体缩小，增减应用后重新居中。");
        var edge=Choice(placement,"屏幕边缘","dock-edge",new[]{"屏幕底部","屏幕左侧","屏幕右侧"},(int)_draft.Edge);edge.SelectionChanged+=(_,_)=>{if(_ready){_draft.Edge=(DockEdge)edge.SelectedIndex;QueuePreview();}};
        var screens=Forms.Screen.AllScreens;int selectedMonitor=Array.FindIndex(screens,s=>s.DeviceName==_draft.MonitorDevice);if(selectedMonitor<0)selectedMonitor=Array.FindIndex(screens,s=>s.Primary);
        var monitor=Choice(placement,"显示器","dock-monitor",screens.Select((s,i)=>$"显示器 {i+1}{(s.Primary?" · 主屏幕":"")}  {s.Bounds.Width} × {s.Bounds.Height}").ToArray(),selectedMonitor);monitor.SelectionChanged+=(_,_)=>{if(_ready&&monitor.SelectedIndex>=0){_draft.MonitorDevice=screens[monitor.SelectedIndex].DeviceName;QueuePreview();}};
        Range(placement,"距离屏幕边缘","edge-distance",0,160,_draft.EdgeDistance,"0' px'",v=>_draft.EdgeDistance=v,1,false);
        var hiding=Section(behavior,"显示与退下","桌面默认固定显示。普通窗口自动隐藏、桌面自动隐藏分别设置；最大化或全屏的退下行为单独保留。");
        Toggle(hiding,"普通窗口下自动隐藏","auto-hide",_draft.AutoHide,v=>_draft.AutoHide=v);Toggle(hiding,"桌面上也自动隐藏","desktop-auto-hide",_draft.HideOnDesktop,v=>_draft.HideOnDesktop=v);Toggle(hiding,"最大化或全屏时退下","fullscreen-hide",_draft.HideWhenFullscreen,v=>_draft.HideWhenFullscreen=v);
        var interfaceCard=Section(interfacePage,"窗口外观","窗口采用中性色与克制的青绿强调，可跟随系统或选择浅色、深色。程序坞底板材质仍独立设置。");
        var interfaceTheme=Choice(interfaceCard,"界面外观","interface-theme",new[]{"跟随系统","浅色","深色"},(int)_draft.InterfaceTheme);interfaceTheme.SelectionChanged+=(_,_)=>{if(!_ready||interfaceTheme.SelectedIndex<0)return;_draft.InterfaceTheme=(InterfaceTheme)interfaceTheme.SelectedIndex;Dispatcher.BeginInvoke(()=>_dock.ReopenSettings(_draft,CurrentSettingsSection));};
        var startup=Section(system,"登录后自动启动","保存设置后生效。启用后在当前账户登录时启动，沿用你的固定项目与布局；取消本次设置不会修改开机启动项。");
        Toggle(startup,"开机自启","start-with-windows",_draft.StartWithWindows,v=>_draft.StartWithWindows=v);
        BuildControlCenterSettings(controlPage);

        var previewColumn=new StackPanel{Margin=new Thickness(0,27,18,18)};
        previewColumn.Children.Add(new TextBlock{Text="预览",FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(4,0,0,15)});
        _wallpaper=new BitmapImage(new Uri("pack://application:,,,/DockForWindows;component/Assets/PreviewWallpaper.png"));_wallpaper.Freeze();
        _scene.Background=new ImageBrush(_wallpaper){Stretch=Stretch.UniformToFill};AutomationProperties.SetAutomationId(_scene,"wallpaper-preview");
        _sampleDock.HorizontalAlignment=HorizontalAlignment.Center;_sampleDock.VerticalAlignment=VerticalAlignment.Bottom;_scene.Children.Add(_sampleDock);
        _sampleIcons.HorizontalAlignment=HorizontalAlignment.Center;_sampleIcons.VerticalAlignment=VerticalAlignment.Bottom;_sampleIcons.Margin=new Thickness(0,0,0,28);_scene.Children.Add(_sampleIcons);
        // The mask and wallpaper share exact bounds. Shadow is a separate sibling
        // so it remains outside the mask, without a white stroke or inset seam.
        var previewHost=new Grid();
        var previewShadow=new System.Windows.Shapes.Path{Fill=Brushes.Black,Opacity=.18,Stretch=Stretch.None,IsHitTestVisible=false,Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=18,ShadowDepth=7,Direction=270,Opacity=.65}};
        var previewFrame=new SquircleBorder{CornerRadius=new CornerRadius(22),Background=Brushes.Transparent,Child=_scene,BorderThickness=new Thickness(0)};
        AutomationProperties.SetAutomationId(previewFrame,"rounded-preview-frame");AutomationProperties.SetAutomationId(previewShadow,"preview-floating-shadow");
        previewFrame.SizeChanged+=(_,_)=>{var mask=SquircleBorder.Outline(previewFrame.RenderSize,22);previewFrame.Clip=mask;previewShadow.Data=mask;};
        previewHost.Children.Add(previewShadow);previewHost.Children.Add(previewFrame);previewColumn.Children.Add(previewHost);
        _previewLabel.Foreground=palette.Secondary;_previewLabel.FontSize=12;_previewLabel.TextWrapping=TextWrapping.Wrap;_previewLabel.Margin=new Thickness(5,12,5,14);previewColumn.Children.Add(_previewLabel);
        _live=Toggle(previewColumn,"桌面实时预览","live-preview",true,_=>{});
        _controlLive=Toggle(previewColumn,"控制中心实时预览","control-live-preview",false,_=>{});
        Help(previewColumn,"开启后边调边看控制中心，保存后提交；关闭设置窗口会结束预览。",new Thickness(6,8,6,0));
        Help(previewColumn,"开启时同步预览桌面 Dock；关闭后仅查看右侧示例。保存才提交主题设置，取消会恢复。",new Thickness(6,10,6,0));
        var footer=new Grid{Margin=new Thickness(0,15,0,0)};footer.RowDefinitions.Add(new(){Height=GridLength.Auto});footer.RowDefinitions.Add(new(){Height=GridLength.Auto});
        _validation.Foreground=Palette.Brush("#D66D61");_validation.FontSize=11;_validation.TextWrapping=TextWrapping.Wrap;footer.Children.Add(_validation);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,0,0)};Grid.SetRow(actions,1);footer.Children.Add(actions);
        var cancel=SettingsButton("取消",(_,_)=>_dock.ReopenSettings(_dock.Model.Settings,CurrentSettingsSection));AutomationProperties.SetAutomationId(cancel,"cancel-settings");actions.Children.Add(cancel);
        var previewButton=SettingsButton("预览",(_,_)=>OpenSettingsPreview());AutomationProperties.SetAutomationId(previewButton,"open-settings-preview");actions.Children.Add(previewButton);
        _save=SettingsButton("保存设置",(_,_)=>SaveSettings(),true);AutomationProperties.SetAutomationId(_save,"save-settings");actions.Children.Add(_save);
        _previewTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(60)};_previewTimer.Tick+=(_,_)=>FlushPreview();
        InstallSettingsPages(pages,previewColumn,footer,page);_ready=true;RefreshAvailability();RefreshSample();
        _scene.SizeChanged+=(_,_)=>RefreshPreviewBackdrop();LocationChanged+=(_,_)=>{if(_ready)Dispatcher.BeginInvoke(RefreshPreviewBackdrop);};
        _dock.Model.ItemsRebuilt+=UpdateSampleIcons;Closed+=(_,_)=>{_ready=false;_previewTimer.Stop();_dock.Model.ItemsRebuilt-=UpdateSampleIcons;_controlPreview?.Dismiss();_controlPreview=null;_dock.CancelPreview();};
        Loaded+=(_,_)=>RefreshSample();_scene.Loaded+=(_,_)=>{if(_sampleDirty)RefreshSample();};_scene.IsVisibleChanged+=(_,_)=>{if(_scene.IsVisible&&_sampleDirty)RefreshSample();};
    }
    private void SaveSettings(){FlushPreview();if(!ValidateColors())return;_draft.Widgets.MergeLive(_widgetBaseline,_dock.Model.Settings.Widgets);_draft.Normalize();if(_dock.CommitSettings(_draft)){_widgetBaseline=_draft.Widgets.Copy();_validation.Text="设置已保存";}}
    private StackPanel Section(Panel parent,string title,string help)
    {
        var outer=new StackPanel{Margin=new Thickness(0,0,0,20)};var content=new StackPanel();
        outer.Children.Add(new TextBlock{Text=title,FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=_ui.Secondary,Margin=new Thickness(10,0,0,7)});
        outer.Children.Add(new Border{Style=(Style)FindResource("SettingsSurface"),Padding=new Thickness(16,5,16,5),Child=content});
        if(!string.IsNullOrWhiteSpace(help))Help(outer,help,new Thickness(10,7,8,0));parent.Children.Add(outer);_settingsSections.Add(new(parent,outer,title,help));return content;
    }
    private void AddSettingsRow(Panel parent,UIElement row)
    {if(parent.Children.Count>0)parent.Children.Add(new Border{Height=.7,Background=(Brush)FindResource("SettingsLine")});parent.Children.Add(row);}
    private void Help(Panel panel,string text,Thickness margin)=>panel.Children.Add(new TextBlock{Text=text,FontSize=11,LineHeight=18,Foreground=_ui.Secondary,TextWrapping=TextWrapping.Wrap,Margin=margin});
    private void Require(UIElement control,Func<bool> predicate){_availability[control]=predicate;}
    private void RefreshAvailability(){foreach(var entry in _availability){entry.Key.IsEnabled=entry.Value();if(entry.Key is Control)entry.Key.ClearValue(UIElement.OpacityProperty);else entry.Key.Opacity=entry.Key.IsEnabled?1:.5;}}
    private TextBlock Label(string text)=>new(){Text=text,FontSize=12,Foreground=_ui.Secondary,Margin=new Thickness(0,14,0,7),TextWrapping=TextWrapping.Wrap};
    private ComboBox Choice(Panel panel,string title,string id,string[] options,int selected)
    {
        var row=new Grid{Margin=new Thickness(0,8,0,8)};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1.6,GridUnitType.Star)});
        row.Children.Add(new TextBlock{Text=title,FontSize=13,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,14,0)});
        var combo=new ComboBox{ItemsSource=options,SelectedIndex=Math.Max(0,selected),FontSize=12,VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetAutomationId(combo,id);AutomationProperties.SetName(combo,title);Grid.SetColumn(combo,1);row.Children.Add(combo);
        if(id is "theme-mode" or "dock-edge" or "interface-theme"){
            combo.Visibility=Visibility.Collapsed;var display=id=="theme-mode"?new[]{"系统","浅色","深色","自定义","玻璃"}:id=="dock-edge"?new[]{"底部","左侧","右侧"}:options;var segment=new SettingsSegmentedControl{ItemsSource=display,FontSize=12};AutomationProperties.SetAutomationId(segment,id+"-segment");AutomationProperties.SetName(segment,title);
            segment.SetBinding(Selector.SelectedIndexProperty,new System.Windows.Data.Binding("SelectedIndex"){Source=combo,Mode=System.Windows.Data.BindingMode.TwoWay});Grid.SetColumn(segment,1);row.Children.Add(segment);
        }
        AddSettingsRow(panel,row);return combo;
    }
    private Slider Range(Panel panel,string title,string id,double min,double max,double value,string format,Action<double> setter,double step=1,bool custom=true)
    {
        var row=new Grid{Margin=new Thickness(0,7,0,7)};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1.3,GridUnitType.Star)});row.Children.Add(new TextBlock{Text=title,FontSize=13,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,0)});
        var field=new Grid();field.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});field.ColumnDefinitions.Add(new(){Width=new GridLength(65)});
        var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=step,IsSnapToTickEnabled=true};AutomationProperties.SetAutomationId(slider,id);AutomationProperties.SetName(slider,title);
        var caption=new TextBlock{FontSize=12,Foreground=_ui.Secondary,TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(7,0,0,0)};void Update()=>caption.Text=slider.Value.ToString(format);
        slider.ValueChanged+=(_,_)=>{Update();if(!_ready||_updating)return;_glassOnlyPreview=false;setter(slider.Value);if(custom)MarkCustom();QueuePreview();};Update();field.Children.Add(slider);Grid.SetColumn(caption,1);field.Children.Add(caption);Grid.SetColumn(field,1);row.Children.Add(field);AddSettingsRow(panel,row);_ranges[id]=slider;return slider;
    }
    private CheckBox Toggle(Panel panel,string title,string id,bool value,Action<bool> setter)
    {var c=new CheckBox{Content=title,IsChecked=value,FontSize=12,Foreground=_ui.Text,Margin=new Thickness(0)};AutomationProperties.SetAutomationId(c,id);AutomationProperties.SetName(c,title);void Changed(){if(!_ready||_updating)return;setter(c.IsChecked==true);QueuePreview();}c.Checked+=(_,_)=>Changed();c.Unchecked+=(_,_)=>Changed();AddSettingsRow(panel,c);return c;}
    private void ColorField(Panel panel,string title,string id,string value,Func<bool> available)
    {
        panel.Children.Add(Label(title));var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var edit=new TextBox{Text=value,FontSize=12,Padding=new Thickness(10,8,10,8),MaxLength=7,Background=Brushes.Transparent,Foreground=_ui.Text};AutomationProperties.SetAutomationId(edit,id);AutomationProperties.SetName(edit,title);
        var swatch=new SquircleBorder{Width=18,Height=18,CornerRadius=new CornerRadius(4),Background=Palette.Brush(value),Margin=new Thickness(0,0,7,0)};
        var choose=SettingsButton("选色",(_,_)=>{using var dialog=new Forms.ColorDialog{FullOpen=true,Color=System.Drawing.ColorTranslator.FromHtml(ThemeAppearance.TryColor(edit.Text,out var hex)?hex:value)};if(dialog.ShowDialog()==Forms.DialogResult.OK)edit.Text=$"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";});
        var chip=new StackPanel{Orientation=Orientation.Horizontal};chip.Children.Add(swatch);chip.Children.Add(new TextBlock{Text="选色",VerticalAlignment=VerticalAlignment.Center});choose.Content=chip;choose.Margin=new Thickness(6,0,0,0);AutomationProperties.SetAutomationId(choose,"pick-"+id);
        _colors[id]=edit;_swatches[id]=swatch;edit.TextChanged+=(_,_)=>{if(!_ready||_updating)return;if(ThemeAppearance.TryColor(edit.Text,out var hex)){if(id=="background-color")_draft.Appearance.BackgroundColor=hex;else if(id=="border-color")_draft.Appearance.BorderColor=hex;else _draft.Appearance.IndicatorColor=hex;swatch.Background=Palette.Brush(hex);if(id is "background-color" or "indicator-color")MarkCustom(id=="background-color");}ValidateColors();QueuePreview();};
        row.Children.Add(edit);Grid.SetColumn(choose,1);row.Children.Add(choose);panel.Children.Add(row);Require(row,available);
    }
    private void MarkCustom(bool preserveColor=false){if(!Glass&&!Custom&&!preserveColor){var color=((SolidColorBrush)new Palette(_draft).Surface).Color;_draft.Appearance.BackgroundColor=$"#{color.R:X2}{color.G:X2}{color.B:X2}";_updating=true;_colors["background-color"].Text=_draft.Appearance.BackgroundColor;_swatches["background-color"].Background=Palette.Brush(_draft.Appearance.BackgroundColor);_updating=false;}if(!Glass)_draft.Theme=DockTheme.Custom;_updating=true;_theme.SelectedIndex=(int)_draft.Theme;_preset.SelectedIndex=0;_updating=false;}
    private bool ValidateColors(){bool valid=_colors.Values.All(c=>ThemeAppearance.TryColor(c.Text,out _));foreach(var edit in _colors.Values)edit.BorderBrush=ThemeAppearance.TryColor(edit.Text,out _)?Brushes.Transparent:Palette.Brush("#D66D61");_validation.Text=valid?"":"颜色格式应为 #RRGGBB。";_save.IsEnabled=valid;return valid;}
    private void RefreshAppearanceControls()
    {
        _updating=true;_theme.SelectedIndex=(int)_draft.Theme;
        foreach(var pair in new[]{("background-color",_draft.Appearance.BackgroundColor),("border-color",_draft.Appearance.BorderColor),("indicator-color",_draft.Appearance.IndicatorColor)}){_colors[pair.Item1].Text=pair.Item2;_swatches[pair.Item1].Background=Palette.Brush(pair.Item2);}
        var values=new Dictionary<string,double>{{"transparency",(1-_draft.Appearance.Opacity)*100},{"corner-radius",_draft.Appearance.CornerRadius},{"border-thickness",_draft.Appearance.BorderThickness},{"border-opacity",_draft.Appearance.BorderOpacity*100},{"shadow-opacity",_draft.Appearance.ShadowOpacity*100},{"shadow-blur",_draft.Appearance.ShadowBlur},{"glass-intensity",_draft.GlassIntensity*100},{"glass-distortion",_draft.GlassDistortion*100},{"glass-frost",_draft.GlassFrost*100},{"glass-transparency",(1-_draft.GlassOpacity)*100},{"glass-highlight",_draft.GlassHighlight*100},{"glass-highlight-width",_draft.GlassHighlightWidth},{"glass-side-darkness",_draft.GlassSideDarkness*100},{"glass-tint",_draft.GlassTint*100}};
        foreach(var p in values)_ranges[p.Key].Value=p.Value;_shadow.IsChecked=_draft.Appearance.ShadowEnabled;_updating=false;RefreshAvailability();ValidateColors();
    }
    private void QueuePreview(){if(!_ready||_updating)return;RefreshAvailability();if(!_previewTimer.IsEnabled)_previewTimer.Start();}
    private void FlushPreview(){_previewTimer.Stop();if(!ValidateColors())return;RefreshAvailability();RefreshSample();if(_live.IsChecked==true){if(_glassOnlyPreview||CurrentSettingsSection==7)_dock.PreviewGlass(_draft);else _dock.PreviewSettings(_draft);}else _dock.CancelPreview();UpdateControlPreview();_glassOnlyPreview=false;}
    private void UpdateSampleIcons(){if(_ready)RefreshSample();}
    public void RefreshInterfaceAppearance(){if(_draft.InterfaceTheme==InterfaceTheme.System)_dock.ReopenSettings(_draft,CurrentSettingsSection);}
    private void RefreshPreviewBackdrop()
    {
        if(!_ready||!_scene.IsLoaded||_scene.ActualWidth<=0||_scene.ActualHeight<=0)return;
        var dpi=VisualTreeHelper.GetDpi(_scene);int w=Math.Max(1,(int)Math.Ceiling(_scene.ActualWidth*dpi.DpiScaleX)),h=Math.Max(1,(int)Math.Ceiling(_scene.ActualHeight*dpi.DpiScaleY));
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new ImageBrush(_wallpaper){Stretch=Stretch.UniformToFill},null,new Rect(0,0,_scene.ActualWidth,_scene.ActualHeight));
        var bitmap=new RenderTargetBitmap(w,h,96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();var point=_scene.PointToScreen(new Point());_sampleDock.BackdropScreenBounds=new Rect(point.X,point.Y,w,h);_sampleDock.Backdrop=bitmap;
    }
    private void RefreshSample()
    {
        if(!_scene.IsVisible||!_scene.IsLoaded){_sampleDirty=true;return;}_sampleDirty=false;
        var p=new Palette(_draft);_sampleDock.Fill=p.Surface;_sampleDock.Stroke=p.Line;_sampleDock.StrokeWidth=Glass?0:p.BorderThickness;_sampleDock.Radius=p.CornerRadius;_sampleDock.GlassIntensity=p.GlassIntensity;
        _sampleDock.UseGlass=Glass;_sampleDock.GlassOpacity=_draft.GlassOpacity;_sampleDock.GlassFrost=_draft.GlassFrost;_sampleDock.GlassDistortion=_draft.GlassDistortion;_sampleDock.GlassHighlight=_draft.GlassHighlight;_sampleDock.GlassHighlightWidth=_draft.GlassHighlightWidth;_sampleDock.GlassSideDarkness=_draft.GlassSideDarkness;_sampleDock.GlassTint=_draft.GlassTint;
        _sampleDock.Effect=p.ShadowEnabled?new DropShadowEffect{BlurRadius=p.ShadowBlur,ShadowDepth=4,Opacity=p.ShadowOpacity}:null;
        _sampleIcons.Children.Clear();var sample=_draft.Copy();sample.IconSize=36;var items=_dock.Model.DisplayItems.Where(i=>i.IsPinned).Take(4).ToArray();
        foreach(var item in items)_sampleIcons.Children.Add(new DockTile(item,p,sample){IsHitTestVisible=false,Focusable=false});
        double reflection=AppearancePolicy.ShowReflections(sample)?36*sample.ReflectionLength+sample.ReflectionGap+4:0;
        _sampleDock.Width=Math.Max(120,items.Length*48+16);_sampleDock.Height=64;_sampleDock.Margin=new Thickness(0,0,0,23+reflection);
        _previewLabel.Text=Glass?$"液态玻璃 · 折射 {_draft.GlassIntensity*100:0}% · 磨砂 {_draft.GlassFrost*100:0}%":"底板透明度 "+((1-_draft.Appearance.Opacity)*100).ToString("0")+"% · 壁纸示例";
        _sampleDock.InvalidateVisual();Dispatcher.BeginInvoke(RefreshPreviewBackdrop,DispatcherPriority.Render);
    }
}



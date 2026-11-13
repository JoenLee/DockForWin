using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.Win32;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

// The settings window frame in the manner of macOS 26 System Settings: a floating Liquid Glass sidebar with the
// traffic lights, a search field and colour-tiled categories; a toolbar of glass capsules (back/forward) with the
// page title; pages that fade under the toolbar. The material lives in SettingsShellWindow.Material.cs.
internal partial class SettingsShellWindow:Window
{
    private readonly DockSettings _draft;
    private readonly Action<DockSettings,int> _review;
    private readonly SettingsPageHost _pages=new();
    private bool _navigationReady;private readonly string[] _extraSearchTerms=new string[8];
    private Grid _sidebar=null!;private ColumnDefinition _sidebarColumn=null!;private bool _collapsed;
    private FrameworkElement _shellContent=null!;private Grid _right=null!;
    private readonly List<Button> _lights=new();private Border _searchFrame=null!;private FrameworkElement _sidebarFooter=null!;
    protected DockSettings SharedDraft=>_draft;
    protected ListBox SidebarNavigation=>_navigation;
    protected SettingsPageHost PageHost=>_pages;
    private readonly ListBox _navigation=new(){Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
    private readonly TextBox _search=new();private readonly Grid _navigationFrame=new(){ClipToBounds=true};
    private readonly Border _selection=new(){Height=36,CornerRadius=new CornerRadius(10),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Hidden,RenderTransform=new TranslateTransform()};
    private readonly TextBlock _noMatches=new(){Text="没有匹配的设置",FontSize=12.5,Margin=new Thickness(18,10,12,0),VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Collapsed};
    private readonly TextBlock _title=new(){FontFamily=DesignSystem.DisplayFont,FontSize=15,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
    private readonly List<Action> _availability=new();private Button _back=null!,_forward=null!;private bool _syncNavigation;
    private static readonly string[] Names={"概览","控制中心","桌面小组件","外观与主题","动画与交互","性能与渲染","通用设置","关于"};
    // Icon tile per category: colour index into DesignSystem.TileColors and an Ionicons glyph.
    private static readonly (int Color,string Glyph)[] Tiles={(0,"Grid"),(1,"Options"),(2,"Calendar"),(3,"Palette"),(4,"Sparkles"),(5,"Performance"),(6,"Settings"),(7,"Desktop")};
    protected static readonly string[] Keys={"appearance","icons","motion","apps","placement","system","interface","control-center"};
    protected static readonly string[] LegacyTitles={"外观与主题","图标与倒影","动画与交互","应用与文件","位置与隐藏","系统与启动","窗口外观","控制中心"};
    private static readonly string[] NavigationKeys={"dashboard","control-center","widgets","appearance","motion","performance","general","about"};
    private static readonly string[] SearchTerms={"概览 Dashboard 状态 材质 常用","面板 模块 布局 缩放","桌面 小组件 天气 预报 城市 日历 日程 月历 时钟 提醒 待办 备忘录 笔记 音乐 播放 桌面 实例","底板 图标 倒影 颜色 材质 玻璃 阴影","波浪 弹跳 缓动","CPU GPU 内存 帧率 渲染","应用 文件 位置 隐藏 开机 启动 窗口 配色","版本 关于"};
    protected int CurrentSettingsSection=>_pages.CurrentSection;
    private static int NavigationIndex(int section)=>section switch{8=>0,7=>1,9=>2,0 or 1=>3,2=>4,10=>5,12=>7,_=>6};
    private static string SectionKey(int section)=>section switch{8=>"dashboard",9=>"widgets",10=>"performance",11=>"general",12=>"about",_=>Keys[Math.Clamp(section,0,7)]};
    // Tile shown in a page header for a page section.
    // Page headers carry the category's line glyph in a soft glass chip (no heavy colour tiles).
    protected static FrameworkElement SectionTile(int section,double size)
    {
        var glyph=new System.Windows.Shapes.Path{Data=LineGlyph(NavigationIndex(section)),StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=18,Height=18,Stretch=Stretch.None,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(size/34,size/34),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        glyph.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"SettingsAccent");
        var chip=new Border{Width=size,Height=size,CornerRadius=new CornerRadius(size*.3),BorderThickness=new Thickness(1),Child=glyph};
        chip.SetResourceReference(Border.BackgroundProperty,"SettingsCard");chip.SetResourceReference(Border.BorderBrushProperty,"SettingsGroupEdge");return chip;
    }
    internal SettingsShellWindow(DockSettings draft,Action<DockSettings,int> review,int initialPage=8) : this(draft,review,initialPage,false) {}
    internal SettingsShellWindow(DockSettings draft,Action<DockSettings,int> review,int initialPage,bool fullSettings)
    {
        _draft=draft.Copy();_review=review;
        Title="设置 · DockForWin";Icon=IosStyles.AppIcon();FontFamily=DesignSystem.TextFont;FontSize=13.5;
        // Pixel-aligned text: the transparent window renders grayscale, where Display mode keeps small glyphs sharp.
        TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);TextOptions.SetTextHintingMode(this,TextHintingMode.Fixed);
        // The window carries a transparent margin for its own shadow (ShadowMargin on each side).
        Width=Math.Min(1120,SystemParameters.WorkArea.Width-32)+2*ShadowMargin;Height=Math.Min(760,SystemParameters.WorkArea.Height-32)+2*ShadowMargin;MinWidth=Math.Min(800,Width);MinHeight=Math.Min(560,Height);
        WindowStartupLocation=WindowStartupLocation.CenterOwner;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;UseLayoutRounding=true;SnapsToDevicePixels=true;
        // One slab of glass with its own rounded outline and shadow: a per-pixel transparent window.
        AllowsTransparency=true;Background=Brushes.Transparent;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/DockForWindows;component/Views/SettingsShellStyles.xaml",UriKind.Relative)});
        WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=ShadowMargin+FrameInset+48,ResizeBorderThickness=new Thickness(ShadowMargin+4),CornerRadius=new CornerRadius(0),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
        SourceInitialized+=(_,_)=>{ApplyBackdrop();WallpaperFrost.Warm(new WindowInteropHelper(this).Handle);};Activated+=(_,_)=>PaintLights(true);Deactivated+=(_,_)=>PaintLights(false);
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/DockForWindows;component/Assets/Ionicons/SettingsIonicons.xaml",UriKind.Relative)});
        var root=new Grid();_sidebarColumn=new(){Width=new GridLength(240)};root.ColumnDefinitions.Add(_sidebarColumn);root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});AutomationProperties.SetAutomationId(root,"settings-shell-two-columns");
        _shellContent=_windowBody=new Border{Child=root,Padding=new Thickness(ShadowMargin)};Content=_shellContent;InstallWindowMaterial(root);

        // Sidebar: window controls, search, categories, about — on a floating pane of glass.
        var sidebar=_sidebar=new Grid();
        sidebar.RowDefinitions.Add(new(){Height=new GridLength(44)});sidebar.RowDefinitions.Add(new(){Height=new GridLength(44)});sidebar.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});sidebar.RowDefinitions.Add(new(){Height=GridLength.Auto});root.Children.Add(SidebarGlass(sidebar));
        var header=new Grid{Margin=new Thickness(11,0,10,0)};
        var lights=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left,Background=Brushes.Transparent};
        lights.Children.Add(Light("关闭","×","settings-shell-window-close",Close));
        lights.Children.Add(Light("最小化","−","settings-shell-window-minimize",()=>WindowState=WindowState.Minimized));
        lights.Children.Add(Light("缩放","+","settings-shell-window-maximize",()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized));
        WindowChrome.SetIsHitTestVisibleInChrome(lights,true);header.Children.Add(lights);sidebar.Children.Add(header);

        _searchFrame=new Border{CornerRadius=new CornerRadius(15),Height=30,Margin=new Thickness(10,4,10,0),VerticalAlignment=VerticalAlignment.Top,BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent};_searchFrame.SetResourceReference(Border.BackgroundProperty,"SettingsQuiet");
        var searchGrid=new Grid();_search.Background=Brushes.Transparent;_search.BorderThickness=new Thickness(0);_search.Padding=new Thickness(26,0,8,0);_search.FontSize=13;_search.VerticalContentAlignment=VerticalAlignment.Center;
        _search.Template=PlainTextBoxTemplate();AutomationProperties.SetAutomationId(_search,"settings-shell-search");AutomationProperties.SetName(_search,"搜索设置与小组件");
        var placeholder=new TextBlock{Text="搜索",FontSize=13,Margin=new Thickness(28,0,0,1),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};placeholder.SetResourceReference(TextBlock.ForegroundProperty,"SettingsTertiary");
        var magnifier=new System.Windows.Shapes.Path{Data=Geometry.Parse("M6.5,1 A5.5,5.5 0 1 1 6.49,1 Z M10.6,10.6 L14.5,14.5"),StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Width=13,Height=13,Stretch=Stretch.Uniform,Margin=new Thickness(9,0,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};magnifier.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"SettingsMuted");
        searchGrid.Children.Add(_search);searchGrid.Children.Add(placeholder);searchGrid.Children.Add(magnifier);_searchFrame.Child=searchGrid;Grid.SetRow(_searchFrame,1);sidebar.Children.Add(_searchFrame);
        _search.GotKeyboardFocus+=(_,_)=>_searchFrame.SetResourceReference(Border.BorderBrushProperty,"SettingsFocusRing");
        _search.LostKeyboardFocus+=(_,_)=>_searchFrame.BorderBrush=Brushes.Transparent;
        _navigationFrame.Children.Add(_selection);_navigationFrame.Children.Add(_navigation);_navigationFrame.Children.Add(_noMatches);AutomationProperties.SetAutomationId(_noMatches,"settings-shell-search-empty");_noMatches.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");Grid.SetRow(_navigationFrame,2);sidebar.Children.Add(_navigationFrame);
        _navigation.ItemContainerStyle=(Style)FindResource("SettingsNavigationItem");ScrollViewer.SetHorizontalScrollBarVisibility(_navigation,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(_navigation,ScrollBarVisibility.Auto);
        KeyboardNavigation.SetTabNavigation(_navigation,KeyboardNavigationMode.Once);AutomationProperties.SetAutomationId(_navigation,fullSettings?"settings-navigation":"settings-shell-navigation");
        for(int i=0;i<Names.Length;i++){
            var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(32)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
            var tile=NavigationGlyph(i);tile.HorizontalAlignment=HorizontalAlignment.Left;tile.VerticalAlignment=VerticalAlignment.Center;AutomationProperties.SetAutomationId(tile,"navigation-glyph-"+i);row.Children.Add(tile);
            var text=new TextBlock{Text=Names[i],FontSize=13.5,Margin=new Thickness(2,0,0,0),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};Grid.SetColumn(text,1);row.Children.Add(text);
            text.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(ListBoxItem),1)});
            var item=new ListBoxItem{Content=row,ToolTip=Names[i],Visibility=i==7?Visibility.Collapsed:Visibility.Visible};AutomationProperties.SetAutomationId(item,"settings-shell-category-"+i);AutomationProperties.SetName(item,Names[i]);_navigation.Items.Add(item);
        }
        if(!fullSettings)for(int i=0;i<8;i++){int section=i;_pages.Register(Keys[i],i,LegacyTitles[i],false,()=>BuildPage(section));}
        RegisterOverviewPages();
        if(!fullSettings){_pages.Register("material",0,"材质与边缘",true,BuildMaterial);_pages.Register("motion-parameters",2,"动画参数",true,BuildMotionParameters);_pages.Register("panel-proportions",7,"面板尺寸与间距",true,BuildPanelProportions);}
        var footer=new StackPanel{Margin=new Thickness(18,8,14,14)};var about=new Button{Content="关于 DockForWin",Style=(Style)FindResource("SettingsToolButton"),Width=double.NaN,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(6,0,6,0),Margin=new Thickness(-6,0,0,0),FontSize=12.5};about.Click+=(_,_)=>_pages.Navigate("about");AutomationProperties.SetAutomationId(about,"settings-about");
        footer.Children.Add(about);var version=new TextBlock{Text="版本 "+typeof(SettingsShellWindow).Assembly.GetName().Version?.ToString(3),FontSize=11,Margin=new Thickness(0,2,0,0)};version.SetResourceReference(TextBlock.ForegroundProperty,"SettingsTertiary");footer.Children.Add(version);
        _sidebarFooter=footer;Grid.SetRow(footer,3);sidebar.Children.Add(footer);

        // Content: toolbar (glass capsules and the title), pages, action bar.
        var right=_right=new Grid();right.RowDefinitions.Add(new(){Height=new GridLength(52)});right.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});right.RowDefinitions.Add(new(){Height=GridLength.Auto});var contentPane=ContentGlass(right);Grid.SetColumn(contentPane,1);root.Children.Add(contentPane);
        var toolbar=new Grid{Margin=new Thickness(10,0,12,0)};toolbar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});toolbar.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        // The sidebar toggle lives in the toolbar, as in Finder or Notes, so it stays reachable when the sidebar folds.
        var collapse=new Button{Style=(Style)FindResource("SettingsToolButton"),ToolTip="显示或隐藏边栏",Content=SidebarGlyph()};collapse.Click+=(_,_)=>ToggleSidebar();AutomationProperties.SetAutomationId(collapse,"settings-sidebar-collapse");AutomationProperties.SetName(collapse,"显示或隐藏边栏");WindowChrome.SetIsHitTestVisibleInChrome(collapse,true);
        _back=Arrow("返回",true,()=>_pages.Back());_forward=Arrow("前进",false,()=>_pages.Forward());
        var arrows=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,0)};
        arrows.Children.Add(GlassCapsule(collapse));arrows.Children.Add(GlassCapsule(_back,_forward).With(c=>c.Margin=new Thickness(8,0,0,0)));toolbar.Children.Add(arrows);
        Grid.SetColumn(_title,1);toolbar.Children.Add(_title);right.Children.Add(toolbar);
        Grid.SetRow(_pages,1);right.Children.Add(_pages);
        // Pages fade under the toolbar and above the action bar instead of being cut (CompleteSettingsShell swaps only the bar).
        InstallScrollEdges(_pages);
        var actions=new Grid{Margin=new Thickness(24,6,24,14)};actions.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});actions.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var note=new TextBlock{Text="更改先保存在草稿中",FontSize=12,VerticalAlignment=VerticalAlignment.Center};note.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");actions.Children.Add(note);var buttons=new StackPanel{Orientation=Orientation.Horizontal};
        buttons.Children.Add(Button("取消",Close,"settings-shell-cancel"));var reviewButton=Button("检查并保存…",()=>{var copy=_draft.Copy();copy.Normalize();int section=_pages.CurrentSection;Close();_review(copy,section);},"settings-shell-review");reviewButton.Style=(Style)FindResource("SettingsPrimaryButton");buttons.Children.Add(reviewButton);Grid.SetColumn(buttons,1);actions.Children.Add(buttons);Grid.SetRow(actions,2);right.Children.Add(actions);
        _pages.Navigated+=()=>{_syncNavigation=true;if(((ListBoxItem)_navigation.Items[NavigationIndex(_pages.CurrentSection)]).Visibility!=Visibility.Visible)_search.Clear();_navigation.SelectedIndex=NavigationIndex(_pages.CurrentSection);_syncNavigation=false;_title.Text=_pages.CurrentTitle;_back.IsEnabled=_pages.CanGoBack;_forward.IsEnabled=_pages.CanGoForward;foreach(var refresh in _availability)refresh();UpdateSelection(true);};
        _navigation.SelectionChanged+=(_,_)=>{RefreshNavigationGlyphs();if(_navigationReady&&!_syncNavigation&&_navigation.SelectedIndex>=0)_pages.Navigate(NavigationKeys[_navigation.SelectedIndex]);};
        _search.TextChanged+=(_,_)=>{placeholder.Visibility=_search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;FilterNavigation();};
        _navigationFrame.SizeChanged+=(_,_)=>UpdateSelection(false);_navigation.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler((_,_)=>UpdateSelection(false)));
        Loaded+=(_,_)=>UpdateSelection(false);PreviewKeyDown+=HandleNavigationKeys;
        ApplySettingsPalette(new Palette(_draft));_navigationReady=!fullSettings;if(!fullSettings)_pages.Navigate(SectionKey(initialPage));
    }
    protected void RegisterSettingsPage(string key,int section,string title,bool detail,Func<FrameworkElement> factory)=>_pages.Register(key,section,title,detail,factory);
    protected void NavigateSettingsPage(string key)=>_pages.Navigate(key);
    protected void SetSettingsSearch(int section,string text)=>_extraSearchTerms[NavigationIndex(section)]+=" "+text;
    protected void CompleteSettingsShell(FrameworkElement footer,int page)
    {
        if(footer.Parent is Panel parent)parent.Children.Remove(footer);
        _right.Children.RemoveAt(_right.Children.Count-1);Grid.SetRow(footer,2);footer.Margin=new Thickness(24,8,24,14);_right.Children.Add(footer);
        Content=_shellContent;_navigationReady=true;_pages.Navigate(SectionKey(page));
    }
    private static bool TransparencyEnabled()
    {
        try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("EnableTransparency") is not int value||value!=0;}catch{return true;}
    }
    // DWM: dark frame colours follow the palette and Windows 11 rounds the corners. The window draws its own
    // material (WallpaperBackdrop), so no system backdrop is requested; the canvas only shows before first paint.
    private void ApplyBackdrop()
    {
        var handle=new WindowInteropHelper(this).Handle;if(handle==0)return;
        int dark=Design.Dark?1:0;NativeMethods.DwmSetWindowAttribute(handle,20,ref dark,4);
        // The window is per-pixel transparent and draws its own outline, so DWM must not round or shadow it.
        Background=Brushes.Transparent;
    }
    private readonly List<System.Windows.Media.Effects.DropShadowEffect> _capsuleShadows=new();
    // Toolbar items sit in capsules of glass, as in macOS 26: a frosted face, the window's rim of light, a soft
    // shadow; two items share one capsule with a hairline between them.
    private Border GlassCapsule(params Button[] buttons)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        for(int i=0;i<buttons.Length;i++){
            if(i>0){var divider=new Border{Width=1,Height=14,Margin=new Thickness(1,0,1,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};divider.SetResourceReference(Border.BackgroundProperty,"SettingsLine");row.Children.Add(divider);}
            row.Children.Add(buttons[i]);
        }
        var capsule=new Border{CornerRadius=new CornerRadius(16),Height=32,Padding=new Thickness(2),BorderThickness=new Thickness(1),Child=row,VerticalAlignment=VerticalAlignment.Center};
        capsule.SetResourceReference(Border.BackgroundProperty,"SettingsGlassControl");capsule.SetResourceReference(Border.BorderBrushProperty,"SettingsGlassRim");
        var shadow=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=14,ShadowDepth=3,Direction=270,Color=Colors.Black,Opacity=Design.Dark?.32:.09};_capsuleShadows.Add(shadow);capsule.Effect=shadow;
        return capsule;
    }
    private Button Light(string name,string label,string id,Action action)
    {
        var glyph=new WindowControlGlyph(label,DesignSystem.Brush(label=="×"?DesignSystem.Hex("#99000000"):label=="−"?DesignSystem.Hex("#8C5A3A00"):DesignSystem.Hex("#80003300"))){RenderTransform=new ScaleTransform(.72,.72),RenderTransformOrigin=new Point(.5,.5)};
        var button=new Button{Content=glyph,Style=(Style)FindResource("SettingsTrafficButton"),ToolTip=name,Tag=label};AutomationProperties.SetName(button,name);AutomationProperties.SetAutomationId(button,id);WindowChrome.SetIsHitTestVisibleInChrome(button,true);button.Click+=(_,_)=>action();
        _lights.Add(button);PaintLight(button,true);return button;
    }
    // macOS colours when the window is key; quiet grey when it is not.
    private void PaintLights(bool active){foreach(var light in _lights)PaintLight(light,active);}
    private void PaintLight(Button light,bool active)
    {
        string label=(string)light.Tag;
        var (fill,edge)=!active?(Design.Inactive,DesignSystem.WithAlpha(Colors.Black,.08)):label=="×"?(DesignSystem.Hex("#FF5F57"),DesignSystem.Hex("#E0443E")):label=="−"?(DesignSystem.Hex("#FEBC2E"),DesignSystem.Hex("#DEA123")):(DesignSystem.Hex("#28C840"),DesignSystem.Hex("#1AAB29"));
        light.Background=DesignSystem.Brush(fill);light.BorderBrush=DesignSystem.Brush(edge);
    }
    private Button Arrow(string name,bool back,Action action)
    {
        var path=new System.Windows.Shapes.Path{Data=Geometry.Parse(back?"M6.5,1 L1.5,6.5 L6.5,12":"M1.5,1 L6.5,6.5 L1.5,12"),StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=8,Height=13,Stretch=Stretch.None};
        path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(Button),1)});
        var button=new Button{Content=path,Style=(Style)FindResource("SettingsToolButton")};AutomationProperties.SetAutomationId(button,back?"settings-shell-back":"settings-shell-forward");AutomationProperties.SetName(button,name);WindowChrome.SetIsHitTestVisibleInChrome(button,true);button.Click+=(_,_)=>action();return button;
    }
    private static FrameworkElement SidebarGlyph()
    {
        var path=new System.Windows.Shapes.Path{Data=Geometry.Parse("M3,1.5 H15 A1.5,1.5 0 0 1 16.5,3 V12 A1.5,1.5 0 0 1 15,13.5 H3 A1.5,1.5 0 0 1 1.5,12 V3 A1.5,1.5 0 0 1 3,1.5 Z M6.5,1.5 V13.5"),StrokeThickness=1.4,Width=18,Height=15,Stretch=Stretch.None};
        path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(Button),1)});
        return path;
    }
    private static ControlTemplate PlainTextBoxTemplate()
    {
        var host=new FrameworkElementFactory(typeof(ScrollViewer)){Name="PART_ContentHost"};host.SetValue(UIElement.FocusableProperty,false);host.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        var border=new FrameworkElementFactory(typeof(Border));border.SetBinding(Border.PaddingProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});border.SetValue(Border.BackgroundProperty,Brushes.Transparent);border.AppendChild(host);
        return new ControlTemplate(typeof(TextBox)){VisualTree=border};
    }
    private static Button Button(string title,Action action,string id)
    {var button=new Button{Content=title,Margin=new Thickness(8,0,0,0),MinWidth=72};AutomationProperties.SetAutomationId(button,id);button.Click+=(_,_)=>action();return button;}
    private void FilterNavigation()
    {
        string query=_search.Text.Trim();int first=-1;
        for(int i=0;i<Names.Length;i++){bool match=query.Length==0||(Names[i]+SearchTerms[i]+_extraSearchTerms[i]).Contains(query,StringComparison.CurrentCultureIgnoreCase);((ListBoxItem)_navigation.Items[i]).Visibility=match&&(i!=7||query.Length>0)?Visibility.Visible:Visibility.Collapsed;if(match&&first<0)first=i;}
        if(first>=0&&_navigation.SelectedItem is ListBoxItem selected&&selected.Visibility!=Visibility.Visible)_navigation.SelectedIndex=first;
        _noMatches.Visibility=first<0?Visibility.Visible:Visibility.Collapsed;
        UpdateSelection(false);
    }
    private void UpdateSelection(bool animate)
    {
        if(!IsLoaded||_navigation.SelectedItem is not ListBoxItem item||item.Visibility!=Visibility.Visible||item.ActualHeight<1){_selection.Visibility=Visibility.Hidden;return;}
        var point=item.TranslatePoint(new Point(),_navigationFrame);_selection.Visibility=Visibility.Visible;_selection.Margin=new Thickness(8,0,0,0);_selection.Width=Math.Max(0,_navigationFrame.ActualWidth-16);
        SettingsSwitchMotion.Move((TranslateTransform)_selection.RenderTransform,TranslateTransform.YProperty,point.Y,animate&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast ? .2 : 0);
    }
    private void HandleNavigationKeys(object sender,KeyEventArgs args)
    {
        if(args.Key==Key.F&&Keyboard.Modifiers==ModifierKeys.Control){_search.Focus();_search.SelectAll();args.Handled=true;}
        else if(args.SystemKey==Key.Left&&Keyboard.Modifiers==ModifierKeys.Alt){_pages.Back();args.Handled=true;}
        else if(args.SystemKey==Key.Right&&Keyboard.Modifiers==ModifierKeys.Alt){_pages.Forward();args.Handled=true;}
        else if(args.Key==Key.Escape){if(_search.Text.Length>0)_search.Clear();else if(_pages.IsDetail)_pages.Back();else return;args.Handled=true;}
    }
}

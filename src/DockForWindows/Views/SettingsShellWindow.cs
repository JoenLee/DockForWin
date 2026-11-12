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
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal partial class SettingsShellWindow:Window
{
    private readonly DockSettings _draft;
    private readonly Action<DockSettings,int> _review;
    private readonly SettingsPageHost _pages=new();
    private bool _navigationReady;private readonly string[] _extraSearchTerms=new string[8];
    private Grid _sidebar=null!;private ColumnDefinition _sidebarColumn=null!;private bool _collapsed;
    private FrameworkElement _shellContent=null!;private Grid _right=null!;
    protected DockSettings SharedDraft=>_draft;
    protected ListBox SidebarNavigation=>_navigation;
    protected SettingsPageHost PageHost=>_pages;
    private readonly ListBox _navigation=new(){Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
    private readonly TextBox _search=new();private readonly Grid _navigationFrame=new(){ClipToBounds=true};
    private readonly Border _selection=new(){Height=40,CornerRadius=new CornerRadius(10),Background=Palette.Brush("#E5F0F0"),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Hidden,RenderTransform=new TranslateTransform()};
    private readonly TextBlock _noMatches=new(){Text="未找到匹配的设置",FontSize=12,Margin=new Thickness(20,10,12,0),VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Collapsed};
    private readonly TextBlock _title=new(){FontSize=14,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};
    private readonly List<Action> _availability=new();private Button _back=null!,_forward=null!;private bool _syncNavigation;
    private static readonly string[] Names={"概览 Dashboard","控制中心","桌面小组件","外观与主题","动画与交互","性能与渲染","通用设置","关于应用"};
    protected static readonly string[] Keys={"appearance","icons","motion","apps","placement","system","interface","control-center"};
    protected static readonly string[] LegacyTitles={"外观与主题","图标与倒影","动画与交互","应用与文件","位置与隐藏","系统与启动","窗口外观","控制中心"};
    private static readonly string[] NavigationKeys={"dashboard","control-center","widgets","appearance","motion","performance","general","about"};
    private static readonly string[] SearchTerms={"概览 状态 内存 材质 常用","面板 模块 布局 缩放","桌面 小组件 天气 预报 城市 日历 日程 月历 时钟 提醒 待办 备忘录 笔记 音乐 播放 桌面 实例","底板 图标 倒影 颜色 材质 玻璃 阴影","波浪 弹跳 缓动","CPU GPU 内存 帧率 渲染","应用 文件 位置 隐藏 开机 启动 窗口 配色","版本 关于"};
    protected int CurrentSettingsSection=>_pages.CurrentSection;
    private static int NavigationIndex(int section)=>section switch{8=>0,7=>1,9=>2,0 or 1=>3,2=>4,10=>5,12=>7,_=>6};
    private static string SectionKey(int section)=>section switch{8=>"dashboard",9=>"widgets",10=>"performance",11=>"general",12=>"about",_=>Keys[Math.Clamp(section,0,7)]};
    internal SettingsShellWindow(DockSettings draft,Action<DockSettings,int> review,int initialPage=8) : this(draft,review,initialPage,false) {}
    internal SettingsShellWindow(DockSettings draft,Action<DockSettings,int> review,int initialPage,bool fullSettings)
    {
        _draft=draft.Copy();_review=review;
        Title="设置 · DockForWin · 界面预览";Icon=IosStyles.AppIcon();FontFamily=new FontFamily("Noto Sans SC, Microsoft YaHei UI, Segoe UI");FontSize=13;
        Width=Math.Min(980,SystemParameters.WorkArea.Width-32);Height=Math.Min(740,SystemParameters.WorkArea.Height-32);MinWidth=Math.Min(740,Width);MinHeight=Math.Min(540,Height);
        WindowStartupLocation=WindowStartupLocation.CenterOwner;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;Background=Brushes.White;UseLayoutRounding=true;SnapsToDevicePixels=true;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/DockForWindows;component/Views/SettingsShellStyles.xaml",UriKind.Relative)});
        WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=48,ResizeBorderThickness=new Thickness(6),CornerRadius=new CornerRadius(12),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
        SourceInitialized+=(_,_)=>{int light=0;var handle=new WindowInteropHelper(this).Handle;NativeMethods.DwmSetWindowAttribute(handle,20,ref light,4);if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)){int round=2;NativeMethods.DwmSetWindowAttribute(handle,33,ref round,4);}};
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/DockForWindows;component/Assets/Ionicons/SettingsIonicons.xaml",UriKind.Relative)});
        var root=new Grid();_sidebarColumn=new(){Width=new GridLength(240)};root.ColumnDefinitions.Add(_sidebarColumn);root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});AutomationProperties.SetAutomationId(root,"settings-shell-two-columns");
        _shellContent=new Border{BorderBrush=Palette.Brush("#D9D9DC"),BorderThickness=new Thickness(.7),CornerRadius=new CornerRadius(12),Child=root};Content=_shellContent;
        var sidebar=_sidebar=new Grid{Background=Palette.Brush("#FAFBFB")};
        sidebar.RowDefinitions.Add(new(){Height=new GridLength(58)});sidebar.RowDefinitions.Add(new(){Height=new GridLength(24)});sidebar.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});sidebar.RowDefinitions.Add(new(){Height=GridLength.Auto});root.Children.Add(sidebar);
        var brandHeader=new Grid{Margin=new Thickness(18,0,12,0)};brandHeader.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});brandHeader.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var identity=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};identity.Children.Add(new Image{Source=IosStyles.AppIcon(),Width=22,Height=22,Margin=new Thickness(0,0,9,0)});identity.Children.Add(new TextBlock{Text="DockForWin",FontSize=16,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});brandHeader.Children.Add(identity);
        var collapse=Button("‹",ToggleSidebar,"settings-sidebar-collapse");collapse.Background=Brushes.Transparent;collapse.BorderThickness=new Thickness(0);collapse.Width=30;collapse.FontSize=23;collapse.Padding=new Thickness(0);collapse.ToolTip="折叠或展开侧边栏";WindowChrome.SetIsHitTestVisibleInChrome(collapse,true);Grid.SetColumn(collapse,1);brandHeader.Children.Add(collapse);sidebar.Children.Add(brandHeader);
        var traffic=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,14,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top};
        traffic.Children.Add(Traffic("最大化或还原","+","#9CD58F",()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized));
        traffic.Children.Add(Traffic("最小化","−","#F3D58D",()=>WindowState=WindowState.Minimized));traffic.Children.Add(Traffic("关闭","×","#EBA19A",Close));
        var searchFrame=new Border{CornerRadius=new CornerRadius(11),Background=Palette.Brush("#DFFFFFFF"),BorderBrush=Palette.Brush("#0C000000"),BorderThickness=new Thickness(1),Height=34,MaxWidth=430,Margin=new Thickness(4,0,22,0),VerticalAlignment=VerticalAlignment.Center};
        var searchGrid=new Grid();_search.Background=Brushes.Transparent;_search.BorderThickness=new Thickness(0);_search.Padding=new Thickness(25,5,7,5);AutomationProperties.SetAutomationId(_search,"settings-shell-search");AutomationProperties.SetName(_search,"搜索设置分类");
        var placeholder=new TextBlock{Text="搜索设置、小组件…",Foreground=Palette.Brush("#8D8D94"),FontSize=12,Margin=new Thickness(25,0,0,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};
        var magnifier=new System.Windows.Shapes.Path{Data=Geometry.Parse("M8,2 A5,5 0 1 1 7.99,2 M11.5,11.5 L15,15"),Stroke=Palette.Brush("#8D8D94"),StrokeThickness=1.5,Width=14,Height=14,Stretch=Stretch.Uniform,Margin=new Thickness(8,0,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};
        searchGrid.Children.Add(_search);searchGrid.Children.Add(placeholder);searchGrid.Children.Add(magnifier);searchFrame.SetResourceReference(Border.BackgroundProperty,"SettingsField");searchFrame.Child=searchGrid;
        AutomationProperties.SetName(_search,"全局搜索设置与小组件");WindowChrome.SetIsHitTestVisibleInChrome(searchFrame,true);
        _search.GotKeyboardFocus+=(_,_)=>searchFrame.SetResourceReference(Border.BorderBrushProperty,"SettingsAccent");
        _search.LostKeyboardFocus+=(_,_)=>searchFrame.SetResourceReference(Border.BorderBrushProperty,"SettingsLine");
        _navigationFrame.Children.Add(_selection);_navigationFrame.Children.Add(_navigation);_navigationFrame.Children.Add(_noMatches);AutomationProperties.SetAutomationId(_noMatches,"settings-shell-search-empty");Grid.SetRow(_navigationFrame,2);sidebar.Children.Add(_navigationFrame);
        _navigation.ItemContainerStyle=(Style)FindResource("SettingsNavigationItem");ScrollViewer.SetHorizontalScrollBarVisibility(_navigation,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(_navigation,ScrollBarVisibility.Auto);
        KeyboardNavigation.SetTabNavigation(_navigation,KeyboardNavigationMode.Once);AutomationProperties.SetAutomationId(_navigation,fullSettings?"settings-navigation":"settings-shell-navigation");
        int[] glyphIndices={1,7,1,0,2,4,5,6};
        for(int i=0;i<Names.Length;i++){
            var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(28)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
            var image=(DrawingImage)FindResource("SettingsIcon-"+glyphIndices[i]);
            var glyph=new System.Windows.Shapes.Path{Data=NavigationGeometry(image.Drawing),Width=18,Height=18,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left,IsHitTestVisible=false};AutomationProperties.SetAutomationId(glyph,"navigation-glyph-"+i);
            glyph.Style=(Style)FindResource("SettingsNavigationGlyph");row.Children.Add(glyph);
            var text=new TextBlock{Text=Names[i],FontSize=13,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};Grid.SetColumn(text,1);row.Children.Add(text);
            text.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(ListBoxItem),1)});
            var item=new ListBoxItem{Content=row,ToolTip=Names[i],Visibility=i==7?Visibility.Collapsed:Visibility.Visible};AutomationProperties.SetAutomationId(item,"settings-shell-category-"+i);AutomationProperties.SetName(item,Names[i]);_navigation.Items.Add(item);
        }
        if(!fullSettings)for(int i=0;i<8;i++){int section=i;_pages.Register(Keys[i],i,LegacyTitles[i],false,()=>BuildPage(section));}
        RegisterOverviewPages();
        if(!fullSettings){_pages.Register("material",0,"材质与边缘",true,BuildMaterial);_pages.Register("motion-parameters",2,"动画参数",true,BuildMotionParameters);_pages.Register("panel-proportions",7,"面板尺寸与间距",true,BuildPanelProportions);}
        var brand=new StackPanel{Margin=new Thickness(20,18,12,18)};var about=Button("关于应用",()=>_pages.Navigate("about"),"settings-about");about.HorizontalAlignment=HorizontalAlignment.Left;about.Padding=new Thickness(0,4,0,4);about.Margin=new Thickness(0);about.Background=Brushes.Transparent;about.BorderThickness=new Thickness(0);brand.Children.Add(about);brand.Children.Add(new TextBlock{Text="v"+typeof(SettingsShellWindow).Assembly.GetName().Version?.ToString(3),FontSize=11,Foreground=Palette.Brush("#8C8C93"),Margin=new Thickness(0,3,0,0)});Grid.SetRow(brand,3);sidebar.Children.Add(brand);
        var right=_right=new Grid{Background=Palette.Brush("#FFFFFF")};right.RowDefinitions.Add(new(){Height=new GridLength(52)});right.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});right.RowDefinitions.Add(new(){Height=GridLength.Auto});Grid.SetColumn(right,1);root.Children.Add(right);
        var header=new Grid{Background=Palette.Brush("#FFFFFF")};header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});Grid.SetColumn(traffic,2);header.Children.Add(traffic);
        var arrows=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(16,0,12,0),VerticalAlignment=VerticalAlignment.Center};_back=Arrow("返回",true,()=>_pages.Back());_forward=Arrow("前进",false,()=>_pages.Forward());arrows.Children.Add(_back);arrows.Children.Add(_forward);header.Children.Add(arrows);_title.Visibility=Visibility.Collapsed;Grid.SetColumn(_title,1);header.Children.Add(_title);Grid.SetColumn(searchFrame,1);header.Children.Add(searchFrame);right.Children.Add(header);
        Grid.SetRow(_pages,1);right.Children.Add(_pages);
        var footer=new Grid{Margin=new Thickness(22,10,22,14)};footer.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});footer.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        footer.Children.Add(new TextBlock{Text="更改保留在草稿中",FontSize=11,Foreground=Palette.Brush("#818188"),VerticalAlignment=VerticalAlignment.Center});var buttons=new StackPanel{Orientation=Orientation.Horizontal};
        buttons.Children.Add(Button("取消",Close,"settings-shell-cancel"));var reviewButton=Button("检查并保存…",()=>{var copy=_draft.Copy();copy.Normalize();int section=_pages.CurrentSection;Close();_review(copy,section);},"settings-shell-review");reviewButton.Background=Palette.Brush("#00575B");reviewButton.Foreground=Brushes.White;reviewButton.BorderBrush=Brushes.Transparent;buttons.Children.Add(reviewButton);Grid.SetColumn(buttons,1);footer.Children.Add(buttons);Grid.SetRow(footer,2);right.Children.Add(footer);
        _pages.Navigated+=()=>{_syncNavigation=true;if(((ListBoxItem)_navigation.Items[NavigationIndex(_pages.CurrentSection)]).Visibility!=Visibility.Visible)_search.Clear();_navigation.SelectedIndex=NavigationIndex(_pages.CurrentSection);_syncNavigation=false;_title.Text=_pages.CurrentTitle;_back.IsEnabled=_pages.CanGoBack;_forward.IsEnabled=_pages.CanGoForward;foreach(var refresh in _availability)refresh();UpdateSelection(true);};
        _navigation.SelectionChanged+=(_,_)=>{if(_navigationReady&&!_syncNavigation&&_navigation.SelectedIndex>=0)_pages.Navigate(NavigationKeys[_navigation.SelectedIndex]);};
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
    private Button Traffic(string name,string label,string color,Action action)
    {
        bool close=label=="×",minimize=label=="−";
        var glyph=new WindowControlGlyph(label,Palette.Brush(close?"#A80B05":minimize?"#976B00":"#177D13"));
        var button=new Button{Content=glyph,Background=Palette.Brush(color),Style=(Style)FindResource("SettingsTrafficButton"),ToolTip=name};AutomationProperties.SetName(button,name);AutomationProperties.SetAutomationId(button,"settings-shell-window-"+(close?"close":minimize?"minimize":"maximize"));WindowChrome.SetIsHitTestVisibleInChrome(button,true);button.Click+=(_,_)=>action();return button;
    }
    private Button Arrow(string name,bool back,Action action)
    {var button=Button("",action,back?"settings-shell-back":"settings-shell-forward");button.Width=30;button.MinHeight=28;button.Padding=new Thickness(6);button.Margin=new Thickness(0,0,3,0);button.BorderThickness=new Thickness(0);button.Background=Brushes.Transparent;button.Content=new System.Windows.Shapes.Path{Data=Geometry.Parse(back?"M10,3 L5,8 L10,13":"M5,3 L10,8 L5,13"),Stroke=Palette.Brush("#626269"),StrokeThickness=1.7,Width=12,Height=14,Stretch=Stretch.Uniform};AutomationProperties.SetName(button,name);WindowChrome.SetIsHitTestVisibleInChrome(button,true);return button;}
    private static Button Button(string title,Action action,string id)
    {var button=new Button{Content=title,Margin=new Thickness(6,0,0,0)};AutomationProperties.SetAutomationId(button,id);button.Click+=(_,_)=>action();return button;}
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
        SettingsSwitchMotion.Move((TranslateTransform)_selection.RenderTransform,TranslateTransform.YProperty,point.Y,animate&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast ? .18 : 0);
    }
    private void HandleNavigationKeys(object sender,KeyEventArgs args)
    {
        if(args.Key==Key.F&&Keyboard.Modifiers==ModifierKeys.Control){_search.Focus();_search.SelectAll();args.Handled=true;}
        else if(args.SystemKey==Key.Left&&Keyboard.Modifiers==ModifierKeys.Alt){_pages.Back();args.Handled=true;}
        else if(args.SystemKey==Key.Right&&Keyboard.Modifiers==ModifierKeys.Alt){_pages.Forward();args.Handled=true;}
        else if(args.Key==Key.Escape){if(_search.Text.Length>0)_search.Clear();else if(_pages.IsDetail)_pages.Back();else return;args.Handled=true;}
    }
}

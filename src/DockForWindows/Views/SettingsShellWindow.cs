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

// Floating white sidebar and compact navigation over the existing settings pages.
internal partial class SettingsShellWindow:Window
{
    private readonly DockSettings _draft;
    private readonly Action<DockSettings,int> _review;
    private readonly SettingsPageHost _pages=new();
    private bool _navigationReady;private readonly string[] _extraSearchTerms=new string[8];
    private Grid _sidebar=null!;private ColumnDefinition _sidebarColumn=null!;private bool _collapsed;
    private FrameworkElement _shellContent=null!;private Grid _right=null!;
    private Border _searchFrame=null!;private StackPanel _detailBar=null!;private FrameworkElement _sidebarFooter=null!;
    protected DockSettings SharedDraft=>_draft;
    protected ListBox SidebarNavigation=>_navigation;
    protected SettingsPageHost PageHost=>_pages;
    private readonly ListBox _navigation=new(){Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
    private readonly TextBox _search=new();private readonly Grid _navigationFrame=new(){ClipToBounds=true};
    private readonly Border _selection=new(){Height=36,CornerRadius=new CornerRadius(10),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Hidden,RenderTransform=new TranslateTransform()};
    private readonly TextBlock _noMatches=new(){Text=LocalizedText.Get("没有匹配的设置"),FontSize=12.5,Margin=new Thickness(18,10,12,0),VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Collapsed};
    private readonly TextBlock _title=new(){FontFamily=DesignSystem.DisplayFont,FontSize=15,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
    private readonly List<Action> _availability=new();private Button _back=null!,_forward=null!;private bool _syncNavigation;
    private static readonly string[] Names={LocalizedText.Get("概览"),LocalizedText.Get("控制中心"),LocalizedText.Get("桌面小组件"),LocalizedText.Get("外观与主题"),LocalizedText.Get("动画与交互"),LocalizedText.Get("性能与渲染"),LocalizedText.Get("通用设置"),LocalizedText.Get("关于")};
    // Icon tile per category: colour index into DesignSystem.TileColors and an Ionicons glyph.
    private static readonly (int Color,string Glyph)[] Tiles={(0,"Grid"),(1,"Options"),(2,"Calendar"),(3,"Palette"),(4,"Sparkles"),(5,"Performance"),(6,"Settings"),(7,"Desktop")};
    protected static readonly string[] Keys={"appearance","icons","motion","apps","placement","system","interface","control-center"};
    protected static readonly string[] LegacyTitles={LocalizedText.Get("外观与主题"),LocalizedText.Get("图标与倒影"),LocalizedText.Get("动画与交互"),LocalizedText.Get("应用与文件"),LocalizedText.Get("位置与隐藏"),LocalizedText.Get("系统与启动"),LocalizedText.Get("窗口外观"),LocalizedText.Get("控制中心")};
    private static readonly string[] NavigationKeys={"dashboard","control-center","widgets","appearance","motion","performance","general","about"};
    private static readonly string[] SearchTerms={LocalizedText.Get("概览 Dashboard 状态 材质 常用"),LocalizedText.Get("面板 模块 布局 缩放"),LocalizedText.Get("桌面 小组件 天气 预报 城市 日历 日程 月历 时钟 提醒 待办 备忘录 笔记 音乐 播放 桌面 实例"),LocalizedText.Get("底板 图标 倒影 颜色 材质 玻璃 阴影"),LocalizedText.Get("波浪 弹跳 缓动"),LocalizedText.Get("CPU GPU 内存 帧率 渲染"),LocalizedText.Get("应用 文件 位置 隐藏 开机 启动 窗口 配色"),LocalizedText.Get("版本 关于")};
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
        _draft=draft.Copy();_review=review;_pages.HeaderInset=TabBarHeight;_pages.MotionEnabled=()=>_draft.Animations&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;
        Title=LocalizedText.Get("设置 · DockForWin");Icon=IosStyles.AppIcon();FontFamily=DesignSystem.TextFont;FontSize=13.5;
        // A wide, flat card in the manner of a music player's mini window: preview on the left, choices on the right.
        Width=Math.Min(1080,SystemParameters.WorkArea.Width-48);Height=Math.Min(700,SystemParameters.WorkArea.Height-48);MinWidth=Math.Min(1000,Width);MinHeight=Math.Min(520,Height);
        WindowStartupLocation=WindowStartupLocation.CenterOwner;WindowStyle=WindowStyle.None;AllowsTransparency=!ComposedShell;Background=Brushes.Transparent;ResizeMode=ResizeMode.CanResize;UseLayoutRounding=true;SnapsToDevicePixels=true;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/DockForWindows;component/Views/SettingsShellStyles.xaml",UriKind.Relative)});
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/DockForWindows;component/Assets/Ionicons/SettingsIonicons.xaml",UriKind.Relative)});
        // Keep native caption/resize hit testing; WPF alpha coverage smooths the custom corner instead of a hard HRGN.
        // With composed transparency there is no WindowChrome: it cannot run on the WS_POPUP window that transparency
        // needs, so SettingsShellWindow.Composition answers the same caption and edge hit tests itself.
        if(!ComposedShell)WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=TabBarHeight,ResizeBorderThickness=new Thickness(ResizeBorder),CornerRadius=new CornerRadius(0),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
        SourceInitialized+=(_,_)=>{ApplyBackdrop();InstallComposition();};KeyboardCue.Track(this);

        var root=_right=new Grid();AutomationProperties.SetAutomationId(root,"settings-shell-two-columns");
        root.RowDefinitions.Add(new(){Height=new GridLength(0)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var shell=new Grid();shell.SetResourceReference(Panel.BackgroundProperty,"SettingsCanvas");
        shell.SizeChanged+=(_,_)=>UpdateShellOutline(shell);
        void RefreshOutline()=>Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render,()=>UpdateShellOutline(shell));
        Loaded+=(_,_)=>RefreshOutline();StateChanged+=(_,_)=>RefreshOutline();DpiChanged+=(_,_)=>RefreshOutline();IsVisibleChanged+=(_,_)=>{if(IsVisible)RefreshOutline();};
        shell.RowDefinitions.Add(new(){Height=new GridLength(TabBarHeight)});shell.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        _sidebarColumn=new ColumnDefinition{Width=new GridLength(204)};shell.ColumnDefinitions.Add(_sidebarColumn);shell.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        Grid.SetRow(root,0);Grid.SetRowSpan(root,2);Grid.SetColumn(root,1);shell.Children.Add(root);_shellContent=shell;Content=shell;
        var toolbar=BuildTabBar();Panel.SetZIndex(toolbar,10);Grid.SetColumnSpan(toolbar,2);shell.Children.Add(toolbar);
        _sidebar=BuildSidebar();Grid.SetRow(_sidebar,0);Grid.SetRowSpan(_sidebar,2);Panel.SetZIndex(_sidebar,11);shell.Children.Add(_sidebar);


        // Detail pages: a quiet way back above the page.
        _detailBar=new StackPanel{Visibility=Visibility.Collapsed};
        Grid.SetRow(_pages,2);root.Children.Add(_pages);
        // Content scrolls underneath the straight-edged, softly blurred toolbar.

        _pages.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler((_,e)=>{if(e.OriginalSource is FrameworkElement source&&source.Parent==_pages&&e.VerticalChange!=0)UpdateTabLine(true);}));

        // The former sidebar stays as an invisible index of the categories: search and keyboard routes still use it.
        var sidebar=new Grid{Visibility=Visibility.Collapsed};_searchFrame=new Border();_sidebarFooter=new StackPanel();
        _navigationFrame.Children.Add(_selection);_navigationFrame.Children.Add(_navigation);_navigationFrame.Children.Add(_noMatches);sidebar.Children.Add(_navigationFrame);sidebar.Children.Add(_search);root.Children.Add(sidebar);
        AutomationProperties.SetAutomationId(_noMatches,"settings-shell-search-empty");AutomationProperties.SetAutomationId(_search,"settings-shell-search");
        AutomationProperties.SetAutomationId(_navigation,fullSettings?"settings-navigation":"settings-shell-navigation");
        for(int i=0;i<Names.Length;i++){
            var item=new ListBoxItem{Content=Names[i],ToolTip=Names[i],Visibility=i==7?Visibility.Collapsed:Visibility.Visible};AutomationProperties.SetAutomationId(item,"settings-shell-category-"+i);AutomationProperties.SetName(item,Names[i]);_navigation.Items.Add(item);
        }
        if(!fullSettings)for(int i=0;i<8;i++){int section=i;_pages.Register(Keys[i],i,LegacyTitles[i],false,()=>BuildPage(section));}
        RegisterOverviewPages();
        if(!fullSettings){_pages.Register("material",0,LocalizedText.Get("材质与边缘"),true,BuildMaterial);_pages.Register("motion-parameters",2,LocalizedText.Get("动画参数"),true,BuildMotionParameters);_pages.Register("panel-proportions",7,LocalizedText.Get("面板尺寸与间距"),true,BuildPanelProportions);}

        // Footer (the full settings window swaps in its own status line, see CompleteSettingsShell).
        var actions=new Grid{Margin=new Thickness(24,6,24,14)};actions.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});actions.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var note=new TextBlock{Text=LocalizedText.Get("更改先保存在草稿中"),FontSize=12,VerticalAlignment=VerticalAlignment.Center};note.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");actions.Children.Add(note);var buttons=new StackPanel{Orientation=Orientation.Horizontal};
        buttons.Children.Add(Button(LocalizedText.Get("取消"),Close,"settings-shell-cancel"));var reviewButton=Button(LocalizedText.Get("检查并保存…"),()=>{var copy=_draft.Copy();copy.Normalize();int section=_pages.CurrentSection;Close();_review(copy,section);},"settings-shell-review");reviewButton.Style=(Style)FindResource("SettingsPrimaryButton");buttons.Children.Add(reviewButton);Grid.SetColumn(buttons,1);actions.Children.Add(buttons);Grid.SetRow(actions,3);root.Children.Add(actions);
        _pages.Navigated+=()=>{_syncNavigation=true;_navigation.SelectedIndex=NavigationIndex(_pages.CurrentSection);_syncNavigation=false;_title.Text=_pages.CurrentTitle;_back.IsEnabled=_pages.CanGoBack;_forward.IsEnabled=_pages.CanGoForward;foreach(var refresh in _availability)refresh();SelectTab(TabFor(_pages.CurrentSection),true);UpdateTabLine(false);};
        _navigation.SelectionChanged+=(_,_)=>{if(_navigationReady&&!_syncNavigation&&_navigation.SelectedIndex>=0)_pages.Navigate(NavigationKeys[_navigation.SelectedIndex]);};
        _search.TextChanged+=(_,_)=>FilterNavigation();
        Loaded+=(_,_)=>SelectTab(TabFor(_pages.CurrentSection),false);PreviewKeyDown+=HandleNavigationKeys;
        ApplySettingsPalette(new Palette(_draft));_navigationReady=!fullSettings;if(!fullSettings)_pages.Navigate(SectionKey(initialPage));
    }
    protected void RegisterSettingsPage(string key,int section,string title,bool detail,Func<FrameworkElement> factory)=>_pages.Register(key,section,title,detail,factory);
    protected void NavigateSettingsPage(string key)=>_pages.Navigate(key);
    protected void SetSettingsSearch(int section,string text)=>_extraSearchTerms[NavigationIndex(section)]+=" "+text;
    protected void CompleteSettingsShell(FrameworkElement footer,int page)
    {
        if(footer.Parent is Panel parent)parent.Children.Remove(footer);
        _right.Children.RemoveAt(_right.Children.Count-1);Grid.SetRow(footer,3);footer.Margin=new Thickness(DesignSystem.PageInset,0,DesignSystem.PageInset,12);_right.Children.Add(footer);
        Content=_shellContent;_navigationReady=true;_pages.Navigate(SectionKey(page==8?0:page));
        // Once the window is up, build the other tabs one per idle moment so the first switch to each is instant.
        ContentRendered+=(_,_)=>{foreach(var (_,key) in Tabs){string k=key;Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,()=>{if(IsLoaded)_pages.Prewarm(new[]{k});});}};
    }
    // Suppress native borders; the content owns the smooth outer silhouette.
    private void ApplyBackdrop()
    {
        var handle=new WindowInteropHelper(this).Handle;if(handle==0)return;
        int dark=Design.Dark?1:0;NativeMethods.DwmSetWindowAttribute(handle,20,ref dark,4);
        int round=1;NativeMethods.DwmSetWindowAttribute(handle,33,ref round,4);
        int border=unchecked((int)0xFFFFFFFE);NativeMethods.DwmSetWindowAttribute(handle,34,ref border,4);
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

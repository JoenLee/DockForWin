using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace DockForWindows.Views;

internal partial class SettingsShellWindow
{
    internal const double TabBarHeight=52;
    private static readonly (string Title,string Key)[] Tabs={(LocalizedText.Get("程序坞"),"appearance"),(LocalizedText.Get("控制中心"),"control-center"),(LocalizedText.Get("小组件"),"widgets"),(LocalizedText.Get("通用"),"general")};
    private readonly List<(Button Button,TextBlock Text)> _tabs=new();
    private Button? _sidebarToggle;
    private bool _permanentClose;
    private WindowState _restoreState=WindowState.Normal;
    private readonly StackPanel _toolbarActions=new(){Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};
    private System.Windows.Shapes.Rectangle? _toolbarBlur;
    protected void SetToolbarActions(Button cancel,Button save){_toolbarActions.Children.Clear();_toolbarActions.Children.Add(cancel);_toolbarActions.Children.Add(save);foreach(var button in new[]{cancel,save})WindowChrome.SetIsHitTestVisibleInChrome(button,true);}
    private static int TabFor(int section)=>section switch{>=0 and <=4=>0,7=>1,9=>2,_=>3};

    private FrameworkElement BuildTabBar()
    {
        var bar=new Grid();bar.SetResourceReference(Panel.BackgroundProperty,"SettingsCanvas");
        bar.ColumnDefinitions.Add(new());bar.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        System.Windows.Data.BindingOperations.SetBinding(bar.ColumnDefinitions[0],ColumnDefinition.WidthProperty,new System.Windows.Data.Binding("Width"){Source=_sidebarColumn});
        var header=new Grid{ClipToBounds=true,IsHitTestVisible=false};
        var sample=new VisualBrush(_pages){ViewboxUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top};
        // The page under the bar is sampled and blurred at half resolution: the blur radius is in DIPs, so it looks
        // the same (and shows through the 82% tint at half opacity), but each scrolled frame touches a quarter of the pixels.
        _toolbarBlur=new System.Windows.Shapes.Rectangle{Fill=sample,Opacity=0,Effect=new System.Windows.Media.Effects.BlurEffect{Radius=10,KernelType=System.Windows.Media.Effects.KernelType.Gaussian}};
        var blurLayer=new Grid{CacheMode=new BitmapCache(.5)};blurLayer.Children.Add(_toolbarBlur);header.Children.Add(blurLayer);
        var tint=new Border{Opacity=.82};tint.SetResourceReference(Border.BackgroundProperty,"SettingsCanvas");header.Children.Add(tint);
        var edge=new Border{Height=1,VerticalAlignment=VerticalAlignment.Bottom};edge.SetResourceReference(Border.BackgroundProperty,"SettingsLine");header.Children.Add(edge);
        header.SizeChanged+=(_,_)=>sample.Viewbox=new Rect(0,0,Math.Max(1,header.ActualWidth),TabBarHeight);Grid.SetColumn(header,1);bar.Children.Add(header);
        var navigation=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0)};
        _back=Arrow(LocalizedText.Get("返回"),true,()=>_pages.Back());_forward=Arrow(LocalizedText.Get("前进"),false,()=>_pages.Forward());
        _back.Width=_forward.Width=28;_back.Height=_forward.Height=22;
        _back.Template=NavigationButtonTemplate();_forward.Template=NavigationButtonTemplate();
        navigation.Children.Add(_back);navigation.Children.Add(_forward);navigation.Margin=new Thickness(0);
        var capsule=new SquircleBorder{Child=navigation,Exponent=2.5,CornerRadius=new CornerRadius(8),Height=26,Padding=new Thickness(2),Margin=new Thickness(14,0,0,0),BorderThickness=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
        capsule.SetResourceReference(Border.BackgroundProperty,"SettingsControlFill");
        var shadow=new System.Windows.Shapes.Path{Margin=capsule.Margin,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=10,ShadowDepth=3,Direction=270,Opacity=.13}};
        shadow.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,"SettingsControlFill");capsule.SizeChanged+=(_,_)=>{shadow.Width=capsule.ActualWidth;shadow.Height=capsule.ActualHeight;shadow.Data=SquircleBorder.Outline(capsule.RenderSize,8,2.5);};
        var shadowLayer=new Grid{IsHitTestVisible=false,CacheMode=new BitmapCache{SnapsToDevicePixels=true}};shadowLayer.Children.Add(shadow);
        AutomationProperties.SetAutomationId(capsule,"settings-navigation-capsule");Grid.SetColumn(shadowLayer,1);bar.Children.Add(shadowLayer);Grid.SetColumn(capsule,1);bar.Children.Add(capsule);
        var controls=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,18,0)};
        controls.Children.Add(_toolbarActions);
        controls.Children.Add(Traffic(LocalizedText.Get("最小化"),"minimize","−","#F7D789","#93600C",HideToTray));
        controls.Children.Add(Traffic(LocalizedText.Get("最大化或还原"),"maximize","+","#9CD591","#276C28",()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized));
        controls.Children.Add(Traffic(LocalizedText.Get("关闭"),"close","×","#F6A099","#A32E29",HideToTray));
        Grid.SetColumn(controls,1);bar.Children.Add(controls);
        Closing+=(_,e)=>{if(!_permanentClose){e.Cancel=true;HideToTray();}};
        StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized)HideToTray();else _restoreState=WindowState;};
        return bar;
    }
    private ControlTemplate NavigationButtonTemplate()
    {
        var border=new FrameworkElementFactory(typeof(SquircleBorder)){Name="Body"};border.SetValue(SquircleBorder.ExponentProperty,2.5);
        border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);
        var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);
        var template=new ControlTemplate(typeof(Button)){VisualTree=border};
        var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("SettingsHover"),"Body"));template.Triggers.Add(hover);
        var pressed=new Trigger{Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("SettingsPressed"),"Body"));template.Triggers.Add(pressed);
        var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.3));template.Triggers.Add(disabled);return template;
    }
    private Grid BuildSidebar()
    {
        var sidebar=new Grid{Margin=new Thickness(SidebarInset)};
        AddFloatingSidebarMaterial(sidebar);
        var layout=new Grid();layout.RowDefinitions.Add(new(){Height=new GridLength(44)});layout.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});sidebar.Children.Add(layout);
        var toggleHost=new Grid{Width=64,HorizontalAlignment=HorizontalAlignment.Left};layout.Children.Add(toggleHost);
        _sidebarToggle=new Button{Content=SidebarGlyph(),Style=(Style)FindResource("SettingsToolButton"),ToolTip=LocalizedText.Get("收起侧边栏"),Width=32,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        _sidebarToggle.SetResourceReference(Control.ForegroundProperty,"SettingsFloatingSidebarInk");
        AutomationProperties.SetAutomationId(_sidebarToggle,"settings-sidebar-toggle");AutomationProperties.SetName(_sidebarToggle,LocalizedText.Get("收起侧边栏"));
        _sidebarToggle.Click+=(_,_)=>ToggleSidebar();WindowChrome.SetIsHitTestVisibleInChrome(_sidebarToggle,true);toggleHost.Children.Add(_sidebarToggle);
        var rows=new StackPanel{Margin=new Thickness(6,8,6,8)};Grid.SetRow(rows,1);layout.Children.Add(rows);
        string[] glyphs={"Desktop","Options","Grid","Settings"};
        for(int i=0;i<Tabs.Length;i++){
            int index=i;var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(32)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
            var icon=new ControlCenterIcon(glyphs[i]){Width=20,Height=20,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};icon.SetResourceReference(ControlCenterIcon.BrushProperty,"SettingsFloatingSidebarInk");row.Children.Add(icon);
            var text=new TextBlock{Text=Tabs[i].Title,FontSize=14,VerticalAlignment=VerticalAlignment.Center};text.Margin=new Thickness(8,0,0,0);Grid.SetColumn(text,1);row.Children.Add(text);
            var button=new Button{Content=row,Height=42,Padding=new Thickness(10,0,10,0),Margin=new Thickness(0,0,0,5),Template=SidebarTemplate(8),HorizontalContentAlignment=HorizontalAlignment.Left,ToolTip=Tabs[i].Title};
            AutomationProperties.SetAutomationId(button,"settings-tab-"+Tabs[i].Key);AutomationProperties.SetName(button,Tabs[i].Title);
            button.Click+=(_,_)=>NavigateTab(index);rows.Children.Add(button);_tabs.Add((button,text));
        }
        return sidebar;
    }
    protected static ControlTemplate SidebarTemplate(double? radius=null)
    {
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(radius??Math.Max(0,SidebarRadius-6)));
        border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        border.SetBinding(Border.PaddingProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)){VisualTree=border};
    }
    private Button Traffic(string name,string id,string glyph,string fill,string edge,Action action)
    {
        var button=new Button{Style=(Style)FindResource("SettingsTrafficButton"),Background=DesignSystem.Brush(DesignSystem.Hex(fill)),BorderBrush=DesignSystem.Brush(DesignSystem.Hex(edge)),Content=new WindowControlGlyph(glyph,DesignSystem.Brush(DesignSystem.Hex(edge))),ToolTip=name,Margin=new Thickness(2,0,2,0)};
        AutomationProperties.SetAutomationId(button,"settings-shell-window-"+id);AutomationProperties.SetName(button,name);WindowChrome.SetIsHitTestVisibleInChrome(button,true);var scale=new ScaleTransform(1,1);button.RenderTransform=scale;button.RenderTransformOrigin=new Point(.5,.5);
        void Hover(double target){double seconds=_draft.Animations&&SystemParameters.ClientAreaAnimation ? .12 : 0;foreach(var property in new[]{ScaleTransform.ScaleXProperty,ScaleTransform.ScaleYProperty})scale.BeginAnimation(property,new System.Windows.Media.Animation.DoubleAnimation(target,TimeSpan.FromSeconds(seconds)){EasingFunction=new System.Windows.Media.Animation.CubicEase{EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut}});}
        button.MouseEnter+=(_,_)=>Hover(1.12);button.MouseLeave+=(_,_)=>Hover(1);button.Click+=(_,_)=>action();return button;
    }
    internal void HideToTray(){if(WindowState!=WindowState.Minimized)_restoreState=WindowState;Hide();}
    internal void RestoreFromTray(){var state=_restoreState;WindowState=state;Show();DockForWindows.Services.NativeMethods.ShowWindowAsync(new System.Windows.Interop.WindowInteropHelper(this).Handle,state==WindowState.Maximized?3:9);Activate();Focus();}
    internal void ClosePermanently(){_permanentClose=true;Close();}
    private void NavigateTab(int index){if(_pages.CurrentKey!=Tabs[index].Key)_pages.Navigate(Tabs[index].Key);}
    private void SelectTab(int index,bool animate)
    {
        for(int i=0;i<_tabs.Count;i++){
            var (button,text)=_tabs[i];bool selected=i==index;
            button.SetResourceReference(Control.BackgroundProperty,selected?"SettingsFloatingSidebarSelection":"SettingsNavChip");
            text.SetResourceReference(TextBlock.ForegroundProperty,"SettingsFloatingSidebarInk");text.FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal;
        }
    }
    private void UpdateTabLine(bool animate){_pages.OpacityMask=null;var page=_pages.Children.OfType<FrameworkElement>().FirstOrDefault(p=>p.Visibility==Visibility.Visible);if(_toolbarBlur is not null)_toolbarBlur.Opacity=page is ScrollViewer scroll&&scroll.VerticalOffset>0?.5:0;}
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using Forms=System.Windows.Forms;

namespace DockForWindows.Views;

// Layout and lifetime only; the original Dock rendering engine is shared unchanged.
public sealed partial class ControlCenterWindow : Window,IGlassBackdropHost
{
    private sealed class Card
    {
        public ControlModule Kind;public Grid Root=new(){Background=Brushes.Transparent};public DockSurface Surface=new();public Grid Body=new();public Border? Holder;public Button? Primary,EditBadge;public TextBlock Status=new();public TextBlock? Title;
        public double SymbolFeedbackAt;public TranslateTransform Enter=new();public ScaleTransform Stretch=new(1,1);public Rect Target;public TranslateTransform Position=new();public ScaleTransform Scale=new(1,1);public AdaptiveGlassAppearance Contrast=new();public bool? Light;public ControlCardShape Shape;
        public ControlMotion.SpringPath MovePath;public double VX,VY,VW,VH;public Point MoveFrom;public double MoveStarted=-1;public Vector Avoidance;
        public System.Windows.Shapes.Path Accent=new(){Fill=Brushes.Transparent,IsHitTestVisible=false};public Thumb? Grip;
    }
    private ControlCenterOptions _savedLayout=null!;private readonly DockSettings _settings,_material;private readonly Palette _palette;private readonly ControlCenterOptions _options;
    private readonly Action<ControlCenterOptions>? _saveLayout;private readonly Action? _openSettings;private readonly Action<string,Exception?>? _log;
    private const double SideGutter=88,VerticalGutter=76;
    private readonly Grid _root=new OverflowGrid(){Margin=new Thickness(SideGutter,VerticalGutter,SideGutter,VerticalGutter),Background=Brushes.Transparent},_stage=new OverflowGrid();private readonly Canvas _cards=new OverflowCanvas();
    private readonly Grid _details=new(){Visibility=Visibility.Collapsed};private readonly Grid _detailHeader=new(),_detailContent=new();private readonly StackPanel _detailBody=new(){Margin=new Thickness(16)};
    private readonly DockSurface _detailSurface;private readonly List<GlassButtonChrome> _chrome=new();private readonly List<Card> _modules=new();private readonly List<DockSurface> _surfaces=new();private readonly GlassBackdropService _capture=new(){RetainWindowSamples=true};
    private readonly IControlCenterConnectivity _connectivity;private readonly MediaControlService _media=new();
    private readonly DispatcherTimer _stateRefresh=new(){Interval=TimeSpan.FromMilliseconds(120)};
    private readonly DispatcherTimer _poll=new(){Interval=TimeSpan.FromMilliseconds(900)},_brightnessDelay=new(){Interval=TimeSpan.FromMilliseconds(65)};
    private readonly Slider _volume=new(){Minimum=0,Maximum=100},_brightness=new(){Minimum=0,Maximum=100,IsEnabled=false};
    private Button _detailRefresh=null!;private readonly TextBlock _message=new(){TextWrapping=TextWrapping.Wrap,FontSize=11,Margin=new Thickness(5,4,5,0)},_detailTitle=new();
    private readonly TranslateTransform _entrance=new();private readonly ScaleTransform _scale=new(1,1),_fit=new(1,1),_detailScale=new(1,1);private readonly Stopwatch _runtime=Stopwatch.StartNew();private readonly Button _edit,_add;
    private readonly TextBlock _stats=new(){Visibility=Visibility.Collapsed,IsHitTestVisible=false,FontSize=10,Foreground=Brushes.White,Background=Palette.Brush("#66000000"),Padding=new Thickness(6,3,6,3),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};private readonly DispatcherTimer _statsTimer=new(){Interval=TimeSpan.FromMilliseconds(500)};
    // 调试：显示 WPF 呈现回调频率与间隔统计，不代表 GPU 实际 FPS。
    public static bool ShowFrameStats{get;set;}
    private IReadOnlyList<WifiNetwork> _networks=Array.Empty<WifiNetwork>();private DockForWindows.Services.MediaState? _mediaState;
    private Forms.Screen? _screen;private bool _positioning,_busy,_polling,_updating,_closing,_allowClose,_muted,_brightnessBusy,_editing,_ready,_detailVisible,_chromeDirty=true;
    private double _openingUntil;private bool _volumeBusy;private double? _pendingVolume;
    private double _pendingBrightness,_lastFrame,_lastCaptureError,_mainHeight;private int _transition;private Card? _drag;private Point _dragStart,_grab;private bool _dragged,_resizing;private Size _resizeStart;private Vector _resizeDelta;
    private ControlModule? _openKind;
    private Button? _play,_previous,_next,_mute,_mediaIcon;
    public BitmapSource? GlassBackdrop {get;private set;}public Rect GlassBackdropBounds {get;private set;}=Rect.Empty;
    public long CapturedFrames {get;private set;}public long AnimationFrames {get;private set;}public double RuntimeSeconds=>_runtime.Elapsed.TotalSeconds;public double LongestAnimationGap {get;private set;}
    public string CaptureSources=>_capture.LastSourceWindows;public bool DismissOnDeactivate {get;set;}=true;private bool Animate=>_settings.Animations&&!_palette.ReducedMotion;
    public ControlCenterWindow(DockSettings settings,Action<string,Exception?>? log=null,Action<ControlCenterOptions>? saveLayout=null,Action? openSettings=null,IControlCenterConnectivity? connectivity=null)
    {
        _connectivity=connectivity??new ConnectivityService();_settings=settings.Copy();_settings.ControlCenter.Normalize();_options=_settings.ControlCenter;_savedLayout=_options.Copy();_material=_options.Material(_settings);_palette=new Palette(settings);_log=log;_saveLayout=saveLayout;_openSettings=openSettings;
        Title="液态玻璃控制中心 · DockForWindows";Width=_options.Geometry.PanelWidth;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;
        FontFamily=new FontFamily("Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI, Microsoft YaHei");FontSize=_options.Geometry.FontSize;Foreground=_palette.Text;UseLayoutRounding=true;Opacity=0;Resources.MergedDictionaries.Add(IosStyles.Create(_palette,withoutGlassShadows:true));
        for(int i=0;i<3;i++)_root.RowDefinitions.Add(new(){Height=GridLength.Auto});var transforms=new TransformGroup();transforms.Children.Add(_scale);transforms.Children.Add(_entrance);_root.RenderTransform=transforms;_root.RenderTransformOrigin=new Point(1,0);
        var host=new Grid{LayoutTransform=_fit};host.Children.Add(_root);Panel.SetZIndex(_stats,999);_stats.Margin=new Thickness(6);host.Children.Add(_stats);Content=host;InstallSafetyFade(host);host.SizeChanged+=(_,_)=>UpdateMaterialMask();
        var heading=new Grid{Margin=new Thickness(5,0,5,10)};heading.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});heading.ColumnDefinitions.Add(new(){Width=GridLength.Auto});heading.ColumnDefinitions.Add(new(){Width=GridLength.Auto});AddPagePicker(heading);heading.Children.Add(new TextBlock{Text="控制中心",FontSize=12,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
        var settingsButton=ActionButton("Settings","控制中心设置",()=>{Dismiss();_openSettings?.Invoke();},19);settingsButton.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(settingsButton,2);heading.Children.Add(settingsButton);_root.Children.Add(heading);InstallRevealGesture(heading);
        _stage.Width=(_options.Geometry.PanelWidth-36)*_options.Geometry.ComponentScale;Width=(_stage.Width+SideGutter*2)*_options.Geometry.PanelScale;_fit.ScaleX=_fit.ScaleY=_options.Geometry.PanelScale;_stage.Children.Add(_cards);_stage.Children.Add(_details);Grid.SetRow(_stage,1);_root.Children.Add(_stage);Grid.SetRow(_message,2);_root.Children.Add(_message);
        double componentZoom=_options.Geometry.ComponentScale;_detailBody.Margin=new Thickness();_detailBody.LayoutTransform=new ScaleTransform(componentZoom,componentZoom);
        _detailSurface=Surface(_options.Geometry.DetailRoundness);_detailSurface.CornerExponent=0;_detailGlassHost=new OverflowGrid();_detailGlassHost.Children.Add(_detailSurface);_details.Children.Add(_detailGlassHost);
        _detailContent.RowDefinitions.Add(new(){Height=GridLength.Auto});_detailContent.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        _detailContent.Margin=new Thickness(ControlCenterTokens.Padding(_options.Geometry.CardPadding)*componentZoom,16*componentZoom,ControlCenterTokens.Padding(_options.Geometry.CardPadding)*componentZoom,20*componentZoom);_detailContent.ClipToBounds=true;
        _detailHeader.Margin=new Thickness(0,0,0,6*componentZoom);_detailHeader.MinHeight=30*componentZoom;_detailContent.Children.Add(_detailHeader);
        _detailTitle.FontSize=16*componentZoom;_detailTitle.FontWeight=FontWeights.SemiBold;_detailTitle.VerticalAlignment=VerticalAlignment.Center;_detailTitle.TextTrimming=TextTrimming.CharacterEllipsis;_detailTitle.Margin=new Thickness(0,0,34*componentZoom,0);_detailHeader.Children.Add(_detailTitle);
        _detailRefresh=ActionButton("Refresh","刷新列表",RefreshDetailDevices,12,true);_detailRefresh.Width=_detailRefresh.Height=30*componentZoom;_detailRefresh.HorizontalAlignment=HorizontalAlignment.Right;StyleCloseButton(_detailRefresh);_detailHeader.Children.Add(_detailRefresh);_detailRefresh.Visibility=Visibility.Collapsed;
        _detailScroll=new ScrollViewer{Content=_detailBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,ClipToBounds=true};InstallShortScrollbars(_detailScroll);
        var viewport=new ScrollEdgeFade(_detailScroll);Grid.SetRow(viewport,1);_detailContent.Children.Add(viewport);_details.Children.Add(_detailContent);_detailContent.Children.Remove(_detailHeader);_details.Children.Add(_detailHeader);_detailHeader.VerticalAlignment=VerticalAlignment.Top;_detailContent.RowDefinitions[0].Height=new GridLength(38*componentZoom);_details.RenderTransform=_detailScale;
        _details.SizeChanged+=(_,_)=>UpdateDetailClip();
        _edit=TextButton("编辑控制",()=>{_editing=!_editing;SyncCards(false);if(!_editing)PersistLayout();},true);AutomationProperties.SetAutomationId(_edit,"control-center-edit");_add=TextButton("添加控件",AddModules,true);
        AutomationProperties.SetAutomationId(_cards,"control-center-modules");SyncCards(false);
        _volume.ValueChanged+=(_,_)=>{if((_updating&&!_volume.IsMouseCaptureWithin)||_closing)return;_pendingVolume=_volume.Value;Status(ControlModule.Volume,$"{_volume.Value:0}%");Run(WriteVolume);};
        _brightness.ValueChanged+=(_,_)=>{if((_updating&&!_brightness.IsMouseCaptureWithin)||_closing)return;_pendingBrightness=_brightness.Value;Status(ControlModule.Brightness,$"{_brightness.Value:0}%");_brightnessDelay.Stop();_brightnessDelay.Start();};
        AutomationProperties.SetAutomationId(_volume,"control-center-volume");AutomationProperties.SetAutomationId(_brightness,"control-center-brightness");
        foreach(var pair in new[]{(Slider:_volume,Kind:ControlModule.Volume),(Slider:_brightness,Kind:ControlModule.Brightness)}){var slider=pair.Slider;var kind=pair.Kind;_ = new ControlCenterElasticSlider(slider,()=>Animate,(stretch,release)=>StretchSlider(kind,stretch,release));slider.ValueChanged+=(_,_)=>{var card=_modules.FirstOrDefault(c=>c.Kind==kind);if(Animate&&slider.IsMouseCaptureWithin&&card is not null&&_runtime.Elapsed.TotalSeconds-card.SymbolFeedbackAt>.16){card.SymbolFeedbackAt=_runtime.Elapsed.TotalSeconds;if(card.Primary?.Content is ControlCenterIcon symbol)symbol.Feedback();}};}
        ApplyMaterialMode();
        _brightnessDelay.Tick+=async(_,_)=>{_brightnessDelay.Stop();await WriteBrightness();};_poll.Tick+=async(_,_)=>await PollSystem();_stateRefresh.Tick+=async(_,_)=>{_stateRefresh.Stop();await PollSystem();};_connectivity.Changed+=SystemChanged;_media.Changed+=MediaChanged;
        PreviewMouseDown+=HandleCenterDown;AddHandler(Mouse.PreviewMouseUpEvent,new MouseButtonEventHandler(HandleCenterUp),true);PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){if(_resizing||_drag is not null)EndDrag();else if(_detailVisible)BackDetails();else Dismiss();e.Handled=true;}};
        Loaded+=async(_,_)=>{_ready=true;UpdateLayout();RefreshChrome();PositionAtTray(true);CompositionTarget.Rendering+=OnRendering;_poll.Start();_mediaPoll.Tick+=async(_,_)=>await RefreshMedia();_mediaPoll.Start();_ = CaptureLoop();await Safe(()=>_connectivity.Initialize());await Safe(()=>_media.Initialize());await PollSystem();if(ShowFrameStats){_stats.Visibility=Visibility.Visible;_statsTimer.Tick+=(_,_)=>_stats.Text=_perf.Describe();_statsTimer.Start();}};
        SourceInitialized+=(_,_)=>HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook((nint h,int message,nint w,nint l,ref bool handled)=>{if(message is 0x7E or 0x2E0)Dispatcher.BeginInvoke(()=>PositionAtTray());return 0;});
        SizeChanged+=(_,_)=>{if(_ready)PositionAtTray();};Closing+=(_,e)=>{if(!_allowClose){e.Cancel=true;Dismiss();}};
        Closed+=(_,_)=>{_ready=false;_navigationLayer?.Release();_mediaPoll.Stop();CompositionTarget.Rendering-=OnRendering;_poll.Stop();_stateRefresh.Stop();_brightnessDelay.Stop();_connectivity.Changed-=SystemChanged;_media.Changed-=MediaChanged;_rateTrial?.Dispose();_connectivity.Dispose();_media.Dispose();_lifetime.Cancel();_statsTimer.Stop();foreach(var surface in _surfaces)surface.Backdrop=null;_surfaceFrames.Clear();_classicBackdrop?.Release();ReleaseCardFrost();GlassBackdrop=null;_wholeFrame=null;_displayedSample?.Dispose();_displayedSample=null;if(!_busy)_capture.Dispose();};
    }
    private DockSurface Surface(double radius)
    {
        var surface=new DockSurface{UseGlass=!SystemParameters.HighContrast,Radius=radius,CornerExponent=3,ConstrainFrostToOutline=true,Opacity=0,Fill=SystemParameters.HighContrast?_palette.SettingsCard:Brushes.White,GlassOpacity=_material.GlassOpacity,GlassIntensity=_material.GlassIntensity,GlassDistortion=_material.GlassDistortion,GlassFrost=_material.GlassFrost,GlassGrain=_material.GlassGrain,GlassHighlight=_material.GlassHighlight,GlassHighlightWidth=_material.GlassHighlightWidth,GlassTint=_material.GlassTint,StrokeWidth=0,IsHitTestVisible=false};
        ConfigureSurfaceMaterial(surface);_surfaces.Add(surface);return surface;
    }
    private Button ActionButton(string glyph,string label,Action action,double size=22,bool editor=false,bool motion=true)
    {
        var button=new Button{Content=new ControlCenterIcon(glyph){Width=size,Height=size,Brush=Foreground},ToolTip=label,Foreground=Foreground,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(3),MinWidth=24,MinHeight=26,Cursor=Cursors.Hand};
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);var hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);hit.AppendChild(presenter);button.Template=new ControlTemplate(typeof(Button)){VisualTree=hit};
        var scale=new ScaleTransform(1,1);button.RenderTransform=scale;button.RenderTransformOrigin=new Point(.5,.5);if(motion){button.MouseEnter+=(_,_)=>ButtonPressFeedback(scale,1);button.MouseLeave+=(_,_)=>ButtonPressFeedback(scale,1);button.PreviewMouseLeftButtonDown+=(_,_)=>ButtonPressFeedback(scale,.975);button.PreviewMouseLeftButtonUp+=(_,_)=>ButtonPressFeedback(scale,1);}button.IsEnabledChanged+=(_,_)=>button.Opacity=button.IsEnabled?1:.35;
        button.Click+=(_,_)=>{if(!_dragged&&(editor||!_editing)&&!_closing&&!_dismissRequested){try{if(Animate&&button.Content is ControlCenterIcon mark)mark.Feedback();action();}catch(Exception ex){Report(ex);}}};AutomationProperties.SetName(button,label);return button;
    }
    private Button TextButton(string text,Action action,bool editor=false)
    {var button=ActionButton("",text,action,12,editor);button.Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Left,VerticalAlignment=VerticalAlignment.Center};button.HorizontalAlignment=HorizontalAlignment.Stretch;button.MinHeight=38;return button;}
    private void ButtonPressFeedback(ScaleTransform transform,double value)
    {if(!Animate){ControlMotion.Stop(transform,ScaleTransform.ScaleXProperty,1);ControlMotion.Stop(transform,ScaleTransform.ScaleYProperty,1);return;}ControlMotion.To(transform,ScaleTransform.ScaleXProperty,value,speed:3.5);ControlMotion.To(transform,ScaleTransform.ScaleYProperty,value,speed:3.5);}
    private void Scale(ScaleTransform transform,double value,int milliseconds)
    {if(!Animate){transform.ScaleX=transform.ScaleY=value;return;}ControlMotion.To(transform,ScaleTransform.ScaleXProperty,value);ControlMotion.To(transform,ScaleTransform.ScaleYProperty,value);}
    private void PersistLayout(){try{_saveLayout?.Invoke(_options.Copy());_savedLayout=_options.Copy();}catch(Exception ex){_options.Modules=_savedLayout.Modules.Select(saved=>{var item=_options.Modules.FirstOrDefault(m=>m.Module==saved.Module)??saved.Copy();item.Visible=saved.Visible;item.Shape=saved.Shape;item.Span=saved.Span;item.PageId=saved.PageId;item.Group=saved.Group;item.DisplayName=saved.DisplayName;item.Glyph=saved.Glyph;return item;}).ToList();_options.Pages=_savedLayout.Pages.Select(p=>p.Copy()).ToList();_options.ActivePageId=_savedLayout.ActivePageId;RefreshPagePicker();SyncCards(false);Report(ex);}}
    private void AddModules(){_page=ControlPage.Gallery;ShowDetails("添加控件");foreach(var module in _options.Modules.Where(m=>!m.Visible||m.PageId!=_options.ActivePageId))AddDetail(ModuleName(module.Module)+(module.Visible?"（移至此页）":""),()=>{module.Visible=true;module.PageId=_options.ActivePageId;SyncCards(true);PersistLayout();HideDetails();},true);AddDetail("管理页面与分组…",ManagePages,true);AddDetail("恢复默认控制布局",ResetControlLayout,true);if(_options.Modules.All(m=>m.Visible&&m.PageId==_options.ActivePageId))_detailBody.Children.Add(new TextBlock{Text="所有模块均已添加",Margin=new Thickness(0,12,0,12)});UpdateDetailLayout();}
    private void Active(ControlModule module,bool active)
    {var card=_modules.FirstOrDefault(c=>c.Kind==module);if(card is null||IsBusy(module))return;if(module==ControlModule.DarkMode&&card.Holder is not null){card.Holder.Background=active?Brushes.White:Palette.Brush("#5A5A5E");if(card.Primary?.Content is ControlCenterIcon symbol){symbol.Tag="keep";symbol.Brush=active?DarkInk:Brushes.White;}}var target=active&&card.Shape==ControlCardShape.Circle?Colors.White:active&&_palette.Accent is SolidColorBrush accent?Color.FromArgb(25,accent.Color.R,accent.Color.G,accent.Color.B):Colors.Transparent;if(UsesClassicBlur&&card.Shape!=ControlCardShape.Circle)target=Colors.Transparent;if(card.Shape==ControlCardShape.Circle){foreach(var glyph in VisualDescendants<ControlCenterIcon>(card.Body)){glyph.Tag=active?"keep":null;glyph.Brush=active?DarkInk:card.Light==true?Brushes.White:DarkInk;}}if(card.Accent.Fill is not SolidColorBrush brush||brush.IsFrozen){brush=new SolidColorBrush(Colors.Transparent);card.Accent.Fill=brush;}if(brush.Color!=target)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(target,TimeSpan.FromMilliseconds(Animate?200:0)));}
    private void Status(ControlModule module,string text)
    {var card=_modules.FirstOrDefault(c=>c.Kind==module);if(card is null||IsBusy(module))return;
        if(card.Shape==ControlCardShape.Circle)text=module==ControlModule.Battery?text.Split(" · ")[0]:module==ControlModule.Projection?"投影":module is ControlModule.Volume or ControlModule.Brightness?text:ControlCenterCatalog.Name(module);
        else if(module==ControlModule.Media&&card.Title is not null){var parts=text.Split('\n');card.Title.Text=parts[0];card.Status.Text=parts.Length>1?parts[1]:"";return;}
        else if(module==ControlModule.Battery&&card.Title is not null){int split=text.IndexOf(" · ",StringComparison.Ordinal);if(split>0){card.Title.Text=text[..split];card.Status.Text=text[(split+3)..];}else{card.Title.Text=text;card.Status.Text="";}return;}
        card.Status.Text=text;}
    private static void TextForeground(DependencyObject obj,Brush brush)
    {if(obj is Border border&&border.Tag as string=="detail-divider"){var line=brush.Clone();line.Opacity=.32;border.BorderBrush=line;}if(obj is TextBlock text)text.Foreground=brush;if(obj is Control control)control.Foreground=brush;if(obj is ControlCenterIcon icon&&icon.Tag as string!="keep")icon.Brush=brush;for(int i=0;i<VisualTreeHelper.GetChildrenCount(obj);i++)TextForeground(VisualTreeHelper.GetChild(obj,i),brush);}
    private void RefreshChrome(){_chrome.Clear();void Visit(DependencyObject obj){if(obj is GlassButtonChrome chrome){_chrome.Add(chrome);chrome.RefreshMaterial();}for(int i=0;i<VisualTreeHelper.GetChildrenCount(obj);i++)Visit(VisualTreeHelper.GetChild(obj,i));}Visit(this);if(UsesClassicBlur){Foreground=Brushes.White;TextForeground(_root,Brushes.White);RefreshHeaderForeground(true);}_chromeDirty=true;}
    public event Action<bool>? PresentationChanged;
    internal bool PresentationOpen=>_entranceStarted&&!_dismissRequested&&!_closing;
    private bool _entranceStarted;
    private void AnimateOpen()
    {
        if(_entranceStarted||_closing||_dismissRequested)return;_entranceStarted=true;
        if(!Animate){ControlMotion.Stop(this,RevealProperty,1);return;}
        foreach(var card in _modules){ControlMotion.Stop(card.Root,OpacityProperty,1);ControlMotion.Stop(card.Enter,TranslateTransform.YProperty,0);ControlMotion.Stop(card.Scale,ScaleTransform.ScaleXProperty,1);ControlMotion.Stop(card.Scale,ScaleTransform.ScaleYProperty,1);}
        double top=double.NaN;int row=-1;foreach(var card in _modules.OrderBy(c=>c.Target.Top)){if(double.IsNaN(top)||Math.Abs(top-card.Target.Top)>1){top=card.Target.Top;row++;}ControlMotion.To(card.Enter,TranslateTransform.YProperty,0,-34,speed:.95,delay:Math.Min(.24,row*.042),damping:.82);}
        SettleReveal(true);
    }
    public void Dismiss()
    {
        if(_closing||_dismissRequested)return;EndDrag();SettleReveal(false);
    }
}
public static class ControlCenterCatalog
{
    public static string Name(ControlModule kind)=>kind switch{ControlModule.Wifi=>"Wi-Fi",ControlModule.Bluetooth=>"蓝牙",ControlModule.Focus=>"专注模式",ControlModule.Projection=>"屏幕投影",ControlModule.Brightness=>"屏幕亮度",ControlModule.Volume=>"音量",ControlModule.Battery=>"电池",ControlModule.DarkMode=>"深色模式",ControlModule.PowerSaver=>"省电模式",ControlModule.RefreshRate=>"屏幕刷新率",ControlModule.Performance=>"性能模式",ControlModule.AudioOutput=>"音频输出",ControlModule.Hdr=>"HDR",ControlModule.Screenshot=>"截图工具",ControlModule.ScreenRecording=>"屏幕录制",ControlModule.KeepAwake=>"保持唤醒",ControlModule.Microphone=>"麦克风",ControlModule.VoiceInput=>"语音输入",ControlModule.NightLight=>"夜间护眼",ControlModule.Airplane=>"飞行模式",ControlModule.MobileHotspot=>"移动热点",_=>"正在播放"};
    public static string ShapeName(ControlCardShape shape)=>shape switch{ControlCardShape.Circle=>"圆形",ControlCardShape.Capsule=>"胶囊",ControlCardShape.Square=>"正方形",ControlCardShape.Strip=>"小小号 · 4×1",ControlCardShape.Large=>"大封面 · 4×7",_=>"矩形"};
}

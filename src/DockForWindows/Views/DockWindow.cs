using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using Forms = System.Windows.Forms;

namespace DockForWindows.Views;

public sealed partial class DockWindow : Window
{
    private readonly DockViewModel _model;
    private readonly WindowTracker _tracker;
    private readonly LaunchService _launcher = new();
    private readonly Grid _root = new();
    private readonly DockStrip _strip = new();
    private readonly ScrollViewer _scroll;
    private readonly DockSurface _surface = new() { IsHitTestVisible = false };
    private readonly TranslateTransform _slide = new();
    private readonly TranslateTransform _contactPulse = new();
    private bool _contactInside;
    private readonly DispatcherTimer _edgeTimer;
    private readonly DispatcherTimer _hideDelay;
    private readonly DispatcherTimer _glassTimer;
    private readonly GlassBackdropService _glassBackdrop = new();
    private bool _glassBusy;
    private bool _glassErrorLogged;
    private readonly ScaleTransform _fitTransform = new(1, 1);
    private double _fitScale = 1;
    private long _glassFrameCount;
    private double _glassCaptureMs;
    private double _glassChangedAt;
    private static readonly TimeSpan GlassFrameInterval = TimeSpan.FromMilliseconds(16), GlassIdleInterval = TimeSpan.FromMilliseconds(250);
    private const double GlassSettleSeconds = 1.5;
    private Palette _palette;
    private DockSettings? _previewSettings;
    private DockSettings ViewSettings => _previewSettings ?? _model.Settings;
    private bool _closing;
    private SettingsWindow? _settings;
    private ControlCenterWindow? _controlCenter;
    private Point _dragStart;
    private DockItem? _dragSource;
    private bool _dragging;
    private bool _dropCommitted;
    private bool _hidden;
    private bool _fullscreenActive;
    private nint _lastForeground;
    private long _fullscreenCandidateTick;
    private bool _fullscreenCandidate;
    private bool _menuOpen;
    private bool _positioning;
    private Forms.Screen _screen = Forms.Screen.PrimaryScreen!;
    public DockViewModel Model => _model;
    public bool IsDockHidden => _hidden;
    private bool ContextAutoHide
    {
        get
        {
            var name=new System.Text.StringBuilder(128);var foreground=NativeMethods.GetForegroundWindow();
            if(foreground!=0)NativeMethods.GetClassName(foreground,name,name.Capacity);
            bool desktop=foreground==0 || name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
            return desktop ? ViewSettings.HideOnDesktop : ViewSettings.AutoHide;
        }
    }
    public DockWindow(DockViewModel model, WindowTracker tracker)
    {
        _model = model; _tracker = tracker; _palette = new(model.Settings);
        Title = "DockForWindows";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Content = _root;
        var rootTransforms = new TransformGroup(); rootTransforms.Children.Add(_slide); rootTransforms.Children.Add(_contactPulse); _root.RenderTransform = rootTransforms;
        _root.Children.Add(_surface);
        _strip.LayoutTransform = _fitTransform;
        System.Windows.Automation.AutomationProperties.SetAutomationId(_surface, "dock-surface");
        System.Windows.Automation.AutomationProperties.SetName(_surface, LocalizedText.Get("程序坞底板"));
        _scroll = new ScrollViewer
        {
            Content = _strip, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(16, 0, 16, 0), CanContentScroll = false, Focusable = false
        };
        _root.Children.Add(_scroll);
        _strip.IconSpanChanged += UpdatePlateSpan;
        _scroll.ScrollChanged += (_, _) => UpdatePlateSpan(_strip.IconSpan);
        _strip.AllowDrop = true;
        _strip.Drop += OnExternalDrop;
        _strip.DragOver += OnDragOver;
        _strip.PreviewMouseWheel += (_, e) =>
        {
            if(_windowPreviews?.IsOpen==true){_windowPreviews.Scroll(e.Delta);e.Handled=true;return;}
            if (ViewSettings.Edge == DockEdge.Bottom) _scroll.ScrollToHorizontalOffset(_scroll.HorizontalOffset - e.Delta);
            else _scroll.ScrollToVerticalOffset(_scroll.VerticalOffset - e.Delta);
            e.Handled = true;
        };
        _root.ContextMenu = BuildBackgroundMenu();
        _model.ItemsRebuilt += Rebuild;
        _model.Error += ShowError;
        _hideDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
        _hideDelay.Tick += (_, _) => { _hideDelay.Stop(); if (!IsMouseOver && !_menuOpen && _windowPreviews?.IsOpen!=true && !_dragging && _settings?.IsVisible != true) SetHidden(true); };
        _edgeTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(100) };
        _edgeTimer.Tick += (_, _) => { UpdateDockPopups();CheckFullscreen(); if(!_fullscreenActive && !ContextAutoHide)SetHidden(false); else if(ContextAutoHide && !_hidden && !IsMouseOver && _windowPreviews?.IsOpen!=true && !_hideDelay.IsEnabled)_hideDelay.Start(); CheckEdge(); if (!_hidden) { _strip.SynchronizePointer(); UpdateContactFeedback(); } };
        _glassTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _glassTimer.Tick += async (_, _) => await UpdateGlassAsync();
        MouseEnter += (_, _) => { _hideDelay.Stop(); if (!_fullscreenActive) SetHidden(false); UpdateContactFeedback(); };
        MouseMove += (_, _) => UpdateContactFeedback();
        MouseLeave += (_, _) => { UpdateContactFeedback(); if (ContextAutoHide || _fullscreenActive) _hideDelay.Start(); };
        SourceInitialized += (_, _) =>
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(WindowMessage);
            // Do not take foreground focus when the floating dock is clicked.
            var handle = source.Handle;StartDockInteractions(handle);
            long style = (long)NativeMethods.GetWindowLongPtr(handle, -20);
            SetExtendedStyle(handle, new nint(style | 0x08000000 | 0x80));
        };
        Loaded += async (_, _) =>
        {
            Rebuild(); PositionDock();
            _edgeTimer.Start(); _glassTimer.Start(); _tracker.Start();
            await _model.LoadIconsAsync();
            if (ContextAutoHide) _hideDelay.Start();
        };
        Closed += (_, _) =>
        {
            _closing = true;StopDockInteractions();
            _edgeTimer.Stop(); _hideDelay.Stop(); _glassTimer.Stop(); _tracker.Dispose();
            if (!_glassBusy) _glassBackdrop.Dispose();
            _model.ItemsRebuilt -= Rebuild;
            _model.Error -= ShowError;
            _settings?.ClosePermanently();
            _controlCenter?.Dismiss();
        };
        Closing += (_, _) => { _closing = true; _edgeTimer.Stop(); _hideDelay.Stop(); _glassTimer.Stop(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) _strip.ResetHover(); };
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetExtendedStyle(nint window, nint styleIndex, nint value);
    private static void SetExtendedStyle(nint window, nint value) => SetExtendedStyle(window, new nint(-20), value);

    private async Task UpdateGlassAsync()
    {
        // The same capture also drives running-dot contrast when the plate is transparent.
        bool enabled = ViewSettings.Theme == DockTheme.Glass && !SystemParameters.IsRemoteSession && !SystemParameters.HighContrast;
        if (!enabled)
        {
            if (_surface.Backdrop is not null) { _surface.Backdrop = null; _surface.InvalidateVisual(); }
            foreach (var tile in _strip.Children.OfType<DockTile>()) tile.RestoreIndicatorColor();RestoreSeparatorColor();
            // Nothing to sample until the theme is Glass again; check for that a few times a second instead of every frame.
            if (_glassTimer.Interval != GlassIdleInterval) _glassTimer.Interval = GlassIdleInterval;
            return;
        }
        if (_glassTimer.Interval != GlassFrameInterval) _glassTimer.Interval = GlassFrameInterval;
        if (_glassBusy || _hidden || _closing || !IsVisible || _surface.ActualWidth <= 0 || _surface.ActualHeight <= 0) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(handle, out var nativeBounds)) return;
        // Keep a fixed screen-space source covering the whole dock window. Its pixels
        // never stretch when the plate grows or shrinks under the hover wave.
        var rectangle = System.Drawing.Rectangle.FromLTRB(nativeBounds.Left, nativeBounds.Top, nativeBounds.Right, nativeBounds.Bottom);
        double radius = _surface.Radius, amount = _palette.GlassIntensity, dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        _glassBusy = true;
        try
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            var frame = await Task.Run(() => _glassBackdrop.Render(handle, rectangle, radius, amount, dpi, raw: true));
            _glassCaptureMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (!_closing && ViewSettings.Theme == DockTheme.Glass && frame is not null)
            {
                int pad = (int)Math.Ceiling(24 * dpi);
                var captureBounds = System.Drawing.Rectangle.Inflate(rectangle, pad, pad);
                var screenBounds = new Rect(captureBounds.X, captureBounds.Y, captureBounds.Width, captureBounds.Height);
                double now = System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
                // GlassBackdropService returns the same frame object while nothing under the Dock changed.
                // Keep refreshing for a moment so the adaptive tint (0.4 s time constant) fully settles, then stop
                // re-arranging and re-rendering an identical image; a new frame resumes updates immediately.
                bool same = ReferenceEquals(frame, _surface.Backdrop) && screenBounds == _surface.BackdropScreenBounds;
                if (!same) _glassChangedAt = now;
                if (!same || now - _glassChangedAt < GlassSettleSeconds)
                {
                    _surface.BackdropScreenBounds = screenBounds;
                    _surface.Backdrop = frame; _surface.InvalidateVisual(); _glassFrameCount++;
                }
                double tint = _surface.Fill is SolidColorBrush currentFill && currentFill.Color.A > 0 ? _surface.EffectiveGlassTint * ViewSettings.GlassOpacity : 0;
                foreach (var tile in _strip.Children.OfType<DockTile>())
                    tile.UpdateIndicatorBackdrop(frame, _surface.BackdropScreenBounds, tint, now, ViewSettings.Animations && !_palette.ReducedMotion);
                UpdateSeparatorBackdrop(frame,_surface.BackdropScreenBounds,tint,now,ViewSettings.Animations&&!_palette.ReducedMotion);
            }
        }
        catch (Exception ex) { if (!_glassErrorLogged) { _model.LogDiagnostic(LocalizedText.Get("玻璃背景采样回退"), ex); _glassErrorLogged = true; } }
        finally { _glassBusy = false; if (_closing) _glassBackdrop.Dispose(); }
    }

    private nint WindowMessage(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x8051)
        {
            Dispatcher.BeginInvoke(() => _model.LogDiagnostic($"LAYOUT theme={ViewSettings.Theme} glass={_palette.GlassIntensity} frame={_surface.Backdrop is not null} frames={_glassFrameCount} captureMs={_glassCaptureMs:F1} gpuTier={RenderCapability.Tier >> 16} fit={_fitScale:F4} root={_root.RenderSize} plateScreen={_surface.PointToScreen(new Point())} hidden={_hidden} iconSpan={_strip.IconSpan} source={_glassBackdrop.LastSourceWindows}"));
            handled = true;
        }
        ObserveShellMessage(message,wParam,lParam);
        if (message is 0x02E0 or 0x007E or 0x001A) Dispatcher.BeginInvoke(PositionDock, DispatcherPriority.Background);
        return 0;
    }
    public void RefreshSystemAppearance()
    {
        _palette = new(ViewSettings);
        Rebuild(); PositionDock();
        if (!_fullscreenActive) SetHidden(false);
        if (ViewSettings.AutoHide && !IsMouseOver && _settings?.IsVisible != true) _hideDelay.Start();
    }
    public void PreviewSettings(DockSettings draft)
    {
        _previewSettings = draft.Copy(); _previewSettings.Normalize();
        RefreshSystemAppearance();
    }
    public void PreviewGlass(DockSettings draft)
    {
        bool reflectionsChanged=AppearancePolicy.ShowReflections(ViewSettings)!=AppearancePolicy.ShowReflections(draft);
        _previewSettings = draft.Copy(); _previewSettings.Normalize(); _palette = new(ViewSettings);
        if(reflectionsChanged)Rebuild();else UpdateSurface();
    }
    public void CancelPreview()
    {
        if (_previewSettings is null) return;
        _previewSettings = null;
        if (!_closing) RefreshSystemAppearance();
    }
    public bool CommitSettings(DockSettings draft)
    {
        string? previousStartup;
        bool startupChanged=false;
        try
        {
            previousStartup=StartupRegistration.ReadCommand();
            // Rebuild the startup command only when its switch changes; preserve an existing registration during ordinary saves.
            string? requested=previousStartup;
            if(draft.StartWithWindows!=(previousStartup is not null))requested=draft.StartWithWindows?StartupRegistration.CurrentCommand(_model.DataDirectory):null;
            startupChanged=requested!=previousStartup;
            if(startupChanged)StartupRegistration.WriteCommand(requested);
        }
        catch(Exception ex){ShowError(LocalizedText.Get("无法更新开机自启：")+ex.Message);return false;}
        if (!_model.TryApplyPreferences(draft)) { if(startupChanged)try{StartupRegistration.WriteCommand(previousStartup);}catch(Exception ex){_model.LogDiagnostic(LocalizedText.Get("恢复开机自启设置失败"),ex);} return false; }
        _previewSettings = null;
        _model.UpdateWindows(WindowTracker.Snapshot());
        if(_controlCenter?.IsVisible==true)_controlCenter.ApplyPreview(_model.Settings);
        RefreshSystemAppearance();
        return true;
    }
    private void Rebuild()
    {
        if (_dragging || _strip.ReorderingOrSettling) return;
        CloseWindowPreviews();_strip.Children.Clear();_separator=null;_separators.Clear();
        _strip.IconSize = ViewSettings.IconSize;
        _strip.Gap = ViewSettings.Spacing;
        _strip.Magnification = ViewSettings.Magnification;
        _strip.HoverLift = ViewSettings.HoverLift;
        _strip.EdgeDistance = ViewSettings.EdgeDistance;
        _strip.ReflectionExtent = AppearancePolicy.ShowReflections(ViewSettings) && !SystemParameters.HighContrast ? ViewSettings.IconSize * ViewSettings.ReflectionLength + ViewSettings.ReflectionGap + 4 : 0;
        _strip.BounceHeadroom = ViewSettings.BounceHeight;
        _strip.Animate = ViewSettings.Animations && !_palette.ReducedMotion;
        _strip.Vertical = ViewSettings.Edge != DockEdge.Bottom;
        _strip.RightEdge = ViewSettings.Edge == DockEdge.Right;
        bool divided = false,hasRecent=false;
        foreach (var item in _model.DisplayItems)
        {
            if ((!divided && (!item.IsPinned || item.Pin.Kind == ItemKind.RecycleBin))||(item.Pin.Kind==ItemKind.RecycleBin&&hasRecent))
            {
                divided=true;if(_strip.Children.Count>0){
                var divider = new Grid { IsHitTestVisible = false };
                var separator=new Border { Background = _palette.Text.CloneCurrentValue(), Opacity = .35, Width = _strip.Vertical ? ViewSettings.IconSize * .75 : 1, Height = _strip.Vertical ? 1 : ViewSettings.IconSize * .75, VerticalAlignment = _strip.Vertical ? VerticalAlignment.Center : VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Margin = _strip.Vertical ? new Thickness(0) : new Thickness(0, 3 + ViewSettings.IconSize * .125, 0, 0) };divider.Children.Add(separator);RegisterSeparator(separator);
                DockStrip.SetIsSeparator(divider, true); _strip.Children.Add(divider);}

            }
            if(!item.IsPinned&&item.Pin.Kind!=ItemKind.RecycleBin)hasRecent=true;
            var tile = new DockTile(item, _palette, ViewSettings);
            tile.MouseEnter+=(_,_)=>QueueWindowPreviews(tile);
            tile.PreviewMouseWheel+=(_,e)=>{if(_windowPreviews?.IsOpen==true&&_previewCandidate==tile){_windowPreviews.Scroll(e.Delta);e.Handled=true;}};
            tile.Invoked += () => ActivateItem(item, tile);
            tile.ContextMenu = BuildItemMenu(item); DockMenuStyle.PlaceBeside(tile.ContextMenu, ViewSettings.Edge, tile);
            tile.PreviewMouseLeftButtonDown += (_, e) => { _dragStart = e.GetPosition(this); _dragSource = item; };
            tile.QueryContinueDrag += (_, e) =>
            {
                // The dock deliberately avoids foreground activation. Check the physical
                // Escape state as well as OLE's translated keyboard state in that case.
                if (e.EscapePressed || (NativeMethods.GetAsyncKeyState(0x1B) & 0x8000) != 0)
                {
                    e.Action = DragAction.Cancel; e.Handled = true;
                }
            };
            tile.PreviewMouseMove += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || _dragSource != item || !item.IsPinned || item.Pin.Kind == ItemKind.RecycleBin) return;
                var position = e.GetPosition(this);
                if ((position - _dragStart).Length < 7) return;
                _dragging = true; _dropCommitted = false; _strip.BeginReorder(tile);
                try { DragDrop.DoDragDrop(tile, new DataObject("DockForWindows.ItemId", item.Pin.Id), DragDropEffects.Move); }
                finally
                {
                    _dragging = false; _dragSource = null;
                    _strip.EndReorder(_dropCommitted, Rebuild);
                }
            };
            tile.MouseLeftButtonUp += (_, e) =>
            {
                if (_dragSource == item && !_dragging && (e.GetPosition(this) - _dragStart).Length < 7) tile.Invoke();
                _dragSource = null; e.Handled = true;
            };
            tile.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { tile.Invoke(); e.Handled = true; } };
            tile.DragOver += (_, e) =>
            {
                if (e.Data.GetDataPresent("DockForWindows.ItemId"))
                {
                    bool canSwap = item.IsPinned && item.Pin.Kind != ItemKind.RecycleBin;
                    e.Effects = canSwap ? DragDropEffects.Move : DragDropEffects.None;
                    if (canSwap) _strip.PreviewSwap(tile);
                    e.Handled = true;
                }
            };
            tile.DragLeave += (_, _) => tile.BorderBrush = Brushes.Transparent;
            tile.Drop += (_, e) =>
            {
                tile.BorderBrush = Brushes.Transparent;
                if (e.Data.GetData("DockForWindows.ItemId") is string id)
                {
                    if (_dragging && _dragSource?.Pin.Id == id && item.IsPinned && item.Pin.Kind != ItemKind.RecycleBin)
                    {
                        _strip.PreviewSwap(tile, true);
                        _dropCommitted = _model.ApplyPinnedOrder(_strip.PreviewPinnedOrder());
                    }
                    e.Handled = true;
                }
            };
            _strip.Children.Add(tile);
        }
        UpdateSurface();
        _root.ContextMenu = BuildBackgroundMenu();
        _strip.InvalidateMeasure();
        if (IsLoaded) PositionDock();
    }
    private void UpdateSurface()
    {
        _surface.Fill = _palette.Surface;
        _surface.Stroke = _palette.Line;
        _surface.StrokeWidth = ViewSettings.Theme == DockTheme.Glass ? 0 : _palette.BorderThickness;
        _surface.Radius = _palette.CornerRadius;
        _surface.GlassIntensity = _palette.GlassIntensity;
        _surface.UseGlass = ViewSettings.Theme == DockTheme.Glass && !SystemParameters.HighContrast;
        _surface.GlassDistortion = ViewSettings.GlassDistortion;
        _surface.GlassHighlight = ViewSettings.GlassHighlight; _surface.GlassHighlightWidth = ViewSettings.GlassHighlightWidth; _surface.GlassSideDarkness = ViewSettings.GlassSideDarkness;
        _surface.GlassTint = ViewSettings.GlassTint; _surface.GlassTintColor = GlassTint.Resolve(ViewSettings);
        _surface.GlassOpacity = ViewSettings.GlassOpacity; _surface.GlassFrost = ViewSettings.GlassFrost;
        _surface.GlassGrain = ViewSettings.GlassGrain;
        _surface.InvalidateVisual();
        _surface.Effect = _palette.ShadowEnabled ? new DropShadowEffect { BlurRadius = _palette.ShadowBlur, ShadowDepth = 5, Opacity = _palette.ShadowOpacity, Color = Colors.Black, RenderingBias = RenderingBias.Performance } : null;
    }
    private void ActivateItem(DockItem item, DockTile tile)
    {
        try
        {
            CloseWindowPreviews();
            tile.Bounce(ViewSettings.Animations && !_palette.ReducedMotion, ViewSettings.BounceHeight, ViewSettings.BounceDuration);
            if (item.IsRunning)
            {
                var windows = item.Windows;
                var foreground=WindowTracker.ForegroundWindow;var owner=foreground==0?0:GetAncestor(foreground,3);
                var active=windows.FirstOrDefault(w=>w.Handle==foreground||w.Handle==owner);
                if(active is not null&&!NativeMethods.IsIconic(active.Handle)){foreach(var window in windows)if(NativeMethods.IsWindow(window.Handle)&&!NativeMethods.IsIconic(window.Handle))NativeMethods.ShowWindowAsync(window.Handle,6);}
                else ActivateTrackedWindow(windows[0]);
            }
            else _launcher.Launch(item.Pin);
            _tracker.Refresh();
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }
    private ContextMenu NewMenu()
    {
        var menu = new ContextMenu { FontSize = 13 };
        NativeMenuStyles.Apply(menu,ViewSettings);menu.Opened += (_, _) => { NativeMenuStyles.Apply(menu,ViewSettings);CloseWindowPreviews();_openDockMenu=menu;_menuOpen = true; _hideDelay.Stop();WatchPopupClicks(); };
        menu.Closed += (_, _) => { if(_openDockMenu==menu)_openDockMenu=null;WatchPopupClicks();_menuOpen = false; if (ViewSettings.AutoHide && !IsMouseOver) _hideDelay.Start(); };
        return menu;
    }
    private static void AddMenu(ContextMenu menu, string label, Action action)
    {
        var entry = new MenuItem { Header = label, Padding = new Thickness(10, 6, 10, 6) };
        entry.Click += (_, _) => action(); menu.Items.Add(entry);
    }
    private ContextMenu BuildBackgroundMenu()
    {
        var menu = NewMenu();
        AddMenu(menu, LocalizedText.Get("添加应用与文件…"), AddApplication);
        AddMenu(menu, LocalizedText.Get("添加文件夹…"), AddFolder);
        AddMenu(menu, LocalizedText.Get("重新加载图标"), () => _ = _model.LoadIconsAsync(true));
        menu.Items.Add(new Separator());
        AddMenu(menu, LocalizedText.Get("程序坞设置…"), OpenSettings);
        AddMenu(menu, LocalizedText.Get("打开控制中心"), ToggleControlCenter);
        menu.Items.Add(new Separator());
        AddMenu(menu, LocalizedText.Get("退出程序坞"), () => Application.Current.Shutdown());
        return menu;
    }
    private ContextMenu BuildItemMenu(DockItem item)
    {
        var menu = NewMenu(); DockMenuStyle.PlaceBeside(menu, ViewSettings.Edge);
        // Populate window choices when opened so the list cannot remain stale. Order as on a Mac: the windows, then
        // what to do with the app, then this icon's options, then the Dock's settings (audit W03-01).
        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();
            foreach (var window in item.Windows)
            {
                string label = window.Title.Length > 40 ? window.Title[..18] + "…" + window.Title[^18..] : window.Title;
                AddMenu(menu, label, () => { if (!WindowTracker.Activate(window)) ShowError(LocalizedText.Get("Windows 暂未允许激活此窗口。")); });
            }
            if (item.IsRunning) menu.Items.Add(new Separator());
            AddMenu(menu, LocalizedText.Get("打开"), () => { try { _launcher.Launch(item.Pin); } catch (Exception ex) { ShowError(ex.Message); } });
            if(LaunchService.CanCreateNewWindow(item.Pin))AddMenu(menu,LocalizedText.Get("新建窗口"),()=>NewApplicationWindow(item));
            if (item.IsRunning) AddMenu(menu, item.Windows.Count == 1 ? LocalizedText.Get("关闭窗口") : LocalizedText.Format($"关闭全部 {item.Windows.Count} 个窗口"), () => { foreach (var window in item.Windows) WindowTracker.Close(window); });
            menu.Items.Add(new Separator());
            if (item.Pin.Kind != ItemKind.RecycleBin) AddMenu(menu, item.IsPinned ? LocalizedText.Get("从程序坞取消固定") : LocalizedText.Get("固定到程序坞"), () => { if (item.IsPinned) _model.Remove(item); else _model.Pin(item); });
            if (File.Exists(item.Pin.Target)) AddMenu(menu, LocalizedText.Get("在资源管理器中显示"), () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + item.Pin.Target + "\"") { UseShellExecute = true }));
            menu.Items.Add(new Separator());
            AddMenu(menu, LocalizedText.Get("程序坞设置…"), OpenSettings);
        };
        return menu;
    }
    public void AddApplication()
    {
        SetHidden(false);
        var picker = new Microsoft.Win32.OpenFileDialog { Title = LocalizedText.Get("选择应用、快捷方式或文件"), Filter = LocalizedText.Get("所有文件 (*.*)|*.*|应用与快捷方式 (*.exe;*.lnk)|*.exe;*.lnk"), Multiselect = true, DereferenceLinks = false };
        _menuOpen = true;
        try { if (picker.ShowDialog() == true) _ = _model.AddAsync(picker.FileNames); }
        finally { _menuOpen = false; }
    }
    public void AddFolder()
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = LocalizedText.Get("选择要固定的文件夹"), Multiselect = true };
        _menuOpen = true;
        try { if (picker.ShowDialog() == true) _ = _model.AddAsync(picker.FolderNames); }
        finally { _menuOpen = false; }
    }
    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private async void OnExternalDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) { await _model.AddAsync(paths); e.Handled = true; }
    }
    public void OpenSettings()
    {
        Reveal();
        if (_settings is not null) { _settings.RestoreFromTray(); return; }
        _settings = new SettingsWindow(this, _palette);
        _settings.Closed += (_, _) => { _settings = null; CancelPreview(); if (!_closing && ViewSettings.AutoHide && !IsMouseOver) _hideDelay.Start(); };
        _settings.Show(); _settings.Activate();
    }

    public event Action<bool>? ControlCenterStateChanged;
    public void ToggleControlCenter()
    {
        if(_closing)return;
        if(_controlCenter?.IsVisible==true){_controlCenter.TogglePresentation();return;}
        var window=new ControlCenterWindow(_model.Settings,(message,error)=>_model.LogDiagnostic(message,error),options=>{var previous=_model.Settings.ControlCenter;_model.Settings.ControlCenter=options;if(!_model.Save()){_model.Settings.ControlCenter=previous;throw new IOException(LocalizedText.Get("控制中心排列未能保存，请检查配置文件的写入权限。"));}},()=>ReopenSettings(_model.Settings,7));_controlCenter=window;
        window.PresentationChanged+=open=>ControlCenterStateChanged?.Invoke(open);window.Closed+=(_,_)=>{if(_controlCenter==window)_controlCenter=null;ControlCenterStateChanged?.Invoke(false);};window.Show();window.Activate();
    }
    internal void ReopenSettings(DockSettings draft, int page)
    {
        if (_closing) return;
        var previous = _settings;
        double left = previous?.Left ?? double.NaN, top = previous?.Top ?? double.NaN;
        double width=previous?.Width??double.NaN,height=previous?.Height??double.NaN;
        var windowState=previous?.WindowState??WindowState.Normal;
        var navigation=previous?.CaptureNavigationState();
        previous?.ClosePermanently();
        _settings = new SettingsWindow(this, new Palette(draft), draft, page);
        if(navigation?.Section==page)_settings.RestoreNavigationState(navigation);
        if(double.IsFinite(width)&&double.IsFinite(height)){_settings.Width=width;_settings.Height=height;}
        _settings.WindowState=windowState;
        _settings.Closed += (_, _) => { _settings = null; CancelPreview(); };
        if (double.IsFinite(left) && double.IsFinite(top)) { _settings.WindowStartupLocation = WindowStartupLocation.Manual; _settings.Left = left; _settings.Top = top; }
        PreviewSettings(draft); _settings.Show(); _settings.Activate();
    }
    public void RefreshSettingsAppearance() => _settings?.RefreshInterfaceAppearance();
    // Called by Program after an unhandled UI fault it decided to survive. Closes the transient surfaces
    // whose state may be inconsistent; the Dock itself, its data and any open settings draft are kept.
    internal void RecoverFromUiFault()
    {
        var panel = _controlCenter; _controlCenter = null;
        try { panel?.CloseAfterFault(); } catch (Exception error) { _model.LogDiagnostic(LocalizedText.Get("关闭出错的控制中心失败"), error); }
        try { CloseWindowPreviews(); if (_openDockMenu is { IsOpen: true } menu) menu.IsOpen = false; } catch (Exception error) { _model.LogDiagnostic(LocalizedText.Get("关闭程序坞弹出层失败"), error); }
        _strip.ResetHover();
        ControlCenterStateChanged?.Invoke(false);
    }
    public void Reveal() { Show(); SetHidden(false); _hideDelay.Stop(); if (ViewSettings.AutoHide) _hideDelay.Start(); }
    public void ShowError(string message)
    {
        SetHidden(false); _menuOpen = true;
        try { MessageBox.Show(message, "DockForWindows", MessageBoxButton.OK, MessageBoxImage.Information); }
        finally { _menuOpen = false; }
    }
    public void PositionDock()
    {
        if (!IsLoaded || _positioning) return;
        _positioning = true;
        try
        {
            _screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == ViewSettings.MonitorDevice) ?? Forms.Screen.PrimaryScreen!;
            var area = _screen.WorkingArea;
            var handle = new WindowInteropHelper(this).Handle;
            // Move first, then obtain this window's effective DPI on its target display.
            if (Forms.Screen.FromHandle(handle).DeviceName != _screen.DeviceName)
                NativeMethods.SetWindowPos(handle, 0, area.Left + 20, area.Top + 20, 0, 0, 0x0015);
            double dpi = NativeMethods.GetDpiForWindow(handle) / 96d;
            if (dpi <= 0) dpi = 1;
            bool vertical = ViewSettings.Edge != DockEdge.Bottom;
            _scroll.Margin = vertical ? new Thickness(0,12,0,12) : new Thickness(16,0,16,0);
            _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _scroll.ScrollToHorizontalOffset(0); _scroll.ScrollToVerticalOffset(0);
            _fitTransform.ScaleX = _fitTransform.ScaleY = 1;
            _strip.EdgeDistance = ViewSettings.EdgeDistance;
            _strip.InvalidateMeasure();
            _strip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var screenBounds = _screen.Bounds;
            double centerAlong = vertical ? screenBounds.Top + screenBounds.Height / 2d : screenBounds.Left + screenBounds.Width / 2d;
            double symmetricRoom = vertical ? 2 * Math.Min(centerAlong - area.Top, area.Bottom - centerAlong) : 2 * Math.Min(centerAlong - area.Left, area.Right - centerAlong);
            double available = symmetricRoom / dpi - 64;
            double desired = vertical ? _strip.DesiredSize.Height : _strip.DesiredSize.Width;
            _fitScale = Math.Min(1, Math.Max(1, available) / Math.Max(1, desired));
            _fitTransform.ScaleX = _fitTransform.ScaleY = _fitScale;
            _strip.EdgeDistance = ViewSettings.EdgeDistance / _fitScale;
            _strip.InvalidateMeasure();
            _strip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Width = Math.Max(100, Math.Min(_strip.DesiredSize.Width + 32, area.Width / dpi - 32));
            Height = Math.Max(80, Math.Min(_strip.DesiredSize.Height + (vertical ? 24 : 0), area.Height / dpi - 32));
            _surface.Width = vertical ? (ViewSettings.IconSize + 28) * _fitScale : Math.Max(1, Math.Min(Width - 32, (_strip.RestingLength + 16) * _fitScale));
            _surface.Height = vertical ? Math.Max(1, Math.Min(Height - 24, (_strip.RestingLength + 16 - _strip.ReflectionExtent) * _fitScale)) : (ViewSettings.IconSize + 28) * _fitScale;
            _surface.VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Bottom;
            _surface.HorizontalAlignment = ViewSettings.Edge == DockEdge.Left ? HorizontalAlignment.Left : ViewSettings.Edge == DockEdge.Right ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            // Plate placement is independent of hover scale, lift and reflections.
            _surface.Margin = ViewSettings.Edge == DockEdge.Left ? new Thickness(ViewSettings.EdgeDistance, 0, 0, 0) : ViewSettings.Edge == DockEdge.Right ? new Thickness(0, 0, ViewSettings.EdgeDistance, 0) : new Thickness(0, 0, 0, ViewSettings.EdgeDistance);
            int width = (int)Math.Round(Width * dpi), height = (int)Math.Round(Height * dpi);
            int x = ViewSettings.Edge == DockEdge.Left ? area.Left : ViewSettings.Edge == DockEdge.Right ? area.Right - width : screenBounds.Left + (screenBounds.Width - width) / 2;
            int y = vertical ? screenBounds.Top + (screenBounds.Height - height) / 2 : area.Bottom - height;
            NativeMethods.SetWindowPos(handle, new nint(-1), x, y, width, height, 0x0010);
            bool remainHidden = _hidden || _fullscreenActive;
            _hidden = false; _slide.BeginAnimation(TranslateTransform.XProperty, null); _slide.BeginAnimation(TranslateTransform.YProperty, null);
            _slide.X = _slide.Y = 0;
            _strip.IsHitTestVisible = !remainHidden;
            if (remainHidden)
            {
                _hidden = true;
                double displacement = vertical ? Width + 12 : Height + 12;
                if (ViewSettings.Edge == DockEdge.Left) displacement = -displacement;
                _slide.SetValue(vertical ? TranslateTransform.XProperty : TranslateTransform.YProperty, displacement);
                _root.Opacity = 0;
                if (_fullscreenActive) Topmost = false;
            }
            Dispatcher.BeginInvoke(() => UpdatePlateSpan(_strip.IconSpan), DispatcherPriority.Render);
        }
        finally { _positioning = false; }
    }
    private void UpdatePlateSpan(Rect iconSpan)
    {
        if (!IsLoaded || iconSpan.IsEmpty) return;
        if(ViewSettings.Edge!=DockEdge.Bottom)
        {
            var verticalSpan=_strip.TransformToAncestor(_root).TransformBounds(iconSpan);
            double top=Math.Max(0,verticalSpan.Top-14*_fitScale),height=Math.Min(_root.ActualHeight-top,verticalSpan.Height+28*_fitScale);
            _surface.VerticalAlignment=VerticalAlignment.Top;_surface.Height=Math.Max(1,height);
            _surface.Margin=ViewSettings.Edge==DockEdge.Left?new Thickness(ViewSettings.EdgeDistance,top,0,0):new Thickness(0,top,ViewSettings.EdgeDistance,0);
            return;
        }
        // Follow the live icon silhouette with the same 14-DIP padding on both
        // ends. Vertical placement and window size are untouched by this update.
        var span = _strip.TransformToAncestor(_root).TransformBounds(iconSpan);
        double plateWidth = Math.Min(_root.ActualWidth, span.Width + 28 * _fitScale);
        double left = (_root.ActualWidth - plateWidth) / 2;
        double right = left + plateWidth;
        if (right <= left) return;
        _surface.HorizontalAlignment = HorizontalAlignment.Left;
        if (Math.Abs(_surface.Width - (right - left)) > .02) _surface.Width = right - left;
        var margin = new Thickness(left, 0, 0, ViewSettings.EdgeDistance);
        if (_surface.Margin != margin) _surface.Margin = margin;
    }
    private void SetHidden(bool hidden)
    {
        if (!ContextAutoHide && !_fullscreenActive) hidden = false;
        if (_hidden == hidden) return;
        _hidden = hidden;
        bool vertical = ViewSettings.Edge != DockEdge.Bottom;
        double displacement = hidden ? (vertical ? Width + 12 : Height + 12) : 0;
        if (ViewSettings.Edge == DockEdge.Left) displacement *= -1;
        var property = vertical ? TranslateTransform.XProperty : TranslateTransform.YProperty;
        _slide.BeginAnimation(vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty, null);
        if (!hidden) Topmost = true;
        double currentOpacity = _root.Opacity;
        double currentDisplacement = (double)_slide.GetValue(property);
        _root.BeginAnimation(OpacityProperty, null); _root.Opacity = currentOpacity;
        if (ViewSettings.Animations && !_palette.ReducedMotion)
        {
            var duration = TimeSpan.FromMilliseconds(hidden ? 440 : 540);
            var animation = new DoubleAnimation(currentDisplacement, displacement, duration) { EasingFunction = new QuinticEase { EasingMode = hidden ? EasingMode.EaseInOut : EasingMode.EaseOut }, FillBehavior = FillBehavior.HoldEnd };
            animation.Completed += (_, _) => { if (_hidden && _fullscreenActive) Topmost = false; };
            _slide.BeginAnimation(property, animation);
            _root.BeginAnimation(OpacityProperty, new DoubleAnimation(hidden ? 0 : 1, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.HoldEnd });
        }
        else { _slide.BeginAnimation(property, null); _slide.SetValue(property, displacement); _root.Opacity = hidden ? 0 : 1; if (hidden && _fullscreenActive) Topmost = false; }
        _strip.IsHitTestVisible = !hidden;
        if (hidden) _strip.ResetHover();
    }
    private void UpdateContactFeedback()
    {
        if (_closing || _hidden || !IsLoaded || PresentationSource.FromVisual(this) is null || _surface.ActualWidth <= 0 || !NativeMethods.GetCursorPos(out var cursor)) return;
        var point = PointFromScreen(new Point(cursor.X, cursor.Y));
        var corner = _surface.TranslatePoint(new Point(0, 0), _root);
        var band = new Rect(corner, _surface.RenderSize);
        bool inside = band.Contains(point);
        if (inside && !_contactInside && ViewSettings.Animations && !_palette.ReducedMotion)
        {
            _contactPulse.BeginAnimation(TranslateTransform.XProperty, null);
            _contactPulse.BeginAnimation(TranslateTransform.YProperty, null);
            _contactPulse.X = _contactPulse.Y = 0;
            bool vertical = ViewSettings.Edge != DockEdge.Bottom;
            double direction = ViewSettings.Edge == DockEdge.Left ? 1 : -1;
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(340),
                KeyFrames =
                {
                    new EasingDoubleKeyFrame(direction, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100)), new CubicEase { EasingMode = EasingMode.EaseOut }),
                    new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340)), new CubicEase { EasingMode = EasingMode.EaseInOut })
                }
            };
            _contactPulse.BeginAnimation(vertical ? TranslateTransform.XProperty : TranslateTransform.YProperty, animation);
        }
        _contactInside = inside;
    }
    private void CheckFullscreen()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        bool covered = false;
        if (ViewSettings.HideWhenFullscreen && foreground != 0 && foreground != new WindowInteropHelper(this).Handle &&
            (_settings is null || foreground != new WindowInteropHelper(_settings).Handle) && !NativeMethods.IsIconic(foreground))
        {
            var name = new System.Text.StringBuilder(128); NativeMethods.GetClassName(foreground, name, name.Capacity);
            if (name.ToString() is "#32768" or "tooltips_class32") return;
            if (name.ToString() is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") &&
                Forms.Screen.FromHandle(foreground).DeviceName == _screen.DeviceName && NativeMethods.GetWindowRect(foreground, out var bounds))
            {
                var work = _screen.WorkingArea;
                covered = NativeMethods.IsZoomed(foreground) || (bounds.Left <= work.Left + 3 && bounds.Top <= work.Top + 3 && bounds.Right >= work.Right - 3 && bounds.Bottom >= work.Bottom - 3);
            }
        }
        long now = Environment.TickCount64;
        if (_lastForeground != foreground || covered != _fullscreenCandidate)
        { _lastForeground = foreground; _fullscreenCandidate = covered; _fullscreenCandidateTick = now; return; }
        if (covered == _fullscreenActive || now - _fullscreenCandidateTick < 160) return;
        _fullscreenActive = covered;
        if (covered) SetHidden(true);
        else { SetHidden(false); if (ViewSettings.AutoHide && !IsMouseOver) _hideDelay.Start(); }
    }
    private void CheckEdge()
    {
        if ((!ContextAutoHide && !_fullscreenActive) || !_hidden || !NativeMethods.GetCursorPos(out var cursor)) return;
        var area = _screen.WorkingArea;
        var bounds = _screen.Bounds;
        var handle = new WindowInteropHelper(this).Handle;
        double dpi = Math.Max(1, NativeMethods.GetDpiForWindow(handle)) / 96d;
        bool alongDock = ViewSettings.Edge == DockEdge.Bottom
            ? Math.Abs(cursor.X - (area.Left + area.Width / 2d)) <= Width * dpi / 2
            : Math.Abs(cursor.Y - (area.Top + area.Height / 2d)) <= Height * dpi / 2;
        bool atEdge = ViewSettings.Edge switch
        {
            DockEdge.Bottom => cursor.Y >= area.Bottom - 10 && cursor.Y < bounds.Bottom,
            DockEdge.Left => cursor.X <= area.Left + 10 && cursor.X >= bounds.Left,
            _ => cursor.X >= area.Right - 10 && cursor.X < bounds.Right
        };
        if (alongDock && atEdge) { SetHidden(false); _hideDelay.Start(); }
    }
}

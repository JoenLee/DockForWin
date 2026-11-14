using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using Forms=System.Windows.Forms;

namespace DockForWindows.Views;

internal sealed class WidgetDesktopService : IWidgetPlacementService,IDisposable
{
    private readonly DockViewModel _model;
    private readonly Dictionary<Guid,WidgetDesktopWindow> _windows=new();
    private readonly DispatcherTimer _shellCheck;
    private bool _disposed;
    private bool _validating;
    private int _shellTicks;
    // Desktop widgets that are top-level windows (see WidgetDesktopWindow.Attach) follow the desktop in z-order;
    // "show desktop" (Win+D) raises the desktop, so they are moved back above it whenever the foreground changes.
    private readonly WinEventProc _foregroundChanged;
    private nint _foregroundHook;
    internal WidgetRuntime Runtime {get;}
    internal static WidgetDesktopService? Current {get;private set;}
    public bool IsAvailable=>!_disposed&&WidgetDesktopLayout.DesktopParent()!=0;
    internal event Action? Changed;
    internal event Action<string>? Error;
    internal WidgetDesktopService(DockViewModel model)
    {
        _model=model;Current=this;Runtime=new(Application.Current.Dispatcher,()=>model.Settings.Widgets,model.TrySaveWidgets,System.IO.Path.Combine(model.DataDirectory,"widget-media"));
        Runtime.Changed+=Refresh;model.WidgetSettingsChanged+=Reconcile;
        _shellCheck=new DispatcherTimer(TimeSpan.FromSeconds(5),DispatcherPriority.Background,(_,_)=>{if(_windows.Values.Any(w=>!w.Attached))Reconcile();foreach(var w in _windows.Values){w.FollowDesktop();w.RefreshOcclusion();}if(++_shellTicks%6==0)_ =ValidatePositions();},Application.Current.Dispatcher);
        _foregroundChanged=(_,_,_,_,_,_,_)=>{foreach(var w in _windows.Values)w.FollowDesktop();};
        _foregroundHook=SetWinEventHook(3,3,0,_foregroundChanged,0,0,0);
        Reconcile();_ =ValidatePositions();_shellCheck.Start();
    }
    private delegate void WinEventProc(nint hook,uint @event,nint window,int objectId,int childId,uint thread,uint time);
    [DllImport("user32.dll")]private static extern nint SetWinEventHook(uint min,uint max,nint module,WinEventProc callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")]private static extern bool UnhookWinEvent(nint hook);
    public bool TryRequest(WidgetPlacementRequest request,out string message)
    {
        if(!IsAvailable){message=LocalizedText.Get("Windows 桌面容器不可用，未添加组件。");return false;}
        _ =AddAsync(request.Instance.Copy());message=LocalizedText.Get("正在读取桌面图标区域并寻找空位…");return true;
    }
    internal async Task<bool> AddAsync(WidgetInstance instance)
    {
        try{
            if(!IsAvailable)throw new InvalidOperationException(LocalizedText.Get("Windows 桌面容器不可用。"));
            if(_model.Settings.Widgets.Instances.Count>=100)throw new InvalidOperationException(LocalizedText.Get("桌面组件已达 100 个。"));
            var screen=Forms.Screen.FromPoint(Forms.Cursor.Position);var occupied=(await WidgetDesktopLayout.Icons()).ToList();if(_disposed)return false;
            occupied.AddRange(OtherBounds(null));instance.Monitor=screen.DeviceName;
            if(!WidgetDesktopLayout.FindSpace(instance,screen,WidgetDesktopLayout.Dpi(screen),occupied,out int col,out int row))throw new InvalidOperationException(LocalizedText.Get("当前显示器没有足够空位，请移动图标或选择较小尺寸。"));
            instance.Column=col;instance.Row=row;var draft=_model.Settings.Widgets.Copy();draft.Instances.Add(instance);
            if(!_model.TrySaveWidgets(draft))return false;
            if(!_windows.TryGetValue(instance.InstanceId,out var window)||!window.Attached){var rollback=_model.Settings.Widgets.Copy();rollback.Instances.RemoveAll(x=>x.InstanceId==instance.InstanceId);_model.TrySaveWidgets(rollback);throw new InvalidOperationException(LocalizedText.Get("无法附着到 Explorer 桌面，已撤回添加。"));}
            return true;
        }catch(Exception e){if(!_disposed){if(Error!=null)Error(e.Message);else MessageBox.Show(e.Message,LocalizedText.Get("添加小组件"),MessageBoxButton.OK,MessageBoxImage.Information);}return false;}
    }
    private IEnumerable<Rect> OtherBounds(Guid? excluding)
    {
        foreach(var x in _model.Settings.Widgets.Instances.Where(x=>x.InstanceId!=excluding)){
            var s=Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==x.Monitor)??Forms.Screen.PrimaryScreen!;yield return WidgetDesktopLayout.Bounds(x,s,WidgetDesktopLayout.Dpi(s));}
    }
    private async Task ValidatePositions()
    {
        if(_disposed||_validating||_model.Settings.Widgets.Instances.Count==0)return;_validating=true;
        try{var occupied=(await WidgetDesktopLayout.Icons()).ToList();if(_disposed)return;var draft=_model.Settings.Widgets.Copy();bool changed=false;
            foreach(var item in draft.Instances){var screen=Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==item.Monitor)??Forms.Screen.PrimaryScreen!;double dpi=WidgetDesktopLayout.Dpi(screen);
                if(WidgetDesktopLayout.FindSpace(item,screen,dpi,occupied,out int col,out int row,item.Column,item.Row)){
                    changed|=col!=item.Column||row!=item.Row||item.Monitor!=screen.DeviceName;item.Column=col;item.Row=row;item.Monitor=screen.DeviceName;occupied.Add(WidgetDesktopLayout.Bounds(item,screen,dpi));if(_windows.TryGetValue(item.InstanceId,out var window)&&!window.IsVisible)window.Show();
                }else if(_windows.TryGetValue(item.InstanceId,out var window))window.Hide();
            }if(changed)_model.TrySaveWidgets(draft);
        }catch(Exception e){Error?.Invoke(LocalizedText.Get("图标避让检查未完成，保留布局：")+e.Message);}finally{_validating=false;}
    }
    internal async Task Place(WidgetDesktopWindow window,Point physical)
    {
        try{var id=window.Instance.InstanceId;if(!_model.Settings.Widgets.Instances.Any(x=>x.InstanceId==id))return;
            var screen=Forms.Screen.FromPoint(new System.Drawing.Point((int)physical.X,(int)physical.Y));double dpi=WidgetDesktopLayout.Dpi(screen),pitch=(WidgetSize.Unit+WidgetSize.Gap)*dpi/96;
            var icons=await WidgetDesktopLayout.Icons();if(_disposed)return;
            // Copied after the await (up to seconds while desktop icons are read), so a save made meanwhile is not overwritten.
            var draft=_model.Settings.Widgets.Copy();var item=draft.Instances.FirstOrDefault(x=>x.InstanceId==id);if(item==null)return;
            var occupied=icons.Concat(OtherBounds(item.InstanceId)).ToArray();
            int desiredX=(int)Math.Round((physical.X-screen.WorkingArea.Left)/pitch),desiredY=(int)Math.Round((physical.Y-screen.WorkingArea.Top)/pitch);
            if(!WidgetDesktopLayout.FindSpace(item,screen,dpi,occupied,out int col,out int row,desiredX,desiredY))throw new InvalidOperationException(LocalizedText.Get("没有合适的空位，位置已恢复。"));
            item.Column=col;item.Row=row;item.Monitor=screen.DeviceName;if(!_model.TrySaveWidgets(draft))window.Position();
        }catch(Exception e){window.Position();MessageBox.Show(e.Message,LocalizedText.Get("移动小组件"));}
    }
    internal void Change(Guid id,Action<WidgetInstance> update)
    {
        var draft=_model.Settings.Widgets.Copy();var item=draft.Instances.FirstOrDefault(x=>x.InstanceId==id);if(item==null)return;update(item);
        // Size/source edits are committed only after validating their new footprint against icons and widgets.
        _ =CommitChanged(draft,item);
    }
    private async Task CommitChanged(WidgetOptions draft,WidgetInstance item)
    {
        try{var screen=Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==item.Monitor)??Forms.Screen.PrimaryScreen!;
            var occupied=(await WidgetDesktopLayout.Icons()).Concat(OtherBounds(item.InstanceId)).ToArray();if(_disposed)return;
            if(!WidgetDesktopLayout.FindSpace(item,screen,WidgetDesktopLayout.Dpi(screen),occupied,out int col,out int row,item.Column,item.Row))throw new InvalidOperationException(LocalizedText.Get("该尺寸没有可用空位。"));
            item.Column=col;item.Row=row;var latest=_model.Settings.Widgets.Copy();int index=latest.Instances.FindIndex(x=>x.InstanceId==item.InstanceId);if(index>=0){latest.Instances[index]=item.Copy();_model.TrySaveWidgets(latest);}
        }catch(Exception e){MessageBox.Show(e.Message,LocalizedText.Get("组件设置"));}
    }
    internal void Delete(Guid id){var draft=_model.Settings.Widgets.Copy();draft.Instances.RemoveAll(x=>x.InstanceId==id);_model.TrySaveWidgets(draft);}
    internal void Duplicate(WidgetInstance item){var copy=item.Copy();copy.InstanceId=Guid.NewGuid();_ =AddAsync(copy);}
    private void Reconcile()
    {
        if(_disposed)return;var instances=_model.Settings.Widgets.Instances;
        foreach(var key in _windows.Keys.Where(id=>instances.All(x=>x.InstanceId!=id)).ToArray()){_windows[key].Close();_windows.Remove(key);}
        foreach(var item in instances){if(!_windows.TryGetValue(item.InstanceId,out var window)||!window.Attached){window?.Close();window=new(this,item);_windows[item.InstanceId]=window;try{window.Show();window.Attach();}catch{window.Close();_windows.Remove(item.InstanceId);continue;}}window.Update(item);}
        Changed?.Invoke();
    }
    private void Refresh(WidgetKind? kind){if(_disposed)return;foreach(var w in _windows.Values){var d=WidgetRegistry.Get(w.Instance.DefinitionId);if(w.IsVisible&&w.ContentActive&&(kind==null||d.Kind==kind||kind==WidgetKind.Weather&&d.Layout==WidgetLayout.CalendarWeather||kind==WidgetKind.Clock&&d.Kind==WidgetKind.Calendar&&d.Layout is WidgetLayout.CalendarClock or WidgetLayout.CalendarGiant or WidgetLayout.CalendarPoster or WidgetLayout.CalendarMinimal))w.Render();}}
    internal WidgetRenderData Data(WidgetInstance instance)
    {
        var kind=WidgetRegistry.Get(instance.DefinitionId).Kind;
        var weather=kind==WidgetKind.Weather||WidgetRegistry.Get(instance.DefinitionId).Layout==WidgetLayout.CalendarWeather?Runtime.Weather(instance.Source):default;
        var notes=kind==WidgetKind.Notes?Runtime.Folder(instance.Source.NotesFolder):default;
        return Runtime.Populate(new(_model.Settings.Widgets,Runtime.ResolvedSource(instance.Source),weather.Weather,Runtime.Media,notes.Notes,notes.Error??weather.Error??(kind==WidgetKind.Music?Runtime.MediaError:null),false,
            id=>{var options=_model.Settings.Widgets.Copy();var task=options.Tasks.FirstOrDefault(t=>t.Id==id);if(task!=null){task.Completed=!task.Completed;_model.TrySaveWidgets(options);}},EditTask,EditNote,Runtime.Command,HistoryArt:Runtime.HistoryArt),kind);
    }
    internal void EditTask(Guid id)=>WidgetDataEditor.Task(_model.Settings.Widgets,id,options=>{var latest=_model.Settings.Widgets.Copy();var item=options.Tasks.FirstOrDefault(x=>x.Id==id);if(item==null||!latest.Tasks.Any(x=>x.Id==id))return false;latest.Tasks.RemoveAll(x=>x.Id==id);latest.Tasks.Add(item.Copy());return _model.TrySaveWidgets(latest);});
    internal void EditNote(Guid id)=>WidgetDataEditor.Note(_model.Settings.Widgets,id,options=>{var latest=_model.Settings.Widgets.Copy();var item=options.Notes.FirstOrDefault(x=>x.Id==id);if(item==null||!latest.Notes.Any(x=>x.Id==id))return false;latest.Notes.RemoveAll(x=>x.Id==id);latest.Notes.Add(item.Copy());return _model.TrySaveWidgets(latest);});
    public void Dispose(){if(_disposed)return;_disposed=true;if(_foregroundHook!=0){UnhookWinEvent(_foregroundHook);_foregroundHook=0;}_model.WidgetSettingsChanged-=Reconcile;Runtime.Changed-=Refresh;Runtime.Dispose();_shellCheck.Stop();foreach(var w in _windows.Values.ToArray())w.Close();_windows.Clear();if(Current==this)Current=null;Changed=null;}
}

internal sealed class WidgetDesktopWindow : Window,DesktopWidgetLayers.ISource
{
    private readonly WidgetDesktopService _service;
    private WidgetHost _host=null!;
    private Point? _press;
    private Point _origin;
    private bool _dragging;
    private nint _handle,_parent;
    private bool _floating;
    internal WidgetInstance Instance {get;private set;}
    internal bool ContentActive {get;private set;}=true;
    private DesktopWidgetLayers.Snapshot? _snapshot;
    private long _snapshotVersion,_snapshotTime=long.MinValue/2;
    private int _snapshotQueued;
    nint DesktopWidgetLayers.ISource.Handle=>_handle;
    DesktopWidgetLayers.Snapshot? DesktopWidgetLayers.ISource.Latest=>Volatile.Read(ref _snapshot);
    internal bool Attached=>_handle!=0&&IsWindow(_handle)&&_parent!=0&&IsWindow(_parent)&&(_floating?GetShellWindow()==_parent:GetParent(_handle)==_parent);
    internal WidgetDesktopWindow(WidgetDesktopService service,WidgetInstance instance)
    {
        _service=service;Instance=instance;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;ResizeMode=ResizeMode.NoResize;
        FontFamily=new FontFamily("Segoe UI Variable, Microsoft YaHei UI, Segoe UI");Update(instance);
        PreviewMouseLeftButtonDown+=(_,e)=>{if(Instance.Locked||Interactive(e.OriginalSource as DependencyObject))return;GetCursorPos(out var p);_press=new(p.X,p.Y);_origin=PhysicalTopLeft();_dragging=false;};
        PreviewMouseMove+=(_,e)=>{if(_press==null||e.LeftButton!=MouseButtonState.Pressed)return;GetCursorPos(out var cursor);var delta=new Point(cursor.X-_press.Value.X,cursor.Y-_press.Value.Y);
            if(!_dragging&&Math.Abs(delta.X)+Math.Abs(delta.Y)<7)return;if(!_dragging){_dragging=true;CaptureMouse();_host.SetGestureActive(true);}MovePhysical(_origin.X+delta.X,_origin.Y+delta.Y);e.Handled=true;};
        PreviewMouseLeftButtonUp+=async(_,e)=>{if(_press==null)return;bool moved=_dragging;_press=null;_dragging=false;if(IsMouseCaptured)ReleaseMouseCapture();_host.SetGestureActive(false);if(moved){e.Handled=true;var at=PhysicalTopLeft();await service.Place(this,at);}};
        LostMouseCapture+=(_,_)=>{if(_dragging){_dragging=false;_press=null;Position();_host.SetGestureActive(false);}};
        Closed+=(_,_)=>{_press=null;if(IsMouseCaptured)ReleaseMouseCapture();DesktopWidgetLayers.Remove(this);Volatile.Write(ref _snapshot,null);};
    }
    private static bool Interactive(DependencyObject? node){while(node!=null){if(node is ButtonBase)return true;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}return false;}
    internal void Attach()
    {
        _handle=new WindowInteropHelper(this).Handle;_parent=WidgetDesktopLayout.DesktopParent();if(_parent==0)throw new InvalidOperationException(LocalizedText.Get("Explorer 桌面不可用。"));
        // Since Windows 11 24H2 the desktop window has no redirection surface (WS_EX_NOREDIRECTIONBITMAP) and only
        // composes Explorer's own children: a child window from another process is laid out and hit-tested but never
        // drawn. There the widget stays a top-level tool window kept directly above the desktop in z-order instead.
        _floating=_parent==GetShellWindow()&&(GetWindowLongPtr(_parent,-20).ToInt64()&0x200000L)!=0;
        if(_floating)
        {
            SetWindowLongPtr(_handle,-20,new nint(GetWindowLongPtr(_handle,-20).ToInt64()|0x80L));
            // Clicks must not activate the widget (it would rise over other windows); WM_MOUSEACTIVATE -> MA_NOACTIVATE.
            // WS_EX_NOACTIVATE is not used: glass sampling skips such windows. Every z-order change lands above the desktop.
            HwndSource.FromHwnd(_handle)?.AddHook((nint h,int message,nint w,nint l,ref bool handled)=>{
                if(message==0x21){handled=true;return 3;}
                if(message==0x46){uint flags=(uint)Marshal.ReadInt32(l,IntPtr.Size*2+16);if((flags&0x4)==0){nint above=AboveDesktop();if(above==_handle)Marshal.WriteInt32(l,IntPtr.Size*2+16,(int)(flags|0x4));else Marshal.WriteIntPtr(l,IntPtr.Size,above);}}
                return 0;});
        }
        else
        {
            long style=GetWindowLongPtr(_handle,-16).ToInt64();SetWindowLongPtr(_handle,-16,new nint((style&~0x80000000L)|0x40000000L));
            SetParent(_handle,_parent);if(GetParent(_handle)!=_parent)throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        Position();
        // A top-level widget is sampled like any other window; only a desktop child needs its pixels published.
        if(!_floating){DesktopWidgetLayers.Add(this);QueueSnapshot();}
    }
    // Glass over the desktop asks for this while it samples the widget: a clock hand or a drag keeps it current
    // a few times a second, a widget nobody samples costs nothing.
    void DesktopWidgetLayers.ISource.RequestRefresh(){if(Environment.TickCount64-Interlocked.Read(ref _snapshotTime)>=200)QueueSnapshot();}
    private void QueueSnapshot()
    {
        if(Interlocked.Exchange(ref _snapshotQueued,1)==1)return;
        try{Dispatcher.BeginInvoke(DispatcherPriority.Background,Snapshot);}catch{_snapshotQueued=0;}
    }
    // The window's own pixels at its physical bounds, premultiplied BGRA like a compositor frame.
    private void Snapshot()
    {
        _snapshotQueued=0;Interlocked.Exchange(ref _snapshotTime,Environment.TickCount64);
        if(_floating||!Attached||!IsVisible||Content is not Visual content||!GetWindowRect(_handle,out var rect)){Volatile.Write(ref _snapshot,null);return;}
        int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;if(width<=0||height<=0||(long)width*height>16_000_000){Volatile.Write(ref _snapshot,null);return;}
        var dpi=VisualTreeHelper.GetDpi(this);var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);
        bitmap.Render(content);var pixels=new byte[width*height*4];bitmap.CopyPixels(pixels,width*4,0);
        Volatile.Write(ref _snapshot,new DesktopWidgetLayers.Snapshot(pixels,new System.Drawing.Rectangle(rect.Left,rect.Top,width,height),++_snapshotVersion));
    }
    // The window directly above the desktop, so inserting after it puts this widget just over the wallpaper.
    private nint AboveDesktop(){nint shell=GetShellWindow();nint above=GetWindow(shell,3);return above==0?new nint(1):above;}
    internal void FollowDesktop(){if(_floating&&Attached)SetWindowPos(_handle,AboveDesktop(),0,0,0,0,0x0001|0x0002|0x0010);}
    internal void Update(WidgetInstance item)
    {
        Instance=item;var definition=WidgetRegistry.Get(item.DefinitionId) with {Size=item.Size};
        if(_host==null||_host.Definition!=definition){_host=new(definition,item.Exponent);Content=new Border{Padding=new Thickness(24),Child=_host};}
        Width=item.Size.Width*item.Appearance.PanelScale+48;Height=item.Size.Height*item.Appearance.PanelScale+48;_host.SetExponent(item.Exponent);Render();Menu();if(Attached)Position();
    }
    internal void Render(){var data=_service.Data(Instance);_host.SetWeather(data.Weather);_host.SetMaterial(Instance.Material,Instance.Appearance);_host.SetContent(WidgetContent.Render(_host.Definition,data));if(_handle!=0)QueueSnapshot();}
    internal void RefreshOcclusion()
    {
        if(!Attached||!IsVisible)return;GetWindowRect(_handle,out var rect);bool active=false;
        foreach(var point in new[]{(.5,.5),(.25,.25),(.75,.25),(.25,.75),(.75,.75)}){var hit=WindowFromPoint(new(){X=(int)(rect.Left+(rect.Right-rect.Left)*point.Item1),Y=(int)(rect.Top+(rect.Bottom-rect.Top)*point.Item2)});if(hit==_handle||IsChild(_handle,hit)){active=true;break;}}
        if(active==ContentActive)return;ContentActive=active;_host.SetActive(active);if(active)Render();
    }
    private void Menu()
    {
        var menu=new ContextMenu();void Add(string title,Action action){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Add(LocalizedText.Get("组件设置…"),()=>WidgetInstanceEditor.Show(Instance,copy=>_service.Change(copy.InstanceId,x=>{x.Size=copy.Size;x.Source=copy.Source;x.Appearance=copy.Appearance;x.Material=copy.Material;})));
        var sizes=new MenuItem{Header=LocalizedText.Get("调整尺寸")};foreach(var size in WidgetSizes.For(WidgetRegistry.Get(Instance.DefinitionId))){var item=new MenuItem{Header=size.ToString(),IsCheckable=true,IsChecked=size==Instance.Size};item.Click+=(_,_)=>_service.Change(Instance.InstanceId,x=>x.Size=size);sizes.Items.Add(item);}menu.Items.Add(sizes);
        Add(Instance.Locked?LocalizedText.Get("解锁位置"):LocalizedText.Get("锁定位置"),()=>_service.Change(Instance.InstanceId,x=>x.Locked=!x.Locked));Add(LocalizedText.Get("复制组件"),()=>_service.Duplicate(Instance));Add(LocalizedText.Get("删除组件"),()=>_service.Delete(Instance.InstanceId));ContextMenu=menu;
    }
    internal void Position()
    {
        var screen=Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==Instance.Monitor)??Forms.Screen.PrimaryScreen!;var bounds=WidgetDesktopLayout.Bounds(Instance,screen,WidgetDesktopLayout.Dpi(screen));double pad=24*WidgetDesktopLayout.Dpi(screen)/96;
        var point=new WidgetDesktopLayout.PointI{X=(int)Math.Round(bounds.X-pad),Y=(int)Math.Round(bounds.Y-pad)};if(!_floating)ScreenToClient(_parent,ref point);
        SetWindowPos(_handle,_floating?AboveDesktop():0,point.X,point.Y,(int)Math.Round(bounds.Width+pad*2),(int)Math.Round(bounds.Height+pad*2),0x0010|0x0040);QueueSnapshot();
    }
    private Point PhysicalTopLeft(){GetWindowRect(_handle,out var rect);var dpi=NativeMethods.GetDpiForWindow(_handle);double pad=24*(dpi==0?96:dpi)/96;return new(rect.Left+pad,rect.Top+pad);}
    private void MovePhysical(double x,double y){var point=new WidgetDesktopLayout.PointI{X=(int)Math.Round(x-24*VisualTreeHelper.GetDpi(this).DpiScaleX),Y=(int)Math.Round(y-24*VisualTreeHelper.GetDpi(this).DpiScaleY)};if(!_floating)ScreenToClient(_parent,ref point);SetWindowPos(_handle,0,point.X,point.Y,0,0,0x0010|0x0001|(_floating?0x0004u:0u));}
    [StructLayout(LayoutKind.Sequential)]private struct RectI{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out WidgetDesktopLayout.PointI point);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint h,out RectI rect);
    [DllImport("user32.dll")]private static extern bool ScreenToClient(nint h,ref WidgetDesktopLayout.PointI point);
    [DllImport("user32.dll",SetLastError=true)]private static extern nint SetParent(nint h,nint parent);
    [DllImport("user32.dll")]private static extern nint GetParent(nint h);
    [DllImport("user32.dll")]private static extern nint GetShellWindow();
    [DllImport("user32.dll")]private static extern nint GetWindow(nint h,uint command);
    [DllImport("user32.dll")]private static extern bool IsWindow(nint h);
    [DllImport("user32.dll")]private static extern nint WindowFromPoint(WidgetDesktopLayout.PointI point);
    [DllImport("user32.dll")]private static extern bool IsChild(nint parent,nint child);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern nint GetWindowLongPtr(nint h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern nint SetWindowLongPtr(nint h,int index,nint value);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint h,nint after,int x,int y,int width,int height,uint flags);
}

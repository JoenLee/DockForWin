using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using DockForWindows.Models;
using DockForWindows.Services;
namespace DockForWindows.Views;
public sealed partial class DockWindow
{
    [DllImport("user32.dll")]private static extern bool RegisterShellHookWindow(nint window);
    [DllImport("user32.dll")]private static extern bool DeregisterShellHookWindow(nint window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")]private static extern nint GetAncestor(nint window,uint flags);
    private uint _shellMessage;private nint _shellWindow;
    private readonly HashSet<nint> _attentionWindows=new();
    private ContextMenu? _openDockMenu;private OutsideClickWatcher? _outsideClicks;
    private DockTile? _previewCandidate;private long _previewDue,_previewLastInside;
    private WindowPreviewStack? _windowPreviews;
    private void StartDockInteractions(nint handle)
    {
        _shellMessage=RegisterWindowMessage("SHELLHOOK");if(RegisterShellHookWindow(handle))_shellWindow=handle;
    }
    private void StopDockInteractions()
    {
        _openDockMenu?.SetCurrentValue(ContextMenu.IsOpenProperty,false);_openDockMenu=null;
        CloseWindowPreviews();_outsideClicks?.Dispose();_outsideClicks=null;
        if(_shellWindow!=0){DeregisterShellHookWindow(_shellWindow);_shellWindow=0;}
    }
    private void ObserveShellMessage(int message,nint kind,nint target)
    {
        if(_shellWindow==0||_shellMessage==0||(uint)message!=_shellMessage||target==0)return;
        if(kind==0x8006)_attentionWindows.Add(target);
        else if(kind is 4 or 0x8004)_attentionWindows.Remove(target);
        UpdateAttentionState();
    }
    private void UpdateAttentionState()
    {
        nint foreground=WindowTracker.ForegroundWindow,owner=foreground==0?0:GetAncestor(foreground,3);
        _attentionWindows.RemoveWhere(h=>!NativeMethods.IsWindow(h)||h==foreground||h==owner);
        foreach(var item in _model.Items)item.NeedsAttention=item.Windows.Any(w=>_attentionWindows.Contains(w.Handle));
    }
    private void WatchPopupClicks()
    {
        if(_openDockMenu?.IsOpen==true||_windowPreviews?.IsOpen==true){
            _outsideClicks??=new OutsideClickWatcher(Dispatcher,point=>{
                if(_openDockMenu?.IsOpen==true&&!ContainsScreenPoint(_openDockMenu,point))_openDockMenu.IsOpen=false;
                if(_windowPreviews?.IsOpen==true&&!_windowPreviews.Contains(point)&&!(_previewCandidate is {} tile&&ContainsScreenPoint(tile,point)))CloseWindowPreviews();
            });
        }else{_outsideClicks?.Dispose();_outsideClicks=null;}
    }
    private static bool ContainsScreenPoint(FrameworkElement view,Point point)
    {
        if(!view.IsVisible||PresentationSource.FromVisual(view) is null)return false;
        return new Rect(view.PointToScreen(new Point()),view.PointToScreen(new Point(view.ActualWidth,view.ActualHeight))).Contains(point);
    }
    private void QueueWindowPreviews(DockTile tile)
    {
        if(_dragging||_openDockMenu?.IsOpen==true)return;
        if(_previewCandidate!=tile){CloseWindowPreviews();_previewCandidate=tile;}
        _previewDue=Environment.TickCount64+350;_previewLastInside=Environment.TickCount64;
    }
    private void UpdateDockPopups()
    {
        UpdateAttentionState();if(_previewCandidate is not {} tile)return;long now=Environment.TickCount64;
        bool inside=tile.IsMouseOver||_windowPreviews?.PointerInside==true;
        if(inside)_previewLastInside=now;
        else if(now-_previewLastInside>280){CloseWindowPreviews();return;}
        if(_dragging||_hidden||tile.Item.Windows.Count<2||!tile.IsLoaded||_openDockMenu?.IsOpen==true){CloseWindowPreviews();return;}
        if(_windowPreviews is not null){_windowPreviews.Update(tile.Item.Windows);return;}
        if(now<_previewDue||!tile.IsMouseOver)return;
        _windowPreviews=new WindowPreviewStack(this,tile,ViewSettings.Edge,()=>{_windowPreviews=null;_previewCandidate=null;WatchPopupClicks();});
        _windowPreviews.Show(tile.Item.Windows);WatchPopupClicks();
    }
    private void CloseWindowPreviews()
    {
        var old=_windowPreviews;_windowPreviews=null;_previewCandidate=null;old?.Dispose();WatchPopupClicks();
    }
    internal async void ActivateTrackedWindow(TrackedWindow window)
    {
        bool requested=WindowTracker.Activate(window);
        if(!requested){
            // Restore is asynchronous; verify its result before reporting a foreground denial.
            await System.Threading.Tasks.Task.Delay(120);
            if(_closing)return;
            if(!NativeMethods.IsWindow(window.Handle)){_tracker.Refresh();return;}
            var foreground=WindowTracker.ForegroundWindow;
            if(foreground!=window.Handle&&GetAncestor(foreground,3)!=window.Handle&&!WindowTracker.Activate(window)){
                ShowError(LocalizedText.Get("Windows 暂未允许切换到该窗口，可通过任务栏手动激活。"));return;
            }
        }
        UpdateAttentionState();_tracker.Refresh();
    }
    private void NewApplicationWindow(DockItem item)
    {
        try{_launcher.LaunchNewWindow(item.Pin);_tracker.Refresh();}catch(Exception e){ShowError(e.Message);}
    }
}

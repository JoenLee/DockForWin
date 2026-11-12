using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using Forms=System.Windows.Forms;
namespace DockForWindows.Views;
// Three small native-composited cards; no screenshots, readback buffers or capture timers.
internal sealed class WindowPreviewStack:IDisposable
{
    private readonly DockWindow _owner;private readonly FrameworkElement _anchor;private readonly DockEdge _edge;private readonly Action _closed;
    private readonly List<PreviewCard> _cards=new();private TrackedWindow[] _windows=Array.Empty<TrackedWindow>();private int _index;private bool _disposed;
    internal bool IsOpen=>!_disposed&&_cards.Any(c=>c.IsVisible);
    internal bool PointerInside=>Contains(new Point(Forms.Cursor.Position.X,Forms.Cursor.Position.Y));
    internal int SelectedIndex=>_index;internal int WindowCount=>_windows.Length;
    internal WindowPreviewStack(DockWindow owner,FrameworkElement anchor,DockEdge edge,Action closed){_owner=owner;_anchor=anchor;_edge=edge;_closed=closed;}
    internal void Show(IReadOnlyList<TrackedWindow> windows){_windows=windows.Where(w=>NativeMethods.IsWindow(w.Handle)).ToArray();if(_windows.Length<2){Dispose();return;}Render();}
    internal void Update(IReadOnlyList<TrackedWindow> windows)
    {
        if(_disposed)return;var next=windows.Where(w=>NativeMethods.IsWindow(w.Handle)).ToArray();if(next.Length<2){Dispose();return;}
        if(next.Select(w=>(w.Handle,w.Title)).SequenceEqual(_windows.Select(w=>(w.Handle,w.Title))))return;
        nint selected=_windows[_index].Handle;_windows=next;_index=Math.Max(0,Array.FindIndex(next,w=>w.Handle==selected));Render();
    }
    internal void Scroll(int delta){if(_disposed||delta==0||_windows.Length==0)return;_index=(_index+(delta>0?-1:1)+_windows.Length)%_windows.Length;Render();}
    internal bool Contains(Point point)=>_cards.Any(c=>c.Contains(point));
    private void Render()
    {
        int count=Math.Min(3,_windows.Length);while(_cards.Count<count)_cards.Add(new PreviewCard(this,_owner));
        while(_cards.Count>count){_cards[^1].Close();_cards.RemoveAt(_cards.Count-1);}
        var a=_anchor.PointToScreen(new Point());var b=_anchor.PointToScreen(new Point(_anchor.ActualWidth,_anchor.ActualHeight));
        var screen=Forms.Screen.FromPoint(new System.Drawing.Point((int)((a.X+b.X)/2),(int)((a.Y+b.Y)/2))).WorkingArea;
        double dpi=VisualTreeHelper.GetDpi(_anchor).DpiScaleX;int w=(int)Math.Round(260*dpi),h=(int)Math.Round(178*dpi),gap=(int)Math.Round(12*dpi),step=(int)Math.Round(10*dpi);
        double x=_edge==DockEdge.Left?b.X+gap:_edge==DockEdge.Right?a.X-w-gap:(a.X+b.X-w)/2;
        double y=_edge==DockEdge.Bottom?a.Y-h-gap:(a.Y+b.Y-h)/2;
        x=Math.Clamp(x,screen.Left+gap,Math.Max(screen.Left+gap,screen.Right-w-gap-step*(count-1)));
        y=Math.Clamp(y,screen.Top+gap+step*(count-1),Math.Max(screen.Top+gap+step*(count-1),screen.Bottom-h-gap));
        for(int i=count-1;i>=0;i--){var card=_cards[i];if(!card.IsVisible)card.Show();card.Position((int)x+i*step,(int)y-i*step,w,h);card.Bind(_windows[(_index+i)%_windows.Length],(_index+i)%_windows.Length+1,_windows.Length,i);}
    }
    private void Activate(TrackedWindow window){Dispose();_owner.ActivateTrackedWindow(window);}
    public void Dispose(){if(_disposed)return;_disposed=true;foreach(var card in _cards)card.Close();_cards.Clear();_windows=Array.Empty<TrackedWindow>();_closed();}
    private sealed class PreviewCard:Window
    {
        [StructLayout(LayoutKind.Sequential)]private struct RectI{public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]private struct SizeI{public int Width,Height;}
        [StructLayout(LayoutKind.Sequential)]private struct ThumbnailProperties{public uint Flags;public RectI Destination,Source;public byte Opacity;[MarshalAs(UnmanagedType.Bool)]public bool Visible;[MarshalAs(UnmanagedType.Bool)]public bool ClientOnly;}
        [DllImport("dwmapi.dll")]private static extern int DwmRegisterThumbnail(nint destination,nint source,out nint thumbnail);
        [DllImport("dwmapi.dll")]private static extern int DwmUnregisterThumbnail(nint thumbnail);
        [DllImport("dwmapi.dll")]private static extern int DwmQueryThumbnailSourceSize(nint thumbnail,out SizeI size);
        [DllImport("dwmapi.dll")]private static extern int DwmUpdateThumbnailProperties(nint thumbnail,ref ThumbnailProperties properties);
        [DllImport("user32.dll")]private static extern bool GetClientRect(nint window,out RectI rectangle);
        [DllImport("gdi32.dll")]private static extern nint CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
        [DllImport("user32.dll")]private static extern int SetWindowRgn(nint window,nint region,bool redraw);
        [DllImport("gdi32.dll")]private static extern bool DeleteObject(nint region);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern nint SetLong(nint window,int index,nint value);
        private nint _handle,_thumbnail,_source;private readonly TextBlock _title=new(){FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(10,0,10,0),VerticalAlignment=VerticalAlignment.Center,Foreground=Brushes.White};
        private readonly TextBlock _fallback=new(){Text="无法获取预览 · 点击切换窗口",Foreground=Brushes.LightGray,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        private readonly WindowPreviewStack _stack;private TrackedWindow? _window;private int _depth;
        internal PreviewCard(WindowPreviewStack stack,DockWindow owner)
        {
            _stack=stack;Owner=owner;Width=260;Height=178;Left=Top=-20000;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Background=new SolidColorBrush(Color.FromRgb(34,39,43));Title="Dock 窗口预览";
            var grid=new Grid();grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new(){Height=new GridLength(30)});grid.Children.Add(_fallback);Grid.SetRow(_title,1);grid.Children.Add(_title);Content=grid;
            SourceInitialized+=(_,_)=>{_handle=new WindowInteropHelper(this).Handle;long style=(long)NativeMethods.GetWindowLongPtr(_handle,-20);SetLong(_handle,-20,new nint(style|0x08000000|0x80));((HwndSource)PresentationSource.FromVisual(this)).AddHook((nint h,int m,nint w,nint l,ref bool handled)=>{if(m==0x0021){handled=true;return 3;}return 0;});};
            SizeChanged+=(_,_)=>UpdateThumbnail();MouseWheel+=(_,e)=>{_stack.Scroll(e.Delta);e.Handled=true;};MouseLeftButtonUp+=(_,e)=>{if(_window is {} window)_stack.Activate(window);e.Handled=true;};
            Closed+=(_,_)=>ReleaseThumbnail();
        }
        internal bool Contains(Point point)=>IsVisible&&NativeMethods.GetWindowRect(_handle,out var r)&&point.X>=r.Left&&point.X<r.Right&&point.Y>=r.Top&&point.Y<r.Bottom;
        internal void Position(int x,int y,int width,int height){NativeMethods.SetWindowPos(_handle,-1,x,y,width,height,0x10);UpdateThumbnail();}
        internal void Bind(TrackedWindow window,int page,int total,int depth)
        {
            _window=window;_depth=depth;_title.Text=$"{page}/{total}  {window.Title}";ToolTip=window.Title;
            if(_source!=window.Handle){ReleaseThumbnail();_source=window.Handle;if(DwmRegisterThumbnail(_handle,_source,out _thumbnail)<0)_thumbnail=0;}
            UpdateThumbnail();
        }
        private void UpdateThumbnail()
        {
            if(_handle==0||!GetClientRect(_handle,out var client))return;double dpi=NativeMethods.GetDpiForWindow(_handle)/96d;
            int w=client.Right,h=client.Bottom,radius=(int)Math.Round(28*dpi);if(w<=0||h<=0)return;
            var region=CreateRoundRectRgn(0,0,w+1,h+1,radius,radius);if(region!=0&&SetWindowRgn(_handle,region,true)==0)DeleteObject(region);
            if(_thumbnail==0||DwmQueryThumbnailSourceSize(_thumbnail,out var size)<0||size.Width<=0||size.Height<=0){_fallback.Visibility=Visibility.Visible;return;}
            int pad=(int)Math.Round(4*dpi),availableH=Math.Max(1,h-(int)Math.Round(30*dpi)-pad*2);double scale=Math.Min((w-pad*2)/(double)size.Width,availableH/(double)size.Height);
            int tw=(int)Math.Round(size.Width*scale),th=(int)Math.Round(size.Height*scale),left=(w-tw)/2,top=pad+(availableH-th)/2;
            var p=new ThumbnailProperties{Flags=1|4|8|16,Destination=new(){Left=left,Top=top,Right=left+tw,Bottom=top+th},Opacity=(byte)(255-_depth*25),Visible=true,ClientOnly=false};
            _fallback.Visibility=DwmUpdateThumbnailProperties(_thumbnail,ref p)>=0?Visibility.Collapsed:Visibility.Visible;
        }
        private void ReleaseThumbnail(){if(_thumbnail!=0){DwmUnregisterThumbnail(_thumbnail);_thumbnail=0;}_source=0;}
    }
}

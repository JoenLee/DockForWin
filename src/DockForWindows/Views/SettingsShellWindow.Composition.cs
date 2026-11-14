using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DockForWindows.Services;

namespace DockForWindows.Views;

// How the settings window reaches the screen. Like the control center (ControlCenterWindow.Composition) it is a
// normal top-level window whose per-pixel alpha DWM blends through the frame extended over the client area, not a
// layered window: with AllowsTransparency WPF reads every frame of the whole window back from the GPU for
// UpdateLayeredWindow, which held page scrolling to about half the frame rate (34-44 against 69-75 updates/s, same
// page, same session, 165 Hz panel on the integrated GPU).
// DWM only honours the client area's alpha on a window without a sizing frame: with WS_THICKFRAME it paints the
// rounded corners as solid squares. The window is therefore a WS_POPUP, and the resizing the frame used to provide
// is done here: the caption and edge hit tests are answered here (the system shows the sizing cursors), a press on
// an edge is tracked with mouse capture, and a maximized window is held to the monitor's work area. WindowChrome is
// not used on this window: it reads the style back with IntPtr.ToInt32, which throws on the WS_POPUP bit as soon
// as the window is maximized or minimized. Without a
// sizing frame Windows does not offer edge snapping (dragging to a screen edge, Win+arrow) for this window.
// Software rendering cannot deliver alpha this way, so it keeps the layered window.
internal partial class SettingsShellWindow
{
    private const int StyleCaption = 0x00C00000, StyleThickFrame = 0x00040000;
    private const uint StylePopup = 0x80000000;
    [StructLayout(LayoutKind.Sequential)] private struct FrameMargins { public int Left, Right, Top, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct StyleChange { public uint Old, New; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public NativeMethods.Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeMethods.WindowRect Monitor, Work; public uint Flags; }
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint window, ref FrameMargins margins);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern nint SetCapture(nint window);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern nint LoadCursor(nint instance, nint name);
    [DllImport("user32.dll")] private static extern nint SetCursor(nint cursor);

    internal static bool ComposedShell => ControlCenterWindow.ComposedTransparency;
    private int _sizingEdge;
    private NativeMethods.Point _sizingStart;
    private NativeMethods.WindowRect _sizingFrom;

    private void InstallComposition()
    {
        if (!ComposedShell) return;
        var handle = new WindowInteropHelper(this).Handle; if (handle == 0) return;
        var source = HwndSource.FromHwnd(handle); if (source is null) return;
        source.AddHook(CompositionHook);
        nint style = NativeMethods.GetWindowLongPtr(handle, -16);
        uint wanted = ((uint)(long)style & ~(uint)(StyleCaption | StyleThickFrame)) | StylePopup;
        SetWindowLongPtr(handle, -16, (nint)unchecked((int)wanted));
        NativeMethods.SetWindowPos(handle, 0, 0, 0, 0, 0, 0x0237); // NOSIZE|NOMOVE|NOZORDER|NOACTIVATE|FRAMECHANGED|NOOWNERZORDER
        if (source.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
        var margins = new FrameMargins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(handle, ref margins);
        int none = 1; NativeMethods.DwmSetWindowAttribute(handle, 38, ref none, 4); // DWMSBT_NONE: no Mica behind the clear corners
    }

    private nint CompositionHook(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        switch (message)
        {
            case 0x0084: // WM_NCHITTEST
                handled = true; return HitTest(handle, lParam);
            case 0x007C when (int)wParam == -16: // WM_STYLECHANGING: WPF re-applies the sizing frame whenever it recomputes the style
            {
                var change = Marshal.PtrToStructure<StyleChange>(lParam);
                uint wanted = (change.New & ~(uint)(StyleCaption | StyleThickFrame)) | StylePopup;
                if (wanted != change.New) { change.New = wanted; Marshal.StructureToPtr(change, lParam, false); }
                break;
            }
            case 0x0024: // WM_GETMINMAXINFO: a popup would otherwise maximize over the taskbar
            {
                var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
                var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor))
                {
                    info.MaxPosition = new() { X = monitor.Work.Left - monitor.Monitor.Left, Y = monitor.Work.Top - monitor.Monitor.Top };
                    info.MaxSize = new() { X = monitor.Work.Right - monitor.Work.Left, Y = monitor.Work.Bottom - monitor.Work.Top };
                    Marshal.StructureToPtr(info, lParam, false);
                }
                break;
            }
            case 0x00A1 when (int)wParam is >= 10 and <= 17 && WindowState == WindowState.Normal: // WM_NCLBUTTONDOWN on an edge
                if (NativeMethods.GetCursorPos(out _sizingStart) && NativeMethods.GetWindowRect(handle, out _sizingFrom))
                {
                    _sizingEdge = (int)wParam; SetCapture(handle); SetSizingCursor(); handled = true;
                }
                break;
            case 0x0200 when _sizingEdge != 0: // WM_MOUSEMOVE while sizing
                if (NativeMethods.GetCursorPos(out var cursor)) ResizeToCursor(handle, cursor);
                SetSizingCursor(); handled = true; break;
            case 0x0202 when _sizingEdge != 0: // WM_LBUTTONUP
                _sizingEdge = 0; ReleaseCapture(); handled = true; break;
            case 0x0215: // WM_CAPTURECHANGED
                _sizingEdge = 0; break;
        }
        return 0;
    }

    // What WindowChrome answered before (CaptionHeight = TabBarHeight, ResizeBorderThickness = ResizeBorder): an element
    // marked IsHitTestVisibleInChrome is client; otherwise the window edges resize (corners get a larger grip) and the
    // top band drags the window, which also gives the system's double-click to maximize.
    internal nint HitTest(nint handle, nint lParam)
    {
        if (!NativeMethods.GetWindowRect(handle, out var r)) return 1;
        int x = unchecked((short)(long)lParam) - r.Left, y = unchecked((short)((long)lParam >> 16)) - r.Top;
        var dpi = VisualTreeHelper.GetDpi(this); var point = new Point(x / dpi.DpiScaleX, y / dpi.DpiScaleY);
        if (InputHitTest(point) is { } hit && System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(hit)) return 1; // HTCLIENT
        if (WindowState == WindowState.Normal)
        {
            double width = (r.Right - r.Left) / dpi.DpiScaleX, height = (r.Bottom - r.Top) / dpi.DpiScaleY;
            bool Near(double v, double limit, double grip) => v < grip || v >= limit - grip;
            int column = point.X < ResizeBorder ? 0 : point.X >= width - ResizeBorder ? 2 : 1, row = point.Y < ResizeBorder ? 0 : point.Y >= height - ResizeBorder ? 2 : 1;
            // Within the corner grip along one edge counts as the corner.
            if (row != 1 && Near(point.X, width, CornerGrip)) column = point.X < width / 2 ? 0 : 2;
            if (column != 1 && Near(point.Y, height, CornerGrip)) row = point.Y < height / 2 ? 0 : 2;
            int code = (row, column) switch { (0, 0) => 13, (0, 1) => 12, (0, 2) => 14, (1, 0) => 10, (1, 2) => 11, (2, 0) => 16, (2, 1) => 15, (2, 2) => 17, _ => 0 };
            if (code != 0) return code;
        }
        return point.Y < TabBarHeight ? 2 : 1; // HTCAPTION : HTCLIENT
    }
    private const double ResizeBorder = 6, CornerGrip = 14;

    private void SetSizingCursor()
    {
        int cursor = _sizingEdge switch { 10 or 11 => 32644, 12 or 15 => 32645, 13 or 17 => 32642, _ => 32643 }; // SIZEWE, SIZENS, SIZENWSE, SIZENESW
        SetCursor(LoadCursor(0, cursor));
    }

    internal void BeginSizing(int edge, NativeMethods.Point start, NativeMethods.WindowRect from) { _sizingEdge = edge; _sizingStart = start; _sizingFrom = from; }
    internal void EndSizing() => _sizingEdge = 0;
    internal void ResizeToCursor(nint handle, NativeMethods.Point cursor)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        int minWidth = (int)Math.Ceiling(MinWidth * dpi.DpiScaleX), minHeight = (int)Math.Ceiling(MinHeight * dpi.DpiScaleY);
        int dx = cursor.X - _sizingStart.X, dy = cursor.Y - _sizingStart.Y;
        var r = _sizingFrom;
        bool left = _sizingEdge is 10 or 13 or 16, right = _sizingEdge is 11 or 14 or 17, top = _sizingEdge is 12 or 13 or 14, bottom = _sizingEdge is 15 or 16 or 17;
        if (left) r.Left = Math.Min(r.Left + dx, r.Right - minWidth);
        if (right) r.Right = Math.Max(r.Right + dx, r.Left + minWidth);
        if (top) r.Top = Math.Min(r.Top + dy, r.Bottom - minHeight);
        if (bottom) r.Bottom = Math.Max(r.Bottom + dy, r.Top + minHeight);
        NativeMethods.SetWindowPos(handle, 0, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, 0x0214); // NOZORDER|NOACTIVATE|NOOWNERZORDER
    }
}

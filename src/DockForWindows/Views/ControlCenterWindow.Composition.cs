using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DockForWindows.Services;

namespace DockForWindows.Views;

// How the panel reaches the screen. It is a normal top-level window whose per-pixel alpha DWM blends through the
// frame extended over the whole client area, the way Mica/acrylic WPF windows become transparent, rather than a
// layered window: with AllowsTransparency WPF reads every frame back from the GPU and hands it to
// UpdateLayeredWindow, which on a laptop's integrated GPU at 165 Hz cost about a fifth of the frame rate.
// Software rendering cannot deliver alpha this way, so it keeps the layered window.
public sealed partial class ControlCenterWindow
{
    [StructLayout(LayoutKind.Sequential)] private struct FrameMargins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint window, ref FrameMargins margins);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, bool redraw);
    internal static bool ComposedTransparency { get; } =
        (RenderCapability.Tier >> 16) >= 2 && !SystemParameters.IsRemoteSession && RenderOptions.ProcessRenderMode != RenderMode.SoftwareOnly;
    // The peripheral frost's falloff reaches 57 DIP beyond the panel (ClassicBlurBackdrop.SetFalloff), plus slack.
    private const double HaloReach = 60;
    private Int32Rect _inputRegion = Int32Rect.Empty;
    private bool _gutterActive;
    private void InstallComposedTransparency()
    {
        if (!ComposedTransparency) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
        var margins = new FrameMargins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(handle, ref margins);
        int corner = 1; DwmSetWindowAttribute(handle, 33, ref corner, 4);                  // DWMWCP_DONOTROUND: the panel draws its own shape
        int border = unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(handle, 34, ref border, 4); // DWMWA_COLOR_NONE: no system outline
    }
    // A layered window lets clicks through wherever it is fully transparent; this window takes every click inside
    // its rectangle. The clear strips beyond the halo are therefore cut away with a window region, so a click there
    // still reaches the window underneath. While content reaches into the gutters (a card being dragged or resized,
    // the safety fade showing) the whole window is kept, as that content may be drawn there.
    private void UpdateInputRegion()
    {
        if (!ComposedTransparency || !_ready || _closing) return;
        var handle = new WindowInteropHelper(this).Handle; if (handle == 0) return;
        var region = Int32Rect.Empty;
        if (!_gutterActive && _drag is null && !_resizing && NativeMethods.GetWindowRect(handle, out var window))
        {
            double scale = _fit.ScaleX * VisualTreeHelper.GetDpi(this).DpiScaleX;
            int side = (int)Math.Floor((SideGutter - HaloReach) * scale), bottom = (int)Math.Floor((VerticalGutter - HaloReach) * scale);
            int width = window.Right - window.Left, height = window.Bottom - window.Top;
            // The top stays whole: the entrance lifts the panel and its halo up to 96 DIP into the top gutter.
            if (side > 0 && width > side * 2 && height > bottom) region = new Int32Rect(side, 0, width - side * 2, height - Math.Max(0, bottom));
        }
        if (region == _inputRegion) return;
        _inputRegion = region;
        if (region.IsEmpty) { SetWindowRgn(handle, 0, true); return; }
        nint created = CreateRectRgn(region.X, region.Y, region.X + region.Width, region.Y + region.Height);
        if (created != 0 && SetWindowRgn(handle, created, true) == 0) DeleteObject(created); // an accepted region belongs to the system
    }
    // Content in the gutters gets the window's edge feather and the whole window for input.
    private void SetGutterActive(bool active)
    {
        if (_gutterActive == active) return;
        _gutterActive = active; UpdateMaterialMask(); UpdateInputRegion();
    }
}

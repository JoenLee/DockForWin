using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DockForWindows.Views;

// What a window's glass sees when nothing else is behind it: the desktop picture (the same source Windows'
// Mica samples), frosted once on a worker thread and drawn screen-aligned, so the colour drifts as the window
// moves, the way macOS tints windows and sidebars with the desktop. Light and dark get their own tone curve:
// light lifts dark pictures toward pastel, dark deepens bright ones, so text contrast never depends on luck.
internal sealed class WallpaperBackdrop : FrameworkElement
{
    public static readonly DependencyProperty DarkProperty = DependencyProperty.Register(nameof(Dark), typeof(bool), typeof(WallpaperBackdrop), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    // Opacity of a pixel-fine grain over the picture: breaks 8-bit banding in the blur and gives the surface tooth.
    public static readonly DependencyProperty GrainProperty = DependencyProperty.Register(nameof(Grain), typeof(double), typeof(WallpaperBackdrop), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool Dark { get => (bool)GetValue(DarkProperty); set => SetValue(DarkProperty, value); }
    public double Grain { get => (double)GetValue(GrainProperty); set => SetValue(GrainProperty, value); }
    private Window? _window;
    private Point _origin = new(double.NaN, double.NaN);
    public WallpaperBackdrop()
    {
        IsHitTestVisible = false; ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear);
        Loaded += (_, _) => Attach(); Unloaded += (_, _) => Detach();
    }
    private void Attach()
    {
        Detach(); _window = Window.GetWindow(this);
        if (_window is not null) { _window.LocationChanged += Invalidate; _window.DpiChanged += Invalidate; }
        WallpaperFrost.Changed += Refresh; LayoutUpdated += Relaid; InvalidateVisual();
    }
    private void Detach()
    {
        if (_window is not null) { _window.LocationChanged -= Invalidate; _window.DpiChanged -= Invalidate; }
        _window = null; WallpaperFrost.Changed -= Refresh; LayoutUpdated -= Relaid;
    }
    private void Invalidate(object? sender, EventArgs args) => InvalidateVisual();
    private void Refresh() => InvalidateVisual();
    // Moving inside the window (sidebar folding, page transitions) does not re-render by itself.
    private void Relaid(object? sender, EventArgs args)
    {
        if (PresentationSource.FromVisual(this) is not null && PointToScreen(new Point()) != _origin) InvalidateVisual();
    }
    protected override void OnRender(DrawingContext drawing)
    {
        var bounds = new Rect(RenderSize);
        if (bounds.IsEmpty || PresentationSource.FromVisual(this) is not HwndSource source) return;
        _origin = PointToScreen(new Point());
        var frost = WallpaperFrost.Get(source.Handle);
        if (frost is null) { drawing.DrawRectangle(WallpaperFrost.Placeholder(Dark), null, bounds); return; }
        // The picture's rectangle is in physical screen pixels; map both corners so DPI and transforms hold.
        var a = PointFromScreen(new Point(frost.Cover.Left, frost.Cover.Top)); var b = PointFromScreen(new Point(frost.Cover.Right, frost.Cover.Bottom));
        drawing.DrawImage(Dark ? frost.Dark : frost.Light, new Rect(a, b));
        if (Grain > 0)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            drawing.PushOpacity(Grain); drawing.DrawRectangle(WallpaperFrost.GrainBrush(dpi.DpiScaleX), null, bounds); drawing.Pop();
        }
    }
}

internal static class WallpaperFrost
{
    // Cover: where the picture lies on screen (physical pixels; may overhang the monitor like "Fill").
    // Clear: the picture itself, for liquid glass to refract. Light/Dark: heavily frosted, toned per appearance.
    internal sealed record Frost(BitmapSource Light, BitmapSource Dark, BitmapSource Clear, Rect Cover);
    private sealed class Entry { public Frost? Frost; public bool Loading; public bool Stale; }
    private static readonly Dictionary<string, Entry> Cache = new(StringComparer.OrdinalIgnoreCase);
    public static event Action? Changed;
    private static bool _watching;
    private const int Width = 480;

    public static Frost? Get(nint window)
    {
        if (!FindMonitor(window, out var device, out var monitor)) return null;
        if (!_watching) { _watching = true; SystemEvents.UserPreferenceChanged += PreferenceChanged; }
        if (!Cache.TryGetValue(device, out var entry)) Cache[device] = entry = new Entry();
        if ((entry.Frost is null || entry.Stale) && !entry.Loading) Load(device, monitor, entry);
        return entry.Frost;
    }
    // Starts decoding before a window is shown so its first frame already carries the picture.
    public static void Warm(nint window) => Get(window);
    private static void PreferenceChanged(object sender, UserPreferenceChangedEventArgs args)
    {
        if (args.Category is not (UserPreferenceCategory.Desktop or UserPreferenceCategory.General or UserPreferenceCategory.Color)) return;
        foreach (var entry in Cache.Values) entry.Stale = true;
        Changed?.Invoke();
    }
    private static void Load(string device, Rect monitor, Entry entry)
    {
        entry.Loading = true;
        var (path, fill, spanned) = Resolve(monitor);
        // Physical pixels throughout (SM_XVIRTUALSCREEN.. for a picture spanned across monitors).
        var target = spanned ? new Rect(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79)) : monitor;
        var dispatcher = Dispatcher.CurrentDispatcher;
        Task.Run(() => Build(path, fill, target)).ContinueWith(task => dispatcher.BeginInvoke(() =>
        {
            entry.Loading = false;
            if (task.Status == TaskStatus.RanToCompletion) { entry.Frost = task.Result; entry.Stale = false; Changed?.Invoke(); }
        }));
    }

    private static Frost Build(string? path, Color fill, Rect target)
    {
        BitmapSource clear;
        try { clear = path is { Length: > 0 } && File.Exists(path) ? Decode(path, ClearWidth) : Aurora(fill); }
        catch { clear = Aurora(fill); }
        clear = new FormatConvertedBitmap(clear, PixelFormats.Bgra32, null, 0); clear.Freeze();
        // The frosted copies start from a small version: blur is cheap there and the result is smooth anyway.
        var small = new TransformedBitmap(clear, new ScaleTransform((double)Width / clear.PixelWidth, (double)Width / clear.PixelWidth));
        var converted = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth, h = converted.PixelHeight; var pixels = new byte[w * h * 4];
        converted.CopyPixels(pixels, w * 4, 0);
        Blur(pixels, w, h, Math.Max(3, w / 34));
        // "Fill": scale the picture to cover the target, centred.
        double scale = Math.Max(target.Width / clear.PixelWidth, target.Height / clear.PixelHeight);
        var cover = new Rect(target.X + (target.Width - clear.PixelWidth * scale) / 2, target.Y + (target.Height - clear.PixelHeight * scale) / 2, clear.PixelWidth * scale, clear.PixelHeight * scale);
        return new Frost(Tone(pixels, w, h, false), Tone(pixels, w, h, true), clear, cover);
    }
    // Decoded below screen size on purpose: magnified, it carries the faint scatter of real glass, so busy photos never fight the text.
    private const int ClearWidth = 960;
    private static BitmapSource Decode(string path, int width)
    {
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.UriSource = new Uri(path); image.DecodePixelWidth = width; image.EndInit(); image.Freeze();
        return image;
    }
    // No picture (solid colour, or none readable): a soft aurora from the desktop colour, so the glass still has
    // light in it. Computed as plain pixels: this runs on a worker thread, where no visual may be created.
    private static BitmapSource Aurora(Color fill)
    {
        const int w = 320, h = 200; var pixels = new byte[w * h * 4];
        var key = fill.R + fill.G + fill.B < 60 ? Color.FromRgb(0x2C, 0x5B, 0xD8) : fill;
        (Color Color, double X, double Y, double R)[] blobs = { (key, 70, 150, 150), (Color.FromRgb(0x3F, 0xC8, 0xE0), 250, 40, 130), (Color.FromRgb(0x8E, 0x7C, 0xF8), 300, 190, 120) };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double r = key.R + (255 - key.R) * .55, g = key.G + (255 - key.G) * .55, b = key.B + (255 - key.B) * .55;
                foreach (var blob in blobs)
                {
                    double d = Math.Sqrt((x - blob.X) * (x - blob.X) + (y - blob.Y) * (y - blob.Y)) / blob.R, a = Math.Max(0, 1 - d) * .86;
                    r += (blob.Color.R - r) * a; g += (blob.Color.G - g) * a; b += (blob.Color.B - b) * a;
                }
                int i = (y * w + x) * 4; pixels[i] = (byte)b; pixels[i + 1] = (byte)g; pixels[i + 2] = (byte)r; pixels[i + 3] = 255;
            }
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4); bitmap.Freeze();
        return bitmap;
    }

    // Three box passes per axis approximate a Gaussian; edges clamp so the frame never darkens at the sides.
    private static void Blur(byte[] pixels, int w, int h, int radius)
    {
        var scratch = new byte[pixels.Length];
        for (int pass = 0; pass < 3; pass++) { Box(pixels, scratch, w, h, radius, true); Box(scratch, pixels, w, h, radius, false); }
    }
    private static void Box(byte[] source, byte[] target, int w, int h, int radius, bool horizontal)
    {
        int lines = horizontal ? h : w, length = horizontal ? w : h, span = radius * 2 + 1;
        int step = horizontal ? 4 : w * 4;
        Parallel.For(0, lines, line =>
        {
            int start = horizontal ? line * w * 4 : line * 4;
            for (int c = 0; c < 4; c++)
            {
                int sum = 0;
                for (int i = -radius; i <= radius; i++) sum += source[start + Math.Clamp(i, 0, length - 1) * step + c];
                for (int i = 0; i < length; i++)
                {
                    target[start + i * step + c] = (byte)(sum / span);
                    sum += source[start + Math.Min(length - 1, i + radius + 1) * step + c] - source[start + Math.Max(0, i - radius) * step + c];
                }
            }
        });
    }
    // Frosted milk glass: the picture's colour is quietened (never boosted — the UI must not wear the wallpaper),
    // then lifted toward porcelain in light, deepened toward graphite in dark.
    private static BitmapSource Tone(byte[] blurred, int w, int h, bool dark)
    {
        var pixels = (byte[])blurred.Clone();
        double saturation = dark ? .5 : .55;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            double b = pixels[i] / 255d, g = pixels[i + 1] / 255d, r = pixels[i + 2] / 255d;
            double y = .2126 * r + .7152 * g + .0722 * b;
            r = y + (r - y) * saturation; g = y + (g - y) * saturation; b = y + (b - y) * saturation;
            if (dark) { r *= .45; g *= .45; b *= .47; }
            else { r = 1 - (1 - r) * .8; g = 1 - (1 - g) * .8; b = 1 - (1 - b) * .8; }
            pixels[i] = (byte)Math.Clamp(Math.Round(b * 255), 0, 255); pixels[i + 1] = (byte)Math.Clamp(Math.Round(g * 255), 0, 255);
            pixels[i + 2] = (byte)Math.Clamp(Math.Round(r * 255), 0, 255); pixels[i + 3] = 255;
        }
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4); bitmap.Freeze();
        return bitmap;
    }

    // Average luminance (0..1) of a region of a picture, sampled on a coarse grid; pixels are read once per picture.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BitmapSource, byte[]> Pixels = new();
    public static double Luminance(BitmapSource image, Rect region)
    {
        var pixels = Pixels.GetValue(image, source =>
        {
            var converted = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(bytes, converted.PixelWidth * 4, 0); return bytes;
        });
        int w = image.PixelWidth, h = image.PixelHeight;
        int x0 = Math.Clamp((int)region.X, 0, w - 1), y0 = Math.Clamp((int)region.Y, 0, h - 1), x1 = Math.Clamp((int)region.Right, x0 + 1, w), y1 = Math.Clamp((int)region.Bottom, y0 + 1, h);
        int sx = Math.Max(1, (x1 - x0) / 24), sy = Math.Max(1, (y1 - y0) / 24); double sum = 0; int n = 0;
        for (int y = y0; y < y1; y += sy) for (int x = x0; x < x1; x += sx) { int i = (y * w + x) * 4; sum += (pixels[i + 2] * .2126 + pixels[i + 1] * .7152 + pixels[i] * .0722) / 255; n++; }
        return n > 0 ? sum / n : .5;
    }
    private static readonly Dictionary<int, ImageBrush> Grains = new();
    public static Brush GrainBrush(double scale)
    {
        int key = (int)Math.Round(scale * 100);
        if (Grains.TryGetValue(key, out var cached)) return cached;
        const int size = 128; var pixels = new byte[size * size * 4]; var random = new Random(1985);
        for (int i = 0; i < pixels.Length; i += 4) { byte v = (byte)(random.Next(2) * 255); pixels[i] = pixels[i + 1] = pixels[i + 2] = v; pixels[i + 3] = (byte)random.Next(40, 256); }
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4); bitmap.Freeze();
        // One grain texel per physical pixel.
        var brush = new ImageBrush(bitmap) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, size / scale, size / scale), Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor); brush.Freeze();
        Grains[key] = brush; return brush;
    }
    private static readonly Brush LightPlaceholder = Frozen(Color.FromRgb(0xEE, 0xF0, 0xF4)), DarkPlaceholder = Frozen(Color.FromRgb(0x1F, 0x20, 0x24));
    public static Brush Placeholder(bool dark) => dark ? DarkPlaceholder : LightPlaceholder;
    private static Brush Frozen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }

    // ---- Which picture is on which monitor ----
    private static bool FindMonitor(nint window, out string device, out Rect bounds)
    {
        device = ""; bounds = Rect.Empty;
        var handle = MonitorFromWindow(window, 2); var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (handle == 0 || !GetMonitorInfo(handle, ref info)) return false;
        device = info.Device; bounds = new Rect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top);
        return bounds.Width > 0 && bounds.Height > 0;
    }
    private static (string? Path, Color Fill, bool Spanned) Resolve(Rect monitor)
    {
        string? path = null; var fill = Color.FromRgb(0x2C, 0x5B, 0xD8); bool spanned = false;
        try
        {
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
            try
            {
                spanned = wallpaper.GetPosition() == 5;
                uint color = wallpaper.GetBackgroundColor(); fill = Color.FromRgb((byte)(color & 0xFF), (byte)((color >> 8) & 0xFF), (byte)((color >> 16) & 0xFF));
                uint count = wallpaper.GetMonitorDevicePathCount();
                for (uint i = 0; i < count && path is null; i++)
                {
                    string id = wallpaper.GetMonitorDevicePathAt(i);
                    var rect = wallpaper.GetMonitorRECT(id);
                    if (rect.Left == (int)monitor.Left && rect.Top == (int)monitor.Top) path = wallpaper.GetWallpaper(id);
                }
            }
            finally { Marshal.FinalReleaseComObject(wallpaper); }
        }
        catch (COMException) { }
        catch (InvalidCastException) { }
        if (string.IsNullOrEmpty(path))
        {
            var buffer = new char[520];
            if (SystemParametersInfo(0x0073, (uint)buffer.Length, buffer, 0)) path = new string(buffer).TrimEnd('\0');
        }
        return (string.IsNullOrWhiteSpace(path) ? null : path, fill, spanned);
    }

    [ComImport, Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")] private class DesktopWallpaperClass { }
    // Only the leading methods are declared; vtable order follows shobjidl_core.h.
    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint index);
        uint GetMonitorDevicePathCount();
        NativeRect GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(int position);
        int GetPosition();
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SystemParametersInfo(uint action, uint size, char[] buffer, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
}

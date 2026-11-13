using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace DockForWindows.Services;

// Print the windows UNDER the dock, rather than screen-grabbing the dock back into itself.
// All work runs on a background worker; nothing is written to disk or capture-excluded.
public sealed class GlassBackdropService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    private delegate bool EnumCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern bool PrintWindow(nint window, nint dc, uint flags);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("combase.dll")] private static extern int RoInitialize(uint mode);
    [DllImport("combase.dll")] private static extern void RoUninitialize();
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);
    private Bitmap? _desktop;
    private Rectangle _desktopBounds;
    private DateTime _desktopTime;
    private bool _disposed;
    private readonly Dictionary<nint, long> _timedOutWindows = new();
    private readonly ConcurrentDictionary<nint, byte> _activePrints = new();
    private int _pendingPrints;
    private WindowFrameCapture? _capture;
    private bool _captureUnavailable;
    private Bitmap? _composite;
    private byte[]? _pixelBuffer;
    public string LastSourceWindows { get; private set; } = "";
    public bool RetainWindowSamples {get;set;}
    private sealed record SavedSample(Bitmap Image,Rectangle Bounds,NativeRect Window,long Time,WindowFrameCapture.FrameStamp? Stamp){public long LastUsed=Time,LastConfirmed=Time;}
    private readonly Dictionary<nint,SavedSample> _savedSamples=new();
    // Last raw (Dock) frame, the pixels it was made from and what it showed, for exact reuse while idle.
    private BitmapSource? _rawFrame;private byte[]? _rawPixels;private string? _rawFrameKey;
    private Dictionary<nint,WindowFrameCapture.FrameStamp>? _rawFrameStamps;
    public long ReusedFrames { get; private set; }

    public BitmapSource? Render(nint dock, Rectangle bounds, double radius, double amount, double dpi, bool raw = false)
        => RenderCore(dock,bounds,radius,amount,dpi,raw,false,out _);

    // Borrowed until the next read; callers retaining a frame must copy before requesting another.
    internal readonly record struct PixelFrame(byte[] Pixels,int Width,int Height);
    internal PixelFrame? ReadPixels(nint window,Rectangle bounds,double dpi)
    {
        RenderCore(window,bounds,0,0,dpi,true,true,out var pixels);
        return pixels;
    }
    private BitmapSource? RenderCore(nint dock,Rectangle bounds,double radius,double amount,double dpi,bool raw,bool pixelsOnly,out PixelFrame? result)
    {
        result=null;
        if (_disposed || bounds.Width <= 0 || bounds.Height <= 0) return null;
        nint oldDpi = SetThreadDpiAwarenessContext(new nint(-4));
        int initialized = RoInitialize(1);
        try
        {
            // Pad the source so light can bend pixels from outside the glass edge inward.
            int pad = (int)Math.Ceiling(24 * dpi);
            var sample = Rectangle.Inflate(bounds, pad, pad);
            nint desktop = GetShellWindow();
            var layers = Layers(dock, desktop, sample);
            // Compose in z-order. Protected/unsupported windows fall back to the lower layer.
            LastSourceWindows = string.Join(", ", layers.ConvertAll(layer => layer.Window.ToString() + ":" + layer.Bounds));
            // Dock frames: when the sampled rectangle, the z-ordered layers and every layer's compositor
            // frame are unchanged, the previous composite is still exact. Skip the GPU copies, the CPU
            // composite and the WPF upload; the next tick still checks every layer, so a change shows at once.
            bool rawFrame = raw && !pixelsOnly;
            string? key = rawFrame ? RawFrameKey(sample, desktop, layers) : null;
            var stamps = rawFrame ? PeekStamps(desktop, layers, sample) : null;
            if (rawFrame && _rawFrame is not null && stamps is not null && key == _rawFrameKey && SameStamps(stamps, _rawFrameStamps))
            {
                ReusedFrames++; _capture?.Trim();
                return _rawFrame;
            }
            if (_composite is null || _composite.Width != sample.Width || _composite.Height != sample.Height)
            { _composite?.Dispose(); _composite = new Bitmap(sample.Width, sample.Height, PixelFormat.Format32bppArgb); _pixelBuffer = new byte[sample.Width * sample.Height * 4]; }
            var composite = _composite;
            using (var graphics = Graphics.FromImage(composite))
            {
                graphics.Clear(System.Drawing.Color.FromArgb(38, 44, 52));
                if (_desktop is null || _desktopBounds != sample || DateTime.UtcNow - _desktopTime > TimeSpan.FromMilliseconds(12))
                {
                    var next = ReadWindow(desktop, sample, out var desktopBounds);
                    if (next is not null) { _desktop?.Dispose(); _desktop = next; _desktopBounds = desktopBounds; _desktopTime = DateTime.UtcNow; }
                }
                if (_desktop is not null) Composite(graphics, _desktop, _desktopBounds, sample);
                for (int i = layers.Count - 1; i >= 0; i--)
                {
                    if (_disposed) return null;
                    using var bitmap = ReadWindow(layers[i].Window, sample, out var layerBounds);
                    if (bitmap is not null) Composite(graphics, bitmap, layerBounds, sample);
                }
            }
            _capture?.Trim();
            foreach(var window in new List<nint>(_savedSamples.Keys))if(Environment.TickCount64-_savedSamples[window].LastUsed>1800||!NativeMethods.IsWindow(window)){_savedSamples[window].Image.Dispose();_savedSamples.Remove(window);}
            LastSourceWindows += $" gpuCopies={_capture?.Copies ?? 0} gpuError={_capture?.LastError} reused={ReusedFrames}";
            var data = composite.LockBits(new Rectangle(0, 0, composite.Width, composite.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = _pixelBuffer!;
            try { for (int y = 0; y < composite.Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * composite.Width * 4, composite.Width * 4); }
            finally { composite.UnlockBits(data); }
            // Gaussian kernels near the monitor edge must extend real edge colours,
            // not blend a made-up dark padding strip into the glass.
            var valid = Rectangle.Intersect(sample, System.Windows.Forms.SystemInformation.VirtualScreen);
            if (!valid.IsEmpty)
            {
                int left = valid.Left - sample.Left, right = valid.Right - sample.Left - 1;
                int top = valid.Top - sample.Top, bottom = valid.Bottom - sample.Top - 1, stride = composite.Width * 4;
                for (int y = 0; y < composite.Height; y++)
                {
                    int sourceY = Math.Clamp(y, top, bottom);
                    if (sourceY != y) Buffer.BlockCopy(pixels, sourceY * stride, pixels, y * stride, stride);
                    int row = y * stride;
                    for (int x = 0; x < left; x++) Buffer.BlockCopy(pixels, row + left * 4, pixels, row + x * 4, 4);
                    for (int x = right + 1; x < composite.Width; x++) Buffer.BlockCopy(pixels, row + right * 4, pixels, row + x * 4, 4);
                }
            }
            if(pixelsOnly){result=new PixelFrame(pixels,composite.Width,composite.Height);return null;}
            if (!raw) return GlassRefraction.Render(pixels, composite.Width, composite.Height, pad, bounds.Width, bounds.Height, radius, amount, dpi);
            int length = composite.Width * composite.Height * 4;
            if (_rawFrame is not null && _rawPixels is not null && _rawPixels.Length == length && pixels.AsSpan(0, length).SequenceEqual(_rawPixels))
            {
                // A layer changed outside the sampled region, or could not be stamped, but these pixels did not.
                _rawFrameKey = key; _rawFrameStamps = stamps; ReusedFrames++;
                return _rawFrame;
            }
            var frame = BitmapSource.Create(composite.Width, composite.Height, 96, 96, PixelFormats.Bgra32, null, pixels, composite.Width * 4); frame.Freeze();
            if (_rawPixels is null || _rawPixels.Length != length) _rawPixels = new byte[length];
            Buffer.BlockCopy(pixels, 0, _rawPixels, 0, length);
            _rawFrame = frame; _rawFrameKey = key; _rawFrameStamps = stamps;
            return frame;
        }
        finally { if (initialized >= 0) RoUninitialize(); SetThreadDpiAwarenessContext(oldDpi); }
    }
    private static List<(nint Window, Rectangle Bounds)> Layers(nint dock, nint desktop, Rectangle sample)
    {
        var layers = new List<(nint Window, Rectangle Bounds)>();
        bool below = false;
        EnumWindows((window, _) =>
        {
            if (window == dock) { below = true; return true; }
            if (!below || window == desktop || !IsWindowVisible(window) || IsIconic(window)) return true;
            var className = new StringBuilder(128); GetClassName(window, className, className.Capacity);
            if (className.ToString() is "WorkerW" or "Progman" or "CEF-OSC-WIDGET") return true;
            // Driver/game HUDs often expose invisible full-screen windows. They are
            // not a backdrop layer and some never return from WM_PRINT.
            long extendedStyle = (long)NativeMethods.GetWindowLongPtr(window, -20);
            if ((extendedStyle & (0x20L | 0x08000000L)) != 0) return true;
            if (DwmGetWindowAttribute(window, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            if (!GetWindowRect(window, out var r)) return true;
            var rectangle = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            if (rectangle.Width > 0 && rectangle.Height > 0 && rectangle.IntersectsWith(sample)) layers.Add((window, rectangle));
            return true;
        }, 0);
        return layers;
    }
    private static string RawFrameKey(Rectangle sample, nint desktop, List<(nint Window, Rectangle Bounds)> layers)
    {
        var key = new StringBuilder(64 + layers.Count * 48);
        key.Append(sample).Append('|').Append(System.Windows.Forms.SystemInformation.VirtualScreen).Append('|').Append(desktop);
        foreach (var layer in layers) key.Append('|').Append(layer.Window).Append(':').Append(layer.Bounds);
        return key.ToString();
    }
    // Null when any layer has no live compositor frame to compare (PrintWindow fallback, first frame, failure).
    private Dictionary<nint, WindowFrameCapture.FrameStamp>? PeekStamps(nint desktop, List<(nint Window, Rectangle Bounds)> layers, Rectangle sample)
    {
        if (_capture is null || !_capture.TryPeekStamp(desktop, sample, out var desktopStamp)) return null;
        var stamps = new Dictionary<nint, WindowFrameCapture.FrameStamp>(layers.Count + 1) { [desktop] = desktopStamp };
        foreach (var layer in layers)
        {
            if (!_capture.TryPeekStamp(layer.Window, sample, out var stamp)) return null;
            stamps[layer.Window] = stamp;
        }
        return stamps;
    }
    private static bool SameStamps(Dictionary<nint, WindowFrameCapture.FrameStamp> current, Dictionary<nint, WindowFrameCapture.FrameStamp>? previous)
    {
        if (previous is null || previous.Count != current.Count) return false;
        foreach (var pair in current) if (!previous.TryGetValue(pair.Key, out var stamp) || stamp != pair.Value) return false;
        return true;
    }
    private Bitmap? ReadWindow(nint window, Rectangle sample, out Rectangle captured)
    {
        if (_capture is null && !_captureUnavailable)
        {
            try { _capture = new WindowFrameCapture(RetainWindowSamples?TimeSpan.FromMilliseconds(4):null); }
            catch { _captureUnavailable = true; }
        }
        if (_capture is not null)
        {
            _savedSamples.TryGetValue(window,out var previous);
            var latest = _capture.Read(window, sample, out captured, RetainWindowSamples ? previous?.Stamp : null, out var stamp, out bool unchanged);
            if (unchanged && previous is not null) {
                previous.LastUsed=previous.LastConfirmed=Environment.TickCount64;
                return (Bitmap)previous.Image.Clone();
            }
            if (latest is not null){Remember(window,latest,captured,stamp);return latest;}
        }
        // A missing compositor frame is not an empty window. Retain the last valid
        // layer briefly rather than alternating between that layer and the wallpaper.
        if(RetainWindowSamples&&_savedSamples.TryGetValue(window,out var saved)&&Environment.TickCount64-saved.LastConfirmed<250&&GetWindowRect(window,out var current)&&current.Equals(saved.Window)&&saved.Bounds.IntersectsWith(sample)){
            captured=saved.Bounds;return (Bitmap)saved.Image.Clone();
        }
        var printed=PrintBounded(window,sample,out captured);if(printed is not null){Remember(window,printed,captured);return printed;}
        if(RetainWindowSamples&&_savedSamples.TryGetValue(window,out saved)&&GetWindowRect(window,out current)&&current.Equals(saved.Window)&&saved.Bounds.IntersectsWith(sample)){
            saved.LastUsed=Environment.TickCount64;captured=saved.Bounds;return (Bitmap)saved.Image.Clone();
        }
        return null;
    }
    private void Remember(nint window,Bitmap image,Rectangle bounds,WindowFrameCapture.FrameStamp? stamp=null)
    {
        if(!RetainWindowSamples||!GetWindowRect(window,out var position))return;
        if(_savedSamples.Remove(window,out var old))old.Image.Dispose();
        _savedSamples[window]=new SavedSample((Bitmap)image.Clone(),bounds,position,Environment.TickCount64,stamp);
    }
    private static void Composite(Graphics graphics, Bitmap bitmap, Rectangle window, Rectangle sample)
    {
        var intersection = Rectangle.Intersect(window, sample);
        if (intersection.IsEmpty) return;
        graphics.DrawImage(bitmap, new Rectangle(intersection.X - sample.X, intersection.Y - sample.Y, intersection.Width, intersection.Height),
            new Rectangle(intersection.X - window.X, intersection.Y - window.Y, intersection.Width, intersection.Height), GraphicsUnit.Pixel);
    }
    private Bitmap? PrintBounded(nint window, Rectangle sample, out Rectangle rectangle)
    {
        rectangle = Rectangle.Empty;
        if (_timedOutWindows.TryGetValue(window, out long retryAfter) && Environment.TickCount64 < retryAfter) return null;
        if ((Volatile.Read(ref _pendingPrints) >= 2 && window != GetShellWindow()) || !_activePrints.TryAdd(window, 0)) return null;
        Interlocked.Increment(ref _pendingPrints);
        var capture = Task.Run(() =>
        {
            nint previous = SetThreadDpiAwarenessContext(new nint(-4));
            try { var image = Print(window, sample, out var bounds); return (Image: image, Bounds: bounds); }
            catch { return (Image: (Bitmap?)null, Bounds: Rectangle.Empty); }
            finally { SetThreadDpiAwarenessContext(previous); _activePrints.TryRemove(window, out _); Interlocked.Decrement(ref _pendingPrints); }
        });
        if (!capture.Wait(80))
        {
            _timedOutWindows[window] = Environment.TickCount64 + 2000;
            _ = capture.ContinueWith(completed => completed.Result.Image?.Dispose(), TaskScheduler.Default);
            return null;
        }
        var result = capture.Result; rectangle = result.Bounds; return result.Image;
    }
    private static Bitmap? Print(nint window, Rectangle sample, out Rectangle rectangle)
    {
        rectangle = Rectangle.Empty;
        if (window == 0 || !GetWindowRect(window, out var r)) return null;
        var windowBounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        rectangle = Rectangle.Intersect(windowBounds, sample);
        if (rectangle.Width <= 0 || rectangle.Height <= 0 || (long)windowBounds.Width * windowBounds.Height > 34_000_000) return null;
        // PrintWindow implementations may reset the supplied DC viewport. Print the
        // real window then crop, so the sampled pixels always match screen coordinates.
        using var fullWindow = new Bitmap(windowBounds.Width, windowBounds.Height, PixelFormat.Format32bppRgb);
        bool success;
        using (var graphics = Graphics.FromImage(fullWindow))
        {
            nint dc = graphics.GetHdc();
            try { success = PrintWindow(window, dc, 2); }
            finally { graphics.ReleaseHdc(dc); }
        }
        if (!success) return null;
        var bitmap = fullWindow.Clone(new Rectangle(rectangle.Left - windowBounds.Left, rectangle.Top - windowBounds.Top, rectangle.Width, rectangle.Height), PixelFormat.Format32bppArgb);
        // Cloning from Format32bppRgb explicitly supplies opaque alpha, so there is
        // no full pixel walk on the CPU before uploading this texture to the GPU.
        return bitmap;
    }
    // Called on the same worker after the last in-flight sample.
    public void Dispose() { _disposed = true;foreach(var saved in _savedSamples.Values)saved.Image.Dispose();_savedSamples.Clear(); _desktop?.Dispose(); _desktop = null; _composite?.Dispose(); _composite = null; _pixelBuffer = null; _rawFrame = null; _rawPixels = null; _rawFrameStamps = null; _capture?.Dispose(); _capture = null; }
}

public static class GlassRefraction
{
    // Rounded Lamé distance and gradient match DockSurface's n=3 -> 2 corner profile.
    public static (double X, double Y, double Rim) Offset(double x, double y, double width, double height, double roundness, double amount, double dpi = 1, double distortion = .65)
    {
        double fraction = Math.Clamp(roundness / 100, 0, 1), exponent = 3 - Math.Pow(fraction, 8);
        double r = Math.Min(width, height) * .5 * fraction;
        double px = x - width * .5, py = y - height * .5;
        double qx = Math.Abs(px) - (width * .5 - r), qy = Math.Abs(py) - (height * .5 - r);
        double nx = 0, ny = 0, distance;
        if (qx > 0 && qy > 0)
        {
            double norm = Math.Pow(Math.Pow(qx, exponent) + Math.Pow(qy, exponent), 1 / exponent);
            nx = Math.Pow(qx / norm, exponent - 1); ny = Math.Pow(qy / norm, exponent - 1);
            double length = Math.Sqrt(nx * nx + ny * ny); nx /= length; ny /= length;
            distance = (norm - r) / length;
        }
        else if (qx > qy) { nx = 1; distance = qx - r; }
        else { ny = 1; distance = qy - r; }
        nx *= Math.Sign(px); ny *= Math.Sign(py);
        double band = Math.Min(Math.Min(width, height) * .35, (6 + 18 * distortion) * dpi);
        double t = Math.Clamp(-distance / Math.Max(1, band), 0, 1);
        double rim = t <= 0 || t >= 1 ? 0 : Math.Sin(Math.PI * t);
        double bend = 64 * t * t * t * Math.Pow(1 - t, 3) * (1 - 2 * t);
        double shift = bend * band * .75 * distortion * amount;
        return (nx * shift, ny * shift, rim);
    }
    public static BitmapSource Render(byte[] source, int sourceWidth, int sourceHeight, int pad, int width, int height, double roundness, double amount, double dpi)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || source.Length < sourceWidth * (long)sourceHeight * 4 || width <= 0 || height <= 0) throw new ArgumentException("Invalid glass source size");
        amount = Math.Clamp(amount, 0, 1);
        var output = new byte[width * height * 4];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            var offset = Offset(x + .5, y + .5, width, height, roundness, amount, dpi);
            double sx = Math.Clamp(x + pad + offset.X, 0, sourceWidth - 1.001), sy = Math.Clamp(y + pad + offset.Y, 0, sourceHeight - 1.001);
            int ix = (int)sx, iy = (int)sy, a = (iy * sourceWidth + ix) * 4;
            int b = a + 4, c = a + sourceWidth * 4, d = c + 4;
            double fx = sx - ix, fy = sy - iy;
            int target = (y * width + x) * 4;
            double shade = 1 - offset.Rim * .08 * amount;
            for (int channel = 0; channel < 3; channel++)
                output[target + channel] = (byte)Math.Clamp(((source[a + channel] * (1 - fx) + source[b + channel] * fx) * (1 - fy) + (source[c + channel] * (1 - fx) + source[d + channel] * fx) * fy) * shade, 0, 255);
            output[target + 3] = 255;
        }
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, output, width * 4);
        image.Freeze(); return image;
    }
}

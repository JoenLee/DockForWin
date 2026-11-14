using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace DockForWindows.Services;

// Keep the compositor's latest window surface and read ONLY the small area under
// the dock. Moving a window repositions this cached surface without WM_PRINT.
public sealed class WindowFrameCapture : IDisposable
{
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface CaptureInterop { nint CreateForWindow(nint window, ref Guid iid); nint CreateForMonitor(nint monitor, ref Guid iid); }
    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface SurfaceInterop { nint GetInterface(ref Guid iid); }
    [DllImport("combase.dll")] private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string text, int length, out nint value);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint value);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint name, ref Guid iid, out nint factory);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, int driver, nint software, uint flags, nint levels, uint count, uint sdk, out nint device, out int selected, out nint context);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgi, out nint device);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out NativeMethods.WindowRect rectangle, int size);
    [StructLayout(LayoutKind.Sequential)] private struct TextureDescription { public uint Width, Height, Mips, ArraySize, Format, SampleCount, SampleQuality, Usage, BindFlags, CpuAccess, MiscFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct Box { public uint Left, Top, Front, Right, Bottom, Back; }
    [StructLayout(LayoutKind.Sequential)] private struct Mapped { public nint Data; public uint RowPitch, DepthPitch; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateTexture(nint device, ref TextureDescription description, nint initial, out nint texture);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void CopyRegion(nint context, nint destination, uint subresource, uint x, uint y, uint z, nint source, uint sourceSubresource, ref Box box);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int MapTexture(nint context, nint texture, uint subresource, uint mapType, uint flags, out Mapped mapped);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void UnmapTexture(nint context, nint texture, uint subresource);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ProtectContext(nint context, int enabled);
    private static T Method<T>(nint instance, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * nint.Size));
    private readonly nint _device, _context;private readonly TimeSpan? _minimumInterval;
    private readonly IDirect3DDevice _winrtDevice;
    private readonly CreateTexture _createTexture;
    private readonly CopyRegion _copy;
    private readonly MapTexture _map;
    private readonly UnmapTexture _unmap;
    private readonly Dictionary<nint, CaptureWindow> _windows = new();
    private readonly Dictionary<nint, long> _failed = new();
    private readonly object _deviceGate = new();
    public long Copies { get; private set; }
    public string? LastError { get; private set; }

    public WindowFrameCapture(TimeSpan? minimumInterval=null)
    {
        _minimumInterval=minimumInterval;
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Windows Graphics Capture unavailable");
        Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out _device, out _, out _context));
        // WGC's frame-pool thread also uses this immediate context. A managed lock
        // alone cannot protect it from the compositor's native GPU submissions.
        var multithreadIid = new Guid("9B7E4E00-342C-4106-A19F-4F2704F689F0");
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(_context, in multithreadIid, out nint multithread));
        try { Method<ProtectContext>(multithread, 5)(multithread, 1); }
        finally { Marshal.Release(multithread); }
        nint dxgi = 0, inspectable = 0;
        try
        {
            var iid = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(_device, in iid, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable));
            _winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally { if (inspectable != 0) Marshal.Release(inspectable); if (dxgi != 0) Marshal.Release(dxgi); }
        _createTexture = Method<CreateTexture>(_device, 5); _copy = Method<CopyRegion>(_context, 46);
        _map = Method<MapTexture>(_context, 14); _unmap = Method<UnmapTexture>(_context, 15);
    }
    private static GraphicsCaptureItem CreateItem(nint window)
    {
        const string name = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(name, name.Length, out nint text));
        nint factory = 0, item = 0; object? wrapper = null;
        try
        {
            var iid = typeof(CaptureInterop).GUID;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(text, ref iid, out factory));
            wrapper = Marshal.GetObjectForIUnknown(factory);
            var itemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            item = ((CaptureInterop)wrapper).CreateForWindow(window, ref itemIid);
            return MarshalInspectable<GraphicsCaptureItem>.FromAbi(item);
        }
        finally { if (item != 0) Marshal.Release(item); if (wrapper is not null) Marshal.FinalReleaseComObject(wrapper); if (factory != 0) Marshal.Release(factory); WindowsDeleteString(text); }
    }
    internal readonly record struct FrameStamp(TimeSpan Time, Rectangle Window, Rectangle Region);
    // Identifies the compositor frame Read would copy for this region, without copying it. False when the
    // window has no live capture yet (Read then falls back), so callers must treat it as "changed".
    internal bool TryPeekStamp(nint window, Rectangle sample, out FrameStamp stamp)
    {
        stamp = default;
        long now = Environment.TickCount64;
        if ((_failed.TryGetValue(window, out long retry) && now < retry) || !_windows.TryGetValue(window, out var capture)) return false;
        if (DwmGetWindowAttribute(window, 9, out var r, Marshal.SizeOf<NativeMethods.WindowRect>()) != 0 && !NativeMethods.GetWindowRect(window, out r)) return false;
        lock (capture.Gate)
        {
            var frame = capture.Latest;
            if (frame is null || frame.SystemRelativeTime <= TimeSpan.Zero) return false;
            var windowBounds = new Rectangle(r.Left, r.Top, frame.ContentSize.Width, frame.ContentSize.Height);
            var captured = Rectangle.Intersect(windowBounds, sample);
            if (captured.Width <= 0 || captured.Height <= 0) return false;
            capture.LastUsed = now; // still in use: keep Trim from closing the session while frames are reused
            stamp = new FrameStamp(frame.SystemRelativeTime, windowBounds, captured);
            return true;
        }
    }
    public Bitmap? Read(nint window, Rectangle sample, out Rectangle captured)
        => Read(window, sample, out captured, null, out _, out _);
    internal Bitmap? Read(nint window, Rectangle sample, out Rectangle captured, FrameStamp? previous, out FrameStamp? stamp, out bool unchanged)
    {
        stamp = null; unchanged = false;
        captured = Rectangle.Empty;
        long now = Environment.TickCount64;
        if (_failed.TryGetValue(window, out long retry) && now < retry) return null;
        try
        {
            if (!_windows.TryGetValue(window, out var capture))
            {
                capture = new CaptureWindow(CreateItem(window), _winrtDevice,_minimumInterval); _windows.Add(window, capture);
            }
            capture.LastUsed = now;
            if (DwmGetWindowAttribute(window, 9, out var r, Marshal.SizeOf<NativeMethods.WindowRect>()) != 0 && !NativeMethods.GetWindowRect(window, out r)) return null;
            lock (capture.Gate)
            {
                var frame = capture.Latest;
                if (frame is null) return null;
                var windowBounds = new Rectangle(r.Left, r.Top, frame.ContentSize.Width, frame.ContentSize.Height);
                captured = Rectangle.Intersect(windowBounds, sample);
                if (captured.Width <= 0 || captured.Height <= 0) return null;
                // Reuse only this exact compositor frame and ROI, never a time-based older frame.
                if (frame.SystemRelativeTime > TimeSpan.Zero) stamp = new FrameStamp(frame.SystemRelativeTime, windowBounds, captured);
                if (stamp is not null && stamp == previous) { unchanged = true; return null; }
                var textureIid = new Guid("6F15AAF2-D208-4E89-9AB4-489535D34F9C");
                nint source = frame.Surface.As<SurfaceInterop>().GetInterface(ref textureIid);
                try
                {
                    lock (_deviceGate)
                    {
                        if (capture.Staging == 0 || capture.StagingWidth < captured.Width || capture.StagingHeight < captured.Height)
                        {
                            if (capture.Staging != 0) Marshal.Release(capture.Staging);
                            capture.Staging = 0;
                            var description = new TextureDescription { Width = (uint)captured.Width, Height = (uint)captured.Height, Mips = 1, ArraySize = 1, Format = 87, SampleCount = 1, Usage = 3, CpuAccess = 0x20000 };
                            Marshal.ThrowExceptionForHR(_createTexture(_device, ref description, 0, out capture.Staging));
                            capture.StagingWidth = captured.Width; capture.StagingHeight = captured.Height;
                        }
                        var box = new Box { Left = (uint)(captured.Left - windowBounds.Left), Top = (uint)(captured.Top - windowBounds.Top), Right = (uint)(captured.Right - windowBounds.Left), Bottom = (uint)(captured.Bottom - windowBounds.Top), Back = 1 };
                        _copy(_context, capture.Staging, 0, 0, 0, 0, source, 0, ref box);
                        Marshal.ThrowExceptionForHR(_map(_context, capture.Staging, 0, 1, 0, out var mapped));
                        try
                        {
                            var bitmap = new Bitmap(captured.Width, captured.Height, PixelFormat.Format32bppPArgb);
                            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                            try
                            {
                                var row = new byte[captured.Width * 4];
                                for (int y = 0; y < captured.Height; y++) { Marshal.Copy(mapped.Data + (int)(y * mapped.RowPitch), row, 0, row.Length); Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length); }
                            }
                            finally { bitmap.UnlockBits(data); }
                            Copies++; return bitmap;
                        }
                        finally { _unmap(_context, capture.Staging, 0); }
                    }
                }
                finally { Marshal.Release(source); }
            }
        }
        catch (Exception ex) { LastError = ex.GetType().Name + ": " + ex.Message; _failed[window] = now + 3000; return null; }
    }
    // Copies this window's part of the sample straight into a reusable buffer (rows packed, 4 bytes per pixel,
    // premultiplied BGRA) instead of allocating a Bitmap, and reports whether every copied pixel is opaque so the
    // caller can skip the windows underneath. False without a live compositor frame; unchanged=true when the frame
    // and region equal 'previous' (the buffer is then left as it is).
    internal bool ReadInto(nint window, Rectangle sample, FrameStamp? previous, ref byte[] buffer, out Rectangle captured, out FrameStamp? stamp, out bool unchanged, out bool opaque)
    {
        stamp = null; unchanged = false; opaque = false; captured = Rectangle.Empty;
        long now = Environment.TickCount64;
        if (_failed.TryGetValue(window, out long retry) && now < retry) return false;
        try
        {
            if (!_windows.TryGetValue(window, out var capture))
            {
                capture = new CaptureWindow(CreateItem(window), _winrtDevice, _minimumInterval); _windows.Add(window, capture);
            }
            capture.LastUsed = now;
            if (DwmGetWindowAttribute(window, 9, out var r, Marshal.SizeOf<NativeMethods.WindowRect>()) != 0 && !NativeMethods.GetWindowRect(window, out r)) return false;
            lock (capture.Gate)
            {
                var frame = capture.Latest;
                if (frame is null) return false;
                var windowBounds = new Rectangle(r.Left, r.Top, frame.ContentSize.Width, frame.ContentSize.Height);
                captured = Rectangle.Intersect(windowBounds, sample);
                if (captured.Width <= 0 || captured.Height <= 0) return false;
                if (frame.SystemRelativeTime > TimeSpan.Zero) stamp = new FrameStamp(frame.SystemRelativeTime, windowBounds, captured);
                if (stamp is not null && stamp == previous) { unchanged = true; return true; }
                var textureIid = new Guid("6F15AAF2-D208-4E89-9AB4-489535D34F9C");
                nint source = frame.Surface.As<SurfaceInterop>().GetInterface(ref textureIid);
                try
                {
                    lock (_deviceGate)
                    {
                        if (capture.Staging == 0 || capture.StagingWidth < captured.Width || capture.StagingHeight < captured.Height)
                        {
                            if (capture.Staging != 0) Marshal.Release(capture.Staging);
                            capture.Staging = 0;
                            var description = new TextureDescription { Width = (uint)captured.Width, Height = (uint)captured.Height, Mips = 1, ArraySize = 1, Format = 87, SampleCount = 1, Usage = 3, CpuAccess = 0x20000 };
                            Marshal.ThrowExceptionForHR(_createTexture(_device, ref description, 0, out capture.Staging));
                            capture.StagingWidth = captured.Width; capture.StagingHeight = captured.Height;
                        }
                        var box = new Box { Left = (uint)(captured.Left - windowBounds.Left), Top = (uint)(captured.Top - windowBounds.Top), Right = (uint)(captured.Right - windowBounds.Left), Bottom = (uint)(captured.Bottom - windowBounds.Top), Back = 1 };
                        _copy(_context, capture.Staging, 0, 0, 0, 0, source, 0, ref box);
                        Marshal.ThrowExceptionForHR(_map(_context, capture.Staging, 0, 1, 0, out var mapped));
                        try
                        {
                            int row = captured.Width * 4, length = row * captured.Height;
                            if (buffer.Length < length) buffer = new byte[length];
                            for (int y = 0; y < captured.Height; y++) Marshal.Copy(mapped.Data + (int)(y * mapped.RowPitch), buffer, y * row, row);
                            opaque = PixelRows.Opaque(buffer.AsSpan(0, length));
                            Copies++; return true;
                        }
                        finally { _unmap(_context, capture.Staging, 0); }
                    }
                }
                finally { Marshal.Release(source); }
            }
        }
        catch (Exception ex) { LastError = ex.GetType().Name + ": " + ex.Message; _failed[window] = now + 3000; return false; }
    }
    public void Trim()
    {
        var obsolete = new List<nint>();
        foreach (var pair in _windows) if (!NativeMethods.IsWindow(pair.Key) || Environment.TickCount64 - pair.Value.LastUsed > 1500) obsolete.Add(pair.Key);
        foreach (nint window in obsolete) { _windows[window].Dispose(); _windows.Remove(window); }
    }
    private sealed class CaptureWindow : IDisposable
    {
        public readonly object Gate = new();
        private readonly IDirect3DDevice _device;
        private readonly Direct3D11CaptureFramePool _pool;
        private readonly GraphicsCaptureSession _session;
        private Windows.Graphics.SizeInt32 _size;
        private bool _closed;
        public Direct3D11CaptureFrame? Latest;
        public nint Staging;
        public int StagingWidth, StagingHeight;
        public long LastUsed;
        public CaptureWindow(GraphicsCaptureItem item, IDirect3DDevice device,TimeSpan? minimumInterval)
        {
            _device = device; _size = item.Size;
            _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _size);
            _session = _pool.CreateCaptureSession(item); _session.IsCursorCaptureEnabled = false;
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)) _session.IsBorderRequired = false;
            if(minimumInterval is {} interval&&Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.Graphics.Capture.GraphicsCaptureSession","MinUpdateInterval")){
                try{_session.MinUpdateInterval=interval;}catch(COMException){ /* Keep the platform default when the driver rejects the interval. */ }
            }
            _pool.FrameArrived += Arrived; _session.StartCapture();
        }
        private void Arrived(Direct3D11CaptureFramePool pool, object args)
        {
            try
            {
                var next = pool.TryGetNextFrame(); if (next is null) return;
                bool resized;
                lock (Gate)
                {
                    if (_closed) { next.Dispose(); return; }
                    resized = next.ContentSize.Width != _size.Width || next.ContentSize.Height != _size.Height;
                    Latest?.Dispose(); Latest = null;
                    if (resized) { _size = next.ContentSize; next.Dispose(); }
                    else Latest = next;
                }
                if (resized && _size.Width > 0 && _size.Height > 0) pool.Recreate(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _size);
            }
            catch { /* Closing or resizing a source can invalidate an in-flight frame. */ }
        }
        public void Dispose()
        {
            lock (Gate) { _closed = true; Latest?.Dispose(); Latest = null; if (Staging != 0) { Marshal.Release(Staging); Staging = 0; } }
            _pool.FrameArrived -= Arrived; _session.Dispose(); _pool.Dispose();
        }
    }
    public void Dispose()
    {
        foreach (var window in _windows.Values) window.Dispose(); _windows.Clear();
        _winrtDevice.Dispose(); Marshal.Release(_context); Marshal.Release(_device);
    }
}

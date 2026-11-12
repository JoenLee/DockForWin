using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;

namespace DockForWindows.Services;

public sealed class IconService : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint SHGetFileInfo(string path, uint attributes, out ShellFileInfo info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHGetStockIconInfo(uint identifier, uint flags, ref StockIconInfo info);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint bitmap);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, nint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);
    [StructLayout(LayoutKind.Sequential)] private struct ImageSize { public int Width; public int Height; }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(ImageSize size, uint flags, out nint bitmap);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StockIconInfo
    {
        public uint Size;
        public nint Icon;
        public int SystemIndex;
        public int IconIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Path;
    }
    private readonly BlockingCollection<Action> _jobs = new();
    private readonly ConcurrentDictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Thread _worker;
    private bool _disposed;
    public IconService()
    {
        _worker = new Thread(() => { foreach (var job in _jobs.GetConsumingEnumerable()) job(); }) { IsBackground = true, Name = "Dock shell icons (STA)" };
        _worker.SetApartmentState(ApartmentState.STA);
        _worker.Start();
    }
    public async Task<ImageSource> GetAsync(PinnedItem pin, bool force = false)
    {
        string key = pin.Kind == ItemKind.RecycleBin ? "recycle-bin" : pin.Target + "|" + pin.ExecutablePath + "|" + pin.ManagedShortcut;
        if (force) _cache.TryRemove(key, out _);
        if (_cache.TryGetValue(key, out var cached)) return cached;
        for (int attempt = 0; attempt < 3 && !_disposed; attempt++)
        {
            var icon = await RunShellAsync(() => Load(pin));
            if (icon is not null) { _cache[key] = icon; return icon; }
            if (attempt < 2) await Task.Delay(150 * (attempt + 1));
        }
        return Fallback();
    }
    public Task<T> RunShellAsync<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_disposed) { completion.SetException(new ObjectDisposedException(nameof(IconService))); return completion.Task; }
        try { _jobs.Add(() => { try { completion.SetResult(action()); } catch (Exception ex) { completion.SetException(ex); } }); }
        catch (InvalidOperationException) { completion.TrySetException(new ObjectDisposedException(nameof(IconService))); }
        return completion.Task;
    }
    public static bool IsFallback(ImageSource? image) => image is DrawingImage;
    private static ImageSource? Load(PinnedItem pin)
    {
        if (pin.Kind == ItemKind.RecycleBin) return LoadCandidate(pin);
        var targets = new List<string> { pin.Target, pin.ExecutablePath, pin.ManagedShortcut };
        if (!string.IsNullOrEmpty(pin.AppUserModelId)) targets.Add("::{4234d49b-0245-4df3-b780-3893943456e1}\\" + pin.AppUserModelId);
        if (!string.IsNullOrWhiteSpace(pin.IconLocation)) { int comma = pin.IconLocation.LastIndexOf(','); targets.Add(comma > 0 ? pin.IconLocation[..comma].Trim('"') : pin.IconLocation); }
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string target in targets)
        {
            if (string.IsNullOrWhiteSpace(target) || !visited.Add(target)) continue;
            if (!target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) && !target.StartsWith("::{", StringComparison.Ordinal) && !File.Exists(target) && !Directory.Exists(target)) continue;
            try { var candidate = pin.Copy(); candidate.Target = target; var image = LoadCandidate(candidate); if (image is not null) return image; }
            catch { }
        }
        return null;
    }
    private static ImageSource? LoadCandidate(PinnedItem pin)
    {
        if (pin.Kind != ItemKind.RecycleBin)
        {
            IShellItemImageFactory? factory = null;
            nint bitmap = 0;
            try
            {
                var iid = typeof(IShellItemImageFactory).GUID;
                SHCreateItemFromParsingName(pin.Target, 0, ref iid, out factory);
                if (factory.GetImage(new ImageSize { Width = 128, Height = 128 }, 0x5, out bitmap) == 0 && bitmap != 0)
                {
                    var large = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    large.Freeze(); return large;
                }
            }
            catch (COMException) { }
            finally
            {
                if (bitmap != 0) DeleteObject(bitmap);
                if (factory is not null) Marshal.FinalReleaseComObject(factory);
            }
        }
        nint handle;
        if (pin.Kind == ItemKind.RecycleBin)
        {
            var stock = new StockIconInfo { Size = (uint)Marshal.SizeOf<StockIconInfo>(), Path = "" };
            SHGetStockIconInfo(31, 0x100, ref stock);
            handle = stock.Icon;
        }
        else
        {
            SHGetFileInfo(pin.Target, 0, out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100);
            handle = info.Icon;
        }
        if (handle == 0) return null;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally { DestroyIcon(handle); }
    }
    private static ImageSource Fallback()
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open())
        {
            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(95, 130, 190)), null, new Rect(2, 2, 44, 44), 11, 11);
            drawing.DrawRoundedRectangle(Brushes.White, null, new Rect(13, 13, 22, 22), 4, 4);
        }
        var image = new DrawingImage(group); image.Freeze(); return image;
    }
    public void Dispose() { _disposed = true; _jobs.CompleteAdding(); }
}

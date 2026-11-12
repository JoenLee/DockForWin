using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DockForWindows.Models;

namespace DockForWindows.Services;

public sealed class WindowTracker : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _refresh;
    private readonly DispatcherTimer _debounce;
    private readonly NativeMethods.WinEventProc _callback;
    private readonly List<nint> _hooks = new();
    private bool _busy;
    private bool _disposed;
    private int _queued;
    public event Action<IReadOnlyList<TrackedWindow>>? WindowsChanged;
    public WindowTracker(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _callback = OnNativeEvent;
        _refresh = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, _) => Refresh(), dispatcher);
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Refresh(); };
    }
    public void Start()
    {
        // Out-of-context callbacks run on the owning message loop. Keep delegates alive.
        _hooks.Add(NativeMethods.SetWinEventHook(0x8000, 0x8003, 0, _callback, 0, 0, 2));
        _hooks.Add(NativeMethods.SetWinEventHook(0x0016, 0x0017, 0, _callback, 0, 0, 2));
        _hooks.Add(NativeMethods.SetWinEventHook(0x0003, 0x0003, 0, _callback, 0, 0, 2));
        _refresh.Start();
        Refresh();
    }
    private void OnNativeEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time)
    {
        if (_disposed || window == 0 || objectId != 0 || childId != 0 || Interlocked.Exchange(ref _queued, 1) != 0) return;
        _dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _queued, 0);
            if (!_disposed && !_debounce.IsEnabled) _debounce.Start();
        });
    }
    public async void Refresh()
    {
        if (_busy || _disposed) return;
        _busy = true;
        try
        {
            var windows = await Task.Run(Snapshot);
            if (!_disposed) WindowsChanged?.Invoke(windows);
        }
        finally { _busy = false; }
    }
    public static IReadOnlyList<TrackedWindow> Snapshot()
    {
        var result = new List<TrackedWindow>();
        var paths = new Dictionary<uint, string>();
        NativeMethods.EnumWindows((window, _) =>
        {
            if (!NativeMethods.IsWindowVisible(window) || NativeMethods.GetWindow(window, 4) != 0) return true;
            if (((long)NativeMethods.GetWindowLongPtr(window, -20) & 0x80) != 0) return true;
            if (NativeMethods.DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            int length = NativeMethods.GetWindowTextLength(window);
            if (length == 0) return true;
            NativeMethods.GetWindowThreadProcessId(window, out uint processId);
            if (processId == Environment.ProcessId) return true;
            if (!paths.TryGetValue(processId, out var path)) paths[processId] = path = NativeMethods.ProcessPath(processId);
            if (path.Length == 0) return true;
            // Desktop and shell infrastructure are not application windows.
            var file = System.IO.Path.GetFileName(path);
            if (file.Equals("ShellExperienceHost.exe", StringComparison.OrdinalIgnoreCase) || file.Equals("SearchHost.exe", StringComparison.OrdinalIgnoreCase) || file.Equals("StartMenuExperienceHost.exe", StringComparison.OrdinalIgnoreCase)) return true;
            var title = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(window, title, title.Capacity);
            if (title.Length > 0 && title.ToString() != "Program Manager") result.Add(new(window, processId, title.ToString(), path));
            return true;
        }, 0);
        return result;
    }
    public static bool Activate(TrackedWindow window)
    {
        if (!NativeMethods.IsWindow(window.Handle)) return false;
        if (NativeMethods.IsIconic(window.Handle)) NativeMethods.ShowWindowAsync(window.Handle, 9);
        bool success = NativeMethods.SetForegroundWindow(window.Handle);
        if (!success) NativeMethods.FlashWindow(window.Handle, true);
        return success;
    }
    public static bool Close(TrackedWindow window) => NativeMethods.IsWindow(window.Handle) && NativeMethods.PostMessage(window.Handle, 0x0010, 0, 0);
    public static nint ForegroundWindow => NativeMethods.GetForegroundWindow();
    public void Dispose()
    {
        _disposed = true;
        _refresh.Stop(); _debounce.Stop();
        foreach (var hook in _hooks.Where(h => h != 0)) NativeMethods.UnhookWinEvent(hook);
    }
}

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace DockForWindows.Services;

// A WH_MOUSE_LL hook on its own thread. Windows waits for the hook thread on every mouse event in the
// session (up to LowLevelHooksTimeout, after which it silently removes the hook), so the hook must never
// share the WPF UI thread, which is busy with layout, glass rendering and animation.
internal sealed class LowLevelMouseHook : IDisposable
{
    // Runs on the hook thread. Return true to swallow the event; keep it short and thread-safe.
    public delegate bool Handler(int message, int x, int y);

    private delegate nint HookProc(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public nint Window; public uint Id; public nint W, L; public uint Time; public int X, Y; }
    [DllImport("user32.dll")] private static extern nint SetWindowsHookEx(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, nint w, nint l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    private const int MouseLowLevel = 14;
    private const uint Quit = 0x0012;

    private readonly Handler _handler;
    private readonly HookProc _callback;
    private readonly Thread _thread;
    private nint _hook;
    private uint _threadId;
    private volatile bool _disposed;

    public bool Active => _hook != 0 && !_disposed;

    public LowLevelMouseHook(Handler handler, string name)
    {
        _handler = handler; _callback = Callback;
        // Not disposed here: if the wait times out the hook thread may still signal it later.
        var started = new ManualResetEventSlim();
        _thread = new Thread(() => Run(started)) { IsBackground = true, Name = name };
        _thread.Start();
        started.Wait(TimeSpan.FromSeconds(2));
    }

    private void Run(ManualResetEventSlim started)
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, 0, 0, 0, 0); // create the queue before Dispose can post WM_QUIT
        _hook = SetWindowsHookEx(MouseLowLevel, _callback, 0, 0);
        started.Set();
        if (_hook == 0) return;
        try { while (!_disposed && GetMessage(out _, 0, 0, 0) > 0) { } }
        finally { UnhookWindowsHookEx(_hook); _hook = 0; }
    }

    private nint Callback(int code, nint message, nint data)
    {
        if (code >= 0 && !_disposed)
        {
            var mouse = Marshal.PtrToStructure<MouseData>(data);
            try { if (_handler((int)message, mouse.X, mouse.Y)) return 1; }
            catch { /* an exception must never cross the hook boundary */ }
        }
        return CallNextHookEx(_hook, code, message, data);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_threadId != 0) PostThreadMessage(_threadId, Quit, 0, 0);
        if (Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(1));
    }
}

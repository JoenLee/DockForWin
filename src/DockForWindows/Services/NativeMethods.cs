using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DockForWindows.Services;

internal static class NativeMethods
{
    internal delegate bool EnumWindowsProc(nint window, nint parameter);
    internal delegate void WinEventProc(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(nint window);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out WindowRect rectangle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint window, StringBuilder name, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] internal static extern int GetWindowTextLength(nint window);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(nint window, int command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool FlashWindow(nint window, bool invert);
    [DllImport("user32.dll")] internal static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] internal static extern nint SetWinEventHook(uint minimum, uint maximum, nint module, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(nint hook);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern nint OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool QueryFullProcessImageName(nint process, int flags, StringBuilder path, ref uint size);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct WindowRect { public int Left, Top, Right, Bottom; }

    internal static string ProcessPath(uint processId)
    {
        var process = OpenProcess(0x1000, false, processId);
        if (process == 0) return "";
        try
        {
            var text = new StringBuilder(32768);
            uint size = (uint)text.Capacity;
            return QueryFullProcessImageName(process, 0, text, ref size) ? text.ToString() : "";
        }
        finally { CloseHandle(process); }
    }
}

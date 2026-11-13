using System;
using System.Runtime.InteropServices;

namespace DockForWindows.Services;

// The Core Audio device enumerator is a per-process singleton: every CoCreateInstance returns the same
// COM identity, so .NET hands every caller the same runtime wrapper. Two [ComImport] classes for this
// CLSID made whichever service ran second fail with "Unable to cast object of type 'Enumerator' to type
// 'Enumerator'", and FinalReleaseComObject on one thread could invalidate the wrapper another thread was
// still using. Keep one definition and run its users one at a time; each call is short.
internal static class CoreAudio
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class DeviceEnumerator { }

    private static readonly object Gate = new();

    // The callback receives the enumerator object; cast it to the caller's own IMMDeviceEnumerator view.
    public static T Use<T>(Func<object, T> action)
    {
        lock (Gate)
        {
            object enumerator = new DeviceEnumerator();
            try { return action(enumerator); }
            finally { if (Marshal.IsComObject(enumerator)) Marshal.FinalReleaseComObject(enumerator); }
        }
    }
}

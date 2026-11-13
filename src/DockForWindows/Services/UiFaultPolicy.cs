using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DockForWindows.Services;

// Decides whether the desktop process keeps running after an unhandled UI-thread exception. A one-off
// fault in a secondary surface (control center, preview, menu) is recovered by closing that surface; the
// Dock and any unsaved settings draft stay. Runtime-level failures, or repeated faults within a short
// window (a crash loop), still end the process so the UI never limps along in a broken state.
public sealed class UiFaultPolicy
{
    private readonly Queue<long> _recent = new();
    public int Limit { get; }
    public long WindowMilliseconds { get; }

    public UiFaultPolicy(int limit = 3, long windowMilliseconds = 60_000)
    {
        Limit = Math.Max(1, limit);
        WindowMilliseconds = Math.Max(1, windowMilliseconds);
    }

    public bool ShouldRecover(Exception error, long nowMilliseconds)
    {
        if (IsFatal(error)) return false;
        while (_recent.Count > 0 && nowMilliseconds - _recent.Peek() > WindowMilliseconds) _recent.Dequeue();
        _recent.Enqueue(nowMilliseconds);
        return _recent.Count < Limit;
    }

    public static bool IsFatal(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is OutOfMemoryException or InsufficientExecutionStackException or AccessViolationException
                or SEHException or BadImageFormatException or InvalidProgramException or TypeInitializationException)
                return true;
            if (current is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions) if (IsFatal(inner)) return true;
        }
        return false;
    }
}

using System;

namespace DockForWindows.Views;

public sealed partial class ControlCenterWindow
{
    // After an unhandled UI fault the panel may be mid-transition with controls moved into temporary hosts.
    // Skip the exit animation, release those hosts and close; DockWindow creates a fresh panel next time.
    internal void CloseAfterFault()
    {
        try { _navigationLayer?.Release(); } catch { /* the window is being discarded either way */ }
        try { FinishDismiss(); }
        catch { _closing = true; _allowClose = true; Close(); }
    }
}

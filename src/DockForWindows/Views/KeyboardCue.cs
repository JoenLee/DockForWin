using System.Windows;
using System.Windows.Input;

namespace DockForWindows.Views;

// Focus rings belong to keyboard use (audit S03-12, S07): WPF gives a control keyboard focus on a mouse click too,
// so templates that only test IsKeyboardFocused leave a ring behind every click. The window carries this inherited
// flag — on while the person navigates with the keyboard, off again at the next pointer press — and the templates
// show their rings only when both are true.
public static class KeyboardCue
{
    public static readonly DependencyProperty VisibleProperty = DependencyProperty.RegisterAttached("Visible", typeof(bool), typeof(KeyboardCue),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetVisible(DependencyObject owner) => (bool)owner.GetValue(VisibleProperty);
    public static void SetVisible(DependencyObject owner, bool value) => owner.SetValue(VisibleProperty, value);

    public static void Track(Window window)
    {
        window.PreviewKeyDown += (_, args) =>
        {
            var key = args.Key == Key.System ? args.SystemKey : args.Key;
            if (key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.F6) SetVisible(window, true);
        };
        window.PreviewMouseDown += (_, _) => { if (GetVisible(window)) SetVisible(window, false); };
    }
}

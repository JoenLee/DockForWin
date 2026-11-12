using System.Windows;
using System.Windows.Media.Imaging;

namespace DockForWindows.Views;

internal interface IGlassBackdropHost
{
    BitmapSource? GlassBackdrop {get;}
    Rect GlassBackdropBounds {get;}
}

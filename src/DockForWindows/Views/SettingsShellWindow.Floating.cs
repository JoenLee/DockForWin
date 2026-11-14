using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DockForWindows.Views;

internal partial class SettingsShellWindow
{
    // Outer and inner radii are independent so the window stays close to the Windows corner style.
    private const double ShellRadius=12,SidebarInset=8,SidebarRadius=9;
    private readonly Border _sidebarOutline=new(){CornerRadius=new CornerRadius(SidebarRadius),BorderThickness=new Thickness(0),IsHitTestVisible=false};
    private readonly Border _sidebarShadow=new(){CornerRadius=new CornerRadius(SidebarRadius),IsHitTestVisible=false,
        Effect=new DropShadowEffect{BlurRadius=12,ShadowDepth=4,Direction=270,Opacity=.13}};

    private void UpdateShellOutline(Grid shell)
    {
        if(shell.ActualWidth<=0||shell.ActualHeight<=0)return;
        double radius=WindowState==WindowState.Maximized?0:ShellRadius;
        // A rounded clip, not an opacity mask: a mask on the root re-renders the whole window into a layer on every
        // frame (and on every animation tick, even when nothing visible changed); the clip gives the same edge.
        var outline=new RectangleGeometry(new Rect(shell.RenderSize),radius,radius);
        outline.Freeze();shell.OpacityMask=null;shell.Clip=outline;
    }

    private void AddFloatingSidebarMaterial(Grid sidebar)
    {
        // The shadow reaches under the page; cached in its own layer it is not re-blurred while the page scrolls.
        var shadowLayer=new Grid{IsHitTestVisible=false,CacheMode=new BitmapCache{SnapsToDevicePixels=true}};shadowLayer.Children.Add(_sidebarShadow);
        sidebar.Children.Add(shadowLayer);sidebar.Children.Add(_sidebarOutline);
    }
    private void RefreshFloatingSidebar()
    {
        _sidebarOutline.Background=SystemParameters.HighContrast?SystemColors.WindowBrush:Design.Dark?(Brush)Resources["SettingsSidebar"]:Brushes.White;
        _sidebarShadow.Background=_sidebarOutline.Background;
        Resources["SettingsFloatingSidebarInk"]=Resources["SettingsInk"];
        Resources["SettingsFloatingSidebarSelection"]=Resources["SettingsSelection"];
        _sidebarShadow.Visibility=SystemParameters.HighContrast?Visibility.Collapsed:Visibility.Visible;
    }
}

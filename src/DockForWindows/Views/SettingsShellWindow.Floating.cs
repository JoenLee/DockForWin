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
        var bounds=new Rect(shell.RenderSize);
        var mask=new DrawingBrush(new GeometryDrawing(Brushes.White,null,new RectangleGeometry(bounds,radius,radius))){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=bounds,ViewportUnits=BrushMappingMode.Absolute,Viewport=bounds,Stretch=Stretch.None};
        mask.Freeze();shell.OpacityMask=mask;
    }

    private void AddFloatingSidebarMaterial(Grid sidebar)
    {
        sidebar.Children.Add(_sidebarShadow);sidebar.Children.Add(_sidebarOutline);
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

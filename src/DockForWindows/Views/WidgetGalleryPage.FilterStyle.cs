using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DockForWindows.Views;

internal sealed partial class WidgetGalleryPage
{
    internal static ControlTemplate CategoryTemplate()
    {
        var border=new FrameworkElementFactory(typeof(Border)){Name="Body"};border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        foreach(var (property,name) in new[]{(Border.BackgroundProperty,"Background"),(Border.BorderBrushProperty,"BorderBrush"),(Border.PaddingProperty,"Padding")})border.SetBinding(property,new System.Windows.Data.Binding(name){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);border.AppendChild(content);
        var template=new ControlTemplate(typeof(Button)){VisualTree=border};
        var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("SettingsHover"),"Body"));template.Triggers.Add(hover);
        return template;
    }
}

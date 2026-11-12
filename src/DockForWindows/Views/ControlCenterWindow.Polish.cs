using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace DockForWindows.Views;
public sealed partial class ControlCenterWindow
{
    private void AttachWifiSignal(Button button,System.Func<DockForWindows.Services.WifiNetwork> read)
    {var label=(TextBlock)button.Content;button.Content=null;label.Margin=new Thickness(12,8,32,8);var row=new Grid();row.Children.Add(label);var icon=new ControlCenterIcon("Wifi"){SignalStrength=(int)System.Math.Min(100u,read().Signal),Brush=Foreground,Width=18,Height=18,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};row.Children.Add(icon);button.Content=row;button.Tag=read;AutomationProperties.SetHelpText(button,$"信号强度 {read().Signal}%");}
    private Style? _blueSliderStyle;
    private Style BlueSliderStyle()
    {
        if(_blueSliderStyle is not null)return _blueSliderStyle;
        var template=(ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:DockForWindows.Views;assembly=DockForWindows" TargetType="Slider">
          <Grid Height="36" Background="Transparent"><Track x:Name="PART_Track" Height="30" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}" IsDirectionReversed="{TemplateBinding IsDirectionReversed}">
            <Track.DecreaseRepeatButton><RepeatButton Command="{x:Static Slider.DecreaseLarge}" Focusable="False"><RepeatButton.Template><ControlTemplate><Border Height="5" CornerRadius="2.5" VerticalAlignment="Center" Background="{DynamicResource ControlSliderAccent}"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton><RepeatButton Command="{x:Static Slider.IncreaseLarge}" Focusable="False"><RepeatButton.Template><ControlTemplate><Border Height="5" CornerRadius="2.5" VerticalAlignment="Center" Background="{DynamicResource ControlSliderAccent}" Opacity=".35"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
            <Track.Thumb><Thumb Width="34" Height="22" Cursor="Hand"><Thumb.Template><ControlTemplate><Grid><local:GlassButtonChrome SurfaceBrush="White"/></Grid></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track></Grid><ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value=".55"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
        """);
        _blueSliderStyle=new Style(typeof(Slider),(Style)FindResource(typeof(Slider)));_blueSliderStyle.Setters.Add(new Setter(Control.TemplateProperty,template));return _blueSliderStyle;
    }
    private void InstallEditBadge(Card card)
    {
        if(card.EditBadge is not null)card.Root.Children.Remove(card.EditBadge);
        var mark=new System.Windows.Shapes.Path{Data=Geometry.Parse("M0,0 L10,0"),Stroke=Brushes.White,StrokeThickness=2,Width=10,Height=2,Stretch=Stretch.Fill,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,Palette.Brush("#A0606266"));border.SetValue(Border.BorderBrushProperty,Palette.Brush("#70FFFFFF"));border.SetValue(Border.BorderThicknessProperty,new Thickness(.8));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(14));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);
        var badge=new Button{Content=mark,Template=new ControlTemplate(typeof(Button)){VisualTree=border},Width=27,Height=27,MinWidth=0,MinHeight=0,Padding=new Thickness(),Margin=new Thickness(-7,-7,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Visibility=_editing?Visibility.Visible:Visibility.Collapsed,ToolTip="移除此控件"};
        AutomationProperties.SetAutomationId(badge,"remove-"+card.Kind);AutomationProperties.SetName(badge,"移除"+ModuleName(card.Kind));badge.Click+=(_,_)=>{if(!_editing)return;_options.Modules.Single(m=>m.Module==card.Kind).Visible=false;SyncCards(true);PersistLayout();};Panel.SetZIndex(badge,20);card.EditBadge=badge;card.Root.Children.Add(badge);
    }
}

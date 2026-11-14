using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal static class NativeMenuStyles
{
    private static readonly ResourceDictionary Styles=(ResourceDictionary)XamlReader.Parse("""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style TargetType="ContextMenu">
    <Setter Property="OverridesDefaultStyle" Value="True"/><Setter Property="FocusVisualStyle" Value="{x:Null}"/><Setter Property="FontFamily" Value="Microsoft YaHei UI, Segoe UI"/><Setter Property="FontSize" Value="13"/>
    <Setter Property="Foreground" Value="{DynamicResource MenuInk}"/><Setter Property="MaxWidth" Value="520"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ContextMenu">
      <Border Margin="7" Padding="4" CornerRadius="11" Background="{DynamicResource MenuBackground}" BorderBrush="{DynamicResource MenuLine}" BorderThickness="0.5"><Border.Effect><DropShadowEffect BlurRadius="16" ShadowDepth="3" Opacity="0.16"/></Border.Effect><StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Cycle"/></Border>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="MenuItem">
    <Setter Property="Foreground" Value="{DynamicResource MenuInk}"/><Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="MenuItem">
      <Border x:Name="Choice" CornerRadius="7" Background="Transparent" Padding="6,5,12,5" Margin="0,1">
        <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="20"/><ColumnDefinition/></Grid.ColumnDefinitions>
          <Path x:Name="Check" Data="M1,5 L4.2,8.2 L11,1.5" Stroke="{TemplateBinding Foreground}" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" Width="12" Height="10" Stretch="None" VerticalAlignment="Center" Margin="3,0,0,0" Visibility="Hidden"/>
          <TextBlock Grid.Column="1" Text="{TemplateBinding Header}" Foreground="{TemplateBinding Foreground}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
        </Grid>
      </Border>
      <ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Check" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="Choice" Property="Background" Value="{DynamicResource MenuAccent}"/><Setter Property="Foreground" Value="{DynamicResource MenuOnAccent}"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger></ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="Separator"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Separator"><Border Height="1" Margin="24,4,6,4" Background="{DynamicResource MenuLine}"/></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>
""");
    internal static void Apply(ContextMenu menu,DockSettings settings)
    {
        if(!menu.Resources.MergedDictionaries.Contains(Styles))menu.Resources.MergedDictionaries.Add(Styles);
        var palette=new Palette(settings);var design=DesignSystem.Palette(palette.Dark,settings.InterfaceAccent);
        menu.Resources["MenuBackground"]=palette.WindowBackground;menu.Resources["MenuInk"]=palette.Text;menu.Resources["MenuAccent"]=palette.Accent;
        menu.Resources["MenuOnAccent"]=palette.AccentText;menu.Resources["MenuLine"]=DesignSystem.Brush(design.Separator);
    }
}

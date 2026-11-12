using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace DockForWindows.Views;

internal static class IosStyles
{
    [ThreadStatic] private static ResourceDictionary? _cachedStyles;
    [ThreadStatic] private static InterfaceColors? _cachedColors;
    [ThreadStatic] private static bool _cachedDark;
    [ThreadStatic] private static bool _cachedWithoutGlassShadows;
    public static ResourceDictionary Create(Palette palette,bool withoutGlassShadows=false)
    {
        if(_cachedStyles is not null&&_cachedColors==palette.Interface&&_cachedDark==palette.Dark&&_cachedWithoutGlassShadows==withoutGlassShadows)return _cachedStyles;
        string sliderShadow=withoutGlassShadows?"":"<Grid.Effect><DropShadowEffect BlurRadius=\"7\" ShadowDepth=\"2\" Opacity=\"0.20\"/></Grid.Effect>";
        string toggleShadow=withoutGlassShadows?"":"<Grid.Effect><DropShadowEffect BlurRadius=\"4\" ShadowDepth=\"1\" Opacity=\"0.18\"/></Grid.Effect>";
        string card = palette.Interface.Card;
        string text = palette.Interface.Text;
        string gray = palette.Interface.Track;
        string accent = palette.Interface.Accent;
        string onAccent = palette.Interface.OnAccent;
        string outline = palette.Dark ? "#35FFFFFF" : "#18000000";
        string highlight = palette.Dark ? "#35333B" : "#F2F2F7";
        var styles=(ResourceDictionary)XamlReader.Parse($$$$"""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:DockForWindows.Views;assembly=DockForWindows">
          <Style TargetType="TextBlock"><Setter Property="FontFamily" Value="Microsoft YaHei, Segoe UI"/></Style>
          <Style TargetType="Slider">
            <Setter Property="MinHeight" Value="36"/><Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider">
              <Grid Height="36" Background="Transparent">
                <Track x:Name="PART_Track" Height="30" VerticalAlignment="Center" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}" IsDirectionReversed="{TemplateBinding IsDirectionReversed}">
                  <Track.DecreaseRepeatButton><RepeatButton Command="{x:Static Slider.DecreaseLarge}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><local:CapsuleBorder Height="5" VerticalAlignment="Center" Background="{{{{accent}}}}"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
                  <Track.IncreaseRepeatButton><RepeatButton Command="{x:Static Slider.IncreaseLarge}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><local:CapsuleBorder Height="5" VerticalAlignment="Center" Background="{{{{gray}}}}"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
                  <Track.Thumb><Thumb Width="34" Height="22" Cursor="Hand"><Thumb.Template><ControlTemplate TargetType="Thumb"><Grid>{{{{sliderShadow}}}}<local:GlassButtonChrome SurfaceBrush="White"/></Grid></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                </Track>
              </Grid>
              <ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.55"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="CheckBox">
            <Setter Property="Foreground" Value="{{{{text}}}}"/><Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox">
              <local:SquircleBorder CornerRadius="16" Background="{{{{card}}}}"><Grid MinHeight="52">
                <ContentPresenter Margin="16,10,78,10" VerticalAlignment="Center"><ContentPresenter.ContentTemplate><DataTemplate><TextBlock Text="{Binding}" TextWrapping="Wrap"/></DataTemplate></ContentPresenter.ContentTemplate></ContentPresenter>
                <Grid Width="44" Height="26" HorizontalAlignment="Right" Margin="0,0,14,0">
                  <local:GlassButtonChrome x:Name="SwitchTrack" SurfaceBrush="{{{{gray}}}}"/>
                  <Grid Width="24" Height="22" HorizontalAlignment="Left" VerticalAlignment="Center" Margin="2,0,0,0"><Grid.RenderTransform><TranslateTransform x:Name="SwitchShift"/></Grid.RenderTransform>{{{{toggleShadow}}}}<local:GlassButtonChrome x:Name="SwitchKnob" SurfaceBrush="White"/></Grid>
                </Grid>
              </Grid></local:SquircleBorder>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="SwitchTrack" Property="SurfaceBrush" Value="{{{{accent}}}}"/><Setter TargetName="SwitchKnob" Property="SurfaceBrush" Value="{{{{onAccent}}}}"/>
                  <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="SwitchShift" Storyboard.TargetProperty="X" To="16" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                  <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="SwitchShift" Storyboard.TargetProperty="X" To="0" Duration="0:0:0.15"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
                </Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.55"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="TextBox">
            <Setter Property="Foreground" Value="{{{{text}}}}"/><Setter Property="CaretBrush" Value="{{{{accent}}}}"/><Setter Property="BorderThickness" Value="1"/><Setter Property="BorderBrush" Value="Transparent"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><local:SquircleBorder Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="10"><ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}"/></local:SquircleBorder></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ComboBoxItem">
            <Setter Property="Foreground" Value="{{{{text}}}}"/><Setter Property="Padding" Value="12,9"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><local:SquircleBorder x:Name="ItemSurface" Background="Transparent" CornerRadius="9" Padding="{TemplateBinding Padding}"><ContentPresenter/></local:SquircleBorder><ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="ItemSurface" Property="Background" Value="{{{{highlight}}}}"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter Property="Foreground" Value="{{{{accent}}}}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ComboBox">
            <Setter Property="Foreground" Value="{{{{text}}}}"/><Setter Property="Background" Value="{{{{card}}}}"/><Setter Property="MinHeight" Value="40"/><Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox">
              <Grid><local:SquircleBorder Background="{TemplateBinding Background}" CornerRadius="16"/>
                <ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}" Background="Transparent"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><local:SquircleBorder Background="Transparent"/></ControlTemplate></ToggleButton.Template></ToggleButton>
                <ContentPresenter Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" Margin="16,10,40,10" VerticalAlignment="Center" IsHitTestVisible="False"/>
                <TextBlock Text="⌄" FontSize="22" Foreground="#8E8E93" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,16,4" IsHitTestVisible="False"/>
                <Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade"><local:SquircleBorder Background="{{{{card}}}}" CornerRadius="16" Padding="6" MinWidth="{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}"><local:SquircleBorder.Effect><DropShadowEffect BlurRadius="18" ShadowDepth="4" Opacity="0.15"/></local:SquircleBorder.Effect><ScrollViewer MaxHeight="320"><ItemsPresenter/></ScrollViewer></local:SquircleBorder></Popup>
              </Grid>
              <ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.55"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ScrollBar">
            <Setter Property="Width" Value="7"/><Setter Property="MinWidth" Value="7"/><Setter Property="Background" Value="Transparent"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="Transparent">
              <Track x:Name="PART_Track" Orientation="{TemplateBinding Orientation}" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}" ViewportSize="{TemplateBinding ViewportSize}" IsDirectionReversed="True">
                <Track.DecreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageUpCommand}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><local:SquircleBorder Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
                <Track.IncreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageDownCommand}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><local:SquircleBorder Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
                <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><local:SquircleBorder Background="#8E8E93" Opacity="0.5" CornerRadius="3" Margin="1,0"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
              </Track>
            </Grid></ControlTemplate></Setter.Value></Setter>
          </Style>
        </ResourceDictionary>
        """);
        _cachedColors=palette.Interface;_cachedDark=palette.Dark;_cachedWithoutGlassShadows=withoutGlassShadows;return _cachedStyles=styles;
    }
    public static ImageSource AppIcon()
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open())
        {
            drawing.DrawRoundedRectangle(Palette.Brush("#007AFF"), null, new Rect(0, 0, 64, 64), 16, 16);
            drawing.DrawRoundedRectangle(Palette.Brush("#55FFFFFF"), null, new Rect(7, 35, 50, 17), 7, 7);
            drawing.DrawRoundedRectangle(Brushes.White, null, new Rect(13, 25, 10, 18), 3, 3);
            drawing.DrawRoundedRectangle(Brushes.White, null, new Rect(27, 15, 11, 28), 3, 3);
            drawing.DrawRoundedRectangle(Brushes.White, null, new Rect(42, 25, 10, 18), 3, 3);
        }
        var icon = new DrawingImage(group); icon.Freeze(); return icon;
    }
}



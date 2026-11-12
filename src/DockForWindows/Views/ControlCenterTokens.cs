using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace DockForWindows.Views;

internal static class ControlCenterTokens
{
    public const double Small=4,Gap=8,IconGap=12,Hit=28,ProgressKnob=10;
    public static double Padding(double value)=>Math.Clamp(Math.Round(value/4)*4,12,24);
    public static double InnerRadius(double outer,double inset,Size inner)=>Math.Clamp(outer-inset,0,Math.Min(inner.Width,inner.Height)/2);
    public static Geometry InsetOutline(Size inner,double outerRadius,double inset,double exponent)
    {
        var outerSize=new Size(inner.Width+2*inset,inner.Height+2*inset);
        var outer=DockSurface.CreateGeometry(outerSize,outerRadius/Math.Min(outerSize.Width,outerSize.Height)*200,0,exponent);
        var band=outer.GetWidenedPathGeometry(new Pen(Brushes.Black,2*inset),.03,ToleranceType.Absolute);
        var outline=Geometry.Combine(outer,band,GeometryCombineMode.Exclude,new TranslateTransform(-inset,-inset),.03,ToleranceType.Absolute);
        outline.Freeze();return outline;
    }
    public static readonly Style MediaProgress=(Style)XamlReader.Parse("""
    <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Slider">
      <Setter Property="MinHeight" Value="18"/><Setter Property="Height" Value="18"/><Setter Property="IsMoveToPointEnabled" Value="True"/><Setter Property="FocusVisualStyle" Value="{x:Null}"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider"><Grid Height="18" Background="Transparent"><Track x:Name="PART_Track" Height="12" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}">
        <Track.DecreaseRepeatButton><RepeatButton Command="{x:Static Slider.DecreaseLarge}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Height="3" CornerRadius="1.5" VerticalAlignment="Center" Background="{Binding Foreground,RelativeSource={RelativeSource AncestorType=Slider}}"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
        <Track.IncreaseRepeatButton><RepeatButton Command="{x:Static Slider.IncreaseLarge}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Height="3" CornerRadius="1.5" Opacity="0.28" VerticalAlignment="Center" Background="{Binding Foreground,RelativeSource={RelativeSource AncestorType=Slider}}"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
        <Track.Thumb><Thumb Width="10" Height="10" Cursor="Hand"><Thumb.Template><ControlTemplate TargetType="Thumb"><Border CornerRadius="5" Background="{Binding Foreground,RelativeSource={RelativeSource AncestorType=Slider}}"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
      </Track></Grid><ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    """);
}

public sealed partial class ControlCenterWindow
{
    private void UpdateCardInsets(Card c)
    {
        if(c.Shape!=Models.ControlCardShape.Capsule||c.Holder is null)return;
        double z=_options.Geometry.ComponentScale,h=c.Root.ActualHeight>0?c.Root.ActualHeight/z:Services.ControlCenterLayout.Unit(_options.Geometry)/z;
        double inset=Math.Clamp(ControlCenterTokens.Padding(_options.Geometry.CardPadding)-4,4,Math.Max(4,h/4)),edge=Math.Max(1,h-2*inset);
        c.Holder.Width=c.Holder.Height=edge;c.Body.Margin=new Thickness(inset*z,0,8*z,0);
        c.Holder.CornerRadius=new CornerRadius(edge/2);
        var bounds=new Rect(0,0,edge,edge);if(c.Holder.Clip is not EllipseGeometry cached||cached.Bounds!=bounds){var circle=new EllipseGeometry(bounds);circle.Freeze();c.Holder.Clip=circle;}
    }
}

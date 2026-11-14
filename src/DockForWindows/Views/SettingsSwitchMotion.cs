using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DockForWindows.Views;

public static class SettingsSwitchMotion
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(SettingsSwitchMotion),new PropertyMetadata(false,Changed));
    public static bool GetEnabled(DependencyObject owner)=>(bool)owner.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject owner,bool value)=>owner.SetValue(EnabledProperty,value);
    private static void Changed(DependencyObject owner,DependencyPropertyChangedEventArgs args)
    {
        if(owner is not CheckBox box)return;
        if((bool)args.NewValue){box.IsEnabledChanged+=EnabledChanged;box.Loaded+=Loaded;box.Unloaded+=Unloaded;box.Checked+=Toggled;box.Unchecked+=Toggled;box.PreviewMouseLeftButtonDown+=Pressed;box.PreviewMouseLeftButtonUp+=Released;box.LostMouseCapture+=Released;box.PreviewKeyDown+=KeyDown;box.PreviewKeyUp+=KeyUp;}
        else{box.IsEnabledChanged-=EnabledChanged;box.Loaded-=Loaded;box.Unloaded-=Unloaded;box.Checked-=Toggled;box.Unchecked-=Toggled;box.PreviewMouseLeftButtonDown-=Pressed;box.PreviewMouseLeftButtonUp-=Released;box.LostMouseCapture-=Released;box.PreviewKeyDown-=KeyDown;box.PreviewKeyUp-=KeyUp;Update(box,false,false);}
    }
    private static void Loaded(object sender,RoutedEventArgs args)=>Update((CheckBox)sender,false,false);
    // A switch that cannot be used keeps its position but loses the accent: blue only ever means "on and usable".
    private static void EnabledChanged(object sender,DependencyPropertyChangedEventArgs args)=>Update((CheckBox)sender,false,false);
    private static void Unloaded(object sender,RoutedEventArgs args)=>Update((CheckBox)sender,false,false);
    private static void Toggled(object sender,RoutedEventArgs args)=>Update((CheckBox)sender,true,false);
    private static void Pressed(object sender,MouseButtonEventArgs args)=>Update((CheckBox)sender,true,true);
    private static void Released(object sender,RoutedEventArgs args)=>Update((CheckBox)sender,true,false);
    private static void KeyDown(object sender,KeyEventArgs args){if(args.Key==Key.Space)Update((CheckBox)sender,true,true);}
    private static void KeyUp(object sender,KeyEventArgs args){if(args.Key==Key.Space)Update((CheckBox)sender,true,false);}
    private static void Update(CheckBox box,bool animate,bool pressed)
    {
        if(box.Template?.FindName("SwitchKnob",box) is not Border knob||box.Template.FindName("SwitchTrack",box) is not Border track)return;
        var transforms=(TransformGroup)knob.RenderTransform;
        if(transforms.IsFrozen||transforms.Children[0].IsFrozen||transforms.Children[1].IsFrozen){transforms=transforms.CloneCurrentValue();knob.RenderTransform=transforms;}
        var scale=(ScaleTransform)transforms.Children[0];var shift=(TranslateTransform)transforms.Children[1];
        bool enabled=box.IsChecked==true;knob.RenderTransformOrigin=new Point(enabled?1:0,.5);
        bool motion=animate&&box.IsLoaded&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;
        // Travel = track width minus the knob and its inset on both sides.
        double width=double.IsNaN(track.Width)?track.ActualWidth:track.Width;
        double travel=Math.Max(0,width-knob.Width-knob.Margin.Left*2);
        if(motion){var movement=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromSeconds(.24)};movement.KeyFrames.Add(new SplineDoubleKeyFrame(enabled?travel:0,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.24)),new KeySpline(.25,.9,.3,1)));shift.BeginAnimation(TranslateTransform.XProperty,movement,HandoffBehavior.SnapshotAndReplace);}
        else Move(shift,TranslateTransform.XProperty,enabled?travel:0,0);
        // While pressed the knob stretches toward the side it will travel to, as on iOS.
        Move(scale,ScaleTransform.ScaleXProperty,pressed?1.22:1,motion ? .14 : 0);
        var brush=(SolidColorBrush)track.Background;if(brush.IsFrozen){brush=brush.CloneCurrentValue();track.Background=brush;}
        var off=box.TryFindResource("SettingsSwitchOff") as SolidColorBrush;var on=box.TryFindResource("SettingsAccent") as SolidColorBrush;
        var color=enabled&&box.IsEnabled?on?.Color??Color.FromRgb(0,122,255):off?.Color??Color.FromArgb(51,120,120,128);
        if(motion)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(color,TimeSpan.FromSeconds(.18)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}},HandoffBehavior.SnapshotAndReplace);
        else{brush.BeginAnimation(SolidColorBrush.ColorProperty,null);brush.Color=color;}
    }
    internal static void Move(Animatable owner,DependencyProperty property,double target,double seconds)
    {
        if(seconds==0){owner.BeginAnimation(property,null);owner.SetValue(property,target);return;}
        owner.BeginAnimation(property,new DoubleAnimation(target,TimeSpan.FromSeconds(seconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}},HandoffBehavior.SnapshotAndReplace);
    }
}

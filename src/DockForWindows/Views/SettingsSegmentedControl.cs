using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DockForWindows.Views;

public sealed class SettingsSegmentedControl:ListBox
{
    private Border? _indicator;
    public SettingsSegmentedControl(){SelectionMode=SelectionMode.Single;SizeChanged+=(_,_)=>UpdateIndicator(false);Loaded+=(_,_)=>UpdateIndicator(false);}
    public override void OnApplyTemplate(){base.OnApplyTemplate();_indicator=GetTemplateChild("PART_Indicator") as Border;UpdateIndicator(false);}
    protected override void OnSelectionChanged(SelectionChangedEventArgs args){base.OnSelectionChanged(args);UpdateIndicator(true);}
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs args){base.OnItemsChanged(args);UpdateIndicator(false);}
    private void UpdateIndicator(bool animate)
    {
        if(_indicator is null)return;
        if(Items.Count==0||ActualWidth<=4){_indicator.Visibility=Visibility.Hidden;return;}
        if(_indicator.RenderTransform.IsFrozen)_indicator.RenderTransform=_indicator.RenderTransform.CloneCurrentValue();
        // The trough has a 2 DIP inset on each side (see SettingsShellStyles.xaml).
        double width=(ActualWidth-4)/Items.Count;_indicator.Width=width;_indicator.Visibility=SelectedIndex<0?Visibility.Hidden:Visibility.Visible;
        SettingsSwitchMotion.Move((TranslateTransform)_indicator.RenderTransform,TranslateTransform.XProperty,Math.Max(0,SelectedIndex)*width,animate&&IsLoaded&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast ? .2 : 0);
    }
}

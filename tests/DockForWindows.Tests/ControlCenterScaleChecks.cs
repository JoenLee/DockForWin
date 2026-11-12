using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class ControlCenterScaleChecks
{
    private static void Near(double actual,double expected,string label)
    {if(Math.Abs(actual-expected)>.01)throw new Exception($"{label}: {actual} vs {expected}");}
    internal static int Run(string directory)
    {
        var app=new Application();
        var store=new SettingsService(Path.Combine(directory,"scale-"+Guid.NewGuid().ToString("N")));
        try
        {
            var reference=new ControlCenterOptions();
            foreach(double panel in new[]{.65,1,1.5})foreach(double component in new[]{.75,1,1.5})
            {
                var draft=new DockSettings();draft.ControlCenter.Geometry.PanelScale=panel;draft.ControlCenter.Geometry.ComponentScale=component;
                var saved=new DockSettings();saved.ApplyPreferences(draft);store.Save(saved);var loaded=store.Load();
                var geometry=loaded.ControlCenter.Geometry;Near(geometry.PanelScale,panel,"saved panel");Near(geometry.ComponentScale,component,"saved component");
                var window=new ControlCenterWindow(loaded);var host=(Grid)window.Content;
                Near(((ScaleTransform)host.LayoutTransform).ScaleX,panel,"WPF panel X");Near(((ScaleTransform)host.LayoutTransform).ScaleY,panel,"WPF panel Y");
                var root=(Grid)host.Children[0];var stage=root.Children.OfType<Grid>().Single(g=>Grid.GetRow(g)==1);var canvas=(Canvas)stage.Children[0];
                foreach(var baseline in ControlCenterLayout.Arrange(reference))
                {
                    var tile=canvas.Children.OfType<Grid>().Single(c=>AutomationProperties.GetAutomationId(c)=="control-card-"+baseline.Module);
                    Near(tile.Width,baseline.Bounds.Width*component,"card width");Near(tile.Height,baseline.Bounds.Height*component,"card height");
                    var body=tile.Children.OfType<Grid>().Single();var zoom=(ScaleTransform)body.LayoutTransform;
                    Near(zoom.ScaleX,component,"content X");Near(zoom.ScaleY,component,"content Y");
                }
                Near(window.Width,(stage.Width+128)*panel,"window width");
                Console.WriteLine($"PASS {panel:P0} panel / {component:P0} components: all WPF cards and settings round-trip");
            }
            var bounds=new Size(690,980);
            foreach(var monitor in new[]{new Size(520,1600),new Size(2000,500),new Size(2200,1600)})
            {double fit=ControlCenterLayout.FitPanel(1.5,bounds,monitor);if(fit>1.5||bounds.Width*fit>monitor.Width+.01||bounds.Height*fit>monitor.Height+.01)throw new Exception("monitor clipping");}
            Console.WriteLine("PASS panel fits portrait, landscape and large work areas");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}

using System.Windows;
using System.Windows.Media;
namespace DockForWindows.Views;
internal partial class SettingsShellWindow
{
    protected void ApplySettingsPalette(Palette palette)
    {
        bool dark=palette.Dark;
        void Set(string key,string light,string night)=>Resources[key]=SystemParameters.HighContrast?SystemColors.WindowBrush:Palette.Brush(dark?night:light);
        Set("SettingsInk","#202B2D","#EEF3F3");Set("SettingsMuted","#859193","#A5B1B3");Set("SettingsLine","#E9EEEE","#354345");
        Set("SettingsCard","#FFFFFF","#222C2E");Set("SettingsField","#FFFFFF","#222C2E");Set("SettingsCanvas","#F3F6F5","#1B2325");Set("SettingsSidebar","#DDF0F6F3","#E6202A2C");Set("SettingsHero","#E3EFEB","#243D3B");
        Set("SettingsTrack","#EFF3F3","#354345");Set("SettingsIndicator","#FFFFFF","#4A5A5C");Set("SettingsSidebarInk","#202B2D","#CED9DB");
        Set("SettingsSidebarIcon","#8B9698","#94A5A8");Set("SettingsSidebarSelectionInk","#00575B","#9DD7D9");Set("SettingsSelection","#E5F0F0","#2B4648");Set("SettingsAccent","#00575B","#90CBCD");Set("SettingsSwitchOff","#E9E9EB","#5B6264");
        if(SystemParameters.HighContrast){Resources["SettingsInk"]=SystemColors.WindowTextBrush;Resources["SettingsMuted"]=SystemColors.WindowTextBrush;Resources["SettingsSidebarInk"]=SystemColors.WindowTextBrush;Resources["SettingsSidebarSelectionInk"]=SystemColors.HighlightTextBrush;Resources["SettingsSelection"]=SystemColors.HighlightBrush;}
        _sidebar.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty,"SettingsSidebar");_right.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty,"SettingsCanvas");
        ((System.Windows.Controls.Grid)_right.Children[0]).SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty,"SettingsCanvas");
        _selection.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty,"SettingsSelection");
        ((System.Windows.Controls.Border)_shellContent).SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty,"SettingsLine");
    }
}

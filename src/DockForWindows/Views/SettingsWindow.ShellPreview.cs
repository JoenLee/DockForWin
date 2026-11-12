namespace DockForWindows.Views;

internal sealed partial class SettingsWindow
{
    private void OpenSettingsPreview()=>NavigateSettingsPage("settings-preview-"+(PageHost.CurrentSection<8?PageHost.CurrentSection:0));
}

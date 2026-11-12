namespace DockForWindows.Models;

public static class AppearancePolicy
{
    public static bool HasPlate(DockSettings settings) => settings.Appearance.Opacity > .00001
        && (settings.Theme != DockTheme.Glass || settings.GlassOpacity > .00001);
    public static bool ReflectionsAllowed(DockSettings settings) => !HasPlate(settings);
    public static bool ShowReflections(DockSettings settings) => settings.Reflections && settings.ReflectionOpacity > 0 && ReflectionsAllowed(settings);
}

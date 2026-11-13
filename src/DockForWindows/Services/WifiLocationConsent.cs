using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace DockForWindows.Services;

// Windows 11 24H2 (build 26100) made nearby Wi-Fi lists depend on location consent. Calling the WLAN list
// APIs without it fails with ERROR_ACCESS_DENIED and makes Windows ask the user to turn location on, in a
// prompt attributed to ".NET Host". Read the same consent switches first so a background status poll never
// raises that prompt; the user can still enable location from the Wi-Fi page.
public static class WifiLocationConsent
{
    private const string Store = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location";
    public const string SettingsUri = "ms-settings:privacy-location";

    public static bool AllowsNetworkList()
    {
        // Earlier builds return the list regardless of location settings.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100)) return true;
        return Evaluate(Read(Registry.LocalMachine, Store), Read(Registry.CurrentUser, Store),
            Read(Registry.CurrentUser, Store + @"\NonPackaged"), PolicyDisablesLocation());
    }

    // device = Location services; user = let apps access location; desktopApps = let desktop apps access
    // location. A missing value is not a refusal: Windows then decides, and the existing error path reports it.
    public static bool Evaluate(string? device, string? user, string? desktopApps, bool policyDisabled)
        => !policyDisabled && !Denied(device) && !Denied(user) && !Denied(desktopApps);

    private static bool Denied(string? value) => string.Equals(value?.Trim(), "Deny", StringComparison.OrdinalIgnoreCase);

    private static string? Read(RegistryKey hive, string path)
    {
        try { using var key = hive.OpenSubKey(path); return key?.GetValue("Value") as string; }
        catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException) { return null; }
    }

    private static bool PolicyDisablesLocation()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors");
            return key?.GetValue("DisableLocation") is int value && value != 0;
        }
        catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DockForWindows.Models;

namespace DockForWindows.Services;

public sealed class LaunchService
{
    public static string Identity(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return path.ToUpperInvariant();
        try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant(); }
        catch { return path.Trim().ToUpperInvariant(); }
    }
    public static PinnedItem CreatePin(string path, string? shortcutDirectory = null)
    {
        path = Path.GetFullPath(path);
        if (Directory.Exists(path)) return new() { Name = Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } name ? name : path, Target = path, Kind = ItemKind.Folder };
        if (!File.Exists(path)) throw new FileNotFoundException("找不到这个文件。", path);
        string extension = Path.GetExtension(path);
        bool executable = extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
        var pin = new PinnedItem { Name = executable ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path), Target = path, ExecutablePath = executable ? path : "", Kind = executable ? ItemKind.Application : ItemKind.Document, WorkingDirectory = Path.GetDirectoryName(path) ?? "" };
        if (!extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return pin;
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcut = ((dynamic)shell!).CreateShortcut(path);
            pin.Name = Path.GetFileNameWithoutExtension(path); pin.SourceShortcut = path;
            pin.ExecutablePath = Environment.ExpandEnvironmentVariables((string)((dynamic)shortcut).TargetPath);
            pin.Arguments = (string)((dynamic)shortcut).Arguments;
            pin.WorkingDirectory = Environment.ExpandEnvironmentVariables((string)((dynamic)shortcut).WorkingDirectory);
            pin.IconLocation = Environment.ExpandEnvironmentVariables((string)((dynamic)shortcut).IconLocation);
            pin.Kind = string.IsNullOrEmpty(pin.ExecutablePath) || Path.GetExtension(pin.ExecutablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase) ? ItemKind.Application : ItemKind.Document;
            if (Directory.Exists(pin.ExecutablePath)) pin.Kind = ItemKind.Folder;
            if (!string.IsNullOrEmpty(pin.ExecutablePath)) pin.Target = pin.ExecutablePath;
            if (!string.IsNullOrEmpty(shortcutDirectory))
            {
                Directory.CreateDirectory(shortcutDirectory);
                pin.ManagedShortcut = Path.Combine(shortcutDirectory, pin.Id + ".lnk");
                File.Copy(path, pin.ManagedShortcut, true);
                if (string.IsNullOrEmpty(pin.ExecutablePath)) pin.Target = pin.ManagedShortcut;
            }
            if (string.IsNullOrEmpty(pin.ExecutablePath) && pin.Kind == ItemKind.Application)
            {
                pin.AppUserModelId = FindApplicationId(pin.Name);
                if (!string.IsNullOrEmpty(pin.AppUserModelId)) { pin.Target = "shell:AppsFolder\\" + pin.AppUserModelId; pin.ExecutablePath = FindPackagedExecutable(pin.AppUserModelId); }
            }
        }
        finally { Release(shortcut); Release(shell); }
        return pin;
    }
    public static PinnedItem MakeIndependent(PinnedItem original, string shortcutDirectory)
    {
        var pin = original.Copy();
        if (Path.GetExtension(pin.Target).Equals(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(pin.Target))
        {
            var resolved = CreatePin(pin.Target, shortcutDirectory);
            resolved.Id = pin.Id; resolved.Name = pin.Name;
            return resolved;
        }
        if (pin.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || File.Exists(pin.Target) || Directory.Exists(pin.Target)) return pin;
        if (File.Exists(pin.ExecutablePath))
        {
            if (pin.Target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) pin.SourceShortcut = pin.Target;
            pin.Target = pin.ExecutablePath;
            if (string.IsNullOrEmpty(pin.WorkingDirectory)) pin.WorkingDirectory = Path.GetDirectoryName(pin.ExecutablePath) ?? "";
        }
        else if (pin.Kind == ItemKind.Application)
        {
            pin.AppUserModelId = string.IsNullOrEmpty(pin.AppUserModelId) ? FindApplicationId(pin.Name) : pin.AppUserModelId;
            if (!string.IsNullOrEmpty(pin.AppUserModelId)) { pin.SourceShortcut = pin.Target; pin.Target = "shell:AppsFolder\\" + pin.AppUserModelId; pin.ExecutablePath = FindPackagedExecutable(pin.AppUserModelId); }
        }
        return pin;
    }
    public static string FindApplicationId(string name)
    {
        object? shell = null, folder = null, items = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
            items = ((dynamic)folder!).Items();
            for (int i = 0; i < (int)((dynamic)items).Count; i++)
            {
                object? item = null;
                try
                {
                    item = ((dynamic)items).Item(i);
                    if (string.Equals((string)((dynamic)item).Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        string id = (string)((dynamic)item).Path;
                        if (id.Contains('!')) return id;
                    }
                }
                finally { Release(item); }
            }
        }
        catch (COMException) { }
        finally { Release(items); Release(folder); Release(shell); }
        return "";
    }
    private static string FindPackagedExecutable(string applicationId)
    {
        string product = applicationId.Split('!')[0].Split('_')[0];
        string prefix = "\\WindowsApps\\" + product + "_";
        return WindowTracker.Snapshot().FirstOrDefault(w => w.ExecutablePath.Contains(prefix, StringComparison.OrdinalIgnoreCase))?.ExecutablePath ?? "";
    }
    internal static string? NewWindowArguments(PinnedItem pin)=>Path.GetFileName(pin.ExecutablePath.Length>0?pin.ExecutablePath:pin.Target).ToLowerInvariant() switch{
        "chrome.exe" or "msedge.exe" or "brave.exe"=>"--new-window",
        "firefox.exe"=>"-new-window about:blank",
        "code.exe" or "code - insiders.exe"=>"--new-window",
        "wt.exe" or "windowsterminal.exe"=>"-w new",_=>null};
    public static bool CanCreateNewWindow(PinnedItem pin)=>pin.Kind==ItemKind.Application&&NewWindowArguments(pin) is not null;
    public void LaunchNewWindow(PinnedItem pin)
    {
        if(!CanCreateNewWindow(pin))throw new InvalidOperationException("此应用未提供已确认的新建窗口命令。");
        var copy=pin.Copy();copy.ManagedShortcut="";copy.Target=copy.ExecutablePath.Length>0?copy.ExecutablePath:copy.Target;
        copy.Arguments=(copy.Arguments+" "+NewWindowArguments(copy)).Trim();Launch(copy);
    }
    public void Launch(PinnedItem pin)
    {
        if (pin.Kind == ItemKind.RecycleBin) { Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder") { UseShellExecute = true }); return; }
        if (!string.IsNullOrEmpty(pin.ManagedShortcut) && File.Exists(pin.ManagedShortcut))
        {
            Process.Start(new ProcessStartInfo(pin.ManagedShortcut) { UseShellExecute = true }); return;
        }
        if (pin.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", pin.Target) { UseShellExecute = true }); return;
        }
        string target = File.Exists(pin.Target) || Directory.Exists(pin.Target) ? pin.Target : pin.ExecutablePath;
        if (!File.Exists(target) && !Directory.Exists(target)) throw new FileNotFoundException("应用已移动或卸载，请从安装目录或开始菜单重新添加。", target);
        var info = new ProcessStartInfo(target) { UseShellExecute = true, Arguments = pin.Arguments };
        if (!string.IsNullOrEmpty(pin.WorkingDirectory) && Directory.Exists(pin.WorkingDirectory)) info.WorkingDirectory = pin.WorkingDirectory;
        Process.Start(info);
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}

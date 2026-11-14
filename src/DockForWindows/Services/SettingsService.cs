using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DockForWindows.Models;

namespace DockForWindows.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public string DataDirectory { get; }
    public string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public string? RecoveryNotice { get; private set; }

    public SettingsService(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? ResolveDataDirectory();
        Directory.CreateDirectory(DataDirectory);
    }

    private static string ResolveDataDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("DOCKFORWINDOWS_DATA");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DockForWindows")
            : Path.GetFullPath(configured);
    }

    public DockSettings Load()
    {
        if (!File.Exists(SettingsPath)) return CreateDefaults();
        try
        {
            var settings = JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(SettingsPath), Options)
                ?? throw new JsonException("配置为空");
            if (settings.SchemaVersion != 1) throw new JsonException("配置版本暂不支持");
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log("读取配置失败", ex);
            var backup = SettingsPath + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            try { File.Copy(SettingsPath, backup); }
            catch (Exception copyError) { Log("保留损坏配置失败", copyError); }
            RecoveryNotice = "配置文件无法读取，已尝试保留原文件副本，本次使用默认设置。";
            return CreateDefaults();
        }
    }

    public bool TryImportFrom(string sourceDirectory)
    {
        if (File.Exists(SettingsPath)) return false;
        string sourceRoot = Path.GetFullPath(sourceDirectory);
        if (string.Equals(sourceRoot.TrimEnd('\\'), Path.GetFullPath(DataDirectory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;
        string sourceSettings = Path.Combine(sourceRoot, "settings.json");
        if (!File.Exists(sourceSettings)) return false;
        try
        {
            using var input = new FileStream(sourceSettings, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var imported = JsonSerializer.Deserialize<DockSettings>(input, Options) ?? throw new JsonException("原配置为空");
            if (imported.SchemaVersion != 1) throw new JsonException("原配置版本暂不支持");
            imported.Normalize();
            string sourceShortcuts = Path.Combine(sourceRoot, "shortcuts") + Path.DirectorySeparatorChar;
            foreach (var pin in System.Linq.Enumerable.Concat(imported.Pins, imported.RecentApps))
            {
                if (string.IsNullOrEmpty(pin.ManagedShortcut) || !File.Exists(pin.ManagedShortcut)) continue;
                string oldPath = Path.GetFullPath(pin.ManagedShortcut);
                if (!oldPath.StartsWith(sourceShortcuts, StringComparison.OrdinalIgnoreCase)) continue;
                string targetDirectory = Path.Combine(DataDirectory, "shortcuts"); Directory.CreateDirectory(targetDirectory);
                string target = Path.Combine(targetDirectory, Path.GetFileName(oldPath)); File.Copy(oldPath, target, true);
                pin.ManagedShortcut = target;
                if (string.Equals(pin.Target, oldPath, StringComparison.OrdinalIgnoreCase)) pin.Target = target;
            }
            Save(imported); Log("已迁移原程序坞配置：" + sourceRoot); return true;
        }
        catch (Exception ex) { Log("导入原配置失败，原文件保持不变", ex); return false; }
    }

    public void Save(DockSettings settings)
    {
        settings.Normalize();
        var temporary = SettingsPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options), new UTF8Encoding(false));
        if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, SettingsPath + ".bak");
        else File.Move(temporary, SettingsPath);
    }

    // Background workers (capture, WLAN, audio) log too; concurrent appends used to lose lines to sharing violations.
    private static readonly object LogGate = new();
    public void Log(string message, Exception? error = null)
    {
        try
        {
            lock (LogGate)
            {
                var log = Path.Combine(DataDirectory, "dock.log");
                if (File.Exists(log) && new FileInfo(log).Length > 2_000_000)
                    File.Move(log, log + ".previous", true);
                File.AppendAllText(log, $"{DateTime.Now:O} {message} {error}\n");
            }
        }
        catch { /* A logging failure must not terminate the desktop process. */ }
    }

    public static DockSettings CreateDefaults()
    {
        var settings = new DockSettings();
        // A fresh profile starts in the liquid glass the settings window offers first, not the old flat theme.
        Views.GlassMaterial.Apply(settings);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        settings.Pins.Add(new PinnedItem { Name = "文件资源管理器", Target = Path.Combine(windows, "explorer.exe"), ExecutablePath = Path.Combine(windows, "explorer.exe") });
        var notepad = Path.Combine(windows, "System32", "notepad.exe");
        if (File.Exists(notepad)) settings.Pins.Add(new PinnedItem { Name = "记事本", Target = notepad, ExecutablePath = notepad });
        settings.Pins.Add(new PinnedItem { Name = "下载", Kind = ItemKind.Folder, Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") });
        settings.Pins.Add(new PinnedItem { Name = "回收站", Kind = ItemKind.RecycleBin, Target = "shell:RecycleBinFolder" });
        return settings;
    }
}

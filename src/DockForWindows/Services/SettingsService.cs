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
                ?? throw new JsonException(LocalizedText.Get("配置为空"));
            if (settings.SchemaVersion != 1) throw new JsonException(LocalizedText.Get("配置版本暂不支持"));
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log(LocalizedText.Get("读取配置失败"), ex);
            var backup = SettingsPath + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            try { File.Copy(SettingsPath, backup); }
            catch (Exception copyError) { Log(LocalizedText.Get("保留损坏配置失败"), copyError); }
            RecoveryNotice = LocalizedText.Get("配置文件无法读取，已尝试保留原文件副本，本次使用默认设置。");
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
            var imported = JsonSerializer.Deserialize<DockSettings>(input, Options) ?? throw new JsonException(LocalizedText.Get("原配置为空"));
            if (imported.SchemaVersion != 1) throw new JsonException(LocalizedText.Get("原配置版本暂不支持"));
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
            Save(imported); Log(LocalizedText.Get("已迁移原程序坞配置：") + sourceRoot); return true;
        }
        catch (Exception ex) { Log(LocalizedText.Get("导入原配置失败，原文件保持不变"), ex); return false; }
    }

    public void Save(DockSettings settings)
    {
        settings.Normalize();
        var temporary = SettingsPath + ".tmp";
        try
        {
            // Flushed to disk before the swap: after a power loss the file is either the old or the new settings,
            // never an empty one that Load would treat as corrupt and replace with defaults.
            var bytes = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(settings, Options));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, SettingsPath + ".bak");
            else File.Move(temporary, SettingsPath);
        }
        catch { try { File.Delete(temporary); } catch { } throw; }
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
        settings.Pins.Add(new PinnedItem { Name = LocalizedText.Get("文件资源管理器"), Target = Path.Combine(windows, "explorer.exe"), ExecutablePath = Path.Combine(windows, "explorer.exe") });
        var notepad = Path.Combine(windows, "System32", "notepad.exe");
        if (File.Exists(notepad)) settings.Pins.Add(new PinnedItem { Name = LocalizedText.Get("记事本"), Target = notepad, ExecutablePath = notepad });
        settings.Pins.Add(new PinnedItem { Name = LocalizedText.Get("下载"), Kind = ItemKind.Folder, Target = "shell:Downloads" });
        settings.Pins.Add(new PinnedItem { Name = LocalizedText.Get("回收站"), Kind = ItemKind.RecycleBin, Target = "shell:RecycleBinFolder" });
        return settings;
    }
}

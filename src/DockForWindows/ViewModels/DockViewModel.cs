using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.ViewModels;

public sealed class DockViewModel
{
    private readonly SettingsService _store;
    private readonly IconService _icons;
    public DockSettings Settings { get; }
    public string DataDirectory => _store.DataDirectory;
    public ObservableCollection<DockItem> Items { get; } = new();
    public IEnumerable<DockItem> DisplayItems => Items.Where(i => i.IsPinned && i.Pin.Kind != ItemKind.RecycleBin)
        .Concat(Items.Where(i => !i.IsPinned)).Concat(Items.Where(i => i.Pin.Kind == ItemKind.RecycleBin));
    public event Action<string>? Error;
    public event Action? ItemsRebuilt;
    public void LogDiagnostic(string message, Exception? error = null) => _store.Log(message, error);
    // Recent-app bookkeeping changes with every foreground switch; it is written a few seconds later in one save
    // (and on exit, Program saves everything) instead of rewriting settings.json on the UI thread each time.
    private readonly System.Windows.Threading.DispatcherTimer _recentSave = new() { Interval = TimeSpan.FromSeconds(3) };
    // File.Exists for recent apps runs on the UI thread on every window refresh; a slow or offline path is asked at most every 30 s.
    private readonly Dictionary<string, (bool Exists, long Checked)> _targetExists = new(StringComparer.OrdinalIgnoreCase);
    public DockViewModel(DockSettings settings, SettingsService store, IconService icons)
    {
        Settings = settings; _store = store; _icons = icons;
        _recentSave.Tick += (_, _) => { _recentSave.Stop(); Save(); };
        foreach (var pin in settings.Pins) Items.Add(new DockItem(pin));
        if (!Items.Any(i => i.Pin.Kind == ItemKind.RecycleBin)) Items.Add(new DockItem(new PinnedItem { Name = LocalizedText.Get("回收站"), Kind = ItemKind.RecycleBin, Target = "shell:RecycleBinFolder" }));
    }
    public async Task LoadIconsAsync(bool force = false)
    {
        bool repaired = false;
        foreach (var item in Items.ToArray())
        {
            try
            {
                var previous = item.Pin;
                var prepared = await _icons.RunShellAsync(() => LaunchService.MakeIndependent(previous, Path.Combine(_store.DataDirectory, "shortcuts")));
                if (previous.Target != prepared.Target || previous.ManagedShortcut != prepared.ManagedShortcut)
                {
                    previous.Target = prepared.Target; previous.ExecutablePath = prepared.ExecutablePath; previous.Kind = prepared.Kind;
                    previous.Arguments = prepared.Arguments; previous.WorkingDirectory = prepared.WorkingDirectory;
                    previous.SourceShortcut = prepared.SourceShortcut; previous.ManagedShortcut = prepared.ManagedShortcut;
                    previous.IconLocation = prepared.IconLocation; previous.AppUserModelId = prepared.AppUserModelId; repaired = true;
                }
                item.Icon = await _icons.GetAsync(item.Pin, force);
            }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex) { _store.Log(LocalizedText.Get("图标重载失败：") + item.Name, ex); }
        }
        if (repaired) Save();
        ItemsRebuilt?.Invoke();
    }
    public async Task AddAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                var pin = await _icons.RunShellAsync(() => LaunchService.CreatePin(path, Path.Combine(_store.DataDirectory, "shortcuts")));
                bool Pinned() => Items.Any(i => i.IsPinned && LaunchService.Identity(i.Pin.Target) == LaunchService.Identity(pin.Target));
                if (Pinned()) continue;
                var icon = await _icons.GetAsync(pin);
                // A drop and the file picker can add concurrently: check again after the await, then insert without awaiting.
                if (Pinned()) continue;
                var existing = Items.FirstOrDefault(i => !i.IsPinned && SameApplication(i.Pin, pin));
                if (existing is not null) Items.Remove(existing);
                var item = new DockItem(pin) { Icon = icon };
                if (existing is not null) item.Windows = existing.Windows;
                Items.Insert(Items.Count(i => i.IsPinned), item);
            }
            catch (Exception ex) { Error?.Invoke(ex.Message); _store.Log(LocalizedText.Get("添加项目失败"), ex); }
        }
        Save(); ItemsRebuilt?.Invoke();
    }
    private static bool SameApplication(PinnedItem left, PinnedItem right) => left.Kind == ItemKind.Application && right.Kind == ItemKind.Application
        && !string.IsNullOrWhiteSpace(left.ExecutablePath)
        && LaunchService.Identity(left.ExecutablePath) == LaunchService.Identity(right.ExecutablePath);
    public void Remove(DockItem item)
    {
        if (item.Pin.Kind == ItemKind.RecycleBin) return;
        if (item.IsRunning && Settings.ShowRunningApps) item.IsPinned = false;
        else Items.Remove(item);
        // Keep pinned items as a contiguous prefix.
        var ordered = Items.OrderByDescending(i => i.IsPinned).ToArray();
        for (int i = 0; i < ordered.Length; i++) { int current = Items.IndexOf(ordered[i]); if (current != i) Items.Move(current, i); }
        Save(); ItemsRebuilt?.Invoke();
    }
    public void Pin(DockItem item)
    {
        if (item.IsPinned) return;
        item.IsPinned = true;
        Items.Move(Items.IndexOf(item), Items.Count(i => i.IsPinned) - 1);
        Save(); ItemsRebuilt?.Invoke();
    }
    public void Reorder(DockItem source, DockItem target)
    {
        if (!source.IsPinned || !target.IsPinned || source == target || source.Pin.Kind == ItemKind.RecycleBin || target.Pin.Kind == ItemKind.RecycleBin) return;
        Items.Move(Items.IndexOf(source), Items.IndexOf(target));
        Save(); ItemsRebuilt?.Invoke();
    }
    public bool ApplyPinnedOrder(IReadOnlyList<string> identifiers)
    {
        var fixedItems = Items.Where(i => i.IsPinned && i.Pin.Kind != ItemKind.RecycleBin).ToArray();
        if (identifiers.Count != fixedItems.Length || identifiers.Distinct().Count() != identifiers.Count || identifiers.Any(id => !fixedItems.Any(i => i.Pin.Id == id))) return false;
        if (fixedItems.Select(i => i.Pin.Id).SequenceEqual(identifiers)) return true;
        var original = Items.ToArray();
        var ordered = identifiers.Select(id => fixedItems.First(i => i.Pin.Id == id)).ToArray();
        int next = 0;
        for (int i = 0; i < Items.Count; i++) if (Items[i].IsPinned && Items[i].Pin.Kind != ItemKind.RecycleBin) Items[i] = ordered[next++];
        bool saved = Save();
        if (!saved)
        {
            for (int i = 0; i < original.Length; i++) Items[i] = original[i];
            Settings.Pins = Items.Where(i => i.IsPinned).Select(i => i.Pin).ToList();
        }
        ItemsRebuilt?.Invoke(); return saved;
    }
    public void UpdateWindows(IReadOnlyList<TrackedWindow> windows)
    {
        bool changed = false;
        foreach (var item in Items.ToArray())
        {
            item.Windows = windows.Where(w => item.Pin.Kind == ItemKind.Application && !string.IsNullOrEmpty(item.Pin.ExecutablePath)
                && LaunchService.Identity(w.ExecutablePath) == LaunchService.Identity(item.Pin.ExecutablePath)).ToArray();
            bool recent = Settings.ShowRecentApps && Settings.RecentApps.Take(Settings.RecentLimit).Any(p => SameApplication(p, item.Pin));
            if (!item.IsPinned && ((!item.IsRunning && !recent) || (!Settings.ShowRunningApps && !recent))) { Items.Remove(item); changed = true; }
        }
        if (Settings.ShowRunningApps)
        {
            foreach (var group in windows.GroupBy(w => LaunchService.Identity(w.ExecutablePath)))
            {
                if (Items.Any(i => i.Pin.Kind == ItemKind.Application && LaunchService.Identity(i.Pin.ExecutablePath) == group.Key)) continue;
                // ApplicationFrameHost aggregates packaged apps; do not pretend it identifies them.
                if (Path.GetFileName(group.First().ExecutablePath).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)) continue;
                var path = group.First().ExecutablePath;
                var pin = new PinnedItem { Target = path, ExecutablePath = path, Name = Path.GetFileNameWithoutExtension(path) };
                var item = new DockItem(pin, false) { Windows = group.ToArray() };
                Items.Add(item); changed = true;
                Remember(pin);
                _ = LoadRunningIconAsync(item);
            }
        }
        var active = windows.FirstOrDefault(w => w.Handle == WindowTracker.ForegroundWindow);
        if (active is not null)
        {
            var activeItem = Items.FirstOrDefault(i => !i.IsPinned && LaunchService.Identity(i.Pin.ExecutablePath) == LaunchService.Identity(active.ExecutablePath));
            if (activeItem is not null) Remember(activeItem.Pin);
        }
        if (Settings.ShowRecentApps)
        {
            foreach (var pin in Settings.RecentApps.Take(Settings.RecentLimit).ToArray())
            {
                if (Items.Any(i => SameApplication(i.Pin, pin)) || !TargetExists(pin.Target)) continue;
                var recentItem = new DockItem(pin, false); Items.Add(recentItem); changed = true; _ = LoadRunningIconAsync(recentItem);
            }
        }
        if (changed) ItemsRebuilt?.Invoke();
    }
    private void Remember(PinnedItem pin)
    {
        if (pin.Kind != ItemKind.Application || Settings.RecentApps.FirstOrDefault() is { } first && SameApplication(first, pin)) return;
        Settings.RecentApps.RemoveAll(p => SameApplication(p, pin)); Settings.RecentApps.Insert(0, pin);
        if (Settings.RecentApps.Count > 8) Settings.RecentApps.RemoveRange(8, Settings.RecentApps.Count - 8);
        _recentSave.Stop(); _recentSave.Start();
    }
    private bool TargetExists(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        long now = Environment.TickCount64;
        if (_targetExists.TryGetValue(path, out var known) && now - known.Checked < 30_000) return known.Exists;
        bool exists = File.Exists(path); _targetExists[path] = (exists, now); return exists;
    }
    private async Task LoadRunningIconAsync(DockItem item)
    {
        item.Icon = await _icons.GetAsync(item.Pin);
        if (Items.Contains(item)) ItemsRebuilt?.Invoke();
    }
    public bool Save()
    {
        _recentSave.Stop();
        try
        {
            Settings.Pins = Items.Where(i => i.IsPinned).Select(i => i.Pin).ToList();
            _store.Save(Settings); return true;
        }
        catch (Exception ex) { Error?.Invoke(LocalizedText.Get("设置保存失败：") + ex.Message); _store.Log(LocalizedText.Get("保存失败"), ex); return false; }
    }
    public event Action? WidgetSettingsChanged;
    private void NotifyWidgets()
    {
        try{WidgetSettingsChanged?.Invoke();}catch(Exception ex){_store.Log(LocalizedText.Get("小组件视图更新失败，配置已经保存"),ex);}
    }
    public bool TrySaveWidgets(WidgetOptions widgets)
    {
        try{var candidate=Settings.Copy();candidate.Widgets=widgets.Copy();candidate.Normalize();candidate.Pins=Items.Where(i=>i.IsPinned).Select(i=>i.Pin).ToList();_store.Save(candidate);Settings.Widgets=candidate.Widgets;NotifyWidgets();return true;}
        catch(Exception ex){Error?.Invoke(LocalizedText.Get("小组件保存失败，原设置已保留：")+ex.Message);_store.Log(LocalizedText.Get("保存小组件失败"),ex);return false;}
    }
    public bool TryApplyPreferences(DockSettings draft)
    {
        try
        {
            var candidate = draft.Copy();
            candidate.Pins = Items.Where(i => i.IsPinned).Select(i => i.Pin).ToList();
            candidate.RecentApps = new List<PinnedItem>(Settings.RecentApps);
            candidate.Normalize();
            _store.Save(candidate);
            Settings.ApplyPreferences(candidate);
            Settings.Pins = candidate.Pins;
            NotifyWidgets();
            return true;
        }
        catch (Exception ex) { Error?.Invoke(LocalizedText.Get("设置保存失败，原设置已保留：") + ex.Message); _store.Log(LocalizedText.Get("保存偏好失败"), ex); return false; }
    }
}

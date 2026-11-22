using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

// Keep playback controls and animated records alive across timeline/volume samples.
internal sealed class WidgetMusicView : Grid
{
    private WidgetRenderData _data;
    private FrameworkElement _root;
    private readonly DispatcherTimer _timer;
    private bool _active = true, _pendingBuild;
    private double _sampleAt;
    internal WidgetDefinition Definition { get; }
    internal WidgetRenderData Data => _data;
    internal WidgetMusicView(WidgetDefinition definition, WidgetRenderData data)
    {
        Definition = definition; _data = data; _sampleAt = ControlMotion.Now;
        _root = BuildRoot(); Children.Add(_root);
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateTimeline(), Dispatcher); _timer.Stop();
        Loaded += (_, _) => { if (_pendingBuild) Rebuild(); SyncTimer(); };
        Unloaded += (_, _) => { _timer.Stop(); foreach (var volume in Elements<WidgetVolumeButton>(_root)) volume.Close(); };
        IsVisibleChanged += (_, _) => SyncTimer();
        Update(data);
    }
    internal static IEnumerable<T> Elements<T>(DependencyObject owner) where T : DependencyObject
    {
        if (owner is T result) yield return result;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(owner); i++)
            foreach (var child in Elements<T>(VisualTreeHelper.GetChild(owner, i))) yield return child;
    }
    private FrameworkElement BuildRoot()
    {
        var linked = _data with { MediaCommand = command => _data.MediaCommand?.Invoke(command), Seek = value => _data.Seek?.Invoke(value) };
        var root = WidgetContent.PlayerLayout(Definition, linked);
        foreach (var volume in Elements<WidgetVolumeButton>(root)) volume.PopupClosed += () => { if (_pendingBuild && IsLoaded) Rebuild(); };
        return root;
    }
    internal void Update(WidgetRenderData data)
    {
        bool rebuild = data.Media?.Title != _data.Media?.Title || data.Media?.Artist != _data.Media?.Artist
            || !ReferenceEquals(data.Media?.Artwork, _data.Media?.Artwork) || data.Error != _data.Error
            || !ReferenceEquals(data.Options.MusicHistory, _data.Options.MusicHistory);
        if (!ReferenceEquals(data.Media, _data.Media)) _sampleAt = ControlMotion.Now;
        _data = data;
        if (rebuild) {
            if (Elements<WidgetVolumeButton>(_root).Any(v => v.IsOpen)) _pendingBuild = true;
            else Rebuild();
        }
        foreach (var player in Elements<WidgetRecordPlayer>(_root)) player.Update(data);
        foreach (var volume in Elements<WidgetVolumeButton>(_root)) volume.Update(data);
        foreach (var button in Elements<Button>(_root)) {
            string id = AutomationProperties.GetAutomationId(button);
            bool enabled = id switch { "widget-music-previous" => data.Media?.CanPrevious == true, "widget-music-next" => data.Media?.CanNext == true, "widget-music-toggle" or "widget-pocket-select" => data.Media?.CanPlay == true, _ => false };
            if (id is not ("widget-music-previous" or "widget-music-next" or "widget-music-toggle" or "widget-pocket-select")) continue;
            button.IsEnabled = enabled && data.MediaCommand != null;
            if (id == "widget-music-toggle") {
                bool playing = data.Media?.Playing == true;
                string name = UiText.IsEnglish ? (playing ? "Pause" : "Play") : (playing ? "暂停" : "播放");
                button.ToolTip = name; AutomationProperties.SetName(button, name);
                var glyph = Elements<Path>(button).FirstOrDefault();
                if (glyph != null) glyph.Data = Geometry.Parse(playing ? "M3,1 L7,1 7,17 3,17 Z M11,1 L15,1 15,17 11,17 Z" : "M4,1 L17,9 4,17 Z");
            }
        }
        ToolTip = data.Error; UpdateTimeline(); SyncTimer();
    }
    private void Rebuild()
    {
        _pendingBuild = false; var next = BuildRoot();
        var oldPlayers = Elements<WidgetRecordPlayer>(_root).ToList();
        foreach (var fresh in Elements<WidgetRecordPlayer>(next).ToArray()) {
            var old = oldPlayers.FirstOrDefault(p => p.Width == fresh.Width && p.HasSleeve == fresh.HasSleeve);
            if (old == null || VisualTreeHelper.GetParent(old) is not Panel before || VisualTreeHelper.GetParent(fresh) is not Panel after) continue;
            int index = after.Children.IndexOf(fresh); before.Children.Remove(old); after.Children.Remove(fresh);
            old.Margin = fresh.Margin; old.HorizontalAlignment = fresh.HorizontalAlignment; old.VerticalAlignment = fresh.VerticalAlignment;
            old.Update(_data); after.Children.Insert(index, old); oldPlayers.Remove(old);
        }
        Children.Clear(); _root = next; Children.Add(next);
    }
    internal void SetActive(bool active)
    {
        _active = active; foreach (var player in Elements<WidgetRecordPlayer>(_root)) player.SetActive(active);
        if (!active) foreach (var volume in Elements<WidgetVolumeButton>(_root)) volume.Close(); SyncTimer();
    }
    private void SyncTimer()
    {
        bool run = _active && IsLoaded && IsVisible && _data.Media is { Playing: true, Duration: var duration } && duration > TimeSpan.Zero;
        if (run) _timer.Start(); else _timer.Stop();
    }
    private static string Time(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"mm\:ss");
    private static void Text(FrameworkElement element, string value)
    {
        if (element is WidgetText widget) widget.SetText(value); else if (element is TextBlock block) block.Text = value;
    }
    private void UpdateTimeline()
    {
        var media = _data.Media; bool known = media?.Duration > TimeSpan.Zero;
        var position = known ? MediaControlService.PositionAt(media!.Position, media.Duration, media.Playing, media.PlaybackRate, ControlMotion.Now - _sampleAt) : TimeSpan.Zero;
        foreach (var slider in Elements<Slider>(_root).Where(s => AutomationProperties.GetAutomationId(s) == "widget-music-seek")) {
            slider.IsEnabled = known && media!.CanSeek && _data.Seek != null;
            if (slider.IsMouseCaptureWithin || slider.IsKeyboardFocusWithin && (Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Home) || Keyboard.IsKeyDown(Key.End))) continue;
            slider.Maximum = known ? media!.Duration.TotalSeconds : 1; slider.Value = position.TotalSeconds;
        }
        foreach (var progress in Elements<ProgressBar>(_root).Where(p => AutomationProperties.GetAutomationId(p) == "widget-music-readonly-progress")) {
            progress.Maximum = known ? media!.Duration.TotalSeconds : 1; progress.Value = position.TotalSeconds; progress.IsEnabled = known;
        }
        foreach (var element in Elements<FrameworkElement>(_root)) switch (AutomationProperties.GetAutomationId(element)) {
            case "widget-music-elapsed": Text(element, known ? Time(position) : "—:—"); break;
            case "widget-music-duration": Text(element, known ? Time(media!.Duration) : "—:—"); break;
            case "widget-music-remaining": Text(element, known ? "−" + Time(media!.Duration - position) : "—:—"); break;
            case "widget-music-times": Text(element, known ? Time(position) + " / " + Time(media!.Duration) : "—:— / —:—"); break;
        }
    }
}

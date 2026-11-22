using System;
using System.Linq;
using System.Threading.Tasks;
using DockForWindows.Models;
using DockForWindows.Views;

namespace DockForWindows.Services;

internal sealed partial class WidgetRuntime
{
    private AudioState? _audio;
    private string? _audioError,_outputName;
    private DateTime _audioDue;
    private bool _audioWanted, _audioReading, _audioAgain, _volumeWriting;
    private double? _pendingVolume;
    private void PollMusicAudio()
    {
        if (_audioWanted && !_disposed && DateTime.UtcNow >= _audioDue && !_audioReading) _ = ReadMusicAudio();
    }
    private async Task ReadMusicAudio()
    {
        if (_disposed) return;
        if (_audioReading) { _audioAgain = true; return; }
        _audioReading = true; _audioDue = DateTime.UtcNow.AddSeconds(5);
        try { var state = await Task.Run(()=>{var audio=SystemControlService.ReadAudio();string? output=null;try{output=AudioDeviceService.Outputs().FirstOrDefault(x=>x.Default)?.Name;}catch{}return (Audio:audio,Output:output);}, _lifetime.Token); if (!_disposed) { _audio = state.Audio;_outputName=state.Output; _audioError = null; } }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (!_disposed) { _audio = null; _audioError = e.Message; } }
        finally {
            _audioReading = false;
            if (!_disposed) { Changed?.Invoke(WidgetKind.Music); if (_audioAgain) { _audioAgain = false; _ = ReadMusicAudio(); } }
        }
    }
    private async void SetMusicVolume(double percent)
    {
        if (_disposed || !double.IsFinite(percent)) return;
        _pendingVolume = Math.Clamp(percent, 0, 100);
        if (_volumeWriting) return;
        _volumeWriting = true;
        try {
            // Match the existing control-center queue: only the latest pointer value reaches Core Audio.
            while (!_disposed && _pendingVolume is double value) {
                _pendingVolume = null;
                await Task.Run(() => SystemControlService.SetVolume(value), _lifetime.Token);
                await Task.Delay(24, _lifetime.Token);
            }
        } catch (OperationCanceledException) { }
        catch (Exception e) { _pendingVolume = null; if (!_disposed) _audioError = e.Message; }
        finally {
            _volumeWriting = false;
            if (!_disposed) { if (_audioError == null) await ReadMusicAudio(); else Changed?.Invoke(WidgetKind.Music); }
        }
    }
}

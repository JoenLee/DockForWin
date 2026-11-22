using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DockForWindows.Models;

namespace DockForWindows.Services;

internal sealed partial class WidgetRuntime
{
    private string? FavoriteKey() => Media is { Title.Length: > 0 } media
        ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(media.SourceApp + "\u001f" + media.Title + "\u001f" + media.Artist))) : null;
    private bool MusicFavorite() => FavoriteKey() is string key && _options().FavoriteTracks.Contains(key, StringComparer.Ordinal);
    private void ToggleMusicFavorite()
    {
        if (_disposed || FavoriteKey() is not string key) return;
        var options = _options().Copy();
        if (!options.FavoriteTracks.Remove(key)) options.FavoriteTracks.Add(key);
        options.Normalize();
        if (_save != null) { if (!_save(options)) MediaError = "收藏保存失败，原数据保留。"; }
        else _options().FavoriteTracks = options.FavoriteTracks;
        Changed?.Invoke(WidgetKind.Music);
    }
}

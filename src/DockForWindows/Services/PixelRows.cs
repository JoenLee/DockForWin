using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DockForWindows.Services;

// Row operations on packed 32-bit premultiplied BGRA pixels for the backdrop composite.
internal static class PixelRows
{
    // True when every pixel's alpha is 255.
    public static bool Opaque(ReadOnlySpan<byte> pixels)
    {
        var words = MemoryMarshal.Cast<byte, uint>(pixels);
        int i = 0;
        if (Vector.IsHardwareAccelerated && words.Length >= Vector<uint>.Count)
        {
            var alpha = new Vector<uint>(0xFF000000u);
            var vectors = MemoryMarshal.Cast<uint, Vector<uint>>(words);
            foreach (var v in vectors) if (!Vector.EqualsAll(v & alpha, alpha)) return false;
            i = vectors.Length * Vector<uint>.Count;
        }
        for (; i < words.Length; i++) if ((words[i] & 0xFF000000u) != 0xFF000000u) return false;
        return true;
    }

    // Premultiplied "source over destination" for one row; fully transparent and fully opaque runs are cheap.
    public static void Over(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var src = MemoryMarshal.Cast<byte, uint>(source);
        var dst = MemoryMarshal.Cast<byte, uint>(destination);
        for (int i = 0; i < src.Length; i++)
        {
            uint s = src[i], a = s >> 24;
            if (a == 0) continue;
            if (a == 255) { dst[i] = s; continue; }
            uint d = dst[i], k = 255 - a;
            // round(d * k / 255) per channel, two channels at a time.
            uint rb = (d & 0x00FF00FFu) * k + 0x00800080u; rb = ((rb + ((rb >> 8) & 0x00FF00FFu)) >> 8) & 0x00FF00FFu;
            uint ga = ((d >> 8) & 0x00FF00FFu) * k + 0x00800080u; ga = (ga + ((ga >> 8) & 0x00FF00FFu)) & 0xFF00FF00u;
            dst[i] = s + (rb | ga);
        }
    }
}

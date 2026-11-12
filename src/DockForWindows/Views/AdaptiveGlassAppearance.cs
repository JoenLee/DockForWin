using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DockForWindows.Views;

// Small CPU samples of the existing raw capture, never another screen capture.
// State is per pane/dot so mixed light and dark areas retain local contrast.
public sealed class AdaptiveGlassAppearance
{
    private bool _initialized;
    private double _target = .5, _lastSample = double.NegativeInfinity, _lastFrame;
    public double Luminance { get; private set; } = .5;
    public void Observe(BitmapSource source, Int32Rect region, double seconds)
    {
        if (!_initialized || seconds - _lastSample >= .1)
        {
            _target = SampleLuminance(source, region);
            _lastSample = seconds;
        }
        double elapsed = _initialized ? Math.Clamp(seconds - _lastFrame, 0, .1) : 0;
        Advance(_target, elapsed);
        _lastFrame = seconds;
    }
    public void Advance(double target, double elapsedSeconds)
    {
        target = double.IsFinite(target) ? Math.Clamp(target, 0, 1) : .5;
        if (!_initialized) { Luminance = target; _initialized = true; return; }
        double elapsed = double.IsFinite(elapsedSeconds) ? Math.Clamp(elapsedSeconds, 0, 1) : 0;
        Luminance += (target - Luminance) * (1 - Math.Exp(-elapsed / .4));
    }
    public static double TintOpacity(double userMaximum, double luminance)
    {
        double maximum = double.IsFinite(userMaximum) ? Math.Clamp(userMaximum, 0, 1) : 0;
        double t = Math.Clamp(((double.IsFinite(luminance) ? luminance : .5) - .08) / .84, 0, 1);
        t = t * t * (3 - 2 * t);
        // A quiet 72..100% of the user's maximum. A zero maximum stays off.
        return maximum * (.72 + .28 * t);
    }
    public static bool LightIndicator(double luminance, bool? previous)
    {
        // Hysteresis prevents flicker on textured backgrounds around mid-grey.
        if (previous is null) return luminance < .5;
        return previous.Value ? luminance < .58 : luminance < .42;
    }
    public static double SampleLuminance(BitmapSource source, Int32Rect region)
    {
        if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32 && source.Format != PixelFormats.Bgr32) return .5;
        int left = Math.Clamp(region.X, 0, source.PixelWidth), top = Math.Clamp(region.Y, 0, source.PixelHeight);
        int right = (int)Math.Clamp((long)region.X + region.Width, left, source.PixelWidth);
        int bottom = (int)Math.Clamp((long)region.Y + region.Height, top, source.PixelHeight);
        if (right <= left || bottom <= top) return .5;
        var pixel = new byte[4]; double sum = 0; int count = 0;
        for (int row = 0; row < 3; row++) for (int column = 0; column < 8; column++)
        {
            int x = left + Math.Min(right - left - 1, (int)((column + .5) * (right - left) / 8));
            int y = top + Math.Min(bottom - top - 1, (int)((row + .5) * (bottom - top) / 3));
            source.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            double alpha = source.Format == PixelFormats.Bgr32 ? 1 : pixel[3] / 255d;
            if (alpha <= 0) continue;
            double scale = source.Format == PixelFormats.Pbgra32 ? 1 / alpha : 1;
            sum += Math.Clamp((.2126 * pixel[2] + .7152 * pixel[1] + .0722 * pixel[0]) * scale / 255, 0, 1);
            count++;
        }
        return count == 0 ? .5 : sum / count;
    }
}

using System;

namespace DockForWindows.Views;

public static class DockMath
{
    public static double Scale(double distance, double radius, double strength)
    {
        if (radius <= 0 || distance >= radius) return 1;
        double t = Math.Clamp(distance / radius, 0, 1);
        return 1 + (Math.Clamp(strength, 1, 1.8) - 1) * (1 + Math.Cos(Math.PI * t)) / 2;
    }
    public static double Step(double current, double target, double elapsedSeconds)
        => current + (target - current) * (1 - Math.Exp(-18 * Math.Clamp(elapsedSeconds, 0, .05)));
}

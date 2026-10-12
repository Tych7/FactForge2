using System;
using Avalonia.Media;

namespace FactForge.Helpers;

public static class PlayerColorHelper
{
    public static IBrush GetColorForName(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return Brushes.Gray;
        
        var hash = playerName.GetHashCode();
        var hue = Math.Abs(hash) % 360;
        
        return new SolidColorBrush(FromHsv(hue, 0.75, 0.9));
    }

    public static Color FromHsv(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;
        
        double r = 0, g = 0, b = 0;
        
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);
        
        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }
}
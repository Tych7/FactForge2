using System;
using Avalonia.Media;
using FactForge.Models;

namespace FactForge.ViewModels;

public class LeaderboardBarItem
{
    public string Name { get; }
    public int Score { get; }
    public IBrush Fill { get; }
    public double Fraction { get; }
    public int Index { get; }

    public LeaderboardBarItem(LeaderboardEntryDto entry, int maxScore, int index)
    {
        Name = entry.Name;
        Score = entry.Score;
        Index = index;
        Fill = GetColor(entry.Name);
        Fraction = maxScore <= 0 ? 0 : (double)Score / maxScore;
    }

    private static IBrush GetColor(string playerName)
    {
        var hash = playerName.GetHashCode();
        var hue = Math.Abs(hash) % 360;
        
        return new SolidColorBrush(FromHsv(hue, 0.75, 0.9));
    }

    private static Color FromHsv(double h, double s, double v)
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

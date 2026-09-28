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
        Fill = AnswerColorPalette.Colors[index % AnswerColorPalette.Colors.Length];
        Fraction = maxScore <= 0 ? 0 : (double)Score / maxScore;
    }
}

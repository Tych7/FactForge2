using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Models;

namespace FactForge.ViewModels;

/// <summary>One answer tile. IsCorrect draws the white border (editor preview + presenter reveal).</summary>
public sealed record AnswerOptionDisplay(string Text, IBrush Background, bool IsCorrect = false);

/// <summary>
/// The single model that SlideCanvas renders. The presenter builds it from a SlideDto,
/// the editor builds it from the selected slide editor view model.
/// </summary>
public partial class SlideDisplay : ObservableObject
{
    public SlideType Type { get; init; }
    public string? Header { get; init; }
    public string? SubText { get; init; }
    public string? Question { get; init; }
    public int TimeSeconds { get; init; }

    // Live values: the presenter updates these, the editor leaves them static.
    [ObservableProperty] private double _secondsRemaining;
    [ObservableProperty] private string _statusText = string.Empty;   // "3 answered" or "20s"
    [ObservableProperty] private string? _correctAnswer;              // open question, after reveal

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMultipleChoice))]
    [NotifyPropertyChangedFor(nameof(IsOpenQuestion))]
    private IReadOnlyList<AnswerOptionDisplay>? _options;

    public bool IsText => Type == SlideType.Text;
    public bool IsLeaderboard => Type == SlideType.Leaderboard;
    public bool IsMultipleChoice => !IsText && !IsLeaderboard && Options is not null;
    public bool IsOpenQuestion => !IsText && !IsLeaderboard && Options is null;
}

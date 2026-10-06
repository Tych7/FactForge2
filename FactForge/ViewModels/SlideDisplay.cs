using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    // Music question
    public string? MusicTitle { get; init; }
    public string? MusicArtist { get; init; }
    public IAsyncRelayCommand? ReplayMusicCommand { get; init; }

    /// <summary>
    /// Total duration of the music in seconds.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MusicTimeText))]
    private double _musicDurationSeconds;

    /// <summary>
    /// Current playback position of the music in seconds.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MusicTimeText))]
    private double _musicPositionSeconds;
    
    public string MusicTimeText =>
    $"{FormatTime(MusicPositionSeconds)} / {FormatTime(MusicDurationSeconds)}";

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));

        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
    
    /// <summary>
    /// Whether the music title and artist have been revealed.
    /// </summary>
    [ObservableProperty]
    private bool _isMusicRevealed;

    /// <summary>Optional picture shown on the right; other content uses the remaining width.</summary>
    public Bitmap? Image { get; init; }
    public bool HasImage => Image is not null;
    public GridLength ContentColumnWidth => HasImage ? new GridLength(3, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
    public GridLength ImageColumnWidth => HasImage ? new GridLength(2, GridUnitType.Star) : new GridLength(0);

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
    public bool IsMultipleChoice => Type == SlideType.MultipleChoice;
    public bool IsOpenQuestion => Type == SlideType.OpenQuestion;
    public bool IsMusicQuestion => Type == SlideType.MusicQuestion;

}

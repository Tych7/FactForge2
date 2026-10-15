using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Data.Entities;
using FactForge.Services;
using FactForge.Views;

namespace FactForge.ViewModels;

public class PlayerResultViewModel
{
    public string Name { get; }
    public int Score { get; }
    public int AnswerCount { get; }
    public int CorrectCount { get; }

    public PlayerResultViewModel(Player player)
    {
        Name = player.Name;
        Score = player.Score;
        AnswerCount = player.Answers.Count;
        CorrectCount = player.Answers.Count(a => a.IsCorrect);
    }
}

public partial class SessionResultViewModel : ObservableObject
{
    private readonly Func<SessionResultViewModel, Task> _remove;

    public int Id { get; }
    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; }
    public bool IsComplete => EndedAt is not null;

    public List<PlayerResultViewModel> Players { get; }
    public List<ScoreChartSeries> ScoreHistory { get; }

    [ObservableProperty]
    private bool _isScoreHistoryExpanded;

    public SessionResultViewModel(
        QuizSession session,
        Func<SessionResultViewModel, Task> remove)
    {
        Id = session.Id;
        _remove = remove;

        StartedAt = session.StartedAt;
        EndedAt = session.EndedAt;

        Players = session.Players
            .OrderByDescending(p => p.Score)
            .Select(p => new PlayerResultViewModel(p))
            .ToList();

        var snapshots = session.Players
            .SelectMany(p => p.ScoreSnapshots)
            .ToList();

        var questionNumbers = snapshots
            .Select(s => s.QuestionNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        ScoreHistory = session.Players.Select(player =>
        {
            var playerSnapshots = player.ScoreSnapshots
                .GroupBy(s => s.QuestionNumber)
                .ToDictionary(g => g.Key, g => g.Last().Score);

            var currentScore = 0;

            var scores = questionNumbers.Select(question =>
            {
                if (playerSnapshots.TryGetValue(question, out var score))
                    currentScore = score;

                return currentScore;
            }).ToList();

            return new ScoreChartSeries
            {
                Name = player.Name,
                Scores = scores
            };
        }).ToList();
    }

    [RelayCommand]
    private void ToggleScoreHistory()
    {
        IsScoreHistoryExpanded = !IsScoreHistoryExpanded;
    }

    [RelayCommand]
    private Task RemoveAsync() => _remove(this);
}

public class ScoreChartSeries
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<int> Scores { get; init; } = Array.Empty<int>();
}

public partial class ResultsViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly IWindowService _windowService;
    private readonly QuizRepository _repository;
    private readonly int _quizId;
    private readonly Action _onBack;
    private readonly Action _onSettings;

    [ObservableProperty]
    private string _quizTitle = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<SessionResultViewModel> Sessions { get; } = new();

    public ResultsViewModel(
        IDialogService dialogService,
        IWindowService windowService,
        QuizRepository repository,
        int quizId,
        Action onBack,
        Action onSettings)
    {
        _dialogService = dialogService;
        _windowService = windowService;
        _repository = repository;
        _quizId = quizId;
        _onBack = onBack;
        _onSettings = onSettings;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var quiz = await _repository.GetQuizWithSlidesAsync(_quizId);
            QuizTitle = quiz?.Title ?? string.Empty;

            var sessions = await _repository.GetSessionsForQuizAsync(_quizId);

            Sessions.Clear();

            foreach (var session in sessions)
            {
                Sessions.Add(
                    new SessionResultViewModel(session, RemoveSessionAsync));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RemoveSessionAsync(SessionResultViewModel session)
    {
        await _repository.DeleteSessionAsync(session.Id);
        Sessions.Remove(session);
    }

    [RelayCommand]
    private async Task OpenMenuAsync()
    {
        var result = await _dialogService.ShowMenuAsync();

        switch (result)
        {
            case AppMenuResult.Options:
                _onSettings();
                break;

            case AppMenuResult.Exit:
                _windowService.Exit();
                break;
        }
    }

    [RelayCommand]
    private void Back() => _onBack();
}

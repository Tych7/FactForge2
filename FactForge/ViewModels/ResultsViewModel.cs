using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Data.Entities;
using FactForge.Services;

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

public class SessionResultViewModel
{
    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; }
    public bool IsComplete => EndedAt is not null;
    public List<PlayerResultViewModel> Players { get; }

    public SessionResultViewModel(QuizSession session)
    {
        StartedAt = session.StartedAt;
        EndedAt = session.EndedAt;
        Players = session.Players
            .OrderByDescending(p => p.Score)
            .Select(p => new PlayerResultViewModel(p))
            .ToList();
    }
}

public partial class ResultsViewModel : ViewModelBase
{
    private readonly QuizRepository _repository;
    private readonly int _quizId;
    private readonly Action _onBack;

    [ObservableProperty] private string _quizTitle = string.Empty;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<SessionResultViewModel> Sessions { get; } = new();

    public ResultsViewModel(QuizRepository repository, int quizId, Action onBack)
    {
        _repository = repository;
        _quizId = quizId;
        _onBack = onBack;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        var quiz = await _repository.GetQuizWithSlidesAsync(_quizId);
        QuizTitle = quiz?.Title ?? string.Empty;

        var sessions = await _repository.GetSessionsForQuizAsync(_quizId);
        Sessions.Clear();
        foreach (var session in sessions)
            Sessions.Add(new SessionResultViewModel(session));
        IsLoading = false;
    }

    [RelayCommand]
    private void Back() => _onBack();
}

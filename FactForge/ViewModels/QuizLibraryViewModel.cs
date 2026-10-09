using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Services;
using FactForge.Views;

namespace FactForge.ViewModels;

public partial class QuizLibraryViewModel : ViewModelBase
{
    private readonly QuizRepository _repository;
    private readonly IDialogService _dialogService;
    private readonly IWindowService _windowService;
    private readonly Action<int> _onEdit;
    private readonly Action<int> _onPresent;
    private readonly Action<int> _onResults;
    private readonly Action _onSettings;
    private List<QuizListItemViewModel> _allQuizzes = new();


    public ObservableCollection<QuizListItemViewModel> Quizzes { get; } = new();

    [ObservableProperty] private string _newQuizTitle = string.Empty;
    [ObservableProperty] private QuizListItemViewModel? _selectedQuiz;
    [ObservableProperty] private bool _isLoading;

    public QuizLibraryViewModel(
        QuizRepository repository, 
        IDialogService dialogService, 
        IWindowService windowService, 
        Action<int> onEdit, 
        Action<int> onPresent, 
        Action<int> onResults,
        Action onSettings)
    {
        _repository = repository;
        _dialogService = dialogService;
        _windowService = windowService;
        _onEdit = onEdit;
        _onPresent = onPresent;
        _onResults = onResults;
        _onSettings = onSettings;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var quizzes = await _repository.GetAllQuizzesAsync();

            _allQuizzes = quizzes
                .Select(quiz => new QuizListItemViewModel(quiz))
                .ToList();

            FilterQuizzes();
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnNewQuizTitleChanged(string value)
    {
        FilterQuizzes();
    }

    private void FilterQuizzes()
    {
        var search = NewQuizTitle.Trim();

        var filteredQuizzes = string.IsNullOrWhiteSpace(search)
            ? _allQuizzes
            : _allQuizzes
                .Where(quiz => quiz.Title.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

        Quizzes.Clear();

        foreach (var quiz in filteredQuizzes)
        {
            Quizzes.Add(quiz);
        }
    }

    [RelayCommand]
    private async Task OpenCreateQuizDialogAsync()
    {
        var title = await _dialogService.ShowCreateQuizAsync();

        if (string.IsNullOrWhiteSpace(title))
            return;

        await CreateQuizAsync(title);
    }

    private async Task CreateQuizAsync(string title)
    {
        var quiz = await _repository.CreateQuizAsync(title.Trim());
        await LoadAsync();
        _onEdit(quiz.Id);
    }

    [RelayCommand]
    private void EditQuiz(QuizListItemViewModel? item)
    {
        if (item is not null) _onEdit(item.Id);
    }

    [RelayCommand]
    private void PresentQuiz(QuizListItemViewModel? item)
    {
        if (item is not null) _onPresent(item.Id);
    }

    [RelayCommand]
    private void ViewResults(QuizListItemViewModel? item)
    {
        if (item is not null) _onResults(item.Id);
    }

    [RelayCommand]
    private async Task DeleteQuizAsync(QuizListItemViewModel? item)
    {
        if (item is null) return;

        var confirmed = await _dialogService.ConfirmAsync(
            "Delete quiz",
            $"Delete \"{item.Title}\"? This can't be undone.");

        if (!confirmed) return;

        await _repository.DeleteQuizAsync(item.Id);
        await LoadAsync();
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
    private void OpenSettings() => _onSettings();

    [RelayCommand]
    private void Exit() => _windowService.Exit();
}

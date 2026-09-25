using System;
using System.Collections.ObjectModel;
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
        var quizzes = await _repository.GetAllQuizzesAsync();
        Quizzes.Clear();
        foreach (var quiz in quizzes)
            Quizzes.Add(new QuizListItemViewModel(quiz));
        IsLoading = false;
    }

    [RelayCommand]
    private async Task CreateQuizAsync()
    {
        var title = string.IsNullOrWhiteSpace(NewQuizTitle) ? "Untitled Quiz" : NewQuizTitle.Trim();
        var quiz = await _repository.CreateQuizAsync(title);
        NewQuizTitle = string.Empty;
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

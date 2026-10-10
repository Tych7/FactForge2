using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Models;
using FactForge.Services;
using FactForge.Views;

namespace FactForge.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    private readonly QuizRepository _repository;
    private readonly int _quizId;
    private readonly Action _onBack;
    private readonly Action<int> _onPresent;
    private readonly IDialogService _dialogService;
    private readonly IWindowService _windowService;
    private readonly Action _onSettings;

    public ObservableCollection<SlideListItemViewModel> Slides { get; } = new();

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private SlideListItemViewModel? _selectedSlide;
    [ObservableProperty] private ViewModelBase? _currentSlideEditor;
    [ObservableProperty] private bool _isLoading;

    public EditorViewModel(
        QuizRepository repository, 
        int quizId, Action onBack, 
        Action<int> onPresent, 
        IDialogService dialogService, 
        IWindowService windowService, 
        Action onSettings)
    {
        _repository = repository;
        _quizId = quizId;
        _onBack = onBack;
        _onPresent = onPresent;
        _windowService = windowService;
        _dialogService = dialogService;
        _onSettings = onSettings;
        _ = LoadAsync(selectSlideId: null);
    }

    private async Task LoadAsync(int? selectSlideId)
    {
        IsLoading = true;
        var quiz = await _repository.GetQuizWithSlidesAsync(_quizId);
        if (quiz is null)
        {
            _onBack();
            return;
        }

        Title = quiz.Title;
        Slides.Clear();
        foreach (var slide in quiz.Slides.OrderBy(s => s.OrderIndex))
            Slides.Add(new SlideListItemViewModel(slide));

        var toSelect = selectSlideId is int id
            ? Slides.FirstOrDefault(s => s.Id == id)
            : Slides.FirstOrDefault();

        await SetSelectedSlideAsync(toSelect);
        IsLoading = false;
    }

    // Bypasses the generated SelectedSlide setter (and its OnSelectedSlideChanged
    // save-then-load hook) because the caller already controls save/load ordering.
#pragma warning disable MVVMTK0034
    private async Task SetSelectedSlideAsync(SlideListItemViewModel? item)
    {
        _selectedSlide = item;
        OnPropertyChanged(nameof(SelectedSlide));
        await LoadSlideEditorAsync(item);
        _lastLoadedSlide = item;
    }
#pragma warning restore MVVMTK0034

    private SlideListItemViewModel? _lastLoadedSlide;

    partial void OnSelectedSlideChanged(SlideListItemViewModel? value)
    {
        _ = SwitchSlideAsync(value);
    }

    // Called when the user picks a different slide in the list: persists whatever
    // was being edited on the previous slide before loading the new one, so
    // switching slides never silently discards in-progress edits.
    private async Task SwitchSlideAsync(SlideListItemViewModel? newSelection)
    {
        if (_lastLoadedSlide is not null && _lastLoadedSlide != newSelection)
            await SaveCurrentSlideEditorAsync();
        await LoadSlideEditorAsync(newSelection);
        _lastLoadedSlide = newSelection;
    }

    private async Task LoadSlideEditorAsync(SlideListItemViewModel? item)
    {
        if (item is null)
        {
            CurrentSlideEditor = null;
            return;
        }

        var quiz = await _repository.GetQuizWithSlidesAsync(_quizId);
        var slide = quiz?.Slides.FirstOrDefault(s => s.Id == item.Id);
        if (slide is null)
        {
            CurrentSlideEditor = null;
            return;
        }

        CurrentSlideEditor = slide.Type switch
        {
            SlideType.Text => new TextSlideEditorViewModel(slide),
            SlideType.MultipleChoice => new MultipleChoiceSlideEditorViewModel(slide),
            SlideType.OpenQuestion => new OpenQuestionSlideEditorViewModel(slide),
            SlideType.MusicQuestion => new MusicQuestionSlideEditorViewModel(slide),
            SlideType.Leaderboard => new LeaderboardSlideEditorViewModel(slide),
            _ => null
        };
    }

    private async Task SaveCurrentSlideEditorAsync()
    {
        var entity = CurrentSlideEditor switch
        {
            TextSlideEditorViewModel t => t.ToEntity(),
            MultipleChoiceSlideEditorViewModel m => m.ToEntity(),
            OpenQuestionSlideEditorViewModel o => o.ToEntity(),
            MusicQuestionSlideEditorViewModel m => m.ToEntity(),
            LeaderboardSlideEditorViewModel l => l.ToEntity(),
            _ => null
        };
        if (entity is null) return;

        await _repository.SaveSlideAsync(entity);

        var index = Slides.ToList().FindIndex(s => s.Id == entity.Id);
        if (index >= 0)
        {
            var quiz = await _repository.GetQuizWithSlidesAsync(_quizId);
            var fresh = quiz?.Slides.FirstOrDefault(s => s.Id == entity.Id);
            if (fresh is not null)
                Slides[index] = new SlideListItemViewModel(fresh);
        }
    }

    [RelayCommand]
    private async Task AddTextSlideAsync() => await AddSlideAsync(SlideType.Text);

    [RelayCommand]
    private async Task AddMultipleChoiceSlideAsync() => await AddSlideAsync(SlideType.MultipleChoice);

    [RelayCommand]
    private async Task AddOpenQuestionSlideAsync() => await AddSlideAsync(SlideType.OpenQuestion);

    [RelayCommand]
    private async Task AddMusicQuestionSlideAsync() => await AddSlideAsync(SlideType.MusicQuestion);

    [RelayCommand]
    private async Task AddLeaderboardSlideAsync() => await AddSlideAsync(SlideType.Leaderboard);

    private async Task AddSlideAsync(SlideType type)
    {
        var insertAt = SelectedSlide is null ? Slides.Count : Slides.IndexOf(SelectedSlide) + 1;
        await SaveCurrentSlideEditorAsync();
        var slide = await _repository.AddSlideAsync(_quizId, type, insertAt);
        await LoadAsync(slide.Id);
    }

    [RelayCommand]
    private async Task DeleteSlideAsync()
    {
        if (SelectedSlide is null) return;
        await _repository.DeleteSlideAsync(SelectedSlide.Id);
        await LoadAsync(selectSlideId: null);
    }

    [RelayCommand]
    private async Task MoveSlideUpAsync()
    {
        if (SelectedSlide is null) return;
        var index = Slides.IndexOf(SelectedSlide);
        if (index <= 0) return;
        var slideId = SelectedSlide.Id;
        // Capture the id before saving: SaveCurrentSlideEditorAsync replaces the selected
        // slide's list entry with a new instance, which drops the ListBox's selection
        // (and nulls SelectedSlide) since it's bound to the old object by reference.
        await SaveCurrentSlideEditorAsync();
        await _repository.ReorderSlideAsync(_quizId, slideId, index - 1);
        await LoadAsync(slideId);
    }

    [RelayCommand]
    private async Task MoveSlideDownAsync()
    {
        if (SelectedSlide is null) return;
        var index = Slides.IndexOf(SelectedSlide);
        if (index < 0 || index >= Slides.Count - 1) return;
        var slideId = SelectedSlide.Id;
        await SaveCurrentSlideEditorAsync();
        await _repository.ReorderSlideAsync(_quizId, slideId, index + 1);
        await LoadAsync(slideId);
    }

    [RelayCommand]
    private async Task SaveTitleAsync()
    {
        await _repository.RenameQuizAsync(_quizId, string.IsNullOrWhiteSpace(Title) ? "Untitled Quiz" : Title.Trim());
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
    private async Task RenameQuizAsync()
    {
        // Opens the dialog pre-filled with the current Title
        var newTitle = await _dialogService.ShowRenameQuizAsync(Title);

        // Guard against null, empty, or unchanged titles
        if (string.IsNullOrWhiteSpace(newTitle) || newTitle.Trim() == Title)
            return;

        // Update the local Title property (notifies UI via INotifyPropertyChanged)
        Title = newTitle.Trim();

        // Persist changes using the existing repository method and _quizId variable
        await _repository.RenameQuizAsync(_quizId, Title);
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await SaveCurrentSlideEditorAsync();
        await SaveTitleAsync();
        _onBack();
    }

    [RelayCommand]
    private async Task PresentAsync()
    {
        await SaveCurrentSlideEditorAsync();
        await SaveTitleAsync();
        _onPresent(_quizId);
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Services;
using FactForge.Services.Presentation;
using FactForge.ViewModels.Presentation;

namespace FactForge.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly QuizRepository _quizRepository;
    private readonly PresentationService _presentationService;
    private readonly AnswerService _answerService;
    private readonly PlayerSessionService _playerSessionService;
    private readonly QrCodeService _qrCodeService;
    private readonly QuizImportExportService _importExportService;
    private readonly IDialogService _dialogService;
    private readonly IWindowService _windowService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private ViewModelBase _currentPage;

    public MainWindowViewModel(
        QuizRepository quizRepository,
        PresentationService presentationService, 
        QrCodeService qrCodeService,
        QuizImportExportService importExportService,
        IDialogService dialogService, 
        IWindowService windowService,
        ISettingsService settingsService,
        AnswerService answerService,
        PlayerSessionService playerSessionService)
    {
        _quizRepository = quizRepository;
        _presentationService = presentationService;
        _answerService = answerService;
        _playerSessionService = playerSessionService;
        _qrCodeService = qrCodeService;
        _importExportService = importExportService;
        _dialogService = dialogService;
        _windowService = windowService;
        _settingsService = settingsService;
        _currentPage = new StartViewModel(NavigateToLibrary);
    }

    private QuizLibraryViewModel CreateLibraryPage() =>
        new(_quizRepository, _dialogService, _windowService, _importExportService, NavigateToEditor, NavigateToPresent, NavigateToResults, NavigateToSettings);

    public void NavigateToLibrary() => CurrentPage = CreateLibraryPage();

    public void NavigateToEditor(int quizId) =>
        CurrentPage = new EditorViewModel(_quizRepository, quizId, NavigateToLibrary, NavigateToPresent, _dialogService, _windowService, NavigateToSettings);

    public void NavigateToPresent(int quizId) =>
        CurrentPage = new PresentViewModel(_presentationService, _dialogService, _answerService, _playerSessionService, _qrCodeService, _quizRepository, quizId, NavigateToLibrary);

    public void NavigateToResults(int quizId) =>
        CurrentPage = new ResultsViewModel(_dialogService, _windowService, _quizRepository, quizId, NavigateToLibrary, NavigateToSettings);
    
    public void NavigateToSettings()
    {
        // Capture the page that was active prior to opening Settings
        var previousPage = CurrentPage;

        CurrentPage = new SettingsViewModel(
            _windowService, 
            _settingsService, 
            onBack: () => CurrentPage = previousPage
        );
    }
}

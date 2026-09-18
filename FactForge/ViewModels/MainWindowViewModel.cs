using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Services;

namespace FactForge.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly QuizRepository _quizRepository;
    private readonly PresentationService _presentationService;
    private readonly QrCodeService _qrCodeService;

    [ObservableProperty] private ViewModelBase _currentPage;

    public MainWindowViewModel(QuizRepository quizRepository, PresentationService presentationService, QrCodeService qrCodeService)
    {
        _quizRepository = quizRepository;
        _presentationService = presentationService;
        _qrCodeService = qrCodeService;
        _currentPage = new StartViewModel(NavigateToLibrary);
    }

    private QuizLibraryViewModel CreateLibraryPage() =>
        new(_quizRepository, NavigateToEditor, NavigateToPresent, NavigateToResults);

    public void NavigateToLibrary() => CurrentPage = CreateLibraryPage();

    public void NavigateToEditor(int quizId) =>
        CurrentPage = new EditorViewModel(_quizRepository, quizId, NavigateToLibrary, NavigateToPresent);

    public void NavigateToPresent(int quizId) =>
        CurrentPage = new PresentViewModel(_presentationService, _qrCodeService, _quizRepository, quizId, NavigateToLibrary);

    public void NavigateToResults(int quizId) =>
        CurrentPage = new ResultsViewModel(_quizRepository, quizId, NavigateToLibrary);
}

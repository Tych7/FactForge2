using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Data.Entities;
using FactForge.Models;
using FactForge.Services;

namespace FactForge.ViewModels;

public enum PresentPhase { Lobby, Question, Revealed, Leaderboard, TextSlide, Finished }

public sealed record AnswerOptionDisplay(string Text, IBrush Background);

public partial class PresentViewModel : ViewModelBase, IDisposable
{
    private readonly PresentationService _presentation;
    private readonly QrCodeService _qrCodeService;
    private readonly QuizRepository _quizRepository;
    private readonly int _quizId;
    private readonly Action _onBack;
    private readonly DispatcherTimer _countdownTimer;

    // Same per-position palette as the player-facing page's .choice-btn:nth-child rules,
    // so the host screen and players' phones always agree on which tile is which color.
    private static readonly IBrush[] AnswerColors =
    {
        new SolidColorBrush(Color.Parse("#D3FF0400")),
        new SolidColorBrush(Color.Parse("#1E88E5")),
        new SolidColorBrush(Color.Parse("#D5FFD621")),
        new SolidColorBrush(Color.Parse("#3CA101")),
    };
    private static readonly IBrush DimmedAnswerColor = new SolidColorBrush(Color.Parse("#333333"));

    private QuizSession? _session;

    [ObservableProperty] private string _quizTitle = string.Empty;
    [ObservableProperty] private string _joinUrl = string.Empty;
    [ObservableProperty] private string _joinCode = string.Empty;
    [ObservableProperty] private Bitmap? _qrImage;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private PresentPhase _phase = PresentPhase.Lobby;

    [ObservableProperty] private SlideDto? _currentSlide;
    [ObservableProperty] private int _slideNumber;
    [ObservableProperty] private int _totalSlides;
    [ObservableProperty] private double _secondsRemaining;
    [ObservableProperty] private int _answeredCount;
    [ObservableProperty] private RevealDto? _lastReveal;
    [ObservableProperty] private System.Collections.Generic.List<AnswerOptionDisplay>? _displayOptions;

    public ObservableCollection<PlayerInfoDto> Players { get; } = new();
    public ObservableCollection<LeaderboardEntryDto> Leaderboard { get; } = new();

    public PresentViewModel(PresentationService presentation, QrCodeService qrCodeService, QuizRepository quizRepository, int quizId, Action onBack)
    {
        _presentation = presentation;
        _qrCodeService = qrCodeService;
        _quizRepository = quizRepository;
        _quizId = quizId;
        _onBack = onBack;

        _presentation.PlayerJoined += OnPlayerJoined;
        _presentation.AnswerTallyChanged += OnAnswerTallyChanged;
        _presentation.SlideRevealedEvent += OnSlideRevealed;
        _presentation.LeaderboardUpdatedEvent += OnLeaderboardUpdated;
        _presentation.SessionEndedEvent += OnSessionEnded;

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _countdownTimer.Tick += (_, _) => TickCountdown();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var quiz = await _quizRepository.GetQuizWithSlidesAsync(_quizId);
        if (quiz is null) { _onBack(); return; }
        QuizTitle = quiz.Title;
        TotalSlides = quiz.Slides.Count;

        var (session, _) = await _presentation.StartSessionAsync(_quizId);
        _session = session;
        JoinCode = session.JoinCode;

        var ip = TryGetLocalIp();
        JoinUrl = $"http://{ip}:{LocalWebHost.Port}/?code={session.JoinCode}";
        QrImage = _qrCodeService.GenerateQrCode(JoinUrl);
    }

    private string TryGetLocalIp()
    {
        try { return _qrCodeService.GetLocalIpAddress(); }
        catch { return "localhost"; }
    }

    private void OnPlayerJoined(PlayerInfoDto player)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Players.Any(p => p.Id == player.Id)) return;
            Players.Add(player);
        });
    }

    private void OnAnswerTallyChanged(AnswerTallyDto tally)
    {
        Dispatcher.UIThread.Post(() => AnsweredCount = tally.Answered);
    }

    private void OnSlideRevealed(RevealDto reveal)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LastReveal = reveal;
            Phase = PresentPhase.Revealed;
            _countdownTimer.Stop();

            var correct = reveal.CorrectAnswer;
            if (CurrentSlide?.Options is { } options && correct is not null)
            {
                DisplayOptions = options
                    .Select((o, i) => new AnswerOptionDisplay(
                        o,
                        string.Equals(o.Trim(), correct.Trim(), StringComparison.OrdinalIgnoreCase)
                            ? AnswerColors[i % AnswerColors.Length]
                            : DimmedAnswerColor))
                    .ToList();
            }
        });
    }

    private void OnLeaderboardUpdated(System.Collections.Generic.List<LeaderboardEntryDto> leaderboard)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Leaderboard.Clear();
            foreach (var entry in leaderboard) Leaderboard.Add(entry);
        });
    }

    private void OnSessionEnded()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Phase = PresentPhase.Finished;
            _countdownTimer.Stop();
        });
    }

    private void TickCountdown()
    {
        if (CurrentSlide?.DeadlineUtc is not { } deadline) return;
        SecondsRemaining = Math.Max(0, (deadline - DateTime.UtcNow).TotalSeconds);
    }

    [RelayCommand]
    private async Task StartQuizAsync() => await AdvanceAsync();

    [RelayCommand(CanExecute = nameof(CanAdvance))]
    private async Task NextAsync()
    {
        if (Phase == PresentPhase.Question)
        {
            await _presentation.RevealCurrentSlideAsync();
            return;
        }
        await AdvanceAsync();
    }

    private bool CanAdvance() => Phase is PresentPhase.Question or PresentPhase.Revealed or PresentPhase.Leaderboard or PresentPhase.TextSlide;

    private async Task AdvanceAsync()
    {
        AnsweredCount = 0;
        LastReveal = null;
        var slide = await _presentation.NextSlideAsync();
        if (slide is null)
        {
            Phase = PresentPhase.Finished;
            await _presentation.EndSessionAsync();
            return;
        }

        CurrentSlide = slide;
        SlideNumber++;
        Phase = slide.Type switch
        {
            SlideType.Text => PresentPhase.TextSlide,
            SlideType.Leaderboard => PresentPhase.Leaderboard,
            _ => PresentPhase.Question
        };
        DisplayOptions = slide.Options?
            .Select((o, i) => new AnswerOptionDisplay(o, AnswerColors[i % AnswerColors.Length]))
            .ToList();

        if (slide.DeadlineUtc is not null)
        {
            SecondsRemaining = slide.TimeSeconds;
            _countdownTimer.Start();
        }
    }

    [RelayCommand]
    private async Task EndQuizAsync()
    {
        if (_presentation.HasActiveSession)
            await _presentation.EndSessionAsync();
        Phase = PresentPhase.Finished;
    }

    [RelayCommand]
    private void Back()
    {
        Dispose();
        _onBack();
    }

    public void Dispose()
    {
        _countdownTimer.Stop();
        _presentation.PlayerJoined -= OnPlayerJoined;
        _presentation.AnswerTallyChanged -= OnAnswerTallyChanged;
        _presentation.SlideRevealedEvent -= OnSlideRevealed;
        _presentation.LeaderboardUpdatedEvent -= OnLeaderboardUpdated;
        _presentation.SessionEndedEvent -= OnSessionEnded;
        if (_presentation.HasActiveSession)
            _ = _presentation.EndSessionAsync();
    }
}

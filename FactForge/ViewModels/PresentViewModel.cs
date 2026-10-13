using System;
using System.Collections.Concurrent;
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
using FactForge.Services.Presentation;


namespace FactForge.ViewModels;

public enum PresentPhase { Lobby, Question, Revealed, Leaderboard, TextSlide, Music, Finished }

public partial class PresentViewModel : ViewModelBase, IDisposable
{
    private readonly PresentationService _presentationService;
    private readonly AnswerService _answerService;
    private readonly PlayerSessionService _playerSessionService;
    private readonly QrCodeService _qrCodeService;
    private readonly QuizRepository _quizRepository;
    private readonly int _quizId;
    private readonly Action _onBack;
    private readonly DispatcherTimer _countdownTimer;

    // Thread-safe queue for incoming players to prevent UI thread flooding during massive joins
    private readonly ConcurrentQueue<PlayerInfoDto> _pendingPlayers = new();

    private static readonly IBrush[] AnswerColors = AnswerColorPalette.Colors;
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
    [ObservableProperty] private SlideDisplay? _slide;

    [ObservableProperty] private int _slideNumber;
    [ObservableProperty] private int _totalSlides;
    [ObservableProperty] private RevealDto? _lastReveal;

    public bool IsSlideVisible => Phase is PresentPhase.Question or PresentPhase.Revealed or PresentPhase.TextSlide or PresentPhase.Music;

    partial void OnPhaseChanged(PresentPhase value) => OnPropertyChanged(nameof(IsSlideVisible));

    public ObservableCollection<PlayerInfoDto> Players { get; } = new();
    public ObservableCollection<LeaderboardBarItem> Leaderboard { get; } = new();

    public PresentViewModel(PresentationService presentationService, AnswerService answerService, PlayerSessionService playerSessionService, QrCodeService qrCodeService, QuizRepository quizRepository, int quizId, Action onBack)
    {
        _presentationService = presentationService;
        _answerService = answerService;
        _playerSessionService = playerSessionService;
        _qrCodeService = qrCodeService;
        _quizRepository = quizRepository;
        _quizId = quizId;
        _onBack = onBack;

        _presentationService.SlideRevealedEvent += OnSlideRevealed;
        _presentationService.LeaderboardUpdatedEvent += OnLeaderboardUpdated;
        _presentationService.SessionEndedEvent += OnSessionEnded;

        _answerService.AnswerTallyChanged += OnAnswerTallyChanged;

        _playerSessionService.PlayerJoined += OnPlayerJoined;

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _countdownTimer.Tick += (_, _) => 
        {
            TickCountdown();
            FlushPendingPlayers();
        };
        
        // Start immediately so the lobby can process incoming players right away
        _countdownTimer.Start();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var quiz = await _quizRepository.GetQuizWithSlidesAsync(_quizId);

        if (quiz is null)
        {
            _onBack();
            return;
        }

        QuizTitle = quiz.Title;
        TotalSlides = quiz.Slides.Count;

        var (session, _) = await _presentationService.StartSessionAsync(_quizId);

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
        // Safely queue from background threads without locking the network thread
        _pendingPlayers.Enqueue(player);
    }

    private void FlushPendingPlayers()
    {
        if (_pendingPlayers.IsEmpty) return;

        Dispatcher.UIThread.Post(() =>
        {
            int addedCount = 0;
            // Process up to 50 players per tick to prevent UI locking during heavy join floods
            while (_pendingPlayers.TryDequeue(out var player) && addedCount < 50)
            {
                if (Players.Any(p => p.Id == player.Id)) continue;
                Players.Add(player);
                addedCount++;
            }
        });
    }

    private void OnAnswerTallyChanged(AnswerTallyDto tally)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Slide is { } slide) slide.StatusText = $"{tally.Answered} / {tally.TotalPlayers} answered";
        });
    }

    private async void OnSlideRevealed(RevealDto reveal)
    {
        await AudioPlaybackService.Shared.StopAsync();

        Dispatcher.UIThread.Post(() =>
        {
            LastReveal = reveal;
            Phase = PresentPhase.Revealed;

            if (Slide is not { } slide) return;

            var correct = reveal.CorrectAnswer;
            slide.CorrectAnswer = correct;

            if (CurrentSlide?.Type == SlideType.MusicQuestion)
            {
                slide.IsMusicRevealed = true;
            }

            if (CurrentSlide?.Options is { } options && correct is not null)
            {
                slide.Options = options
                    .Select((o, i) =>
                    {
                        var isCorrect = string.Equals(o.Trim(), correct.Trim(), StringComparison.OrdinalIgnoreCase);
                        return new AnswerOptionDisplay(
                            o,
                            isCorrect ? AnswerColors[i % AnswerColors.Length] : DimmedAnswerColor,
                            isCorrect);
                    })
                    .ToList();
            }
        });
    }

    private void OnLeaderboardUpdated(System.Collections.Generic.List<LeaderboardEntryDto> leaderboard)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Leaderboard.Clear();
            var maxScore = leaderboard.Count == 0 ? 0 : leaderboard.Max(e => e.Score);
            for (var i = 0; i < leaderboard.Count; i++)
                Leaderboard.Add(new LeaderboardBarItem(leaderboard[i], maxScore, i));
        });
    }

    private void OnSessionEnded()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Phase = PresentPhase.Finished;
        });
    }

    private async void TickCountdown()
    {
        if (Slide is null)
            return;

        if (CurrentSlide?.Type == SlideType.MusicQuestion)
        {
            Slide.MusicPositionSeconds = AudioPlaybackService.Shared.PositionSeconds;
            Slide.MusicDurationSeconds = AudioPlaybackService.Shared.DurationSeconds;

            if (Slide.MusicDurationSeconds > 0 &&
                Slide.MusicPositionSeconds >= Slide.MusicDurationSeconds - 0.05)
            {
                Slide.MusicPositionSeconds = Slide.MusicDurationSeconds;
            }
        }

        if (CurrentSlide?.DeadlineUtc is { } deadline)
        {
            Slide.SecondsRemaining = Math.Max(
                0,
                (deadline - DateTime.UtcNow).TotalSeconds);

            if (Slide.SecondsRemaining <= 0)
            {
                if (Phase == PresentPhase.Question)
                    await _presentationService.RevealCurrentSlideAsync();
            }
        }
    }

    [RelayCommand]
    private async Task StartQuizAsync() => await AdvanceAsync();

    [RelayCommand(CanExecute = nameof(CanAdvance))]
    private async Task NextAsync()
    {
        if (Phase == PresentPhase.Question ||
            Phase == PresentPhase.Music)
        {
            await _presentationService.RevealCurrentSlideAsync();
            return;
        }

        await AdvanceAsync();
    }

    private bool CanAdvance() => Phase is PresentPhase.Question or PresentPhase.Revealed or PresentPhase.Leaderboard or PresentPhase.TextSlide or PresentPhase.Music;

    private async Task AdvanceAsync()
    {
        LastReveal = null;

        await AudioPlaybackService.Shared.StopAsync();

        var slide = await _presentationService.NextSlideAsync();
        if (slide is null)
        {
            Phase = PresentPhase.Finished;
            await _presentationService.EndSessionAsync();
            return;
        }

        CurrentSlide = slide;
        SlideNumber++;

        var waveformPeaks =
            slide.Type == SlideType.MusicQuestion
                ? await AudioWaveformService.Shared.GetPeaksAsync(
                    slide.MusicFilePath)
                : Array.Empty<float>();

        Slide = new SlideDisplay
        {
            Type = slide.Type,
            Header = slide.Header,
            SubText = slide.SubText,
            Question = slide.Question,
            TimeSeconds = slide.TimeSeconds,
            SecondsRemaining = slide.TimeSeconds,
            MusicTitle = slide.Title,
            MusicArtist = slide.Artist,
            MusicDurationSeconds = 0,
            MusicPositionSeconds = 0,
            ReplayMusicCommand = ReplayMusicCommand,
            MusicWaveformPeaks = waveformPeaks,
            StatusText = slide.DeadlineUtc is not null ? "0/0 answered" : string.Empty,
            Image = SlideImageService.Shared.Load(slide.ImagePath),
            Options = slide.Options?
                .Select((o, i) => new AnswerOptionDisplay(
                    o,
                    AnswerColors[i % AnswerColors.Length]))
                .ToList()
        };

        Phase = slide.Type switch
        {
            SlideType.Text => PresentPhase.TextSlide,
            SlideType.Leaderboard => PresentPhase.Leaderboard,
            SlideType.MusicQuestion => PresentPhase.Music,
            _ => PresentPhase.Question
        };

        if (slide.Type == SlideType.MusicQuestion)
        {
            var path = SlideAudioService.Shared.GetPath(slide.MusicFilePath);

            if (path is not null)
                await AudioPlaybackService.Shared.PlayAsync(path);
        }
    }

    [RelayCommand]
    private async Task ReplayMusicAsync()
    {
        if (CurrentSlide?.Type != SlideType.MusicQuestion)
            return;

        var path = SlideAudioService.Shared.GetPath(CurrentSlide.MusicFilePath);

        if (path is null)
            return;

        Slide!.MusicPositionSeconds = 0;

        await AudioPlaybackService.Shared.PlayAsync(path);
    }

    [RelayCommand]
    private async Task EndQuizAsync()
    {
        if (_presentationService.HasActiveSession)
            await _presentationService.EndSessionAsync();
        
        await AudioPlaybackService.Shared.StopAsync();
        Phase = PresentPhase.Finished;
    }

    [RelayCommand]
    private async Task Back()
    {
        Dispose();
        await AudioPlaybackService.Shared.StopAsync();
        _onBack();
    }

    public void Dispose()
    {
        _countdownTimer.Stop();
        _playerSessionService.PlayerJoined -= OnPlayerJoined;
        _answerService.AnswerTallyChanged -= OnAnswerTallyChanged;
        _presentationService.SlideRevealedEvent -= OnSlideRevealed;
        _presentationService.LeaderboardUpdatedEvent -= OnLeaderboardUpdated;
        _presentationService.SessionEndedEvent -= OnSessionEnded;
        if (_presentationService.HasActiveSession)
            _ = _presentationService.EndSessionAsync();
    }
}
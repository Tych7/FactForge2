using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Data;
using FactForge.Data.Entities;
using FactForge.Models;
using FactForge.Services;
using FactForge.Services.Presentation;
using FactForge.Views.Dialogs;


namespace FactForge.ViewModels.Presentation;

public enum PresentPhase { Lobby, Question, Revealed, Leaderboard, TextSlide, Music, Finished }

public partial class PresentViewModel : ViewModelBase, IDisposable
{
    private readonly PresentationService _presentationService;
    private readonly AnswerService _answerService;
    private readonly PlayerSessionService _playerSessionService;
    private readonly IDialogService _dialogService;
    private readonly QrCodeService _qrCodeService;
    private readonly QuizRepository _quizRepository;
    private readonly int _quizId;
    private readonly Action _onBack;
    private readonly DispatcherTimer _countdownTimer;
    private static readonly IBrush[] AnswerColors = AnswerColorPalette.Colors;
    private static readonly IBrush DimmedAnswerColor = new SolidColorBrush(Color.Parse("#333333"));

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
    [ObservableProperty]
    private bool _isRejoinQrVisible;

    public bool IsSlideVisible => Phase is PresentPhase.Question or PresentPhase.Revealed or PresentPhase.TextSlide or PresentPhase.Music;

    partial void OnPhaseChanged(PresentPhase value) => OnPropertyChanged(nameof(IsSlideVisible));

    public ObservableCollection<PlayerInfoDto> Players { get; } = new();
    public ObservableCollection<LeaderboardBarItem> Leaderboard { get; } = new();

    public PresentViewModel(
        PresentationService presentationService,
        IDialogService dialogService,  
        AnswerService answerService, 
        PlayerSessionService playerSessionService, 
        QrCodeService qrCodeService, 
        QuizRepository quizRepository, 
        int quizId, 
        Action onBack)
    {
        _presentationService = presentationService;
        _dialogService = dialogService;
        _answerService = answerService;
        _playerSessionService = playerSessionService;
        _qrCodeService = qrCodeService;
        _quizRepository = quizRepository;
        _quizId = quizId;
        _onBack = onBack;

        _presentationService.SlideRevealedEvent += OnSlideRevealed;
        _presentationService.LeaderboardUpdatedEvent += OnLeaderboardUpdated;
        _presentationService.SessionEndedEvent += OnSessionEnded;
        _playerSessionService.PlayerDisconnected += OnPlayerDisconnected;

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

        _ = InitializeSessionAsync();
    }

    [RelayCommand]
    private async Task StartQuizAsync() => await AdvanceAsync();

    [RelayCommand]
    private async Task ShowRejoinQrAsync()
    {
        await _dialogService.ShowRejoinQrAsync(this);
    }

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
            await FinishQuizAsync();
            return;
        }

        CurrentSlide = slide;
        SlideNumber++;

        Slide = await CreateSlideDisplayAsync(slide);
        Phase = GetPhase(slide.Type);

        if (slide.Type == SlideType.MusicQuestion) await StartMusicAsync(slide);
    }

    private async Task FinishQuizAsync()
    {
        Phase = PresentPhase.Finished;
        await _presentationService.EndSessionAsync();
    }

    private async Task<SlideDisplay> CreateSlideDisplayAsync(SlideDto slide)
    {
        var waveformPeaks = await GetWaveformPeaksAsync(slide);

        var tally = _playerSessionService.GetCurrentAnswerTally();

        return new SlideDisplay
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
            StatusText = slide.DeadlineUtc is not null
                ? $"{tally.Answered} / {tally.TotalPlayers} answered"
                : string.Empty,
            Image = SlideImageService.Shared.Load(slide.ImagePath),
            Options = slide.Options?
                .Select((option, index) => new AnswerOptionDisplay(
                    option,
                    AnswerColors[index % AnswerColors.Length]))
                .ToList()
        };
    }

    private static async Task<IReadOnlyList<float>> GetWaveformPeaksAsync(SlideDto slide)
    {
        if (slide.Type != SlideType.MusicQuestion)
            return Array.Empty<float>();

        return await AudioWaveformService.Shared.GetPeaksAsync(
            slide.MusicFilePath);
    }

    private static PresentPhase GetPhase(SlideType type) => type switch
    {
        SlideType.Text => PresentPhase.TextSlide,
        SlideType.Leaderboard => PresentPhase.Leaderboard,
        SlideType.MusicQuestion => PresentPhase.Music,
        _ => PresentPhase.Question
    };

    private static async Task StartMusicAsync(SlideDto slide)
    {
        var path = SlideAudioService.Shared.GetPath(slide.MusicFilePath);

        if (path is not null)
            await AudioPlaybackService.Shared.PlayAsync(path);
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
        _playerSessionService.PlayerDisconnected -= OnPlayerDisconnected;
        _answerService.AnswerTallyChanged -= OnAnswerTallyChanged;
        _presentationService.SlideRevealedEvent -= OnSlideRevealed;
        _presentationService.LeaderboardUpdatedEvent -= OnLeaderboardUpdated;
        _presentationService.SessionEndedEvent -= OnSessionEnded;
        if (_presentationService.HasActiveSession)
            _ = _presentationService.EndSessionAsync();
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Services;

namespace FactForge.ViewModels;

/// <summary>Implemented by every slide editor view model that supports audio.</summary>
public interface IHasSlideAudio
{
    SlideAudioEditorViewModel Audio { get; }
}

/// <summary>Audio state and preview commands for one slide.</summary>
public partial class SlideAudioEditorViewModel : ViewModelBase
{
    private readonly AudioPlaybackService _previewPlayer = new();
    private readonly DispatcherTimer _positionTimer;
    private bool _previewPaused;
    private bool _disposed;

    [ObservableProperty]
    private string? _fileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAudio))]
    private string? _audioPath;

    [ObservableProperty]
    private IReadOnlyList<float> _waveformPeaks = Array.Empty<float>();

    [ObservableProperty]
    private double _positionSeconds;

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private bool _isPreviewPlaying;

    public bool HasAudio => !string.IsNullOrWhiteSpace(AudioPath);

    public bool IsPreviewPaused => _previewPaused;

    public string PreviewIcon =>
    IsPreviewPlaying
        ? "avares://FactForge/Assets/Pause.svg"
        : "avares://FactForge/Assets/Play.svg";

    public string PreviewTimeText =>
        $"{FormatTime(PositionSeconds)} / {FormatTime(DurationSeconds)}";

    public SlideAudioEditorViewModel(string? fileName)
    {
        _fileName = fileName;
        _audioPath = SlideAudioService.Shared.GetPath(fileName);

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _positionTimer.Tick += OnPositionTimerTick;

        if (HasAudio)
            _ = RefreshWaveformAsync();
    }

    [RelayCommand]
    private async Task ChooseAsync()
    {
        var name = await SlideAudioService.Shared.PickAndImportAsync();
        if (name is null)
            return;

        await StopPreviewAsync();

        FileName = name;
        AudioPath = SlideAudioService.Shared.GetPath(name);

        PositionSeconds = 0;
        DurationSeconds = 0;

        await RefreshWaveformAsync();
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        await StopPreviewAsync();

        FileName = null;
        AudioPath = null;
        WaveformPeaks = Array.Empty<float>();
        PositionSeconds = 0;
        DurationSeconds = 0;
    }

    [RelayCommand]
    private async Task TogglePreviewAsync()
    {
        if (!HasAudio || string.IsNullOrWhiteSpace(AudioPath))
            return;

        if (IsPreviewPlaying)
        {
            _previewPlayer.Pause();
            IsPreviewPlaying = false;
            _previewPaused = true;
            OnPropertyChanged(nameof(IsPreviewPaused));
            OnPropertyChanged(nameof(PreviewIcon));
            return;
        }

        if (_previewPaused)
        {
            _previewPlayer.Resume();
            _previewPaused = false;
            IsPreviewPlaying = true;
            _positionTimer.Start();
            OnPropertyChanged(nameof(IsPreviewPaused));
            OnPropertyChanged(nameof(PreviewIcon));
            return;
        }

        try
        {
            await _previewPlayer.PlayAsync(AudioPath);

            PositionSeconds = _previewPlayer.PositionSeconds;
            DurationSeconds = _previewPlayer.DurationSeconds;

            _previewPaused = false;
            IsPreviewPlaying = true;
            _positionTimer.Start();

            OnPropertyChanged(nameof(IsPreviewPaused));
            OnPropertyChanged(nameof(PreviewIcon));
        }
        catch
        {
            await StopPreviewAsync();
            throw;
        }
    }

    public async Task StopPreviewAsync()
    {
        _positionTimer.Stop();

        await _previewPlayer.StopAsync();

        IsPreviewPlaying = false;
        _previewPaused = false;
        PositionSeconds = 0;

        OnPropertyChanged(nameof(IsPreviewPaused));
        OnPropertyChanged(nameof(PreviewIcon));
    }

    private async Task RefreshWaveformAsync()
    {
        try
        {
            WaveformPeaks =
                await AudioWaveformService.Shared.GetPeaksAsync(FileName);
        }
        catch
        {
            // Waveform generation can fail independently of audio playback.
            WaveformPeaks = Array.Empty<float>();
        }
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        PositionSeconds = _previewPlayer.PositionSeconds;
        DurationSeconds = _previewPlayer.DurationSeconds;

        if (DurationSeconds > 0 && PositionSeconds >= DurationSeconds)
        {
            _positionTimer.Stop();
            IsPreviewPlaying = false;
            _previewPaused = false;

            OnPropertyChanged(nameof(IsPreviewPaused));
            OnPropertyChanged(nameof(PreviewIcon));
        }
    }

    private static string FormatTime(double seconds)
    {
        var totalSeconds = Math.Max(0, (int)seconds);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    partial void OnPositionSecondsChanged(double value) =>
        OnPropertyChanged(nameof(PreviewTimeText));

    partial void OnDurationSecondsChanged(double value) =>
        OnPropertyChanged(nameof(PreviewTimeText));

    partial void OnIsPreviewPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewIcon));
    }

    public async Task DisposePreviewAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _positionTimer.Stop();
        _positionTimer.Tick -= OnPositionTimerTick;

        await _previewPlayer.DisposeAsync();
    }
}

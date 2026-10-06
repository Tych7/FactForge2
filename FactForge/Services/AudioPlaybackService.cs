using System;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using System.Diagnostics;

namespace FactForge.Services;

/// <summary>
/// Handles audio playback for the presenter.
/// File management is handled separately by SlideAudioService.
/// </summary>
public sealed class AudioPlaybackService : IDisposable
{
    public static AudioPlaybackService Shared { get; } = new();

    private readonly LibVLC _libVlc;
    private MediaPlayer? _mediaPlayer;
    private readonly Stopwatch _playbackClock = new();

    private AudioPlaybackService()
    {
        Core.Initialize();
        _libVlc = new LibVLC();
    }

    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;

    public bool IsPaused =>
        _mediaPlayer is not null && !_mediaPlayer.IsPlaying && !_playbackClock.IsRunning;

    public double PositionSeconds =>
        _mediaPlayer is null
            ? 0
            : Math.Min(
                _playbackClock.Elapsed.TotalSeconds,
                DurationSeconds);

    public double DurationSeconds =>
        _mediaPlayer?.Length is long length
            ? length / 1000.0
            : 0;

    public async Task PlayAsync(string filePath)
    {
        await StopAsync();

        var mediaPlayer = await Task.Run(() =>
        {
            var media = new Media(_libVlc, filePath, FromType.FromPath);
            var player = new MediaPlayer(media);

            player.Play();
            media.Dispose();

            return player;
        });

        _mediaPlayer = mediaPlayer;
        _playbackClock.Restart();
        _isPlaying = true;

        mediaPlayer.EndReached += OnEndReached;
    }

    public void Pause()
    {
        if (_mediaPlayer is null || !_isPlaying)
            return;

        _mediaPlayer.Pause();
        _playbackClock.Stop();
        _isPlaying = false;
    }

    public void Resume()
    {
        if (_mediaPlayer is null || _isPlaying)
            return;

        _mediaPlayer.Play();
        _playbackClock.Start();
        _isPlaying = true;
    }

    public async Task ReplayAsync()
    {
        if (_mediaPlayer is null)
            return;

        await Task.Run(() =>
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Play();
        });

        _playbackClock.Restart();
        _isPlaying = true;
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        _playbackClock.Stop();
        _isPlaying = false;
    }

    public async Task StopAsync()
    {
        var mediaPlayer = _mediaPlayer;
        _mediaPlayer = null;

        _playbackClock.Stop();
        _playbackClock.Reset();
        _isPlaying = false;

        if (mediaPlayer is null)
            return;

        mediaPlayer.EndReached -= OnEndReached;

        await Task.Run(() =>
        {
            mediaPlayer.Stop();
            mediaPlayer.Dispose();
        });
    }

    public async Task DisposeAsync()
    {
        await StopAsync();
        _libVlc.Dispose();
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _libVlc.Dispose();
    }
}
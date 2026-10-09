
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

namespace FactForge.Services;

/// <summary>
/// Handles audio playback for the presenter.
/// File management is handled separately by SlideAudioService.
/// </summary>
public sealed class AudioPlaybackService : IDisposable
{
    public static AudioPlaybackService Shared { get; } = new();

    private readonly LibVLC _libVlc;
    private readonly SemaphoreSlim _playLock = new(1, 1);
    private readonly Stopwatch _playbackClock = new();

    private MediaPlayer? _mediaPlayer;
    private bool _isPlaying;
    private bool _disposed;

    private AudioPlaybackService()
    {
        Core.Initialize();
        _libVlc = new LibVLC();
    }

    public bool IsPlaying => _isPlaying;

    public bool IsPaused =>
        _mediaPlayer is not null &&
        !_mediaPlayer.IsPlaying &&
        !_isPlaying;

    public double PositionSeconds =>
        _playbackClock.Elapsed.TotalSeconds;

    public double DurationSeconds =>
        _mediaPlayer?.Length is long length && length > 0
            ? length / 1000.0
            : 0;

    /// <summary>
    /// Creates the native player before it is needed.
    /// Call this while entering the lobby, not when the music slide appears.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _playLock.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_mediaPlayer is not null)
                return;

            await Task.Run(() =>
            {
                _mediaPlayer = new MediaPlayer(_libVlc);
                _mediaPlayer.EndReached += OnEndReached;
            });
        }
        finally
        {
            _playLock.Release();
        }
    }

    public async Task PlayAsync(string filePath)
    {
        await _playLock.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_mediaPlayer is null)
            {
                _mediaPlayer = new MediaPlayer(_libVlc);
                _mediaPlayer.EndReached += OnEndReached;
            }

            var player = _mediaPlayer;

            await Task.Run(() =>
            {
                player.Stop();

                using var media = new Media(
                    _libVlc,
                    filePath,
                    FromType.FromPath);

                player.Media = media;

                // Starts playback asynchronously in LibVLC.
                player.Play();
            });

            _playbackClock.Restart();
            _isPlaying = true;
        }
        finally
        {
            _playLock.Release();
        }
    }

    public void Pause()
    {
        var player = _mediaPlayer;

        if (player is null || !_isPlaying)
            return;

        player.Pause();
        _playbackClock.Stop();
        _isPlaying = false;
    }

    public void Resume()
    {
        var player = _mediaPlayer;

        if (player is null || _isPlaying)
            return;

        player.Play();
        _playbackClock.Start();
        _isPlaying = true;
    }

    public async Task ReplayAsync()
    {
        await _playLock.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_mediaPlayer?.Media is null)
                return;

            var player = _mediaPlayer;

            await Task.Run(() =>
            {
                player.Stop();
                player.Time = 0;
                player.Play();
            });

            _playbackClock.Restart();
            _isPlaying = true;
        }
        finally
        {
            _playLock.Release();
        }
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        _playbackClock.Stop();
        _isPlaying = false;
    }

    public async Task StopAsync()
    {
        await _playLock.WaitAsync();

        try
        {
            var player = _mediaPlayer;

            _playbackClock.Stop();
            _playbackClock.Reset();
            _isPlaying = false;

            if (player is null)
                return;

            await Task.Run(() => player.Stop());
        }
        finally
        {
            _playLock.Release();
        }
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
            return;

        await _playLock.WaitAsync();

        try
        {
            _disposed = true;

            var player = _mediaPlayer;
            _mediaPlayer = null;

            if (player is not null)
            {
                player.EndReached -= OnEndReached;
                await Task.Run(() =>
                {
                    player.Stop();
                    player.Dispose();
                });
            }

            _libVlc.Dispose();
        }
        finally
        {
            _playLock.Release();
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        _playLock.Dispose();
    }
}
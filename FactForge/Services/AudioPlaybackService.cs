using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetCoreAudio;

namespace FactForge.Services;

/// <summary>
/// Handles audio playback for the presenter.
/// File management is handled separately by SlideAudioService.
/// </summary>
public sealed class AudioPlaybackService : IDisposable
{
    public static AudioPlaybackService Shared { get; } = new();

    private readonly SemaphoreSlim _playLock = new(1, 1);
    private readonly Stopwatch _playbackClock = new();

    private Player? _player;
    private bool _disposed;
    private double _durationSeconds;
    private string? _currentFilePath;

    public bool IsPlaying => _player?.Playing ?? false;

    public bool IsPaused => _player?.Paused ?? false;
    

    // NetCoreAudio does not provide a reliable playback position
    // or duration across all supported player backends.
    public double PositionSeconds => _playbackClock.Elapsed.TotalSeconds;

    public double DurationSeconds => _durationSeconds;

    /// <summary>
    /// Creates the audio player before it is needed.
    /// Call this while entering the lobby.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _playLock.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _player ??= new Player();
        }
        finally
        {
            _playLock.Release();
        }
    }

    private async Task SetDurationSeconds(string filePath)
    {
        _durationSeconds = await Task.Run(() =>
            {
                var start = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                start.ArgumentList.Add("-v");
                start.ArgumentList.Add("error");
                start.ArgumentList.Add("-show_entries");
                start.ArgumentList.Add("format=duration");
                start.ArgumentList.Add("-of");
                start.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
                start.ArgumentList.Add(filePath);

                using var process = Process.Start(start)
                    ?? throw new InvalidOperationException("Could not start FFprobe.");

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return double.TryParse(
                    output.Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var duration)
                    ? duration
                    : 0;
            });
    }

    public async Task PlayAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "The audio file could not be found.",
                filePath);
        }

        await SetDurationSeconds(filePath);

        await InitializeAsync();
        await _playLock.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var player = _player
                ?? throw new InvalidOperationException(
                    "The audio player has not been initialized.");

            await player.Stop();
            await player.Play(filePath);

            _currentFilePath = filePath;
            _playbackClock.Restart();
        }
        finally
        {
            _playLock.Release();
        }
    }

    public async Task PauseAsync()
    {
        await _playLock.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_player is null || !_player.Playing || _player.Paused)
                return;

            await _player.Pause();

            _playbackClock.Stop();
        }
        finally
        {
            _playLock.Release();
        }
    }

    public async Task ResumeAsync()
    {
        await _playLock.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_player is null || !_player.Paused)
                return;

            await _player.Resume();

            _playbackClock.Start();
        }
        finally
        {
            _playLock.Release();
        }
    }

    public async Task ReplayAsync()
    {
        await _playLock.WaitAsync();

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var player = _player;

            if (player is null)
                return;

            // Stop and start again using the current track.
            await player.Stop();

            // NetCoreAudio requires the file path for playback.
            // Keep the current path to make replay possible.
            if (string.IsNullOrWhiteSpace(_currentFilePath))
                return;

            await player.Play(_currentFilePath);

            _playbackClock.Restart();
        }
        finally
        {
            _playLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _playLock.WaitAsync();

        try
        {
            if (_player is not null)
                await _player.Stop();

            _playbackClock.Reset();
            _currentFilePath = null;
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
            if (_disposed)
                return;

            _disposed = true;

            if (_player is not null)
            {
                await _player.Stop();
                _player = null;
            }

            _playbackClock.Reset();
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

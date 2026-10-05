using System;
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
    private MediaPlayer? _mediaPlayer;

    private AudioPlaybackService()
    {
        Core.Initialize();
        _libVlc = new LibVLC();
    }

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
    }

    public async Task StopAsync()
    {
        var mediaPlayer = _mediaPlayer;
        _mediaPlayer = null;

        if (mediaPlayer is null)
            return;

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
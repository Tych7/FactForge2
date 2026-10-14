using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using FactForge.Data;
using FactForge.Models;
using FactForge.Services;
using FactForge.Services.Presentation;

namespace FactForge.ViewModels.Presentation;

public partial class PresentViewModel
{
    private async void TickCountdown()
    {
        if (Slide is null || Phase == PresentPhase.Revealed)
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
}
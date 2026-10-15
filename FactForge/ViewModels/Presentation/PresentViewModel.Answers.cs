using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using FactForge.Data;
using FactForge.Models;
using FactForge.Services;
using FactForge.Services.Presentation;

namespace FactForge.ViewModels.Presentation;

//Answers

public partial class PresentViewModel
{
    private void OnAnswerTallyChanged(AnswerTallyDto tally)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Slide is { } slide)
                slide.StatusText = $"{tally.Answered} / {tally.TotalPlayers} answered";

            if (tally.TotalPlayers > 0 &&
                tally.Answered >= tally.TotalPlayers &&
                Phase is PresentPhase.Question or PresentPhase.Music)
            {
                _ = _presentationService.RevealCurrentSlideAsync();
            }
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
}
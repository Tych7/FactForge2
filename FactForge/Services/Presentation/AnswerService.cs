using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FactForge.Data;
using FactForge.Data.Entities;
using FactForge.Hubs;
using FactForge.Models;
using FactForge.Services.Presentation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FactForge.Services;

public class AnswerService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IHubContext<QuizHub> _hub;
    private readonly PresentationSessionState _state;

    public event Action<AnswerTallyDto>? AnswerTallyChanged;

    public AnswerService(
        IDbContextFactory<AppDbContext> contextFactory,
        IHubContext<QuizHub> hub,
        PresentationSessionState state)
    {
        _contextFactory = contextFactory;
        _hub = hub;
        _state = state;
    }

    public async Task<SubmitAnswerResultDto> SubmitAnswerAsync(
        string connectionId,
        string answerText)
    {
        await _state.Gate.WaitAsync();

        ActiveSession active;
        Slide slide;
        Player player;
        double elapsedMs;

        try
        {
            active = _state.Active
                ?? throw new InvalidOperationException("No active session.");

            if (!active.ConnectionToPlayerId.TryGetValue(connectionId, out var playerId))
                return new SubmitAnswerResultDto(false, false, 0);

            if (active.Revealed || active.CurrentSlidePosition < 0)
                return new SubmitAnswerResultDto(false, false, 0);

            if (!active.AnsweredPlayerIds.Add(playerId))
                return new SubmitAnswerResultDto(false, false, 0);

            slide = active.Slides[active.CurrentSlidePosition];
            player = active.PlayersById[playerId];

            var deadline = active.CurrentSlideDeadlineUtc ?? DateTime.UtcNow;
            var totalMs = slide.TimeSeconds * 1000.0;

            elapsedMs = Math.Clamp(
                totalMs - (deadline - DateTime.UtcNow).TotalMilliseconds,
                0,
                totalMs);
        }
        finally
        {
            _state.Gate.Release();
        }

        var isCorrect = IsAnswerCorrect(slide, answerText);

        var remainingFraction = Math.Clamp(
            1.0 - elapsedMs / (slide.TimeSeconds * 1000.0),
            0,
            1);

        var points = isCorrect
            ? (int)Math.Round(500 + 500 * remainingFraction)
            : 0;

        await using var db = await _contextFactory.CreateDbContextAsync();

        db.PlayerAnswers.Add(new PlayerAnswer
        {
            PlayerId = player.Id,
            SlideId = slide.Id,
            AnswerText = answerText,
            IsCorrect = isCorrect,
            AnswerMs = (int)elapsedMs,
            PointsAwarded = points,
            SubmittedAt = DateTime.UtcNow
        });

        var dbPlayer = await db.Players.FindAsync(player.Id);

        if (dbPlayer is not null)
        {
            dbPlayer.Score += points;
            player.Score = dbPlayer.Score;
        }

        await db.SaveChangesAsync();

        var tally = new AnswerTallyDto(
            active.AnsweredPlayerIds.Count,
            active.PlayersById.Count);

        await _hub.Clients.All.SendAsync("AnswerTally", tally);
        AnswerTallyChanged?.Invoke(tally);

        return new SubmitAnswerResultDto(true, isCorrect, points);
    }

    public async Task<SubmitAnswerResultDto> SubmitMusicAnswerAsync(
        string connectionId,
        string artist,
        string title)
    {
        await _state.Gate.WaitAsync();

        ActiveSession active;
        Slide slide;
        Player player;
        double elapsedMs;

        try
        {
            active = _state.Active
                ?? throw new InvalidOperationException("No active session.");

            if (!active.ConnectionToPlayerId.TryGetValue(connectionId, out var playerId))
                return new SubmitAnswerResultDto(false, false, 0);

            if (active.Revealed || active.CurrentSlidePosition < 0)
                return new SubmitAnswerResultDto(false, false, 0);

            if (!active.AnsweredPlayerIds.Add(playerId))
                return new SubmitAnswerResultDto(false, false, 0);

            slide = active.Slides[active.CurrentSlidePosition];
            player = active.PlayersById[playerId];

            if (slide.Type != SlideType.MusicQuestion)
                return new SubmitAnswerResultDto(false, false, 0);

            var deadline = active.CurrentSlideDeadlineUtc ?? DateTime.UtcNow;
            var totalMs = slide.TimeSeconds * 1000.0;

            elapsedMs = Math.Clamp(
                totalMs - (deadline - DateTime.UtcNow).TotalMilliseconds,
                0,
                totalMs);
        }
        finally
        {
            _state.Gate.Release();
        }

        var artistCorrect = slide.Artist?
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Any(a => NormalizeAnswer(artist).Contains(NormalizeAnswer(a)))
            ?? false;

        var titleCorrect =
            NormalizeAnswer(slide.Title) == NormalizeAnswer(title);

        var remainingFraction = Math.Clamp(
            1.0 - elapsedMs / (slide.TimeSeconds * 1000.0),
            0,
            1);

        var maxPoints = 500 + 500 * remainingFraction;

        var points = (int)Math.Round(
            maxPoints *
            ((artistCorrect ? 0.5 : 0.0) +
             (titleCorrect ? 0.5 : 0.0)));

        var isCorrect = artistCorrect && titleCorrect;

        await using var db = await _contextFactory.CreateDbContextAsync();

        db.PlayerAnswers.Add(new PlayerAnswer
        {
            PlayerId = player.Id,
            SlideId = slide.Id,
            AnswerText = $"Artist: {artist} | Title: {title}",
            IsCorrect = isCorrect,
            AnswerMs = (int)elapsedMs,
            PointsAwarded = points,
            SubmittedAt = DateTime.UtcNow
        });

        var dbPlayer = await db.Players.FindAsync(player.Id);

        if (dbPlayer is not null)
        {
            dbPlayer.Score += points;
            player.Score = dbPlayer.Score;
        }

        await db.SaveChangesAsync();

        var tally = new AnswerTallyDto(
            active.AnsweredPlayerIds.Count,
            active.PlayersById.Count);

        await _hub.Clients.All.SendAsync("AnswerTally", tally);
        AnswerTallyChanged?.Invoke(tally);

        return new SubmitAnswerResultDto(
            true, isCorrect, points, artistCorrect, titleCorrect);
    }

    private static bool IsAnswerCorrect(Slide slide, string answerText) =>
        slide.Type switch
        {
            SlideType.MultipleChoice => slide.Options.Any(o =>
                o.IsCorrect &&
                NormalizeAnswer(o.Text) == NormalizeAnswer(answerText)),

            SlideType.OpenQuestion =>
                IsOpenQuestionCorrect(slide.CorrectAnswer, answerText),

            _ => false
        };

    private static bool IsOpenQuestionCorrect(
        string? correctAnswer,
        string? playerAnswer)
    {
        if (string.IsNullOrWhiteSpace(correctAnswer) ||
            string.IsNullOrWhiteSpace(playerAnswer))
        {
            return false;
        }

        var normCorrect = NormalizeAnswer(correctAnswer);
        var normPlayer = NormalizeAnswer(playerAnswer);

        if (normCorrect == normPlayer)
            return true;

        if (double.TryParse(correctAnswer, out var correctNum) &&
            double.TryParse(playerAnswer, out var playerNum))
        {
            if (Math.Abs(correctNum - playerNum) < 0.0001)
                return true;
        }

        var correctTokens = GetSignificantTokens(correctAnswer);
        var playerTokens = GetSignificantTokens(playerAnswer);

        return correctTokens.Count > 0 &&
               correctTokens.All(playerTokens.Contains);
    }

    private static HashSet<string> GetSignificantTokens(string text)
    {
        var stopWords = new HashSet<string>
        {
            "in", "de", "het", "a", "an", "the", "van", "op", "om"
        };

        return text
            .Split(new[] { ' ', '-', '_', '.', ',' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeAnswer)
            .Where(t => !string.IsNullOrEmpty(t) && !stopWords.Contains(t))
            .ToHashSet();
    }

    private static string NormalizeAnswer(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return string.Empty;

        return new string(answer
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();
    }
}
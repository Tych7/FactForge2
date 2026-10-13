using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FactForge.Data;
using FactForge.Data.Entities;
using FactForge.Hubs;
using FactForge.Models;
using FactForge.Services.Presentation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FactForge.Services;

// Owns the single live quiz session: current slide, joined players, the
// server-authoritative countdown, scoring, and broadcasting state to every
// connected player + the presenter over SignalR. Only one session runs at a time,
// matching a single desktop app presenting to one room.
public class PresentationService : IAsyncDisposable
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly QuizRepository _quizRepository;
    private readonly IHubContext<QuizHub> _hub;
    private readonly PresentationSessionState _state;
    private readonly PlayerSessionService _playerSessionService;

    public event Action<PlayerInfoDto>? PlayerJoined;
    public event Action<AnswerTallyDto>? AnswerTallyChanged;
    public event Action<RevealDto>? SlideRevealedEvent;
    public event Action<List<LeaderboardEntryDto>>? LeaderboardUpdatedEvent;
    public event Action? SessionEndedEvent;

    
    public PresentationService(
        IDbContextFactory<AppDbContext> contextFactory,
        QuizRepository quizRepository,
        IHubContext<QuizHub> hub,
        PresentationSessionState state,
        PlayerSessionService playerSessionService)
    {
        _contextFactory = contextFactory;
        _quizRepository = quizRepository;
        _hub = hub;

        _state = state;
        _playerSessionService = playerSessionService;

        _playerSessionService.PlayerJoined += dto =>
            PlayerJoined?.Invoke(dto);
    }


    public bool HasActiveSession => _state.Active is not null;

    public async Task<(QuizSession session, List<Slide> slides)> StartSessionAsync(int quizId)
    {
        await _state.Gate.WaitAsync();
        try
        {
            var quiz = await _quizRepository.GetQuizWithSlidesAsync(quizId)
                ?? throw new InvalidOperationException("Quiz not found.");

            await using var db = await _contextFactory.CreateDbContextAsync();
            var session = new QuizSession
            {
                QuizId = quizId,
                JoinCode = GenerateJoinCode(),
                StartedAt = DateTime.UtcNow
            };
            db.QuizSessions.Add(session);
            await db.SaveChangesAsync();

            _state.Active?.DeadlineTimer?.Dispose();
            _state.Active = new ActiveSession { Session = session, Slides = quiz.Slides };

            foreach (var slide in quiz.Slides)
            {
                Console.WriteLine(
                    $"SESSION Slide {slide.Id}: " +
                    $"Type={slide.Type}, " +
                    $"MusicFilePath='{slide.MusicFilePath}', " +
                    $"Title='{slide.Title}', " +
                    $"Artist='{slide.Artist}'");
            }

            return (session, quiz.Slides);
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task<SlideDto?> NextSlideAsync()
    {
        await _state.Gate.WaitAsync();
        ActiveSession active;
        int position;
        try
        {
            active = _state.Active ?? throw new InvalidOperationException("No active session.");
            active.DeadlineTimer?.Dispose();
            active.DeadlineTimer = null;
            active.AnsweredPlayerIds.Clear();
            active.Revealed = false;
            active.CurrentSlidePosition++;
            position = active.CurrentSlidePosition;

            if (position >= active.Slides.Count)
            {
                active.CurrentSlidePosition = active.Slides.Count - 1;
                return null;
            }
        }
        finally
        {
            _state.Gate.Release();
        }

        var slide = active.Slides[position];
        DateTime? deadline = null;

        if (slide.Type is not SlideType.Text and not SlideType.Leaderboard)
        {
            deadline = DateTime.UtcNow.AddSeconds(slide.TimeSeconds);
            active.CurrentSlideDeadlineUtc = deadline;
            active.DeadlineTimer = new Timer(_ => _ = RevealCurrentSlideAsync(), null,
                TimeSpan.FromSeconds(slide.TimeSeconds), Timeout.InfiniteTimeSpan);
        }

        await using (var db = await _contextFactory.CreateDbContextAsync())
        {
            var session = await db.QuizSessions.FindAsync(active.Session.Id);
            if (session is not null)
            {
                session.CurrentSlideId = slide.Id;
                session.CurrentSlideDeadlineUtc = deadline;
                await db.SaveChangesAsync();
            }
        }

        var dto = new SlideDto(
            slide.Id,
            slide.Type,
            slide.Header,
            slide.SubText,
            slide.Question,
            slide.Type == SlideType.MultipleChoice
                ? slide.Options.Select(o => o.Text).ToList()
                : null,
            slide.TimeSeconds,
            deadline,
            slide.MusicFilePath,
            slide.Title,
            slide.Artist,
            slide.ImagePath);

        await _hub.Clients.All.SendAsync("SlideStarted", dto);

        if (slide.Type == SlideType.Leaderboard)
            await BroadcastLeaderboardAsync();

        return dto;
    }

    public async Task<SubmitAnswerResultDto> SubmitAnswerAsync(string connectionId, string answerText)
    {
        await _state.Gate.WaitAsync();
        ActiveSession active;
        Slide slide;
        Player player;
        double elapsedMs;
        try
        {
            active = _state.Active ?? throw new InvalidOperationException("No active session.");
            if (!active.ConnectionToPlayerId.TryGetValue(connectionId, out var playerId))
                return new SubmitAnswerResultDto(false, false, 0);
            if (active.Revealed || active.CurrentSlidePosition < 0)
                return new SubmitAnswerResultDto(false, false, 0);
            if (!active.AnsweredPlayerIds.Add(playerId))
                return new SubmitAnswerResultDto(false, false, 0); // already answered this slide

            slide = active.Slides[active.CurrentSlidePosition];
            player = active.PlayersById[playerId];
            var deadline = active.CurrentSlideDeadlineUtc ?? DateTime.UtcNow;
            var totalMs = slide.TimeSeconds * 1000.0;
            elapsedMs = Math.Clamp(totalMs - (deadline - DateTime.UtcNow).TotalMilliseconds, 0, totalMs);
        }
        finally
        {
            _state.Gate.Release();
        }

        var isCorrect = IsAnswerCorrect(slide, answerText);
        var remainingFraction = Math.Clamp(1.0 - elapsedMs / (slide.TimeSeconds * 1000.0), 0, 1);
        var points = isCorrect ? (int)Math.Round(500 + 500 * remainingFraction) : 0;

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

        var tally = new AnswerTallyDto(active.AnsweredPlayerIds.Count, active.PlayersById.Count);
        await _hub.Clients.All.SendAsync("AnswerTally", tally);
        AnswerTallyChanged?.Invoke(tally);
        
        // PASS isCorrect TO THE CLIENT INSTEAD OF FALSE
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
            active = _state.Active ?? throw new InvalidOperationException("No active session.");

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

        return new SubmitAnswerResultDto(true, isCorrect, points, artistCorrect, titleCorrect);
    }

    public async Task<RevealDto?> RevealCurrentSlideAsync()
    {
        await _state.Gate.WaitAsync();
        ActiveSession active;
        Slide slide;
        try
        {
            active = _state.Active ?? throw new InvalidOperationException("No active session.");
            if (active.Revealed || active.CurrentSlidePosition < 0) return null;
            active.Revealed = true;
            active.DeadlineTimer?.Dispose();
            active.DeadlineTimer = null;
            slide = active.Slides[active.CurrentSlidePosition];
        }
        finally
        {
            _state.Gate.Release();
        }

        if (slide.Type == SlideType.Text) return null;

        await using var db = await _contextFactory.CreateDbContextAsync();
        var answers = await db.PlayerAnswers.Where(a => a.SlideId == slide.Id).ToListAsync();

        List<OptionTallyDto>? optionTallies = null;
        if (slide.Type == SlideType.MultipleChoice)
        {
            optionTallies = slide.Options
                .Select(o => new OptionTallyDto(
                    o.Text,
                    answers.Count(a => string.Equals(a.AnswerText, o.Text, StringComparison.OrdinalIgnoreCase)),
                    o.IsCorrect))
                .ToList();
        }

        // Slide.CorrectAnswer only holds text for open questions; for multiple choice
        // the correct answer lives on the option, not the slide.
        var correctAnswerText = slide.Type == SlideType.MultipleChoice
            ? slide.Options.FirstOrDefault(o => o.IsCorrect)?.Text
            : slide.CorrectAnswer;

        var reveal = new RevealDto(slide.Id, correctAnswerText, optionTallies);
        await _hub.Clients.All.SendAsync("SlideRevealed", reveal);

        foreach (var player in active.PlayersById.Values)
        {
            if (player.ConnectionId is null) continue;

            var points = answers
                .Where(a => a.PlayerId == player.Id)
                .Select(a => a.PointsAwarded)
                .FirstOrDefault();

            await _hub.Clients.Client(player.ConnectionId)
                .SendAsync("QuestionPoints", points);
        }

        SlideRevealedEvent?.Invoke(reveal);
        await BroadcastLeaderboardAsync();
        return reveal;
    }

    public async Task BroadcastLeaderboardAsync()
    {
        var leaderboard = _playerSessionService.GetJoinedPlayers()
            .OrderByDescending(p => p.Score)
            .Select(p => new LeaderboardEntryDto(p.Name, p.Score))
            .ToList();
        await _hub.Clients.All.SendAsync("LeaderboardUpdated", leaderboard);
        LeaderboardUpdatedEvent?.Invoke(leaderboard);
    }

    public async Task EndSessionAsync()
    {
        await _state.Gate.WaitAsync();
        ActiveSession active;
        try
        {
            active = _state.Active ?? throw new InvalidOperationException("No active session.");
            active.DeadlineTimer?.Dispose();
        }
        finally
        {
            _state.Gate.Release();
        }

        await using (var db = await _contextFactory.CreateDbContextAsync())
        {
            var session = await db.QuizSessions.FindAsync(active.Session.Id);
            if (session is not null)
            {
                session.EndedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        var leaderboard = _playerSessionService.GetJoinedPlayers().Select(p => new LeaderboardEntryDto(p.Name, p.Score)).ToList();
        await _hub.Clients.All.SendAsync("SessionEnded", leaderboard);
        SessionEndedEvent?.Invoke();
        _state.Active = null;
    }
    
    private static bool IsAnswerCorrect(Slide slide, string answerText) => slide.Type switch
    {
        SlideType.MultipleChoice => slide.Options.Any(o =>
            o.IsCorrect &&
            NormalizeAnswer(o.Text) == NormalizeAnswer(answerText)),

        SlideType.OpenQuestion => IsOpenQuestionCorrect(slide.CorrectAnswer, answerText),

        _ => false
    };

    private static bool IsOpenQuestionCorrect(string? correctAnswer, string? playerAnswer)
    {
        if (string.IsNullOrWhiteSpace(correctAnswer) || string.IsNullOrWhiteSpace(playerAnswer))
            return false;

        var normCorrect = NormalizeAnswer(correctAnswer);
        var normPlayer = NormalizeAnswer(playerAnswer);

        // 1. Direct normalized match
        if (normCorrect == normPlayer)
            return true;

        // 2. Numeric comparison (handles "1945" vs "1945")
        if (double.TryParse(correctAnswer, out var correctNum) && double.TryParse(playerAnswer, out var playerNum))
        {
            if (Math.Abs(correctNum - playerNum) < 0.0001)
                return true;
        }

        // 3. Token check: ensure all significant words/numbers in the correct answer appear in the player's answer
        var correctTokens = GetSignificantTokens(correctAnswer);
        var playerTokens = GetSignificantTokens(playerAnswer);

        if (correctTokens.Count > 0 && correctTokens.All(t => playerTokens.Contains(t)))
        {
            return true;
        }

        return false;
    }

    private static HashSet<string> GetSignificantTokens(string text)
    {
        // Common stop words in English and Dutch that players might prefix an answer with
        var stopWords = new HashSet<string> { "in", "de", "het", "a", "an", "the", "van", "op", "om" };
        
        return text
            .Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries)
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

    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no O/0/I/1 confusion
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 5).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    public ValueTask DisposeAsync()
    {
        _state.Active?.DeadlineTimer?.Dispose();
        _state.Gate.Dispose();
        return ValueTask.CompletedTask;
    }

    
}

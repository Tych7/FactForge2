using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FactForge.Data;
using FactForge.Data.Entities;
using FactForge.Hubs;
using FactForge.Models;
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
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ActiveSession? _active;

    // In-process notifications for the presenter's own UI (which is not a SignalR
    // client, just another consumer running in the same app), raised alongside the
    // matching SignalR broadcast to phones. Handlers may run on a background
    // thread (hub calls) so subscribers must marshal to the UI thread themselves.
    public event Action<PlayerInfoDto>? PlayerJoined;
    public event Action<AnswerTallyDto>? AnswerTallyChanged;
    public event Action<RevealDto>? SlideRevealedEvent;
    public event Action<List<LeaderboardEntryDto>>? LeaderboardUpdatedEvent;
    public event Action? SessionEndedEvent;

    public PresentationService(
        IDbContextFactory<AppDbContext> contextFactory,
        QuizRepository quizRepository,
        IHubContext<QuizHub> hub)
    {
        _contextFactory = contextFactory;
        _quizRepository = quizRepository;
        _hub = hub;
    }

    public bool HasActiveSession => _active is not null;

    public async Task<(QuizSession session, List<Slide> slides)> StartSessionAsync(int quizId)
    {
        await _gate.WaitAsync();
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

            _active?.DeadlineTimer?.Dispose();
            _active = new ActiveSession { Session = session, Slides = quiz.Slides };
            return (session, quiz.Slides);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PlayerInfoDto?> JoinPlayerAsync(string joinCode, string name, string connectionId)
    {
        await _gate.WaitAsync();
        try
        {
            var active = _active;
            if (active is null || !string.Equals(active.Session.JoinCode, joinCode, StringComparison.OrdinalIgnoreCase))
                return null;

            await using var db = await _contextFactory.CreateDbContextAsync();
            var player = new Player
            {
                SessionId = active.Session.Id,
                Name = name,
                JoinedAt = DateTime.UtcNow,
                ConnectionId = connectionId
            };
            db.Players.Add(player);
            await db.SaveChangesAsync();

            active.ConnectionToPlayerId[connectionId] = player.Id;
            active.PlayersById[player.Id] = player;

            var dto = new PlayerInfoDto(player.Id, player.Name, player.Score);
            await _hub.Clients.All.SendAsync("PlayerJoined", dto);
            PlayerJoined?.Invoke(dto);
            return dto;
        }
        finally
        {
            _gate.Release();
        }
    }

    public List<PlayerInfoDto> GetJoinedPlayers()
    {
        var active = _active;
        if (active is null) return new List<PlayerInfoDto>();
        return active.PlayersById.Values
            .OrderByDescending(p => p.Score)
            .Select(p => new PlayerInfoDto(p.Id, p.Name, p.Score))
            .ToList();
    }

    public async Task<SlideDto?> NextSlideAsync()
    {
        await _gate.WaitAsync();
        ActiveSession active;
        int position;
        try
        {
            active = _active ?? throw new InvalidOperationException("No active session.");
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
            _gate.Release();
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
            slide.Id, slide.Type, slide.Header, slide.SubText, slide.Question,
            slide.Type == SlideType.MultipleChoice ? slide.Options.Select(o => o.Text).ToList() : null,
            slide.TimeSeconds, deadline);

        await _hub.Clients.All.SendAsync("SlideStarted", dto);

        if (slide.Type == SlideType.Leaderboard)
            await BroadcastLeaderboardAsync();

        return dto;
    }

    public async Task<SubmitAnswerResultDto> SubmitAnswerAsync(string connectionId, string answerText)
    {
        await _gate.WaitAsync();
        ActiveSession active;
        Slide slide;
        Player player;
        double elapsedMs;
        try
        {
            active = _active ?? throw new InvalidOperationException("No active session.");
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
            _gate.Release();
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
        return new SubmitAnswerResultDto(true, false, 0);
    }

    public async Task<RevealDto?> RevealCurrentSlideAsync()
    {
        await _gate.WaitAsync();
        ActiveSession active;
        Slide slide;
        try
        {
            active = _active ?? throw new InvalidOperationException("No active session.");
            if (active.Revealed || active.CurrentSlidePosition < 0) return null;
            active.Revealed = true;
            active.DeadlineTimer?.Dispose();
            active.DeadlineTimer = null;
            slide = active.Slides[active.CurrentSlidePosition];
        }
        finally
        {
            _gate.Release();
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
        SlideRevealedEvent?.Invoke(reveal);
        await BroadcastLeaderboardAsync();
        return reveal;
    }

    public async Task BroadcastLeaderboardAsync()
    {
        var leaderboard = GetJoinedPlayers()
            .OrderByDescending(p => p.Score)
            .Select(p => new LeaderboardEntryDto(p.Name, p.Score))
            .ToList();
        await _hub.Clients.All.SendAsync("LeaderboardUpdated", leaderboard);
        LeaderboardUpdatedEvent?.Invoke(leaderboard);
    }

    public async Task EndSessionAsync()
    {
        await _gate.WaitAsync();
        ActiveSession active;
        try
        {
            active = _active ?? throw new InvalidOperationException("No active session.");
            active.DeadlineTimer?.Dispose();
        }
        finally
        {
            _gate.Release();
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

        var leaderboard = GetJoinedPlayers().Select(p => new LeaderboardEntryDto(p.Name, p.Score)).ToList();
        await _hub.Clients.All.SendAsync("SessionEnded", leaderboard);
        SessionEndedEvent?.Invoke();
        _active = null;
    }

    public void PlayerDisconnected(string connectionId)
    {
        _active?.ConnectionToPlayerId.Remove(connectionId);
    }

    private static bool IsAnswerCorrect(Slide slide, string answerText) => slide.Type switch
    {
        SlideType.MultipleChoice => slide.Options.Any(o => o.IsCorrect && string.Equals(o.Text, answerText, StringComparison.OrdinalIgnoreCase)),
        SlideType.OpenQuestion => string.Equals(slide.CorrectAnswer?.Trim(), answerText.Trim(), StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no O/0/I/1 confusion
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 5).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    public ValueTask DisposeAsync()
    {
        _active?.DeadlineTimer?.Dispose();
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class ActiveSession
    {
        public required QuizSession Session { get; init; }
        public required List<Slide> Slides { get; init; }
        public int CurrentSlidePosition { get; set; } = -1;
        public bool Revealed { get; set; }
        public DateTime? CurrentSlideDeadlineUtc { get; set; }
        public Timer? DeadlineTimer { get; set; }
        public Dictionary<string, int> ConnectionToPlayerId { get; } = new();
        public Dictionary<int, Player> PlayersById { get; } = new();
        public HashSet<int> AnsweredPlayerIds { get; } = new();
    }
}

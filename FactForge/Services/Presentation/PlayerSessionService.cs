
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FactForge.Data;
using FactForge.Data.Entities;
using FactForge.Hubs;
using FactForge.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FactForge.Services.Presentation;

/// <summary>
/// Manages players belonging to the currently active quiz session.
/// </summary>
public sealed class PlayerSessionService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IHubContext<QuizHub> _hub;
    private readonly PresentationSessionState _state;

    public event Action<PlayerInfoDto>? PlayerJoined;
    public event Action<PlayerInfoDto>? PlayerDisconnected;

    public PlayerSessionService(
        IDbContextFactory<AppDbContext> contextFactory,
        IHubContext<QuizHub> hub,
        PresentationSessionState state)
    {
        _contextFactory = contextFactory;
        _hub = hub;
        _state = state;
    }

    public async Task<PlayerInfoDto?> JoinPlayerAsync(
        string joinCode,
        string name,
        string connectionId)
    {
        await _state.Gate.WaitAsync();

        try
        {
            var active = _state.Active;

            if (active is null ||
                !string.Equals(
                    active.Session.JoinCode,
                    joinCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            await using var db =
                await _contextFactory.CreateDbContextAsync();

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

            var dto = new PlayerInfoDto(
                player.Id,
                player.Name,
                player.Score);

            await _hub.Clients.All.SendAsync("PlayerJoined", dto);

            PlayerJoined?.Invoke(dto);

            return dto;
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public List<PlayerInfoDto> GetJoinedPlayers()
    {
        var active = _state.Active;

        if (active is null)
            return new List<PlayerInfoDto>();

        return active.PlayersById.Values
            .OrderByDescending(player => player.Score)
            .Select(player => new PlayerInfoDto(
                player.Id,
                player.Name,
                player.Score,
                IsConnected: player.ConnectionId is not null))
            .ToList();
    }

    public AnswerTallyDto GetCurrentAnswerTally()
    {
        var active = _state.Active;

        return new AnswerTallyDto(
            active?.AnsweredPlayerIds.Count ?? 0,
            active?.PlayersById.Count ?? 0);
    }

    
    public async Task PlayerDisconnectedAsync(string connectionId)
    {
        await _state.Gate.WaitAsync();

        try
        {
            var active = _state.Active;

            if (active is null ||
                !active.ConnectionToPlayerId.Remove(connectionId, out var playerId))
            {
                return;
            }

            if (!active.PlayersById.TryGetValue(playerId, out var player))
                return;

            // Ignore a stale disconnect if this player has already reconnected
            // using a different connection.
            if (player.ConnectionId != connectionId)
                return;

            player.ConnectionId = null;

            var dto = new PlayerInfoDto(
                player.Id,
                player.Name,
                player.Score,
                IsConnected: false);

            PlayerDisconnected?.Invoke(dto);

            await _hub.Clients.All.SendAsync("PlayerDisconnected", dto);
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task<List<PlayerInfoDto>> GetDisconnectedPlayersAsync(string joinCode)
    {
        await _state.Gate.WaitAsync();

        try
        {
            var active = _state.Active;

            if (active is null ||
                !string.Equals(
                    active.Session.JoinCode,
                    joinCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new List<PlayerInfoDto>();
            }

            return active.PlayersById.Values
                .Where(p => p.ConnectionId is null)
                .OrderBy(p => p.Name)
                .Select(p => new PlayerInfoDto(
                    p.Id,
                    p.Name,
                    p.Score,
                    IsConnected: false))
                .ToList();
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    public async Task<RejoinSessionDto?> RejoinPlayerAsync(
        string joinCode,
        int playerId,
        string connectionId)
    {
        await _state.Gate.WaitAsync();

        try
        {
            var active = _state.Active;

            if (active is null ||
                !string.Equals(
                    active.Session.JoinCode,
                    joinCode,
                    StringComparison.OrdinalIgnoreCase) ||
                !active.PlayersById.TryGetValue(playerId, out var player) ||
                player.ConnectionId is not null)
            {
                return null;
            }

            // Reuse the existing player rather than creating a new record.
            player.ConnectionId = connectionId;
            active.ConnectionToPlayerId[connectionId] = player.Id;

            var playerDto = new PlayerInfoDto(
                player.Id,
                player.Name,
                player.Score,
                IsConnected: true);

            SlideDto? slideDto = null;
            var isRevealed = active.Revealed;
            var hasAnswered = false;
            var questionPoints = 0;

            if (active.CurrentSlidePosition >= 0 &&
                active.CurrentSlidePosition < active.Slides.Count)
            {
                var slide = active.Slides[active.CurrentSlidePosition];

                slideDto = new SlideDto(
                    slide.Id,
                    slide.Type,
                    slide.Header,
                    slide.SubText,
                    slide.Question,
                    slide.Type == SlideType.MultipleChoice
                        ? slide.Options.Select(o => o.Text).ToList()
                        : null,
                    slide.TimeSeconds,
                    active.CurrentSlideDeadlineUtc,
                    slide.MusicFilePath,
                    slide.Title,
                    slide.Artist,
                    slide.ImagePath);

                hasAnswered = active.AnsweredPlayerIds.Contains(player.Id);

                if (hasAnswered)
                {
                    await using var db =
                        await _contextFactory.CreateDbContextAsync();

                    questionPoints = await db.PlayerAnswers
                        .Where(a =>
                            a.PlayerId == player.Id &&
                            a.SlideId == slide.Id)
                        .Select(a => a.PointsAwarded)
                        .FirstOrDefaultAsync();
                }
            }

            PlayerJoined?.Invoke(playerDto);

            await _hub.Clients.All.SendAsync("PlayerJoined", playerDto);

            return new RejoinSessionDto(
                playerDto,
                slideDto,
                isRevealed,
                hasAnswered,
                questionPoints);
        }
        finally
        {
            _state.Gate.Release();
        }
    }
}

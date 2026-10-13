
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
                player.Score))
            .ToList();
    }

    public void PlayerDisconnected(string connectionId)
    {
        _state.Active?.ConnectionToPlayerId.Remove(connectionId);
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FactForge.Models;
using FactForge.Services;
using FactForge.Services.Presentation;
using Microsoft.AspNetCore.SignalR;

namespace FactForge.Hubs;

public class QuizHub : Hub
{
    private readonly AnswerService _answerService;
    private readonly PlayerSessionService _playerSessionService;

    public QuizHub(
        AnswerService answerService,
        PlayerSessionService playerSessionService)
    {
        _answerService = answerService;
        _playerSessionService = playerSessionService;
    }

    public async Task<PlayerInfoDto?> JoinSession(
        string joinCode,
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return await _playerSessionService.JoinPlayerAsync(
            joinCode.Trim(),
            name.Trim(),
            Context.ConnectionId);
    }

    public async Task<SubmitAnswerResultDto> SubmitAnswer(
        string answerText)
    {
        return await _answerService.SubmitAnswerAsync(
            Context.ConnectionId,
            answerText ?? string.Empty);
    }

    public async Task<SubmitAnswerResultDto> SubmitMusicAnswer(
        string artist,
        string title)
    {
        return await _answerService.SubmitMusicAnswerAsync(
            Context.ConnectionId,
            artist ?? string.Empty,
            title ?? string.Empty);
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _playerSessionService.PlayerDisconnectedAsync(
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    public Task<List<PlayerInfoDto>> GetDisconnectedPlayers(string joinCode)
    {
        return _playerSessionService.GetDisconnectedPlayersAsync(
            joinCode.Trim());
    }

    public Task<RejoinSessionDto?> RejoinSession(string joinCode, int playerId)
    {
        return _playerSessionService.RejoinPlayerAsync(
            joinCode.Trim(),
            playerId,
            Context.ConnectionId);
    }

}

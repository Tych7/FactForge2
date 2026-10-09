using System;
using System.Threading.Tasks;
using FactForge.Models;
using FactForge.Services;
using Microsoft.AspNetCore.SignalR;

namespace FactForge.Hubs;

// Thin adapter: every real decision (state, validation, broadcasting) lives in
// PresentationService, which is shared between the hub and the presenter's own UI.
public class QuizHub : Hub
{
    private readonly PresentationService _presentation;

    public QuizHub(PresentationService presentation)
    {
        _presentation = presentation;
    }

    public async Task<PlayerInfoDto?> JoinSession(string joinCode, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return await _presentation.JoinPlayerAsync(joinCode.Trim(), name.Trim(), Context.ConnectionId);
    }

    public async Task<SubmitAnswerResultDto> SubmitAnswer(string answerText)
    {
        return await _presentation.SubmitAnswerAsync(Context.ConnectionId, answerText ?? string.Empty);
    }

    public async Task<SubmitAnswerResultDto> SubmitMusicAnswer(
        string artist,
        string title)
    {
        return await _presentation.SubmitMusicAnswerAsync(
            Context.ConnectionId,
            artist ?? string.Empty,
            title ?? string.Empty);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _presentation.PlayerDisconnected(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

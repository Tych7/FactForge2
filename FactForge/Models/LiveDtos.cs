using System;
using System.Collections.Generic;

namespace FactForge.Models;

// DTOs sent over SignalR between the presenter/desktop side and the player web app.
// Kept separate from the EF entities so the wire contract doesn't leak DB shape.

public record PlayerInfoDto(int Id, string Name, int Score);

public record SlideDto(
    int SlideId,
    SlideType Type,
    string? Header,
    string? SubText,
    string? Question,
    List<string>? Options,
    int TimeSeconds,
    DateTime? DeadlineUtc);

public record AnswerTallyDto(int Answered, int TotalPlayers);

public record OptionTallyDto(string Text, int Count, bool IsCorrect);

public record RevealDto(
    int SlideId,
    string? CorrectAnswer,
    List<OptionTallyDto>? OptionTallies);

public record LeaderboardEntryDto(string Name, int Score);

public record SubmitAnswerResultDto(bool Accepted, bool IsCorrect, int PointsAwarded);

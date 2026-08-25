using System;
using System.Collections.Generic;

namespace FactForge.Data.Entities;

public class Player
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public QuizSession? Session { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public int Score { get; set; }

    public string? ConnectionId { get; set; }

    public List<PlayerAnswer> Answers { get; set; } = new();
}

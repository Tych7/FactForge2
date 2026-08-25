using System;
using System.Collections.Generic;

namespace FactForge.Data.Entities;

public class QuizSession
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    public string JoinCode { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public int? CurrentSlideId { get; set; }
    public DateTime? CurrentSlideDeadlineUtc { get; set; }

    public List<Player> Players { get; set; } = new();
}

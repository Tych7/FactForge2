using System;

namespace FactForge.Data.Entities;

public class PlayerAnswer
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public int SlideId { get; set; }
    public Slide? Slide { get; set; }

    public string AnswerText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int AnswerMs { get; set; }
    public int PointsAwarded { get; set; }
    public DateTime SubmittedAt { get; set; }
}

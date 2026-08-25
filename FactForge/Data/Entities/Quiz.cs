using System;
using System.Collections.Generic;

namespace FactForge.Data.Entities;

public class Quiz
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<Slide> Slides { get; set; } = new();
    public List<QuizSession> Sessions { get; set; } = new();
}

using System.Collections.Generic;
using FactForge.Models;

namespace FactForge.Data.Entities;

public class Slide
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    public int OrderIndex { get; set; }
    public SlideType Type { get; set; }

    // Text slides
    public string? Header { get; set; }
    public string? SubText { get; set; }

    // Question slides (MultipleChoice / OpenQuestion)
    public string? Question { get; set; }
    public string? CorrectAnswer { get; set; }
    public int TimeSeconds { get; set; } = 20;

    public List<SlideOption> Options { get; set; } = new();
}

using System;
using FactForge.Data.Entities;

namespace FactForge.ViewModels;

public class QuizListItemViewModel
{
    public int Id { get; }
    public string Title { get; }
    public DateTime UpdatedAt { get; }

    public QuizListItemViewModel(Quiz quiz)
    {
        Id = quiz.Id;
        Title = quiz.Title;
        UpdatedAt = quiz.UpdatedAt;
    }
}

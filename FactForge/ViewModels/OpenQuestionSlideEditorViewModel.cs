using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.ViewModels;

public partial class OpenQuestionSlideEditorViewModel : ViewModelBase
{
    public int SlideId { get; }

    [ObservableProperty] private string _question;
    [ObservableProperty] private string _correctAnswer;
    [ObservableProperty] private int _timeSeconds;

    public OpenQuestionSlideEditorViewModel(Slide slide)
    {
        SlideId = slide.Id;
        _question = slide.Question ?? string.Empty;
        _correctAnswer = slide.CorrectAnswer ?? string.Empty;
        _timeSeconds = slide.TimeSeconds;
    }

    public Slide ToEntity() => new()
    {
        Id = SlideId,
        Type = SlideType.OpenQuestion,
        Question = Question,
        CorrectAnswer = CorrectAnswer,
        TimeSeconds = TimeSeconds
    };
}

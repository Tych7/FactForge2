using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Data;
using FactForge.Data.Entities;

namespace FactForge.ViewModels;

public partial class OpenQuestionSlideEditorViewModel : ViewModelBase, IHasSlideImage
{
    public int SlideId { get; }

    public SlideImageEditorViewModel Image { get; }

    [ObservableProperty] private string _question;
    [ObservableProperty] private string _correctAnswer;
    [ObservableProperty] private int _timeSeconds;
    [ObservableProperty] private bool _numbersOnly;

    public OpenQuestionSlideEditorViewModel(Slide slide)
    {
        SlideId = slide.Id;
        Image = new SlideImageEditorViewModel(slide.ImagePath);
        _question = slide.Question ?? string.Empty;
        _correctAnswer = slide.CorrectAnswer ?? string.Empty;
        _timeSeconds = slide.TimeSeconds;
        _numbersOnly = slide.NumbersOnly;
    }

    public Slide ToEntity() => new()
    {
        Id = SlideId,
        Type = SlideType.OpenQuestion,
        Question = Question,
        CorrectAnswer = CorrectAnswer,
        TimeSeconds = TimeSeconds,
        NumbersOnly = NumbersOnly,
        ImagePath = Image.FileName,
    };

    private bool _filteringInvalidCharacters;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCorrectAnswerError))]
    private string? _correctAnswerError;

    public bool HasCorrectAnswerError =>
        !string.IsNullOrEmpty(CorrectAnswerError);

    partial void OnNumbersOnlyChanged(bool value)
    {
        CorrectAnswerError = null;

        if (value && CorrectAnswer.Any(c => !char.IsDigit(c)))
        {
            _filteringInvalidCharacters = true;

            CorrectAnswer = new string(
                CorrectAnswer.Where(char.IsDigit).ToArray());

            _filteringInvalidCharacters = false;
        }
    }

    partial void OnCorrectAnswerChanged(string value)
    {
        if (_filteringInvalidCharacters)
            return;

        if (NumbersOnly && value.Any(c => !char.IsDigit(c)))
        {
            CorrectAnswerError =
                "With Numbers Only enabled, you can only enter numbers.";

            _filteringInvalidCharacters = true;

            CorrectAnswer = new string(
                value.Where(char.IsDigit).ToArray());

            _filteringInvalidCharacters = false;

            return;
        }

        CorrectAnswerError = null;
    }
}
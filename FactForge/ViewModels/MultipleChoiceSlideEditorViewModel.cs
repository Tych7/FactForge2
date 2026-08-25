using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.ViewModels;

public partial class OptionEditorViewModel : ViewModelBase
{
    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _isCorrect;

    public OptionEditorViewModel(string text, bool isCorrect)
    {
        _text = text;
        _isCorrect = isCorrect;
    }
}

public partial class MultipleChoiceSlideEditorViewModel : ViewModelBase
{
    public int SlideId { get; }

    [ObservableProperty] private string _question;
    [ObservableProperty] private int _timeSeconds;

    public ObservableCollection<OptionEditorViewModel> Options { get; }

    public MultipleChoiceSlideEditorViewModel(Slide slide)
    {
        SlideId = slide.Id;
        _question = slide.Question ?? string.Empty;
        _timeSeconds = slide.TimeSeconds;

        var options = slide.Options.OrderBy(o => o.OrderIndex).Select(o => new OptionEditorViewModel(o.Text, o.IsCorrect)).ToList();
        while (options.Count < 4)
            options.Add(new OptionEditorViewModel(string.Empty, false));
        Options = new ObservableCollection<OptionEditorViewModel>(options);
    }

    public Slide ToEntity() => new()
    {
        Id = SlideId,
        Type = SlideType.MultipleChoice,
        Question = Question,
        TimeSeconds = TimeSeconds,
        Options = Options.Select(o => new SlideOption { Text = o.Text, IsCorrect = o.IsCorrect }).ToList()
    };
}

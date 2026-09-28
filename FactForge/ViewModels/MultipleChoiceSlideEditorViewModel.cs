using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.ViewModels;

public partial class OptionEditorViewModel : ViewModelBase
{
    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _isCorrect;

    // Fixed at creation time from the option's position, matching the tile colors players
    // and the presenter see, so the editor's preview can show it without re-deriving it.
    public IBrush PreviewColor { get; }

    public OptionEditorViewModel(string text, bool isCorrect, int index)
    {
        _text = text;
        _isCorrect = isCorrect;
        PreviewColor = AnswerColorPalette.Colors[index % AnswerColorPalette.Colors.Length];
    }
}

public partial class MultipleChoiceSlideEditorViewModel : ViewModelBase, IHasSlideImage
{
    public int SlideId { get; }

    public SlideImageEditorViewModel Image { get; }

    [ObservableProperty] private string _question;
    [ObservableProperty] private int _timeSeconds;
    [ObservableProperty] private int _optionCount;

    public ObservableCollection<OptionEditorViewModel> Options { get; }

    public MultipleChoiceSlideEditorViewModel(Slide slide)
    {
        SlideId = slide.Id;
        Image = new SlideImageEditorViewModel(slide.ImagePath);
        _question = slide.Question ?? string.Empty;
        _timeSeconds = slide.TimeSeconds;

        var options = slide.Options.OrderBy(o => o.OrderIndex)
            .Select((o, i) => new OptionEditorViewModel(o.Text, o.IsCorrect, i)).ToList();
        _optionCount = options.Count <= 2 ? 2 : 4;
        while (options.Count < _optionCount)
            options.Add(new OptionEditorViewModel(string.Empty, false, options.Count));
        Options = new ObservableCollection<OptionEditorViewModel>(options);
    }

    [RelayCommand]
    private void UseTwoAnswers() => SetOptionCount(2);

    [RelayCommand]
    private void UseFourAnswers() => SetOptionCount(4);

    private void SetOptionCount(int count)
    {
        if (OptionCount == count) return;

        if (count > Options.Count)
        {
            while (Options.Count < count)
                Options.Add(new OptionEditorViewModel(string.Empty, false, Options.Count));
        }
        else
        {
            while (Options.Count > count)
                Options.RemoveAt(Options.Count - 1);
            if (!Options.Any(o => o.IsCorrect))
                Options[0].IsCorrect = true;
        }

        OptionCount = count;
    }

    public Slide ToEntity() => new()
    {
        Id = SlideId,
        Type = SlideType.MultipleChoice,
        Question = Question,
        TimeSeconds = TimeSeconds,
        ImagePath = Image.FileName,
        Options = Options.Select(o => new SlideOption { Text = o.Text, IsCorrect = o.IsCorrect }).ToList()
    };
}

using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.ViewModels;

public class SlideListItemViewModel
{
    public int Id { get; }
    public SlideType Type { get; }
    public int OrderIndex { get; }

    public string TypeLabel => Type switch
    {
        SlideType.Text => "Text",
        SlideType.MultipleChoice => "Multiple Choice Question",
        SlideType.OpenQuestion => "Open Question",
        SlideType.Leaderboard => "Leaderboard",
        SlideType.MusicQuestion => "Music Question",
        _ => "Slide"
    };

    public string Preview { get; }

    public SlideListItemViewModel(Slide slide)
    {
        Id = slide.Id;
        Type = slide.Type;
        OrderIndex = slide.OrderIndex;
        Preview = slide.Type switch
        {
            SlideType.Text => string.IsNullOrWhiteSpace(slide.Header) ? "(empty)" : slide.Header!,
            SlideType.Leaderboard => "Standings",
            _ => string.IsNullOrWhiteSpace(slide.Question) ? "(empty)" : slide.Question!
        };
    }
}

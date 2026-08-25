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
        SlideType.MultipleChoice => "Multiple Choice",
        SlideType.OpenQuestion => "Open Question",
        _ => "Slide"
    };

    public string Preview { get; }

    public SlideListItemViewModel(Slide slide)
    {
        Id = slide.Id;
        Type = slide.Type;
        OrderIndex = slide.OrderIndex;
        var text = slide.Type == SlideType.Text ? slide.Header : slide.Question;
        Preview = string.IsNullOrWhiteSpace(text) ? "(empty)" : text!;
    }
}

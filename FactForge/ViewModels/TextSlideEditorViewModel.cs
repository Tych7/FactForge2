using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.ViewModels;

public partial class TextSlideEditorViewModel : ViewModelBase
{
    public int SlideId { get; }

    [ObservableProperty] private string _header;
    [ObservableProperty] private string _subText;

    public TextSlideEditorViewModel(Slide slide)
    {
        SlideId = slide.Id;
        _header = slide.Header ?? string.Empty;
        _subText = slide.SubText ?? string.Empty;
    }

    public Slide ToEntity() => new()
    {
        Id = SlideId,
        Type = SlideType.Text,
        Header = Header,
        SubText = SubText
    };
}

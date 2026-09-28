using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.ViewModels;

public partial class TextSlideEditorViewModel : ViewModelBase, IHasSlideImage
{
    public int SlideId { get; }

    public SlideImageEditorViewModel Image { get; }

    [ObservableProperty] private string _header;
    [ObservableProperty] private string _subText;

    public TextSlideEditorViewModel(Slide slide)
    {
        SlideId = slide.Id;
        Image = new SlideImageEditorViewModel(slide.ImagePath);
        _header = slide.Header ?? string.Empty;
        _subText = slide.SubText ?? string.Empty;
    }

    public Slide ToEntity() => new()
    {
        Id = SlideId,
        Type = SlideType.Text,
        Header = Header,
        SubText = SubText,
        ImagePath = Image.FileName,
    };
}

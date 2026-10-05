using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.ViewModels;

public partial class MusicQuestionSlideEditorViewModel : ViewModelBase, IHasSlideAudio, IHasSlideImage
{
    public int SlideId { get; }

    public SlideAudioEditorViewModel Audio { get; }
    public SlideImageEditorViewModel Image { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private string _artist;
    [ObservableProperty] private int _timeSeconds;

    public MusicQuestionSlideEditorViewModel(Slide slide)
    {
        SlideId = slide.Id;

        Audio = new SlideAudioEditorViewModel(slide.MusicFilePath);
        Image = new SlideImageEditorViewModel(slide.ImagePath);

        _title = slide.Title ?? string.Empty;
        _artist = slide.Artist ?? string.Empty;
        _timeSeconds = slide.TimeSeconds;
    }

    public Slide ToEntity() => new()
    {
        Id = SlideId,
        Type = SlideType.MusicQuestion,
        MusicFilePath = Audio.FileName,
        Title = Title,
        Artist = Artist,
        TimeSeconds = TimeSeconds,
        ImagePath = Image.FileName,
    };
}
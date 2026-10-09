using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.ViewModels;

public partial class MusicQuestionSlideEditorViewModel
    : ViewModelBase, IHasSlideAudio, IHasSlideImage
{
    public int SlideId { get; }

    public SlideAudioEditorViewModel Audio { get; }
    public SlideImageEditorViewModel Image { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private int _timeSeconds;

    public ObservableCollection<ArtistEditorViewModel> Artists { get; } = new();

    public MusicQuestionSlideEditorViewModel(Slide slide)
    {
        SlideId = slide.Id;

        Audio = new SlideAudioEditorViewModel(slide.MusicFilePath);
        Image = new SlideImageEditorViewModel(slide.ImagePath);

        _title = slide.Title ?? string.Empty;
        _timeSeconds = slide.TimeSeconds;

        var artists = slide.Artist?
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToList();

        if (artists is not null && artists.Count > 0)
        {
            foreach (var artist in artists)
                Artists.Add(new ArtistEditorViewModel(artist));
        }
        else
        {
            Artists.Add(new ArtistEditorViewModel());
        }

        UpdateArtistRemoveButtons();
    }

    [RelayCommand]
    private void AddArtist()
    {
        Artists.Add(new ArtistEditorViewModel());
        UpdateArtistRemoveButtons();
    }

    [RelayCommand]
    private void RemoveArtist(ArtistEditorViewModel artist)
    {
        if (Artists.Count <= 1)
            return;

        Artists.Remove(artist);
        UpdateArtistRemoveButtons();
    }

    private void UpdateArtistRemoveButtons()
    {
        var canRemove = Artists.Count > 1;

        foreach (var artist in Artists)
            artist.CanRemove = canRemove;
    }

    public Slide ToEntity() => new()
    {
        Id = SlideId,
        Type = SlideType.MusicQuestion,
        MusicFilePath = Audio.FileName,
        Title = Title,
        Artist = string.Join(";",
            Artists
                .Select(x => x.Text.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))),
        TimeSeconds = TimeSeconds,
        ImagePath = Image.FileName,
    };
}
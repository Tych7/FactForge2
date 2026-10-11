using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Services;

namespace FactForge.ViewModels;

/// <summary>Implemented by every slide editor view model that supports audio.</summary>
public interface IHasSlideAudio
{
    SlideAudioEditorViewModel Audio { get; }
}

/// <summary>Audio state + commands for one slide. Composed into each slide editor VM.</summary>
public partial class SlideAudioEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _fileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAudio))]
    private string? _audioPath;

    public bool HasAudio => AudioPath is not null;

    public SlideAudioEditorViewModel(string? fileName)
    {
        _fileName = fileName;
        _audioPath = SlideAudioService.Shared.GetPath(fileName);
    }

    [RelayCommand]
    private async Task ChooseAsync()
    {
        var name = await SlideAudioService.Shared.PickAndImportAsync();
        if (name is null) return;

        FileName = name;
        AudioPath = SlideAudioService.Shared.GetPath(name);
    }

    [RelayCommand]
    private void Remove()
    {
        FileName = null;
        AudioPath = null;
    }
}
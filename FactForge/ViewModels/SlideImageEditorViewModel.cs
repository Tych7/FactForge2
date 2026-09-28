using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Services;

namespace FactForge.ViewModels;

/// <summary>Implemented by every slide editor view model that supports an image.</summary>
public interface IHasSlideImage
{
    SlideImageEditorViewModel Image { get; }
}

/// <summary>Image state + commands for one slide. Composed into each slide editor VM.</summary>
public partial class SlideImageEditorViewModel : ViewModelBase
{
    /// <summary>Stored file name (what goes into Slide.ImagePath), or null for no image.</summary>
    public string? FileName { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private Bitmap? _bitmap;

    public bool HasImage => Bitmap is not null;

    public SlideImageEditorViewModel(string? fileName)
    {
        FileName = fileName;
        _bitmap = SlideImageService.Shared.Load(fileName);
    }

    [RelayCommand]
    private async Task ChooseAsync()
    {
        var name = await SlideImageService.Shared.PickAndImportAsync();
        if (name is null) return;
        FileName = name;
        Bitmap = SlideImageService.Shared.Load(name);
    }

    [RelayCommand]
    private void Remove()
    {
        FileName = null;
        Bitmap = null;
    }
}

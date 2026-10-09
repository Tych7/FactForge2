using CommunityToolkit.Mvvm.ComponentModel;

namespace FactForge.ViewModels;

public partial class ArtistEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _text = string.Empty;

    
    [ObservableProperty]
    private bool _canRemove;

    public ArtistEditorViewModel()
    {
    }

    public ArtistEditorViewModel(string text)
    {
        _text = text;
    }
}
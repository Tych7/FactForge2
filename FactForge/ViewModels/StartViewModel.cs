using System;
using CommunityToolkit.Mvvm.Input;

namespace FactForge.ViewModels;

public partial class StartViewModel : ViewModelBase
{
    private readonly Action _onPlay;

    public StartViewModel(Action onPlay)
    {
        _onPlay = onPlay;
    }

    [RelayCommand]
    private void Play() => _onPlay();
}

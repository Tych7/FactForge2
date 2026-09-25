using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactForge.Services;
using System;

namespace FactForge.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IWindowService _windowService;
    private readonly Action _onBack;

    [ObservableProperty] private bool _isFullScreen;
    [ObservableProperty] private bool _isDarkMode;

    public SettingsViewModel(IWindowService windowService, Action onBack)
    {
        _windowService = windowService;
        _onBack = onBack;

        // set backing fields directly so the On*Changed handlers below don't
        // re-apply a state we're only reading, not changing
        _isFullScreen = windowService.IsFullScreen;
        _isDarkMode = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    }

    partial void OnIsFullScreenChanged(bool value) => _windowService.SetFullScreen(value);

    partial void OnIsDarkModeChanged(bool value)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    [RelayCommand]
    private void Back() => _onBack();
}
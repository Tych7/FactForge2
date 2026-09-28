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
    private readonly ISettingsService _settingsService;
    private readonly Action _onBack;

    [ObservableProperty] private bool _isFullScreen;
    [ObservableProperty] private bool _isDarkMode;

    public SettingsViewModel(IWindowService windowService, ISettingsService settingsService, Action onBack)
    {
        _windowService = windowService;
        _settingsService = settingsService;
        _onBack = onBack;

        _isFullScreen = windowService.IsFullScreen;
        _isDarkMode = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    }

    partial void OnIsFullScreenChanged(bool value)
    {
        _windowService.SetFullScreen(value);
        _settingsService.Current.IsFullScreen = value;
        _settingsService.Save();
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;

        _settingsService.Current.IsDarkMode = value;
        _settingsService.Save();
    }

    [RelayCommand]
    private void Back() => _onBack();
}
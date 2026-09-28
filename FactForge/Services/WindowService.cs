using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace FactForge.Services;

public interface IWindowService
{
    bool IsFullScreen { get; }
    void SetFullScreen(bool fullScreen);
    void Exit();
}

public class WindowService : IWindowService
{
    private static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public bool IsFullScreen => MainWindow?.WindowState == WindowState.FullScreen;

    public void SetFullScreen(bool fullScreen)
    {
        if (MainWindow is null) return;
        MainWindow.WindowState = fullScreen ? WindowState.FullScreen : WindowState.Normal;
    }

    public void Exit()
    {
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
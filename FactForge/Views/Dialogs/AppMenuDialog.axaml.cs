using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FactForge.Views;

public enum AppMenuResult { None, Options, Exit }

public partial class AppMenuDialog : Window
{
    public AppMenuDialog()
    {
        InitializeComponent();
    }

    private void OnOptions(object? sender, RoutedEventArgs e) => Close(AppMenuResult.Options);
    private void OnExit(object? sender, RoutedEventArgs e) => Close(AppMenuResult.Exit);

    private void OnBackgroundPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == sender) Close(AppMenuResult.None);
    }
}
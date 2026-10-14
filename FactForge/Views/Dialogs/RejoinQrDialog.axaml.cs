using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FactForge.ViewModels.Presentation;

namespace FactForge.Views.Dialogs;

public partial class RejoinQrDialog : Window
{
    public RejoinQrDialog()
    {
        InitializeComponent();
    }

    public RejoinQrDialog(PresentViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnBackgroundPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Grid)
            Close();
    }
}
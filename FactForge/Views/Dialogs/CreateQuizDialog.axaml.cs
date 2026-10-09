using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FactForge.Views;

public partial class CreateQuizDialog : Window
{
    public CreateQuizDialog()
    {
        InitializeComponent();


            Opened += (_, _) => QuizTitleTextBox.Focus();
    }

    private void OnCreate(object? sender, RoutedEventArgs e)
    {
        var title = QuizTitleTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            QuizTitleTextBox.Focus();
            return;
        }

        Close(title);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnBackgroundPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        if (e.Source == sender)
            Close(null);
    }

}

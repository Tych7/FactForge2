using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FactForge.Views;

public partial class RenameQuizDialog : Window
{
    public RenameQuizDialog()
    {
        InitializeComponent();
    }

    public RenameQuizDialog(string currentTitle) : this()
    {
        QuizTitleTextBox.Text = currentTitle;

        Opened += (_, _) =>
        {
            QuizTitleTextBox.Focus();
            QuizTitleTextBox.SelectAll();
        };
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        Submit();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Submit();
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }

    private void Submit()
    {
        var title = QuizTitleTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            QuizTitleTextBox.Focus();
            return;
        }

        Close(title);
    }

    private void OnBackgroundPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == sender)
            Close(null);
    }
}
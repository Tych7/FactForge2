
using Avalonia;
using Avalonia.Controls;
using FactForge.ViewModels;

namespace FactForge.Views;

public partial class AudioPicker : UserControl
{
    public AudioPicker()
    {
        InitializeComponent();

        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDetachedFromVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is SlideAudioEditorViewModel viewModel)
            _ = viewModel.StopPreviewAsync();
    }
}

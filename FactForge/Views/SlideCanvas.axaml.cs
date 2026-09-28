using System;
using Avalonia.Controls;
using FactForge.ViewModels;

namespace FactForge.Views;

public partial class SlideCanvas : UserControl
{
    public SlideCanvas()
    {
        InitializeComponent();
    }

    // Image is fixed for the lifetime of a SlideDisplay (a new SlideDisplay is created per slide /
    // per preview refresh), so the column split only needs to be applied when the DataContext changes.
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        var root = this.FindControl<Grid>("Root");
        if (root is null) return;

        var hasImage = (DataContext as SlideDisplay)?.HasImage == true;
        // With an image: 60% content / 40% image (right). Without: image column collapses to 0.
        root.ColumnDefinitions = hasImage
            ? new ColumnDefinitions("3*,2*")
            : new ColumnDefinitions("*,0");
    }
}

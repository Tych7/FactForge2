using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace FactForge.Views;

public partial class LeaderboardChart : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<LeaderboardChart, IEnumerable?>(nameof(Items));

    public IEnumerable? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public LeaderboardChart()
    {
        InitializeComponent();
    }
}

using Avalonia.Media;

namespace FactForge.ViewModels;

// Same per-position palette as the player-facing page's .choice-btn:nth-child rules, so the
// host screen, the editor's slide preview, and players' phones all agree on which tile is which color.
public static class AnswerColorPalette
{
    public static readonly IBrush[] Colors =
    {
        new SolidColorBrush(Color.Parse("#D3FF0400")),
        new SolidColorBrush(Color.Parse("#1E88E5")),
        new SolidColorBrush(Color.Parse("#D5FFD621")),
        new SolidColorBrush(Color.Parse("#3CA101")),
    };
}

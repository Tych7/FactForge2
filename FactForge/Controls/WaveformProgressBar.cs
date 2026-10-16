
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace FactForge.Controls;

public sealed class WaveformProgressBar : Control
{
    public static readonly StyledProperty<IReadOnlyList<float>?> PeaksProperty =
        AvaloniaProperty.Register<WaveformProgressBar,
            IReadOnlyList<float>?>(nameof(Peaks));

    public static readonly StyledProperty<double> PositionSecondsProperty =
        AvaloniaProperty.Register<WaveformProgressBar, double>(
            nameof(PositionSeconds));

    public static readonly StyledProperty<double> DurationSecondsProperty =
        AvaloniaProperty.Register<WaveformProgressBar, double>(
            nameof(DurationSeconds));

    public static readonly StyledProperty<IBrush?> PlayedBrushProperty =
        AvaloniaProperty.Register<WaveformProgressBar, IBrush?>(
            nameof(PlayedBrush));

    public static readonly StyledProperty<IBrush?> UnplayedBrushProperty =
        AvaloniaProperty.Register<WaveformProgressBar, IBrush?>(
            nameof(UnplayedBrush));

    public IReadOnlyList<float>? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public double PositionSeconds
    {
        get => GetValue(PositionSecondsProperty);
        set => SetValue(PositionSecondsProperty, value);
    }

    public double DurationSeconds
    {
        get => GetValue(DurationSecondsProperty);
        set => SetValue(DurationSecondsProperty, value);
    }

    public IBrush? PlayedBrush
    {
        get => GetValue(PlayedBrushProperty);
        set => SetValue(PlayedBrushProperty, value);
    }

    public IBrush? UnplayedBrush
    {
        get => GetValue(UnplayedBrushProperty);
        set => SetValue(UnplayedBrushProperty, value);
    }
    
    private readonly DispatcherTimer _renderTimer =
    new(DispatcherPriority.Render)
    {
        Interval = TimeSpan.FromMilliseconds(33)
    };

    public WaveformProgressBar()
    {
        _renderTimer.Tick += (_, _) => InvalidateVisual();
        _renderTimer.Start();
    }

    static WaveformProgressBar()
    {
        AffectsRender<WaveformProgressBar>(
            PeaksProperty,
            PositionSecondsProperty,
            DurationSecondsProperty,
            PlayedBrushProperty,
            UnplayedBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var peaks = Peaks;

        if (peaks is null || peaks.Count == 0 ||
            Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var progress = DurationSeconds > 0
            ? Math.Clamp(PositionSeconds / DurationSeconds, 0, 1)
            : 0;

        const double gap = 2;
        var barWidth = Math.Max(
            1, (Bounds.Width - gap * (peaks.Count - 1)) / peaks.Count);

        for (var i = 0; i < peaks.Count; i++)
        {
            var amplitude = Math.Clamp(peaks[i], 0, 1);

            // Keep quiet sections visible.
            var barHeight = Math.Max(3, amplitude * Bounds.Height);
            var x = i * (barWidth + gap);
            var y = (Bounds.Height - barHeight) / 2;

            var brush = (i + 0.5) / peaks.Count <= progress
                ? PlayedBrush
                : UnplayedBrush;

            if (brush is null)
                continue;

            context.FillRectangle(
                brush,
                new Rect(x, y, barWidth, barHeight),
                2);
        }
    }
}

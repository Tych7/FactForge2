
using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FactForge.ViewModels;

namespace FactForge.Controls;

public class ScoreHistoryChart : Control
{
    public static readonly StyledProperty<
        System.Collections.Generic.IReadOnlyList<ScoreChartSeries>> SeriesProperty =
        AvaloniaProperty.Register<ScoreHistoryChart,
            System.Collections.Generic.IReadOnlyList<ScoreChartSeries>>(
            nameof(Series), Array.Empty<ScoreChartSeries>());

    public System.Collections.Generic.IReadOnlyList<ScoreChartSeries> Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    private static readonly Color[] Colors =
    {
        Color.Parse("#20B8AA"),
        Color.Parse("#6488F5"),
        Color.Parse("#F2A65A"),
        Color.Parse("#D977B5"),
        Color.Parse("#8BC56B"),
        Color.Parse("#A78BFA")
    };

    static ScoreHistoryChart()
    {
        AffectsRender<ScoreHistoryChart>(SeriesProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var series = Series
            .Where(s => s.Scores.Count > 0)
            .ToList();

        if (series.Count == 0)
        {
            context.DrawText(
                new FormattedText(
                    "Score history will appear after a question is completed.",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    13,
                    Brushes.Gray),
                new Point(12, 12));
            return;
        }

        var questionCount = series.Max(s => s.Scores.Count);
        var maxScore = series.Max(s => s.Scores.Max());
        maxScore = Math.Max(maxScore, 100);

        const double left = 48;
        const double right = 16;
        const double top = 16;
        const double bottom = 36;
        const double legendHeight = 28;

        var plotWidth = Math.Max(1, Bounds.Width - left - right);
        var plotHeight = Math.Max(1, Bounds.Height - top - bottom - legendHeight);

        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#334155")), 1);
        var labelBrush = new SolidColorBrush(Color.Parse("#94A3B8"));

        for (var i = 0; i <= 4; i++)
        {
            var y = top + plotHeight * i / 4;
            var score = maxScore * (4 - i) / 4;

            context.DrawLine(gridPen,
                new Point(left, y),
                new Point(left + plotWidth, y));

            context.DrawText(
                new FormattedText(
                    score.ToString("0"),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    11,
                    labelBrush),
                new Point(2, y - 7));
        }

        for (var i = 0; i < questionCount; i++)
        {
            var x = left + (questionCount <= 1
                ? plotWidth
                : plotWidth * i / (questionCount - 1));

            var label = (i + 1).ToString();

            context.DrawText(
                new FormattedText(
                    label,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    11,
                    labelBrush),
                new Point(x - 4, top + plotHeight + 8));
        }

        for (var s = 0; s < series.Count; s++)
        {
            var item = series[s];
            var color = Colors[s % Colors.Length];
            var brush = new SolidColorBrush(color);
            var pen = new Pen(brush, 2.5);

            var geometry = new StreamGeometry();

            using (var builder = geometry.Open())
            {
                for (var i = 0; i < item.Scores.Count; i++)
                {
                    var x = left + (questionCount <= 1
                        ? plotWidth
                        : plotWidth * i / (questionCount - 1));

                    var y = top + plotHeight *
                        (1 - item.Scores[i] / (double)maxScore);

                    if (i == 0)
                        builder.BeginFigure(new Point(x, y), false);
                    else
                        builder.LineTo(new Point(x, y));
                }

                builder.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);

            for (var i = 0; i < item.Scores.Count; i++)
            {
                var x = left + (questionCount <= 1
                    ? plotWidth
                    : plotWidth * i / (questionCount - 1));

                var y = top + plotHeight *
                    (1 - item.Scores[i] / (double)maxScore);

                context.DrawEllipse(brush, null, new Point(x, y), 3.5, 3.5);
            }

            var legendX = left + (s % 3) * (plotWidth / 3);
            var legendY = top + plotHeight + bottom + 2
                + (s / 3) * 18;

            context.DrawLine(new Pen(brush, 3),
                new Point(legendX, legendY + 6),
                new Point(legendX + 16, legendY + 6));

            context.DrawText(
                new FormattedText(
                    item.Name,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    11,
                    labelBrush),
                new Point(legendX + 21, legendY));
        }
    }
}

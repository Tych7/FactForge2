using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using FactForge.ViewModels;

namespace FactForge.Views;

public partial class LeaderboardBarColumn : UserControl
{
    private const double GrowMs = 800;
    private const int StaggerMs = 90;
    private const int StartDelayMs = 80;

    private bool _played;
    private bool _animating;
    private CancellationTokenSource? _cts;

    public LeaderboardBarColumn()
    {
        InitializeComponent();
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _cts?.Cancel();
        _played = false;
        _animating = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (Bar is null || Plot is null || !IsEffectivelyVisible) return;

        var target = TargetHeight();
        if (Plot.Bounds.Height < 8 || target < 0) return;

        if (_played)
        {
            if (!_animating && Math.Abs(Bar.Height - target) > 1)
                Bar.Height = target;
            return;
        }

        _played = true;
        _ = GrowAsync();
    }

    private async Task GrowAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _animating = true;

        try
        {
            Bar.Transitions = null;
            Bar.Height = 0;

            var delay = StartDelayMs;
            if (DataContext is LeaderboardBarItem item)
                delay += item.Index * StaggerMs;

            await Task.Delay(delay, token);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            Bar.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Layoutable.HeightProperty,
                    Duration = TimeSpan.FromMilliseconds(GrowMs),
                    Easing = new CubicEaseOut()
                }
            };
            Bar.Height = TargetHeight();
            await Task.Delay(TimeSpan.FromMilliseconds(GrowMs), token);
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _animating = false;
        }
    }

    private double TargetHeight()
    {
        var fraction = DataContext is LeaderboardBarItem item ? item.Fraction : 0;
        var reserved = 32;
        var maxBar = Math.Max(0, Plot.Bounds.Height - reserved);
        return Math.Max(4, maxBar * fraction);
    }
}

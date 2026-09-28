// MERGE NOTE: I haven't seen EditorViewModel, so this is written as a partial class.
// If EditorViewModel isn't `partial`, add the keyword or move these members into it.
//
// Assumptions to check:
//  - the slide editor VMs (Text/MultipleChoice/OpenQuestion/Leaderboard) implement INotifyPropertyChanged
//  - MultipleChoiceSlideEditorViewModel.Options is an ObservableCollection<OptionEditorViewModel>
//  - OptionEditorViewModel.PreviewColor is an IBrush
//  - the SlideType member names for multiple choice / open question (MultipleChoice / OpenQuestion below)
//
// Wiring: wherever CurrentSlideEditor changes (e.g. its partial OnCurrentSlideEditorChanged, or the
// place that assigns it), call:  WatchEditorForPreview(CurrentSlideEditor);

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Models;

namespace FactForge.ViewModels;

public partial class EditorViewModel
{
    [ObservableProperty] private SlideDisplay? _previewSlide;

    private INotifyPropertyChanged? _watchedEditor;
    private ObservableCollection<OptionEditorViewModel>? _watchedOptions;

    private void WatchEditorForPreview(object? editor)
    {
        UnwatchPreview();

        if (editor is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += OnWatchedEditorChanged;
            _watchedEditor = npc;
        }

        if (editor is MultipleChoiceSlideEditorViewModel mc)
        {
            _watchedOptions = mc.Options;
            _watchedOptions.CollectionChanged += OnWatchedOptionsChanged;
            foreach (var o in _watchedOptions) o.PropertyChanged += OnWatchedEditorChanged;
        }

        RefreshPreview();
    }

    private void UnwatchPreview()
    {
        if (_watchedEditor is not null) _watchedEditor.PropertyChanged -= OnWatchedEditorChanged;
        _watchedEditor = null;

        if (_watchedOptions is not null)
        {
            _watchedOptions.CollectionChanged -= OnWatchedOptionsChanged;
            foreach (var o in _watchedOptions) o.PropertyChanged -= OnWatchedEditorChanged;
        }
        _watchedOptions = null;
    }

    private void OnWatchedEditorChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreview();

    partial void OnCurrentSlideEditorChanged(ViewModelBase? value)
    => WatchEditorForPreview(value);

    // Options added/removed: re-hook the per-option handlers, then rebuild.
    private void OnWatchedOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => WatchEditorForPreview(CurrentSlideEditor);

    private void RefreshPreview() => PreviewSlide = BuildPreview();

    private SlideDisplay? BuildPreview() => CurrentSlideEditor switch
    {
        TextSlideEditorViewModel t => new SlideDisplay
        {
            Type = SlideType.Text,
            Header = t.Header,
            SubText = t.SubText
        },

        MultipleChoiceSlideEditorViewModel m => new SlideDisplay
        {
            Type = SlideType.MultipleChoice,          // adjust to your enum member name
            Question = m.Question,
            TimeSeconds = m.TimeSeconds,
            SecondsRemaining = m.TimeSeconds,         // full bar, static
            StatusText = $"{m.TimeSeconds}s",
            Options = m.Options
                .Select(o => new AnswerOptionDisplay(o.Text, o.PreviewColor, o.IsCorrect))
                .ToList()
        },

        OpenQuestionSlideEditorViewModel q => new SlideDisplay
        {
            Type = SlideType.OpenQuestion,            // adjust to your enum member name
            Question = q.Question,
            TimeSeconds = q.TimeSeconds,
            SecondsRemaining = q.TimeSeconds,
            StatusText = $"{q.TimeSeconds}s"
        },

        LeaderboardSlideEditorViewModel => new SlideDisplay { Type = SlideType.Leaderboard },

        _ => null
    };
}

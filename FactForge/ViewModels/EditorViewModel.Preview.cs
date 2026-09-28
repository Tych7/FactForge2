// Partial of EditorViewModel (it is already `partial`, and CurrentSlideEditor is an [ObservableProperty]).
// Now also watches each editor's Image so picking/removing a picture refreshes the preview.

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
    private INotifyPropertyChanged? _watchedImage;
    private ObservableCollection<OptionEditorViewModel>? _watchedOptions;

    partial void OnCurrentSlideEditorChanged(ViewModelBase? value) => WatchEditorForPreview(value);

    private void WatchEditorForPreview(object? editor)
    {
        UnwatchPreview();

        if (editor is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += OnWatchedEditorChanged;
            _watchedEditor = npc;
        }

        if (editor is IHasSlideImage { Image: INotifyPropertyChanged imageNpc })
        {
            imageNpc.PropertyChanged += OnWatchedEditorChanged;
            _watchedImage = imageNpc;
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

        if (_watchedImage is not null) _watchedImage.PropertyChanged -= OnWatchedEditorChanged;
        _watchedImage = null;

        if (_watchedOptions is not null)
        {
            _watchedOptions.CollectionChanged -= OnWatchedOptionsChanged;
            foreach (var o in _watchedOptions) o.PropertyChanged -= OnWatchedEditorChanged;
        }
        _watchedOptions = null;
    }

    private void OnWatchedEditorChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreview();

    // Options added/removed: re-hook the per-option handlers, then rebuild.
    private void OnWatchedOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => WatchEditorForPreview(CurrentSlideEditor);

    private void RefreshPreview() => PreviewSlide = BuildPreview();

    private SlideDisplay? BuildPreview()
    {
        // Bitmap is cached in the editor VM, so rebuilding the preview per keystroke doesn't reload the file.
        var image = (CurrentSlideEditor as IHasSlideImage)?.Image.Bitmap;

        return CurrentSlideEditor switch
        {
            TextSlideEditorViewModel t => new SlideDisplay
            {
                Type = SlideType.Text,
                Header = t.Header,
                SubText = t.SubText,
                Image = image
            },

            MultipleChoiceSlideEditorViewModel m => new SlideDisplay
            {
                Type = SlideType.MultipleChoice,
                Question = m.Question,
                TimeSeconds = m.TimeSeconds,
                SecondsRemaining = m.TimeSeconds,
                StatusText = $"{m.TimeSeconds}s",
                Image = image,
                Options = m.Options
                    .Select(o => new AnswerOptionDisplay(o.Text, o.PreviewColor, o.IsCorrect))
                    .ToList()
            },

            OpenQuestionSlideEditorViewModel q => new SlideDisplay
            {
                Type = SlideType.OpenQuestion,
                Question = q.Question,
                TimeSeconds = q.TimeSeconds,
                SecondsRemaining = q.TimeSeconds,
                StatusText = $"{q.TimeSeconds}s",
                Image = image
            },

            LeaderboardSlideEditorViewModel => new SlideDisplay { Type = SlideType.Leaderboard },

            _ => null
        };
    }
}

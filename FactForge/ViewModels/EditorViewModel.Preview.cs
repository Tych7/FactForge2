// Partial of EditorViewModel (it is already `partial`, and CurrentSlideEditor is an [ObservableProperty]).
// Now also watches each editor's Image so picking/removing a picture refreshes the preview.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FactForge.Models;
using FactForge.Services;

namespace FactForge.ViewModels;

public partial class EditorViewModel
{
    [ObservableProperty] private SlideDisplay? _previewSlide;

    private INotifyPropertyChanged? _watchedEditor;
    private INotifyPropertyChanged? _watchedImage;
    private ObservableCollection<OptionEditorViewModel>? _watchedOptions;

    private INotifyPropertyChanged? _watchedAudio;

    private string? _waveformRequestedFileName;
    private IReadOnlyList<float> _previewWaveformPeaks =
        Array.Empty<float>();

    private int _waveformRequestVersion;

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

        if (editor is IHasSlideAudio { Audio: INotifyPropertyChanged audioNpc })
        {
            audioNpc.PropertyChanged += OnWatchedEditorChanged;
            _watchedAudio = audioNpc;
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

        if (_watchedAudio is not null) _watchedAudio.PropertyChanged -= OnWatchedEditorChanged;
        _watchedAudio = null;
    }

    private void OnWatchedEditorChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreview();

    // Options added/removed: re-hook the per-option handlers, then rebuild.
    private void OnWatchedOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => WatchEditorForPreview(CurrentSlideEditor);

    private void RefreshPreview()
    {
        var fileName =
            (CurrentSlideEditor as MusicQuestionSlideEditorViewModel)
            ?.Audio.FileName;

        if (!string.Equals(
                fileName,
                _waveformRequestedFileName,
                StringComparison.Ordinal))
        {
            _waveformRequestedFileName = fileName;
            var version = ++_waveformRequestVersion;

            _previewWaveformPeaks = Array.Empty<float>();

            if (!string.IsNullOrWhiteSpace(fileName))
                _ = LoadPreviewWaveformAsync(fileName, version);
        }

        PreviewSlide = BuildPreview();
    }

    private async Task LoadPreviewWaveformAsync(string fileName, int version)
    {
        IReadOnlyList<float> peaks;

        try
        {
            peaks = await AudioWaveformService.Shared
                .GetPeaksAsync(fileName);
        }
        catch
        {
            // A waveform failure should not prevent editing the quiz.
            peaks = Array.Empty<float>();
        }

        Dispatcher.UIThread.Post(() =>
        {
            // Ignore results from a slide or audio file that is no longer selected.
            if (version != _waveformRequestVersion)
                return;

            _previewWaveformPeaks = peaks;
            PreviewSlide = BuildPreview();
        });
    }
    
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

            MusicQuestionSlideEditorViewModel mu => new SlideDisplay
            {
                Type = SlideType.MusicQuestion,
                TimeSeconds = mu.TimeSeconds,
                SecondsRemaining = mu.TimeSeconds,
                StatusText = $"{mu.TimeSeconds}s",
                Image = image,
                MusicTitle = mu.Title,
                MusicArtist = string.Join(
                    ";",
                    mu.Artists
                        .Select(a => a.Text.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))),
                MusicWaveformPeaks = _previewWaveformPeaks
            },

            LeaderboardSlideEditorViewModel => new SlideDisplay { Type = SlideType.Leaderboard },

            _ => null
        };
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace FactForge.Services;

/// <summary>
/// Copies picked audio files into the app's own folder so a quiz keeps working
/// if the original file is moved. Slides only store the file NAME.
///
/// Uses a shared instance because the slide editor view models are created with plain `new`.
/// </summary>
public sealed class SlideAudioService
{
    public static SlideAudioService Shared { get; } = new();

    private readonly string _folder;

    private SlideAudioService()
    {
        _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FactForge", "audio");

        Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// Opens the file picker, copies the chosen audio file into the app folder,
    /// and returns its stored file name.
    /// </summary>
    public async Task<string?> PickAndImportAsync()
    {
        var window =
            (Application.Current?.ApplicationLifetime
             as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (window is null)
            return null;

        var files = await window.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose an audio file",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Audio files")
                    {
                        Patterns = new[]
                        {
                            "*.mp3",
                            "*.wav",
                            "*.ogg",
                            "*.flac",
                            "*.m4a",
                            "*.aac"
                        }
                    }
                }
            });

        if (files.Count == 0)
            return null;

        var sourceFile = files[0];

        var extension = Path.GetExtension(sourceFile.Name);

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = Path.Combine(_folder, storedName);

        await using var source = await sourceFile.OpenReadAsync();
        await using var target = File.Create(destinationPath);

        await source.CopyToAsync(target);

        return storedName;
    }

    /// <summary>
    /// Returns the full path of an imported audio file.
    /// Returns null when the file does not exist.
    /// </summary>
    public string? GetPath(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        // Never allow path traversal.
        fileName = Path.GetFileName(fileName);

        var path = Path.Combine(_folder, fileName);

        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Checks whether an imported audio file exists.
    /// </summary>
    public bool Exists(string? fileName)
    {
        return GetPath(fileName) is not null;
    }

    /// <summary>
    /// Deletes an imported audio file.
    /// </summary>
    public bool Delete(string? fileName)
    {
        var path = GetPath(fileName);

        if (path is null)
            return false;

        try
        {
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
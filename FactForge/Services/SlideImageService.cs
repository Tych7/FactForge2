using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace FactForge.Services;

/// <summary>
/// Copies picked images into the app's own folder (so a quiz keeps working if the original
/// file is moved) and loads them as cached bitmaps. Slides only store the file NAME.
/// Uses a shared instance because the slide editor view models are created with plain `new`.
/// </summary>
public sealed class SlideImageService
{
    public static SlideImageService Shared { get; } = new();

    private readonly string _folder;
    private readonly Dictionary<string, Bitmap> _cache = new();

    private SlideImageService()
    {
        _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FactForge", "images");
        Directory.CreateDirectory(_folder);
    }

    /// <summary>Opens the file picker, copies the chosen image into the app folder, returns its stored file name.</summary>
    public async Task<string?> PickAndImportAsync()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null) return null;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an image",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });
        if (files.Count == 0) return null;

        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(files[0].Name)}";
        await using var source = await files[0].OpenReadAsync();
        await using var target = File.Create(Path.Combine(_folder, storedName));
        await source.CopyToAsync(target);
        return storedName;
    }

    public Bitmap? Load(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Support imported quizzes that store an absolute media path.
        // Continue supporting existing quizzes that store only a filename.
        var path = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(_folder, Path.GetFileName(fileName));

        if (!File.Exists(path))
            return null;

        // Cache by resolved path so different files with the same filename
        // in different imported quiz folders cannot collide.
        path = Path.GetFullPath(path);

        if (_cache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            using var stream = File.OpenRead(path);
            var bitmap = Bitmap.DecodeToWidth(
                stream,
                1024,
                BitmapInterpolationMode.HighQuality);

            _cache[path] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

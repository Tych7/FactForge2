
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.Services;

public sealed class QuizImportExportService
{
    private readonly QuizRepository _repository;

    private static readonly string AppDataDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FactForge");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public QuizImportExportService(QuizRepository repository)
    {
        _repository = repository;
    }

    public async Task ExportAsync(int quizId, string zipPath)
    {
        var quiz = await _repository.GetQuizWithSlidesAsync(quizId);

        if (quiz is null)
            throw new InvalidOperationException("The selected quiz could not be found.");

        var export = new QuizExportDto
        {
            FormatVersion = 1,
            Title = quiz.Title,
            Slides = new List<SlideExportDto>()
        };

        // Create the JSON data and assign portable asset paths.
        for (var i = 0; i < quiz.Slides.Count; i++)
        {
            var slide = quiz.Slides[i];

            var dto = new SlideExportDto
            {
                OrderIndex = slide.OrderIndex,
                Type = slide.Type,
                Header = slide.Header,
                SubText = slide.SubText,
                Question = slide.Question,
                CorrectAnswer = slide.CorrectAnswer,
                NumbersOnly = slide.NumbersOnly,
                TimeSeconds = slide.TimeSeconds,
                Title = slide.Title,
                Artist = slide.Artist,
                Options = slide.Options
                    .OrderBy(o => o.OrderIndex)
                    .Select(o => new OptionExportDto
                    {
                        OrderIndex = o.OrderIndex,
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    })
                    .ToList()
            };

            if (!string.IsNullOrWhiteSpace(slide.ImagePath))
            {
                var extension = Path.GetExtension(slide.ImagePath);
                var archivePath = $"assets/images/slide-{i + 1}{extension}";

                dto.ImagePath = archivePath;
            }

            if (!string.IsNullOrWhiteSpace(slide.MusicFilePath))
            {
                var extension = Path.GetExtension(slide.MusicFilePath);
                var archivePath = $"assets/audio/slide-{i + 1}{extension}";

                dto.MusicFilePath = archivePath;
            }

            export.Slides.Add(dto);
        }

        var json = JsonSerializer.Serialize(export, JsonOptions);

        // Create or replace the destination archive.
        var fullZipPath = Path.GetFullPath(zipPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullZipPath)!);

        await using var output = new FileStream(
            fullZipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        using var archive = new ZipArchive(output, ZipArchiveMode.Create);

        var jsonEntry = archive.CreateEntry("quiz.json");
        await using (var entryStream = jsonEntry.Open())
        await using (var writer = new StreamWriter(entryStream))
        {
            await writer.WriteAsync(json);
        }

        // Include each referenced image and audio file.
        for (var i = 0; i < quiz.Slides.Count; i++)
        {
            var slide = quiz.Slides[i];
            var dto = export.Slides[i];

            if (dto.ImagePath is not null)
            {
                await AddAssetAsync(
                    archive, slide.ImagePath!, dto.ImagePath);
            }

            if (dto.MusicFilePath is not null)
            {
                await AddAssetAsync(
                    archive, slide.MusicFilePath!, dto.MusicFilePath);
            }
        }
    }

    public async Task<Quiz> ImportAsync(string zipPath)
    {
        await using var input = File.OpenRead(zipPath);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);

        var jsonEntry = archive.GetEntry("quiz.json")
            ?? throw new InvalidDataException(
                "This ZIP does not contain a quiz.json file.");

        QuizExportDto? imported;

        await using (var stream = jsonEntry.Open())
        {
            imported = await JsonSerializer.DeserializeAsync<QuizExportDto>(
                stream, JsonOptions);
        }

        if (imported is null || string.IsNullOrWhiteSpace(imported.Title))
            throw new InvalidDataException("The quiz JSON is empty or invalid.");

        if (imported.FormatVersion != 1)
            throw new InvalidDataException(
                $"Unsupported quiz format version: {imported.FormatVersion}.");

        imported.Slides ??= new List<SlideExportDto>();

        var importDirectory = Path.Combine(
            AppDataDirectory,
            "imported-assets",
            Guid.NewGuid().ToString("N"));

        var slides = new List<Slide>();

        foreach (var dto in imported.Slides.OrderBy(s => s.OrderIndex))
        {
            var slide = new Slide
            {
                OrderIndex = dto.OrderIndex,
                Type = dto.Type,
                Header = dto.Header,
                SubText = dto.SubText,
                Question = dto.Question,
                CorrectAnswer = dto.CorrectAnswer,
                NumbersOnly = dto.NumbersOnly,
                TimeSeconds = dto.TimeSeconds,
                Title = dto.Title,
                Artist = dto.Artist,
                Options = (dto.Options ?? new List<OptionExportDto>())
                    .OrderBy(o => o.OrderIndex)
                    .Select(o => new SlideOption
                    {
                        OrderIndex = o.OrderIndex,
                        Text = o.Text ?? string.Empty,
                        IsCorrect = o.IsCorrect
                    })
                    .ToList()
            };

            if (!string.IsNullOrWhiteSpace(dto.ImagePath))
            {
                slide.ImagePath = await ExtractAssetAsync(
                    archive, dto.ImagePath, "images", importDirectory);
            }

            if (!string.IsNullOrWhiteSpace(dto.MusicFilePath))
            {
                slide.MusicFilePath = await ExtractAssetAsync(
                    archive, dto.MusicFilePath, "audio", importDirectory);
            }

            slides.Add(slide);
        }

        // Only create the quiz after the archive and its referenced assets
        // have been validated and prepared.
        return await _repository.ImportQuizAsync(imported.Title.Trim(), slides);
    }

    private static async Task AddAssetAsync(
        ZipArchive archive,
        string originalPath,
        string archivePath)
    {
        var sourcePath = ResolveSourcePath(originalPath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                $"A media file referenced by the quiz could not be found: {originalPath}",
                sourcePath);
        }

        var entry = archive.CreateEntry(
            archivePath.Replace('\\', '/'),
            CompressionLevel.Optimal);

        await using var source = File.OpenRead(sourcePath);
        await using var destination = entry.Open();

        await source.CopyToAsync(destination);
    }

    private static string ResolveSourcePath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        var candidates = new[]
        {
            Path.Combine(AppDataDirectory, path),
            Path.Combine(AppDataDirectory, "audio", path),
            Path.Combine(AppDataDirectory, "images", path),
            Path.Combine(AppDataDirectory, "FactForge", "audio", path),
            Path.Combine(AppDataDirectory, "FactForge", "images", path)
        };

        return candidates.FirstOrDefault(File.Exists) 
            ?? candidates[0];
    }

    private static async Task<string> ExtractAssetAsync(
        ZipArchive archive,
        string archivePath,
        string expectedFolder,
        string importDirectory)
    {
        var normalized = archivePath.Replace('\\', '/');

        // Only permit the asset folder expected for this field.
        var prefix = $"assets/{expectedFolder}/";

        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Invalid asset path in quiz JSON: {archivePath}");
        }

        var fileName = normalized[prefix.Length..];

        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Contains('/') ||
            fileName.Contains(':') ||
            fileName is "." or "..")
        {
            throw new InvalidDataException(
                $"Invalid asset filename in quiz JSON: {archivePath}");
        }

        var entry = archive.GetEntry(normalized)
            ?? throw new InvalidDataException(
                $"The ZIP is missing the referenced asset: {archivePath}");

        var targetDirectory = Path.Combine(importDirectory, expectedFolder);
        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, fileName);

        await using var source = entry.Open();
        await using var destination = new FileStream(
            targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        await source.CopyToAsync(destination);

        return targetPath;
    }
}

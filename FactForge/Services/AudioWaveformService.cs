
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FactForge.Services;

public sealed class AudioWaveformService
{
    public static AudioWaveformService Shared { get; } = new();

    private const int SampleRate = 11025;
    private const int PeakCount = 150;

    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<float>>>
        _cache = new(StringComparer.OrdinalIgnoreCase);

    private AudioWaveformService() { }

    public Task<IReadOnlyList<float>> GetPeaksAsync(string? fileName)
    {
        var path = SlideAudioService.Shared.GetPath(fileName);

        if (path is null)
            return Task.FromResult<IReadOnlyList<float>>(
                Array.Empty<float>());

        return _cache.GetOrAdd(
            path,
            p => Task.Run(() => GeneratePeaks(p)));
    }

    private static IReadOnlyList<float> GeneratePeaks(string path)
    {
        var start = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        start.ArgumentList.Add("-v");
        start.ArgumentList.Add("error");
        start.ArgumentList.Add("-i");
        start.ArgumentList.Add(path);
        start.ArgumentList.Add("-vn");
        start.ArgumentList.Add("-ac");
        start.ArgumentList.Add("1");
        start.ArgumentList.Add("-ar");
        start.ArgumentList.Add(SampleRate.ToString(
            CultureInfo.InvariantCulture));
        start.ArgumentList.Add("-f");
        start.ArgumentList.Add("f32le");
        start.ArgumentList.Add("pipe:1");

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException(
                "Could not start FFmpeg.");

        using var memory = new MemoryStream();
        var copyTask = process.StandardOutput.BaseStream.CopyToAsync(memory);
        var errorTask = process.StandardError.ReadToEndAsync();

        Task.WaitAll(copyTask, process.WaitForExitAsync());

        var error = errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"FFmpeg could not decode the audio: {error}");

        var bytes = memory.ToArray();
        var sampleCount = bytes.Length / sizeof(float);

        if (sampleCount == 0)
            return Array.Empty<float>();

        var peaks = new float[PeakCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToSingle(bytes, i * sizeof(float));

            if (!float.IsFinite(sample))
                continue;

            var bucket = Math.Min(
                PeakCount - 1,
                (int)((long)i * PeakCount / sampleCount));

            peaks[bucket] = Math.Max(
                peaks[bucket], Math.Abs(sample));
        }

        var max = peaks.Max();

        if (max > 0)
        {
            for (var i = 0; i < peaks.Length; i++)
                peaks[i] /= max;
        }

        return peaks;
    }
}

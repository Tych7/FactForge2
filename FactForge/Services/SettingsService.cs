using System;
using System.IO;
using System.Text.Json;

namespace FactForge.Services;

public class AppSettings
{
    public bool IsFullScreen { get; set; }
    public bool IsDarkMode { get; set; } = true;
}

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
}

public class SettingsService : ISettingsService
{
    private readonly string _filePath;

    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FactForge");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "settings.json");

        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null) return settings;
            }
        }
        catch
        {
            // corrupt or unreadable file — fall back to defaults
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // best-effort — a failed save shouldn't crash the app
        }
    }
}
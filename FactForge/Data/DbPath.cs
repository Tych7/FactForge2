using System;
using System.IO;

namespace FactForge.Data;

public static class DbPath
{
    public static string GetDbFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FactForge");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "factforge.db");
    }

    public static string GetConnectionString() => $"Data Source={GetDbFilePath()}";
}

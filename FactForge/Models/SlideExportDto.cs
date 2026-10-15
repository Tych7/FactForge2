
using System.Collections.Generic;
using FactForge.Data;

namespace FactForge.Models;

public sealed class SlideExportDto
{
    public int OrderIndex { get; set; }
    public SlideType Type { get; set; }
    public string? Header { get; set; }
    public string? SubText { get; set; }
    public string? Question { get; set; }
    public string? CorrectAnswer { get; set; }
    public int TimeSeconds { get; set; }
    public string? ImagePath { get; set; }
    public string? MusicFilePath { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public List<OptionExportDto>? Options { get; set; }
}

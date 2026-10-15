
using System.Collections.Generic;

namespace FactForge.Models;

public sealed class QuizExportDto
{
    public int FormatVersion { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<SlideExportDto> Slides { get; set; } = new();
}

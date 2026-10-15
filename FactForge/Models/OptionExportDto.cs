namespace FactForge.Models;

public sealed class OptionExportDto
{
    public int OrderIndex { get; set; }
    public string? Text { get; set; }
    public bool IsCorrect { get; set; }
}
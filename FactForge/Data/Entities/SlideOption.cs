namespace FactForge.Data.Entities;

public class SlideOption
{
    public int Id { get; set; }
    public int SlideId { get; set; }
    public Slide? Slide { get; set; }

    public int OrderIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}

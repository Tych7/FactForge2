using System.Collections.Generic;

namespace FactForge.Models;

public record RevealDto(
    int SlideId,
    string? CorrectAnswer,
    List<OptionTallyDto>? OptionTallies);
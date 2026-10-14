namespace FactForge.Models;

public record OptionTallyDto(
    string Text, 
    int Count, 
    bool IsCorrect
);
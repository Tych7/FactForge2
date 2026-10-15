using FactForge.Models;

namespace FactForge.Models;

public record RejoinSessionDto(
    PlayerInfoDto Player,
    SlideDto? CurrentSlide,
    bool IsRevealed,
    bool HasAnswered,
    int QuestionPoints
);
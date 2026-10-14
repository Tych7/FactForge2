namespace FactForge.Models;

public record SubmitAnswerResultDto(
    bool Accepted,
    bool IsCorrect,
    int PointsAwarded,
    bool ArtistCorrect = false,
    bool TitleCorrect = false);
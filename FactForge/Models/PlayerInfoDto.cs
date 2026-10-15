namespace FactForge.Models;

public record PlayerInfoDto(
    int Id,
    string Name,
    int Score,
    bool IsConnected = true
);
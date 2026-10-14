using System;
using System.Collections.Generic;
using FactForge.Data;

namespace FactForge.Models;

public record SlideDto(
    int SlideId,
    SlideType Type,
    string? Header,
    string? SubText,
    string? Question,
    List<string>? Options,
    int TimeSeconds,
    DateTime? DeadlineUtc,

    // New fields for music slides
    string? MusicFilePath = null,  // Path to the audio file (e.g., "Sounds/music-fragment.mp3")
    string? Title = null,          // Expected title of the song (for validation)
    string? Artist = null,         // Expected artist of the song

    string? ImagePath = null);
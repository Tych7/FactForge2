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
    string? MusicFilePath = null,
    string? Title = null,
    string? Artist = null,
    string? ImagePath = null,
    bool NumbersOnly = false);
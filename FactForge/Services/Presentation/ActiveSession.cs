
using System;
using System.Collections.Generic;
using System.Threading;
using FactForge.Data.Entities;

namespace FactForge.Services.Presentation;

public class ActiveSession
    {
        public required QuizSession Session { get; init; }
        public required List<Slide> Slides { get; init; }
        public int CurrentSlidePosition { get; set; } = -1;
        public bool Revealed { get; set; }
        public DateTime? CurrentSlideDeadlineUtc { get; set; }
        public Timer? DeadlineTimer { get; set; }
        public Dictionary<string, int> ConnectionToPlayerId { get; } = new();
        public Dictionary<int, Player> PlayersById { get; } = new();
        public HashSet<int> AnsweredPlayerIds { get; } = new();
    }

using System.Threading;
using FactForge.Services.Presentation;

namespace FactForge.Services;

public sealed class PresentationSessionState
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);

    internal ActiveSession? Active { get; set; }
}

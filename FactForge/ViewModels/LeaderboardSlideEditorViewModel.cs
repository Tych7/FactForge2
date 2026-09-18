using FactForge.Data.Entities;
using FactForge.Models;

namespace FactForge.ViewModels;

public partial class LeaderboardSlideEditorViewModel : ViewModelBase
{
    public int SlideId { get; }

    public LeaderboardSlideEditorViewModel(Slide slide)
    {
        SlideId = slide.Id;
    }

    public Slide ToEntity() => new()
    {
        Id = SlideId,
        Type = SlideType.Leaderboard
    };
}

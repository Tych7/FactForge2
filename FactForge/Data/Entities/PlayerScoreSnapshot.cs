
namespace FactForge.Data.Entities;

public class PlayerScoreSnapshot
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public int SlideId { get; set; }
    public Slide? Slide { get; set; }

    public int QuestionNumber { get; set; }
    public int Score { get; set; }
}

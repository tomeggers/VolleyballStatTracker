namespace VolleyballStatTracker.Models;

public class Match
{
    public const int OpponentMaxLength = 100;

    public int Id { get; set; }
    public required string Opponent { get; set; }
    public DateOnly Date { get; set; }

    // Null while the match is still being played.
    public DateTime? FinishedAt { get; set; }
    public bool IsFinished => FinishedAt is not null;

    public List<Set> Sets { get; set; } = [];
}

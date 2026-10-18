namespace VolleyballStatTracker.Models;

public class Match
{
    public int Id { get; set; }
    public required string Opponent { get; set; }
    public DateOnly Date { get; set; }

    public List<Set> Sets { get; set; } = [];
}

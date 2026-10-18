namespace VolleyballStatTracker.Models;

public class Set
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public Match? Match { get; set; }
    public int SetNumber { get; set; }

    public List<StatEntry> StatEntries { get; set; } = [];
}

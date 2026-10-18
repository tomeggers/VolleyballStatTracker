namespace VolleyballStatTracker.Models;

public class Player
{
    public const int NameMaxLength = 50;

    public int Id { get; set; }
    public required string Name { get; set; }
    public int ShirtNumber { get; set; }

    public List<StatEntry> StatEntries { get; set; } = [];
}

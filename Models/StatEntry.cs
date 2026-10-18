namespace VolleyballStatTracker.Models;

public class StatEntry
{
    public int Id { get; set; }
    public int SetId { get; set; }
    public Set? Set { get; set; }

    // Null for OpponentPoint, where no specific player is at fault.
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }

    public StatType StatType { get; set; }

    // Only used when StatType is Reception, a 0-3 pass grade.
    public int? Grade { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public PointResult PointResult => StatRules.GetPointResult(StatType, Grade);
}

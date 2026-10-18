namespace VolleyballStatTracker.Models;

public enum StatType
{
    Kill,
    AttackError,
    AttackInPlay,
    ServiceAce,
    ServiceError,
    ServeInPlay,
    BlockSolo,
    BlockAssist,
    Dig,
    Assist,
    Reception,      // uses StatEntry.Grade, 0-3
    OpponentPoint   // no Player attached
}

public enum PointResult
{
    None,
    UsScored,
    ThemScored
}

public static class StatRules
{
    public static PointResult GetPointResult(StatType statType, int? grade) => statType switch
    {
        StatType.Kill or StatType.BlockSolo or StatType.BlockAssist or StatType.ServiceAce => PointResult.UsScored,
        StatType.AttackError or StatType.ServiceError or StatType.OpponentPoint => PointResult.ThemScored,
        StatType.Reception => grade == 0 ? PointResult.ThemScored : PointResult.None,
        _ => PointResult.None
    };
}

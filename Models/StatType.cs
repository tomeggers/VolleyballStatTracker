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

public enum SetResult
{
    InProgress,
    Won,
    Lost
}

public static class StatRules
{
    public const int MaxSets = 5;
    public const int SetsToWin = 3;

    // The player buttons on the Log page, in display order. Reception is graded separately.
    public static readonly (StatType Type, string Label)[] PlayerActions =
    [
        (StatType.Kill, "Kill"),
        (StatType.AttackError, "Attack error"),
        (StatType.AttackInPlay, "Attack, in play"),
        (StatType.ServiceAce, "Service ace"),
        (StatType.ServiceError, "Service error"),
        (StatType.ServeInPlay, "Serve, in play"),
        (StatType.BlockSolo, "Block, solo"),
        (StatType.BlockAssist, "Block, assist"),
        (StatType.Dig, "Dig"),
        (StatType.Assist, "Assist")
    ];

    public static string Label(StatType statType, int? grade) => statType switch
    {
        StatType.Reception => $"Reception {grade}",
        StatType.OpponentPoint => "Opponent point",
        _ => PlayerActions.First(a => a.Type == statType).Label
    };

    // Opponent points have no player; reception needs a 0-3 grade; nothing else takes a grade.
    public static bool IsValidEntry(StatType statType, bool hasPlayer, int? grade) =>
        Enum.IsDefined(statType) && statType switch
        {
            StatType.OpponentPoint => !hasPlayer && grade is null,
            StatType.Reception => hasPlayer && grade is >= 0 and <= 3,
            _ => hasPlayer && grade is null
        };

    public static bool IsMatchDecided(int setsWon, int setsLost) =>
        setsWon >= SetsToWin || setsLost >= SetsToWin;

    public static (int Us, int Them) Score(IEnumerable<StatEntry> entries) =>
        (entries.Count(e => e.PointResult == PointResult.UsScored),
         entries.Count(e => e.PointResult == PointResult.ThemScored));

    // Sets go to 25 (15 in the fifth set) and must be won by two.
    public static SetResult GetSetResult(int setNumber, int us, int them)
    {
        var target = setNumber == MaxSets ? 15 : 25;

        if (us >= target && us - them >= 2) return SetResult.Won;
        if (them >= target && them - us >= 2) return SetResult.Lost;
        return SetResult.InProgress;
    }

    public static PointResult GetPointResult(StatType statType, int? grade) => statType switch
    {
        StatType.Kill or StatType.BlockSolo or StatType.BlockAssist or StatType.ServiceAce => PointResult.UsScored,
        StatType.AttackError or StatType.ServiceError or StatType.OpponentPoint => PointResult.ThemScored,
        StatType.Reception => grade == 0 ? PointResult.ThemScored : PointResult.None,
        _ => PointResult.None
    };
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VolleyballStatTracker.Data;
using VolleyballStatTracker.Models;

namespace VolleyballStatTracker.Pages.Matches;

public class LogModel(AppDbContext db) : PageModel
{
    public Set CurrentSet { get; set; } = null!;
    public Match Match { get; set; } = null!;
    public List<Player> Players { get; set; } = [];
    public List<SetSummary> SetSummaries { get; set; } = [];

    public SetSummary Current => SetSummaries.First(s => s.SetId == CurrentSet.Id);
    public int SetsWon => SetSummaries.Count(s => s.Result == SetResult.Won);
    public int SetsLost => SetSummaries.Count(s => s.Result == SetResult.Lost);
    public bool IsSetOver => Current.Result != SetResult.InProgress;

    public bool HasNextSet => SetSummaries.Any(s => s.SetNumber == CurrentSet.SetNumber + 1);
    public bool CanAddSet =>
        !Match.IsFinished
        && CurrentSet.SetNumber < StatRules.MaxSets
        && !StatRules.IsMatchDecided(SetsWon, SetsLost);

    public record SetSummary(int SetId, int SetNumber, int Us, int Them, SetResult Result);

    // A point-scoring tap is locked once the set is decided; assists and digs on the final rally still go in.
    public bool IsLocked(StatType statType, int? grade = null) =>
        IsSetOver && StatRules.GetPointResult(statType, grade) != PointResult.None;

    public static string ButtonClass(StatType statType, int? grade = null) =>
        StatRules.GetPointResult(statType, grade) switch
        {
            PointResult.UsScored => "btn-us",
            PointResult.ThemScored => "btn-them",
            _ => "btn-neutral"
        };

    public async Task<IActionResult> OnGetAsync(int setId)
    {
        // Load from the match down: a no-tracking query can't Include set -> match -> sets (a cycle).
        var match = await db.Matches
            .AsNoTracking()
            .Include(m => m.Sets)
                .ThenInclude(s => s.StatEntries)
            .FirstOrDefaultAsync(m => m.Sets.Any(s => s.Id == setId));
        var set = match?.Sets.FirstOrDefault(s => s.Id == setId);
        if (match is null || set is null)
        {
            return NotFound();
        }

        Match = match;
        CurrentSet = set;
        Players = await db.Players.AsNoTracking().OrderBy(p => p.ShirtNumber).ToListAsync();
        SetSummaries = Match.Sets
            .OrderBy(s => s.SetNumber)
            .Select(s =>
            {
                var (us, them) = StatRules.Score(s.StatEntries);
                return new SetSummary(s.Id, s.SetNumber, us, them, StatRules.GetSetResult(s.SetNumber, us, them));
            })
            .ToList();

        return Page();
    }

    public Task<IActionResult> OnPostLogStatAsync(int setId, int? playerId, StatType statType, int? grade) =>
        AddEntryAsync(setId, playerId, statType, grade);

    public Task<IActionResult> OnPostOpponentPointAsync(int setId) =>
        AddEntryAsync(setId, null, StatType.OpponentPoint, null);

    public async Task<IActionResult> OnPostUndoAsync(int setId)
    {
        var set = await db.Sets.Include(s => s.Match).FirstOrDefaultAsync(s => s.Id == setId);
        if (set is null)
        {
            return NotFound();
        }

        if (!set.Match!.IsFinished)
        {
            var last = await db.StatEntries
                .Where(e => e.SetId == setId)
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();

            if (last is not null)
            {
                db.StatEntries.Remove(last);
                await db.SaveChangesAsync();
            }
        }

        return RedirectToPage(new { setId });
    }

    public async Task<IActionResult> OnPostNextSetAsync(int setId)
    {
        var set = await db.Sets
            .Include(s => s.Match!.Sets)
                .ThenInclude(s => s.StatEntries)
            .FirstOrDefaultAsync(s => s.Id == setId);
        if (set is null)
        {
            return NotFound();
        }

        // Going back to fix an earlier set, then pressing Next, should not create a duplicate.
        var next = set.Match!.Sets.FirstOrDefault(s => s.SetNumber == set.SetNumber + 1);
        if (next is not null)
        {
            return RedirectToPage(new { setId = next.Id });
        }

        var results = set.Match.Sets.Select(s =>
        {
            var (us, them) = StatRules.Score(s.StatEntries);
            return StatRules.GetSetResult(s.SetNumber, us, them);
        }).ToList();
        var matchDecided = StatRules.IsMatchDecided(
            results.Count(r => r == SetResult.Won), results.Count(r => r == SetResult.Lost));

        if (set.Match.IsFinished || set.SetNumber >= StatRules.MaxSets || matchDecided)
        {
            return RedirectToPage(new { setId });
        }

        next = new Set { MatchId = set.MatchId, SetNumber = set.SetNumber + 1 };
        db.Sets.Add(next);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A double tap got there first; the unique index rejected the copy, so open the one that exists.
            var existing = await db.Sets.AsNoTracking()
                .FirstAsync(s => s.MatchId == set.MatchId && s.SetNumber == set.SetNumber + 1);
            return RedirectToPage(new { setId = existing.Id });
        }

        return RedirectToPage(new { setId = next.Id });
    }

    public async Task<IActionResult> OnPostEndMatchAsync(int setId)
    {
        var updated = await db.Matches
            .Where(m => m.Sets.Any(s => s.Id == setId))
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.FinishedAt, DateTime.UtcNow));

        return updated == 0 ? NotFound() : RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostReopenMatchAsync(int setId)
    {
        var updated = await db.Matches
            .Where(m => m.Sets.Any(s => s.Id == setId))
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.FinishedAt, (DateTime?)null));

        return updated == 0 ? NotFound() : RedirectToPage(new { setId });
    }

    private async Task<IActionResult> AddEntryAsync(int setId, int? playerId, StatType statType, int? grade)
    {
        // An undefined enum value (e.g. statType=99) fails binding and silently becomes Kill,
        // so a binding error has to be rejected here rather than trusted.
        if (!ModelState.IsValid || !StatRules.IsValidEntry(statType, playerId is not null, grade))
        {
            return BadRequest();
        }

        var set = await db.Sets
            .Include(s => s.Match)
            .Include(s => s.StatEntries)
            .FirstOrDefaultAsync(s => s.Id == setId);
        if (set is null)
        {
            return NotFound();
        }

        if (playerId is not null && !await db.Players.AnyAsync(p => p.Id == playerId))
        {
            return BadRequest();
        }

        var (us, them) = StatRules.Score(set.StatEntries);
        var setOver = StatRules.GetSetResult(set.SetNumber, us, them) != SetResult.InProgress;
        var scoresPoint = StatRules.GetPointResult(statType, grade) != PointResult.None;

        // Once the match is ended, or the set is decided, only Undo can change the score.
        if (!set.Match!.IsFinished && !(setOver && scoresPoint))
        {
            db.StatEntries.Add(new StatEntry
            {
                SetId = setId,
                PlayerId = playerId,
                StatType = statType,
                Grade = grade
            });
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { setId });
    }
}

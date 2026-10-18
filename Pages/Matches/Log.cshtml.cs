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
    public int? SelectedPlayerId { get; set; }

    public SetSummary Current => SetSummaries.First(s => s.SetId == CurrentSet.Id);
    public int SetsWon => SetSummaries.Count(s => s.Result == SetResult.Won);
    public int SetsLost => SetSummaries.Count(s => s.Result == SetResult.Lost);
    public bool IsSetOver => Current.Result != SetResult.InProgress;

    public bool HasNextSet => SetSummaries.Any(s => s.SetNumber == CurrentSet.SetNumber + 1);
    public bool CanAddSet =>
        !Match.IsFinished
        && CurrentSet.SetNumber < StatRules.MaxSets
        && !StatRules.IsMatchDecided(SetsWon, SetsLost);

    public StatEntry? LastEntry => CurrentSet.StatEntries.OrderByDescending(e => e.Id).FirstOrDefault();

    public record SetSummary(int SetId, int SetNumber, int Us, int Them, SetResult Result);

    // What the page's script needs to redraw after a tap, without reloading.
    public record LogState(
        bool Accepted,
        int Us,
        int Them,
        string SetResult,
        int SetsWon,
        int SetsLost,
        bool CanAddSet,
        string? LastEntry,
        string LastEntryEffect);

    // A point-scoring tap is locked once the set is decided; assists and digs on the final rally still go in.
    public bool IsLocked(StatType statType, int? grade = null) =>
        IsSetOver && Scores(statType, grade);

    public static bool Scores(StatType statType, int? grade = null) =>
        StatRules.GetPointResult(statType, grade) != PointResult.None;

    public static string Effect(StatType statType, int? grade = null) =>
        StatRules.GetPointResult(statType, grade) switch
        {
            PointResult.UsScored => "us",
            PointResult.ThemScored => "them",
            _ => "none"
        };

    public static string ShortName(Player player) =>
        player.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? player.Name;

    public string Describe(StatEntry entry)
    {
        var player = Players.FirstOrDefault(p => p.Id == entry.PlayerId);
        var who = player is null ? "" : $" · #{player.ShirtNumber} {ShortName(player)}";
        var effect = Effect(entry.StatType, entry.Grade) switch
        {
            "us" => " · +1 us",
            "them" => " · +1 them",
            _ => ""
        };
        return StatRules.Label(entry.StatType, entry.Grade) + who + effect;
    }

    public async Task<IActionResult> OnGetAsync(int setId, int? playerId)
    {
        if (!await LoadAsync(setId))
        {
            return NotFound();
        }

        SelectedPlayerId = Players.Any(p => p.Id == playerId) ? playerId : null;
        return Page();
    }

    public Task<IActionResult> OnPostLogStatAsync(int setId, int? playerId, StatType statType, int? grade) =>
        AddEntryAsync(setId, playerId, statType, grade);

    public Task<IActionResult> OnPostOpponentPointAsync(int setId, int? playerId) =>
        AddEntryAsync(setId, null, StatType.OpponentPoint, null, keepPlayerId: playerId);

    public async Task<IActionResult> OnPostUndoAsync(int setId, int? playerId)
    {
        var set = await db.Sets.Include(s => s.Match).FirstOrDefaultAsync(s => s.Id == setId);
        if (set is null)
        {
            return NotFound();
        }

        var removed = false;
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
                removed = true;
            }
        }

        return await RespondAsync(setId, removed, playerId);
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

    private async Task<IActionResult> AddEntryAsync(
        int setId, int? playerId, StatType statType, int? grade, int? keepPlayerId = null)
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

        // Once the match is ended, or the set is decided, only Undo can change the score.
        var accepted = !set.Match!.IsFinished && !(setOver && Scores(statType, grade));
        if (accepted)
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

        return await RespondAsync(setId, accepted, keepPlayerId ?? playerId);
    }

    // The page's script asks for JSON so it can update in place; a plain form post gets a redirect.
    private async Task<IActionResult> RespondAsync(int setId, bool accepted, int? playerId)
    {
        if (!Request.Headers.Accept.ToString().Contains("application/json"))
        {
            return RedirectToPage(new { setId, playerId });
        }

        await LoadAsync(setId);
        var last = LastEntry;
        return new JsonResult(new LogState(
            accepted,
            Current.Us,
            Current.Them,
            Current.Result.ToString(),
            SetsWon,
            SetsLost,
            CanAddSet,
            last is null ? null : Describe(last),
            last is null ? "none" : Effect(last.StatType, last.Grade)));
    }

    private async Task<bool> LoadAsync(int setId)
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
            return false;
        }

        Match = match;
        CurrentSet = set;
        Players = await db.Players.AsNoTracking().OrderBy(p => p.ShirtNumber).ToListAsync();
        SetSummaries = match.Sets
            .OrderBy(s => s.SetNumber)
            .Select(s =>
            {
                var (us, them) = StatRules.Score(s.StatEntries);
                return new SetSummary(s.Id, s.SetNumber, us, them, StatRules.GetSetResult(s.SetNumber, us, them));
            })
            .ToList();

        return true;
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VolleyballStatTracker.Data;
using VolleyballStatTracker.Models;

namespace VolleyballStatTracker.Pages.Matches;

public class LogModel(AppDbContext db) : PageModel
{
    public Set CurrentSet { get; set; } = null!;
    public Match Match => CurrentSet.Match!;
    public List<Player> Players { get; set; } = [];
    public List<SetSummary> SetSummaries { get; set; } = [];

    public SetSummary Current => SetSummaries.First(s => s.SetId == CurrentSet.Id);
    public int SetsWon => SetSummaries.Count(s => s.Result == SetResult.Won);
    public int SetsLost => SetSummaries.Count(s => s.Result == SetResult.Lost);

    public bool HasNextSet => SetSummaries.Any(s => s.SetNumber == CurrentSet.SetNumber + 1);
    public bool CanAddSet => !Match.IsFinished && CurrentSet.SetNumber < StatRules.MaxSets;

    public record SetSummary(int SetId, int SetNumber, int Us, int Them, SetResult Result);

    public async Task<IActionResult> OnGetAsync(int setId)
    {
        var set = await LoadSetAsync(setId);
        if (set is null)
        {
            return NotFound();
        }

        CurrentSet = set;
        Players = await db.Players.OrderBy(p => p.ShirtNumber).ToListAsync();
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

    public async Task<IActionResult> OnPostLogStatAsync(int setId, int? playerId, StatType statType, int? grade)
    {
        if (await IsMatchFinishedAsync(setId))
        {
            return RedirectToPage(new { setId });
        }

        db.StatEntries.Add(new StatEntry
        {
            SetId = setId,
            PlayerId = playerId,
            StatType = statType,
            Grade = grade
        });
        await db.SaveChangesAsync();

        return RedirectToPage(new { setId });
    }

    public async Task<IActionResult> OnPostOpponentPointAsync(int setId)
    {
        if (await IsMatchFinishedAsync(setId))
        {
            return RedirectToPage(new { setId });
        }

        db.StatEntries.Add(new StatEntry { SetId = setId, StatType = StatType.OpponentPoint });
        await db.SaveChangesAsync();

        return RedirectToPage(new { setId });
    }

    public async Task<IActionResult> OnPostUndoAsync(int setId)
    {
        if (await IsMatchFinishedAsync(setId))
        {
            return RedirectToPage(new { setId });
        }

        var last = await db.StatEntries
            .Where(e => e.SetId == setId)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();

        if (last is not null)
        {
            db.StatEntries.Remove(last);
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { setId });
    }

    public async Task<IActionResult> OnPostNextSetAsync(int setId)
    {
        var set = await LoadSetAsync(setId);
        if (set is null)
        {
            return NotFound();
        }

        // Going back to fix an earlier set, then pressing Next, should not create a duplicate.
        var next = set.Match!.Sets.FirstOrDefault(s => s.SetNumber == set.SetNumber + 1);
        if (next is null)
        {
            if (set.Match.IsFinished || set.SetNumber >= StatRules.MaxSets)
            {
                return RedirectToPage(new { setId });
            }

            next = new Set { MatchId = set.MatchId, SetNumber = set.SetNumber + 1 };
            db.Sets.Add(next);
            await db.SaveChangesAsync();
        }

        return RedirectToPage(new { setId = next.Id });
    }

    public async Task<IActionResult> OnPostEndMatchAsync(int setId)
    {
        var set = await LoadSetAsync(setId);
        if (set is null)
        {
            return NotFound();
        }

        set.Match!.FinishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostReopenMatchAsync(int setId)
    {
        var set = await LoadSetAsync(setId);
        if (set is null)
        {
            return NotFound();
        }

        set.Match!.FinishedAt = null;
        await db.SaveChangesAsync();

        return RedirectToPage(new { setId });
    }

    private Task<Set?> LoadSetAsync(int setId) =>
        db.Sets
            .Include(s => s.Match!.Sets)
                .ThenInclude(s => s.StatEntries)
            .FirstOrDefaultAsync(s => s.Id == setId);

    private Task<bool> IsMatchFinishedAsync(int setId) =>
        db.Sets.AnyAsync(s => s.Id == setId && s.Match!.FinishedAt != null);
}

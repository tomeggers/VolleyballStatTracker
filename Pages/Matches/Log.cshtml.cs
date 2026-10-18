using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VolleyballStatTracker.Data;
using VolleyballStatTracker.Models;

namespace VolleyballStatTracker.Pages.Matches;

public class LogModel(AppDbContext db) : PageModel
{
    public Set CurrentSet { get; set; } = null!;
    public List<Player> Players { get; set; } = [];
    public List<StatEntry> Entries { get; set; } = [];

    public int UsScore => Entries.Count(e => e.PointResult == PointResult.UsScored);
    public int ThemScore => Entries.Count(e => e.PointResult == PointResult.ThemScored);

    public async Task<IActionResult> OnGetAsync(int setId)
    {
        var set = await db.Sets.Include(s => s.Match).FirstOrDefaultAsync(s => s.Id == setId);
        if (set is null)
        {
            return NotFound();
        }

        CurrentSet = set;
        Players = await db.Players.OrderBy(p => p.ShirtNumber).ToListAsync();
        Entries = await db.StatEntries.Where(e => e.SetId == setId).ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostLogStatAsync(int setId, int? playerId, StatType statType, int? grade)
    {
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
        db.StatEntries.Add(new StatEntry { SetId = setId, StatType = StatType.OpponentPoint });
        await db.SaveChangesAsync();

        return RedirectToPage(new { setId });
    }

    public async Task<IActionResult> OnPostUndoAsync(int setId)
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

        return RedirectToPage(new { setId });
    }
}

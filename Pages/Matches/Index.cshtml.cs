using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VolleyballStatTracker.Data;
using VolleyballStatTracker.Models;

namespace VolleyballStatTracker.Pages.Matches;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<MatchRow> Matches { get; set; } = [];

    public record MatchRow(Match Match, int SetsWon, int SetsLost, int? LatestSetId);

    public async Task OnGetAsync()
    {
        var matches = await db.Matches
            .AsNoTracking()
            .Include(m => m.Sets)
                .ThenInclude(s => s.StatEntries)
            .OrderByDescending(m => m.Date)
                .ThenByDescending(m => m.Id)
            .ToListAsync();

        Matches = matches.Select(m =>
        {
            var results = m.Sets.Select(s =>
            {
                var (us, them) = StatRules.Score(s.StatEntries);
                return StatRules.GetSetResult(s.SetNumber, us, them);
            }).ToList();

            var latestSet = m.Sets.OrderByDescending(s => s.SetNumber).FirstOrDefault();

            return new MatchRow(
                m,
                results.Count(r => r == SetResult.Won),
                results.Count(r => r == SetResult.Lost),
                latestSet?.Id);
        }).ToList();
    }
}

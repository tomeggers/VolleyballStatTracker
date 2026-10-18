using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VolleyballStatTracker.Data;
using VolleyballStatTracker.Models;

namespace VolleyballStatTracker.Pages.Players;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Player> Players { get; set; } = [];

    [BindProperty]
    public string NewName { get; set; } = "";

    [BindProperty]
    public int NewShirtNumber { get; set; }

    public async Task OnGetAsync()
    {
        Players = await db.Players.OrderBy(p => p.ShirtNumber).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!string.IsNullOrWhiteSpace(NewName))
        {
            db.Players.Add(new Player { Name = NewName, ShirtNumber = NewShirtNumber });
            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}

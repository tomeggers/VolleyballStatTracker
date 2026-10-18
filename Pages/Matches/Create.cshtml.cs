using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VolleyballStatTracker.Data;
using VolleyballStatTracker.Models;

namespace VolleyballStatTracker.Pages.Matches;

public class CreateModel(AppDbContext db) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Enter the opponent.")]
    [StringLength(Match.OpponentMaxLength)]
    public string Opponent { get; set; } = "";

    [BindProperty]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var match = new Match { Opponent = Opponent.Trim(), Date = Date };
        match.Sets.Add(new Set { SetNumber = 1 });

        db.Matches.Add(match);
        await db.SaveChangesAsync();

        var firstSet = match.Sets.First();
        return RedirectToPage("Log", new { setId = firstSet.Id });
    }
}

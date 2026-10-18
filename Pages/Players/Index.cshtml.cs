using System.ComponentModel.DataAnnotations;
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
    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(Player.NameMaxLength)]
    [Display(Name = "Name")]
    public string NewName { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Enter a shirt number.")]
    [Range(0, 99, ErrorMessage = "Shirt number must be 0 to 99.")]
    [Display(Name = "Shirt number")]
    public int? NewShirtNumber { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPlayersAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid && await db.Players.AnyAsync(p => p.ShirtNumber == NewShirtNumber))
        {
            ModelState.AddModelError(nameof(NewShirtNumber), $"#{NewShirtNumber} is already taken.");
        }

        if (!ModelState.IsValid)
        {
            await LoadPlayersAsync();
            return Page();
        }

        db.Players.Add(new Player { Name = NewName.Trim(), ShirtNumber = NewShirtNumber!.Value });
        await db.SaveChangesAsync();

        return RedirectToPage();
    }

    private async Task LoadPlayersAsync()
    {
        Players = await db.Players.AsNoTracking().OrderBy(p => p.ShirtNumber).ToListAsync();
    }
}

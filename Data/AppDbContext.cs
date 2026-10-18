using Microsoft.EntityFrameworkCore;
using VolleyballStatTracker.Models;

namespace VolleyballStatTracker.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Set> Sets => Set<Set>();
    public DbSet<StatEntry> StatEntries => Set<StatEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Set>()
            .HasOne(s => s.Match)
            .WithMany(m => m.Sets)
            .HasForeignKey(s => s.MatchId);

        modelBuilder.Entity<StatEntry>()
            .HasOne(e => e.Set)
            .WithMany(s => s.StatEntries)
            .HasForeignKey(e => e.SetId);

        modelBuilder.Entity<StatEntry>()
            .HasOne(e => e.Player)
            .WithMany(p => p.StatEntries)
            .HasForeignKey(e => e.PlayerId)
            .IsRequired(false);
    }
}
